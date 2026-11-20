# OrderFlow

Laboratório de backend com **.NET 10**, API REST, RabbitMQ e Docker. O sistema simula o recebimento e o processamento assíncrono de pedidos.

## Estado do projeto

- **OrderFlow.Api:** endpoint `POST /pedidos`, validação básica e publicação do evento `PedidoCriado` na fila `pedidos.criados`, com confirmação de publicação pelo RabbitMQ.
- **OrderFlow.Worker:** consumidor implementado em .NET Worker Service (`BackgroundService`), responsável por desserializar eventos, registrar o processamento nos logs e confirmar o recebimento com ACK manual.
- **RabbitMQ:** executado localmente com Docker Compose (`deploy/compose.yaml`).
- **Makefile:** atalhos para iniciar infraestrutura, executar API e Worker, compilar e fazer testes manuais.
- **Testes manuais:** `tests/pedidos.http` com REST Client ou `make test-api` com `curl`.
- **Ainda não implementado:** persistência de pedidos, consulta de status, retry/DLQ, idempotência, testes automatizados e observabilidade.

> `202 Accepted` significa que o broker confirmou a publicação, **não** que o Worker concluiu o processamento. Nesta etapa, o processamento do Worker se limita a registrar informações no log.

## Arquitetura local

```text
REST Client / curl
       |
       | POST /pedidos
       v
OrderFlow.Api (.NET 10, processo local)
       |
       | publica PedidoCriado
       v
RabbitMQ (Docker, fila pedidos.criados)
       |
       | consome evento
       v
OrderFlow.Worker (.NET 10, processo local)
       |
       +--> logs + ACK manual
```

O RabbitMQ é o único componente que precisa rodar no Docker para este estágio. A API e o Worker são processos .NET locais, permitindo depuração com breakpoints.

## Pré-requisitos

- **.NET SDK 10** — `dotnet --version`
- **Docker e Docker Compose** — `docker compose version`
- **GNU Make** — `make --version`
- Git
- Opcional: VS Code com a extensão **REST Client** (`humao.rest-client`)

Execute os comandos na **raiz do repositório**. Os exemplos de terminal consideram Linux com **ZSH**; os comandos `make` também funcionam com Bash.

## Primeira configuração (uma vez por máquina)

### 1. Clonar, restaurar e compilar

```bash
git clone https://github.com/miqueiaspenha/OrderFlow.git
cd OrderFlow
dotnet restore
make build
```

Se o projeto já estiver clonado, apenas entre na pasta. O Worker precisa estar presente em `src/OrderFlow.Worker` e adicionado à solução `OrderFlow.slnx`.

### 2. Configurar o RabbitMQ no Docker

```bash
make setup
```

O comando cria `deploy/.env` a partir de `deploy/.env.example` **sem sobrescrever um arquivo existente**. Edite `deploy/.env`:

```dotenv
RABBITMQ_USER=orderflow
RABBITMQ_PASSWORD=troque-por-uma-senha-forte
```

O arquivo é ignorado pelo Git e não deve ser versionado. Inicie e confira a infraestrutura:

```bash
make up
make ps
```

| Serviço | Endereço |
| --- | --- |
| AMQP | `localhost:5672` |
| Painel RabbitMQ | http://localhost:15672 |

No painel, use as credenciais do `.env`. O virtual host é **`orderflow`**.

> A configuração inicial de usuário/senha é aplicada quando o volume do RabbitMQ é criado. Editar o `.env` posteriormente **não muda automaticamente** as credenciais que já existem no broker.

### 3. Configurar os User Secrets da API e do Worker

Os dois projetos usam as **mesmas credenciais**, mas têm **UserSecretsId independentes**. Configure as chaves nos dois projetos. Se o Worker ainda não tiver um `UserSecretsId`, execute antes:

```bash
dotnet user-secrets init --project src/OrderFlow.Worker
```

Configure host, virtual host e usuário (substitua `orderflow` se você escolheu outro usuário no `.env`):

```bash
for projeto in src/OrderFlow.Api src/OrderFlow.Worker; do
  dotnet user-secrets set 'RabbitMQ:Host' 'localhost' --project "$projeto"
  dotnet user-secrets set 'RabbitMQ:VirtualHost' 'orderflow' --project "$projeto"
  dotnet user-secrets set 'RabbitMQ:UserName' 'orderflow' --project "$projeto"
done
```

**No ZSH**, informe a senha sem mostrá-la na tela ou inseri-la no histórico:

```zsh
read -rs 'RABBITMQ_PASSWORD?Senha do RabbitMQ: '
echo
for projeto in src/OrderFlow.Api src/OrderFlow.Worker; do
  dotnet user-secrets set 'RabbitMQ:Password' "$RABBITMQ_PASSWORD" --project "$projeto"
done
unset RABBITMQ_PASSWORD
```

> No Bash, o comando para ler a senha é `read -rsp 'Senha do RabbitMQ: ' RABBITMQ_PASSWORD`.

Os User Secrets **não são criptografados**: são apropriados para desenvolvimento. As variáveis de ambiente têm precedência; se existirem valores antigos, remova-os do terminal:

