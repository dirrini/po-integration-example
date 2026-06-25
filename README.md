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
- Caddy for HTTPS and automatic certificate renewal
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
POST http://localhost:5000/api/orders
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
80  -> HTTP redirect and Let's Encrypt validation
443 -> HTTPS app traffic
```

Caddy terminates HTTPS and proxies to the frontend container. The frontend proxies `/api/...` requests to the backend container through Nginx, so the backend does not need to be publicly exposed.

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
EXTERNAL_PRODUCTS_TOKEN_URL
EXTERNAL_PRODUCTS_CLIENT_ID
EXTERNAL_PRODUCTS_CLIENT_SECRET
```

Create these GitHub Actions environment variables:

```text
AZURE_VM_HOST
APP_DOMAIN
FRONTEND_DIRECT_ORDERS_URL
FRONTEND_AWS_API_GATEWAY_ORDERS_URL
```

For your current VM:

```text
AZURE_VM_HOST=64.236.155.201
APP_DOMAIN=po-integration-example.dirrini.tech
FRONTEND_DIRECT_ORDERS_URL=https://po-integration-example.dirrini.tech/api/orders
FRONTEND_AWS_API_GATEWAY_ORDERS_URL=https://xiak2r5r5d.execute-api.us-east-1.amazonaws.com/sap-pulse-api-1
```

The frontend URL values are public configuration. Angular bakes them into the browser bundle during the Docker image build.

The workflow runs on pushes to `main` and can also be started manually from the GitHub Actions tab.

## External GraphQL Integration

The worker sends processed purchase orders to the configured GraphQL endpoint using:

```text
ExternalProducts__GraphqlUrl
ExternalProducts__TokenUrl
ExternalProducts__ClientId
ExternalProducts__ClientSecret
```

In development, the default target is:

```text
http://host.docker.internal:4000/graphql
http://host.docker.internal:4000/api/token
```

In production, set the real external GraphQL URL, token URL, client ID, and client secret in `.env` or GitHub Actions secrets. The backend uses the client credentials flow to request a short-lived integration JWT, and that JWT is used as the bearer token for GraphQL calls.

## Author

Developed by Diego S.
