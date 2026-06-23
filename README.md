# Nexus Orders Integration Demo

Angular and ASP.NET Core application that simulates a SAP purchase order intake flow. The frontend acts as a SAP-like interface, the API receives purchase orders through a MuleSoft-style endpoint, RabbitMQ queues the work, and a backend worker integrates processed orders with an external GraphQL product service.

## Architecture

```text
Angular UI
  -> ASP.NET Core API
  -> RabbitMQ queue
  -> ASP.NET Core worker
  -> External GraphQL product API
```

The backend is split into three projects:

- `Nexus.Orders.Api`: HTTP API, worker startup, RabbitMQ consumer.
- `Nexus.Orders.Application`: contracts and domain models.
- `Nexus.Orders.Infrastructure`: RabbitMQ publisher and external HTTP/GraphQL integration.

## Tech Stack

- Angular 17
- ASP.NET Core / .NET 8
- RabbitMQ
- Docker and Docker Compose
- Nginx for production frontend hosting
- GitHub Actions for Azure VM deployment

## Local Development

Local development uses `docker-compose.yml` and `.env.local`.

Start the stack:

```bash
docker compose --env-file .env.local up -d --build
```

Open:

```text
Frontend: http://localhost:4200
Backend:  http://localhost:5000
RabbitMQ: http://localhost:15672
```

RabbitMQ local credentials:

```text
guest / guest
```

Useful commands:

```bash
docker compose --env-file .env.local ps
docker compose --env-file .env.local logs -f backend
docker compose --env-file .env.local logs -f frontend
docker compose --env-file .env.local down
```

Worker controls:

```text
GET http://localhost:5000/api/worker/start
GET http://localhost:5000/api/worker/stop
```

Purchase order endpoint:

```text
POST http://localhost:5000/api/mulesoft/orders
```

## Environment Files

Development variables live in:

```text
.env.local
```

Production variables live in:

```text
.env
```

Production template:

```text
.env.production.example
```

The real `.env` and `.env.local` files are ignored by Git. Keep secrets out of commits.

## Production Deployment

Production uses:

```text
docker-compose.prod.yml
```

Run on the server:

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Production ports:

```text
80 -> Angular/Nginx frontend
```

The frontend proxies `/api/...` requests to the backend container through Nginx, so the backend does not need to be publicly exposed.

For Azure VM setup details, see:

```text
DEPLOY_AZURE_VM.md
```

## GitHub Actions Deployment

The deployment workflow is:

```text
.github/workflows/deploy-azure.yml
```

Create these GitHub Actions secrets:

```text
AZURE_VM_USER
AZURE_VM_SSH_KEY
RABBITMQ_DEFAULT_USER
RABBITMQ_DEFAULT_PASS
EXTERNAL_PRODUCTS_GRAPHQL_URL
EXTERNAL_PRODUCTS_API_KEY
```

Create this GitHub Actions repository variable:

```text
AZURE_VM_HOST
```

For your current VM:

```text
AZURE_VM_HOST=64.236.155.201
```

The workflow runs on pushes to `main` and can also be started manually from the GitHub Actions tab.

## External GraphQL Integration

The worker sends processed purchase orders to the configured GraphQL endpoint using:

```text
ExternalProducts__GraphqlUrl
ExternalProducts__ApiKey
```

In development, the default target is:

```text
http://host.docker.internal:4000/graphql
```

In production, set the real external GraphQL URL and bearer token in `.env` or GitHub Actions secrets.

## Repository Description

SAP-style purchase order integration demo using Angular, ASP.NET Core, RabbitMQ, Docker, and GraphQL, with local development and Azure VM deployment workflows.

## Suggested Tags

```text
angular
dotnet
aspnet-core
docker
docker-compose
rabbitmq
graphql
azure
github-actions
microservices
clean-architecture
integration
sap
purchase-orders
message-queue
```
