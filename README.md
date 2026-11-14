# OrderFlow

Laboratório de backend com **.NET 10**, API REST, RabbitMQ e Docker. O projeto simula a recepção de pedidos e evoluirá para processamento assíncrono com um Worker.

## Estado atual

- **OrderFlow.Api:** endpoint `POST /pedidos`, validação básica e publicação de `PedidoCriado` na fila `pedidos.criados` com confirmação de publicação.
- **RabbitMQ:** roda localmente pelo Docker Compose em `deploy/compose.yaml`.
- **Testes manuais:** requisições REST Client em `tests/pedidos.http`.
- **Ainda não implementado:** Worker, persistência, consulta de status e testes automatizados.

> `202 Accepted` significa que o broker confirmou a publicação, **não** que o pedido já foi processado.

## Arquitetura atual

```text
REST Client / curl
       |
       | POST /pedidos
       v
OrderFlow.Api (.NET 10 executado localmente)
       |
       | publica PedidoCriado
       v
RabbitMQ (Docker, fila pedidos.criados)
       |
       v
OrderFlow.Worker (próxima etapa)
```

## Pré-requisitos

- **.NET SDK 10** — `dotnet --version`
- **Docker e Docker Compose** — `docker compose version`
- Git
- Opcional: VS Code com extensão **REST Client** (`humao.rest-client`)

Os comandos abaixo são para terminal **Bash/Linux** e partem da raiz do repositório.

## Primeira configuração (uma vez por computador)

### 1. Clonar e restaurar

```bash
git clone https://github.com/miqueiaspenha/OrderFlow.git
cd OrderFlow
dotnet restore
```

Se o repositório já está clonado, apenas entre na pasta do projeto.

### 2. Configurar o RabbitMQ

```bash
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
```

Edite `deploy/.env` para definir o usuário e uma senha própria:

```dotenv
RABBITMQ_USER=orderflow
RABBITMQ_PASSWORD=troque-por-uma-senha-forte
```

O `.env` está ignorado pelo Git; não envie credenciais para o repositório.

### 3. Subir o RabbitMQ

```bash
docker compose -f deploy/compose.yaml --env-file deploy/.env up -d
docker compose -f deploy/compose.yaml --env-file deploy/.env ps
```

| Serviço | Endereço |
| --- | --- |
| RabbitMQ / AMQP | `localhost:5672` |
| Painel RabbitMQ | http://localhost:15672 |

Entre no painel com as credenciais de `deploy/.env`. O virtual host é **`orderflow`**.

> As credenciais definidas no Compose são aplicadas na **primeira inicialização do volume**. Editar o `.env` depois não altera automaticamente usuários ou senhas existentes.

### 4. Configurar .NET User Secrets

O `UserSecretsId` já existe no `src/OrderFlow.Api/OrderFlow.Api.csproj`, mas os **valores** precisam ser configurados separadamente em cada máquina.

```bash
dotnet user-secrets set 'RabbitMQ:Host' 'localhost' --project src/OrderFlow.Api
dotnet user-secrets set 'RabbitMQ:VirtualHost' 'orderflow' --project src/OrderFlow.Api
dotnet user-secrets set 'RabbitMQ:UserName' 'orderflow' --project src/OrderFlow.Api
```

Use **o mesmo usuário** do `.env` se tiver escolhido outro. Para a senha, evite gravá-la diretamente no histórico do shell:

```bash
read -rsp 'Senha do RabbitMQ: ' RABBITMQ_PASSWORD
echo
dotnet user-secrets set 'RabbitMQ:Password' "$RABBITMQ_PASSWORD" --project src/OrderFlow.Api
unset RABBITMQ_PASSWORD
```

Os User Secrets são armazenados fora do repositório e **não são criptografados**: use-os apenas para desenvolvimento.

Se existirem variáveis antigas de ambiente, remova-as da sessão, pois elas têm prioridade sobre os User Secrets:

