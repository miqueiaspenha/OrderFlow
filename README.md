# OrderFlow

Laboratório de backend com **.NET 10**, API REST, RabbitMQ e Docker. O projeto simula a recepção de pedidos e evoluirá para processamento assíncrono com um Worker.

## Estado atual

- **OrderFlow.Api:** endpoint `POST /pedidos`, validação básica e publicação de `PedidoCriado` na fila `pedidos.criados` com confirmação de publicação.
- **RabbitMQ:** roda localmente pelo Docker Compose em `deploy/compose.yaml`.
- **Makefile:** automatiza configuração inicial, inicialização do RabbitMQ, execução da API, testes manuais e comandos de manutenção.
- **Testes manuais:** requisições REST Client em `tests/pedidos.http` e `make test-api`.
- **Ainda não implementado nesta etapa documentada:** Worker, persistência, consulta de status e testes automatizados.

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
- **GNU Make** — `make --version`
- Git
- Opcional: VS Code com a extensão **REST Client** (`humao.rest-client`)

Os comandos devem ser executados na **raiz do repositório**. Os exemplos usam terminal Linux com **ZSH** (também são compatíveis com Bash, exceto o comando `read` indicado abaixo).

## Primeira configuração (uma vez por computador)

### 1. Clonar e restaurar

```bash
git clone https://github.com/miqueiaspenha/OrderFlow.git
cd OrderFlow
dotnet restore
```

Se já tiver clonado, apenas acesse a pasta do projeto.

### 2. Criar as configurações locais do RabbitMQ

O Makefile simplifica a criação do arquivo de ambiente:

```bash
make setup
```

O comando copia `deploy/.env.example` para `deploy/.env` **apenas se o arquivo ainda não existir**. Depois, edite `deploy/.env` e defina uma senha própria:

```dotenv
RABBITMQ_USER=orderflow
RABBITMQ_PASSWORD=troque-por-uma-senha-forte
```

O arquivo `.env` é ignorado pelo Git. Não envie credenciais ao repositório.

### 3. Subir o RabbitMQ

```bash
make up
make ps
```

| Serviço | Endereço |
| --- | --- |
| RabbitMQ / AMQP | `localhost:5672` |
| Painel RabbitMQ | http://localhost:15672 |

Entre no painel com as credenciais definidas em `deploy/.env`. O virtual host é **`orderflow`**.

> O RabbitMQ cria usuário, senha e virtual host no **primeiro uso do volume**. Alterar `deploy/.env` posteriormente não redefine automaticamente as credenciais já existentes.

### 4. Configurar .NET User Secrets

O `UserSecretsId` já está no arquivo `src/OrderFlow.Api/OrderFlow.Api.csproj`, mas os valores são específicos de cada máquina.

```bash
dotnet user-secrets set 'RabbitMQ:Host' 'localhost' --project src/OrderFlow.Api
dotnet user-secrets set 'RabbitMQ:VirtualHost' 'orderflow' --project src/OrderFlow.Api
dotnet user-secrets set 'RabbitMQ:UserName' 'orderflow' --project src/OrderFlow.Api
```

Se o usuário do `.env` for diferente, ajuste `RabbitMQ:UserName` também. **No ZSH**, configure a senha sem incluí-la diretamente no histórico:

```zsh
read -rs 'RABBITMQ_PASSWORD?Senha do RabbitMQ: '
echo
dotnet user-secrets set 'RabbitMQ:Password' "$RABBITMQ_PASSWORD" --project src/OrderFlow.Api
unset RABBITMQ_PASSWORD
```

> No Bash, a leitura da senha seria `read -rsp 'Senha do RabbitMQ: ' RABBITMQ_PASSWORD`. O ZSH usa a sintaxe acima.

Os User Secrets **não são criptografados**: use-os apenas em desenvolvimento. Se houver variáveis de ambiente anteriores, remova-as da sessão, pois elas prevalecem sobre os User Secrets:

```bash
unset RabbitMQ__Host RabbitMQ__VirtualHost RabbitMQ__UserName RabbitMQ__Password
```

### 5. Compilar e executar a API

```bash
make build
make api
```

Mantenha o terminal da API aberto. A aplicação deverá escutar em `http://127.0.0.1:5000`.

