.DEFAULT_GOAL := help

COMPOSE := docker compose --env-file deploy/.env -f deploy/compose.yaml
API_PROJECT := src/OrderFlow.Api
API_URL := http://127.0.0.1:5000

.PHONY: help setup check-env up stop down ps logs build api dev test-api

help:
	@printf 'OrderFlow - comandos disponíveis\n\n'
	@printf '  make setup     Criar deploy/.env a partir do exemplo (se ainda não existir)\n'
	@printf '  make up        Subir o RabbitMQ com Docker Compose\n'
	@printf '  make api       Executar a API .NET 10 localmente\n'
	@printf '  make dev       Subir RabbitMQ e executar a API\n'
	@printf '  make test-api  Enviar um pedido de teste (API deve estar rodando)\n'
	@printf '  make build     Compilar a solução\n'
	@printf '  make ps        Ver status dos serviços\n'
	@printf '  make logs      Acompanhar logs do RabbitMQ\n'
	@printf '  make stop      Parar serviços preservando containers e dados\n'
	@printf '  make down      Remover containers do Compose (preserva volume)\n'

setup:
	@if [ -f deploy/.env ]; then \
		echo 'deploy/.env já existe; não foi alterado.'; \
	else \
		cp deploy/.env.example deploy/.env; \
		chmod 600 deploy/.env; \
		echo 'Arquivo deploy/.env criado. Edite a senha antes de executar make up.'; \
	fi

check-env:
	@test -f deploy/.env || (echo 'Arquivo deploy/.env ausente. Execute make setup e configure as credenciais.' && exit 1)

up: check-env
	$(COMPOSE) up -d

stop: check-env
	$(COMPOSE) stop

down: check-env
	$(COMPOSE) down

ps: check-env
	$(COMPOSE) ps

logs: check-env
	$(COMPOSE) logs -f rabbitmq

build:
	dotnet build

api:
	DOTNET_ENVIRONMENT=Development dotnet run --project $(API_PROJECT) --no-launch-profile --urls $(API_URL)

dev: up api

test-api:
	curl --fail-with-body -i -sS -X POST $(API_URL)/pedidos \
		-H 'Content-Type: application/json' \
		-d '{"clienteId":"cliente-123","itens":[{"produtoId":"produto-001","quantidade":2,"precoUnitario":29.90}]}'