```bash
unset RabbitMQ__Host RabbitMQ__VirtualHost RabbitMQ__UserName RabbitMQ__Password
```

## Execução diária

Você pode iniciar a API e o Worker em **terminais separados**, na raiz do projeto:

**Terminal 1 — subir RabbitMQ e executar API:**

```bash
make dev
```

Esse comando executa `make up` e inicia a API em `http://127.0.0.1:5000`. Deixe o terminal aberto.

**Terminal 2 — executar o Worker:**

```bash
make worker
```

O Worker deverá ficar aguardando mensagens na fila. Para interrompê-lo, use `Ctrl+C` nesse terminal.

**Terminal 3 — enviar um pedido de teste:**

```bash
make test-api
```

Alternativamente, abra `tests/pedidos.http` com a extensão REST Client e clique em **Send Request**.

### O que esperar

1. A API recebe `POST /pedidos`, valida o JSON e publica `PedidoCriado`.
2. A API retorna **HTTP 202 Accepted**, incluindo um `pedidoId` e `status: "Enfileirado"`.
3. O Worker consome a mensagem, registra `PedidoId`/`ClienteId` nos logs e envia **ACK** após esse processamento inicial.
4. A fila fica sem mensagens prontas se todos os eventos tiverem sido consumidos.

Para conferir o estado da fila:

```bash
docker exec orderflow-rabbitmq \
  rabbitmqctl list_queues -p orderflow \
  name messages_ready messages_unacknowledged consumers
```

Após o processamento completo, a saída deve indicar `messages_ready=0`, `messages_unacknowledged=0` e normalmente `consumers=1` com o Worker conectado. Se houver mensagens antigas ou outros consumidores, os números podem variar.

> Nesta implementação inicial, eventos com JSON inválido recebem NACK sem reenvio; **sem DLQ configurada, eles são descartados**. Ainda não há tratamento robusto de falhas transitórias nem persistência, portanto não use esse fluxo para dados importantes.

## Automação com Makefile

Execute `make` ou `make help` para listar os atalhos.

| Comando | Função |
| --- | --- |
| `make help` | Lista os atalhos disponíveis. |
| `make setup` | Cria `deploy/.env` a partir do exemplo, se necessário. |
| `make up` | Inicia o RabbitMQ no Docker. |
| `make api` | Executa somente a API .NET localmente. |
| `make worker` | Executa somente o consumidor de mensagens em .NET. |
| `make dev` | Inicia RabbitMQ e API no mesmo terminal; **não inicia o Worker**. |
| `make test-api` | Envia uma requisição de pedido válido com `curl`. |
| `make build` | Compila a solução. |
| `make ps` | Exibe o status dos containers. |
| `make logs` | Acompanha os logs do RabbitMQ. |
| `make stop` | Para o container preservando dados. |
| `make down` | Remove o container/rede do Compose, preservando o volume. |

Para desligar a API e o Worker, pressione `Ctrl+C` nos respectivos terminais. `make stop` e `make down` afetam somente os serviços Docker. **Evite `docker compose down -v`**, pois ele remove também o volume persistente do RabbitMQ.

## Problemas comuns

- **`make: command not found`:** instale GNU Make.
- **`deploy/.env` ausente:** execute `make setup` e edite a senha.
- **API ou Worker não inicia:** confirme os User Secrets do respectivo projeto, a variável `DOTNET_ENVIRONMENT=Development` e a disponibilidade do RabbitMQ (`make ps`).
- **`ACCESS_REFUSED`:** compare usuário/senha/vhost dos User Secrets com os criados no RabbitMQ (lembre-se da persistência do volume).
- **Tentativa de conexão com `lab-rabbitmq`:** esse hostname era usado na rede Docker da VPS. Na máquina local, configure `RabbitMQ:Host=localhost`.
- **Porta 5672/15672 ocupada:** confira se já existe outro RabbitMQ na máquina.
- **A API retorna 202, mas o Worker não registra o pedido:** confirme se o Worker está ativo, inspecione seus logs e verifique a fila com `rabbitmqctl list_queues`.
- **`read: -p: no coprocess`:** no ZSH, use `read -rs 'VAR?Prompt: '` em vez de `read -rsp` do Bash.

## Estrutura principal

```text
OrderFlow/
├── src/
│   ├── OrderFlow.Api/          # Endpoint e publicação de pedidos
│   └── OrderFlow.Worker/       # Consumo RabbitMQ e ACK
├── tests/
│   └── pedidos.http            # Requisições manuais
├── deploy/
│   ├── compose.yaml            # RabbitMQ local
│   ├── .env.example            # Modelo sem segredos
│   └── .env                    # Local, não versionado
├── docs/
├── Makefile                    # Comandos de desenvolvimento
├── OrderFlow.slnx
└── README.md
```

## Próximas etapas

1. Adicionar testes automatizados e consolidar tratamento de falhas do consumidor.
2. Persistir pedidos e consultar seu status.
3. Implementar idempotência, retry e dead-letter queue.
4. Adicionar logs estruturados e observabilidade.
5. Preparar Docker Compose para deploy integral do sistema.