## Automação com Makefile

O arquivo `Makefile`, na raiz do repositório, concentra os comandos do ambiente local. **Não é necessário memorizar os parâmetros do Docker Compose e do `dotnet run`.** Execute `make` ou `make help` para consultar as opções.

| Comando | O que faz |
| --- | --- |
| `make help` | Lista os comandos disponíveis. |
| `make setup` | Cria `deploy/.env` a partir do exemplo, sem sobrescrever um arquivo existente. |
| `make up` | Sobe o RabbitMQ pelo Docker Compose. |
| `make api` | Executa a API .NET 10 em modo Development na porta 5000. |
| `make dev` | Sobe o RabbitMQ e, em seguida, inicia a API **no mesmo terminal**. |
| `make test-api` | Envia um pedido válido via `curl` para a API em execução. |
| `make build` | Compila a solução. |
| `make ps` | Mostra o estado dos containers do Compose. |
| `make logs` | Acompanha os logs do RabbitMQ. |
| `make stop` | Para o RabbitMQ, preservando containers e dados. |
| `make down` | Remove o container e a rede do Compose, preservando o volume. |

**Atenção:** `make setup` não define sua senha automaticamente. Configure `deploy/.env` e os User Secrets **uma vez por máquina** antes de usar `make dev`. `make stop` e `make down` agem sobre os serviços Docker: não encerram uma API iniciada em outro terminal. Para parar o `make api`/`make dev`, pressione `Ctrl+C` no terminal correspondente.

## Uso diário

Depois da configuração inicial, basta executar **em um terminal**:

```bash
make dev
```

Isso inicializa o RabbitMQ e executa a API localmente. Deixe esse terminal aberto e use **outro terminal** para enviar um pedido:

```bash
make test-api
```

Se preferir executar a infraestrutura e a API separadamente, use `make up` em um terminal e `make api` em outro.

## Testar a API

### Pelo REST Client

Abra `tests/pedidos.http` no VS Code/OpenVSCode com a extensão REST Client e clique em **Send Request** acima da requisição desejada.

### Pelo Makefile

Com a API em execução:

```bash
make test-api
```

Resultado esperado: HTTP **202 Accepted**, contendo `pedidoId` e `status: "Enfileirado"`. Pedidos inválidos devem retornar **400 Bad Request**.

Para conferir quantas mensagens aguardam consumo:

```bash
docker exec orderflow-rabbitmq rabbitmqctl list_queues -p orderflow name messages_ready consumers
```

Sem o Worker, as mensagens permanecerão na fila.

## Problemas comuns

- **`make: command not found`**: instale o GNU Make na sua máquina.
- **Arquivo `deploy/.env` ausente**: execute `make setup` e configure sua senha antes de `make up`.
- **`Connection refused`**: execute `make ps` e aguarde o RabbitMQ ficar saudável.
- **`ACCESS_REFUSED` / autenticação**: confira usuário, senha, virtual host e as credenciais salvas no volume do RabbitMQ.
- **Tentativa de conexão com `lab-rabbitmq`**: esse era o hostname na VPS. Na máquina local, configure `RabbitMQ:Host=localhost` nos User Secrets.
- **Portas 5672 ou 15672 ocupadas**: verifique se já existe outra instância do RabbitMQ.
- **API encerra imediatamente**: confira a saída do terminal e se `Program.cs` termina com `app.Run();`.

## Estrutura principal

```text
OrderFlow/
├── src/
│   └── OrderFlow.Api/       # API e publicador RabbitMQ
├── tests/
│   └── pedidos.http         # Requisições manuais
├── deploy/
│   ├── compose.yaml         # Infraestrutura local
│   ├── .env.example         # Exemplo sem credenciais reais
│   └── .env                 # Local, ignorado pelo Git
├── docs/
├── Makefile                 # Comandos de desenvolvimento
├── OrderFlow.slnx
└── README.md
```

## Próximas etapas

1. Criar `OrderFlow.Worker` e consumir `pedidos.criados`.
2. Persistir pedidos e consultar status.
3. Implementar idempotência, retry e dead-letter queue.
4. Adicionar testes automatizados, logs estruturados e observabilidade.
5. Preparar Compose para deploy integral do sistema.