```bash
unset RabbitMQ__Host RabbitMQ__VirtualHost RabbitMQ__UserName RabbitMQ__Password
```

### 5. Compilar e executar a API

```bash
dotnet build
DOTNET_ENVIRONMENT=Development dotnet run \
  --project src/OrderFlow.Api \
  --no-launch-profile \
  --urls http://127.0.0.1:5000
```

Deixe esse terminal aberto. A API estará em `http://127.0.0.1:5000`.

## Testar a API

### Pelo REST Client

Abra `tests/pedidos.http` no VS Code com a extensão REST Client instalada e clique em **Send Request** acima de uma requisição.

### Pelo curl

Em outro terminal:

```bash
curl -i -X POST http://127.0.0.1:5000/pedidos \
  -H 'Content-Type: application/json' \
  -d '{
    "clienteId": "cliente-123",
    "itens": [{
      "produtoId": "produto-001",
      "quantidade": 2,
      "precoUnitario": 29.90
    }]
  }'
```

**Resultado esperado:** HTTP `202 Accepted`, com `pedidoId` e `status: "Enfileirado"`. Pedidos inválidos devem resultar em `400 Bad Request`.

Para consultar a fila:

```bash
docker exec orderflow-rabbitmq rabbitmqctl list_queues -p orderflow name messages_ready consumers
```

Enquanto o Worker não existir, as mensagens permanecerão aguardando na fila.

## Uso diário

Depois da primeira configuração, normalmente bastam dois comandos, em terminais distintos:

**Terminal 1 — infraestrutura:**

```bash
docker compose -f deploy/compose.yaml --env-file deploy/.env up -d
```

**Terminal 2 — API:**

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project src/OrderFlow.Api --no-launch-profile --urls http://127.0.0.1:5000
```

| Operação | Comando |
| --- | --- |
| Status | `docker compose -f deploy/compose.yaml --env-file deploy/.env ps` |
| Logs | `docker logs -f orderflow-rabbitmq` |
| Parar RabbitMQ | `docker compose -f deploy/compose.yaml --env-file deploy/.env stop` |
| Reiniciar RabbitMQ | `docker compose -f deploy/compose.yaml --env-file deploy/.env start` |
| Remover containers e rede | `docker compose -f deploy/compose.yaml --env-file deploy/.env down` |

`down` preserva o volume. **Evite `down -v`: ele também remove os dados persistidos do RabbitMQ.**

## Problemas comuns

- **`Connection refused`**: confira `docker compose ... ps` e aguarde o RabbitMQ ficar saudável.
- **`ACCESS_REFUSED` / autenticação**: confira usuário, senha, virtual host e a observação sobre credenciais persistidas no volume.
- **Tentativa de acesso a `lab-rabbitmq`**: esse era o hostname na rede Docker da VPS. Na máquina local, configure `RabbitMQ:Host=localhost` em User Secrets.
- **Porta 5672 ou 15672 já ocupada**: verifique se existe outra instância do RabbitMQ.
- **API encerra imediatamente**: confira o log do terminal e se `Program.cs` finaliza com `app.Run();`.

## Estrutura principal

```text
OrderFlow/
├── src/
│   └── OrderFlow.Api/       # API e publicador RabbitMQ
├── tests/
│   └── pedidos.http         # Testes manuais
├── deploy/
│   ├── compose.yaml         # Infraestrutura local
│   ├── .env.example         # Exemplo de variáveis
│   └── .env                 # Local, ignorado pelo Git
├── docs/
├── OrderFlow.slnx
└── README.md
```

## Próximas etapas

1. Criar `OrderFlow.Worker` e consumir `pedidos.criados`.
2. Persistir pedidos e consultar status.
3. Implementar idempotência, retry e dead-letter queue.
4. Adicionar testes automatizados, logs estruturados e observabilidade.
5. Preparar Compose para deploy integral do sistema.
