# OrderFlow

Laboratório de desenvolvimento Backend com .NET 10 e DevOps.

## Objetivo

Simular o processamento assíncrono de pedidos utilizando APIs REST, mensageria e processamento em background.

## Tecnologias planejadas

- .NET 10 / ASP.NET Core
- RabbitMQ
- PostgreSQL
- Docker
- Redis
- OpenTelemetry
- Prometheus e Grafana
- Testes automatizados

## Estado atual

Estrutura inicial da solução e projeto OrderFlow.Api criados.

## Arquitetura inicial

Cliente HTTP -> OrderFlow.Api -> RabbitMQ -> OrderFlow.Worker
