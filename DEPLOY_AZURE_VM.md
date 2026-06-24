# Azure VM Docker Deployment

This deployment is for `dotnet-angular-app`.

The production stack uses:

- `caddy`: HTTPS reverse proxy with automatic Let's Encrypt certificates.
- `frontend`: Nginx serving the Angular build on port 80.
- `backend`: ASP.NET Core API on the internal Docker network.
- `rabbitmq`: RabbitMQ broker with a persisted Docker volume.

The browser talks to Nginx at `/api/...`; Nginx proxies those requests to the backend container.

## 1. Prepare Ubuntu VM

Install Docker and the Compose plugin:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl git
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $VERSION_CODENAME stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER
```

Log out and back in after `usermod`.

## 2. Copy project to VM

```bash
git clone <your-repo-url> dotnet-angular-app
cd dotnet-angular-app
```

Or upload this folder with `scp`/SFTP.

## 3. Create production env file

```bash
cp .env.production.example .env
nano .env
```

Set:

```env
APP_DOMAIN=po-integration-example.dirrini.tech
EXTERNAL_PRODUCTS_GRAPHQL_URL=https://your-project-pulse-domain.com/graphql
EXTERNAL_PRODUCTS_API_KEY=<integration-api-key-from-project-pulse>
RABBITMQ_DEFAULT_PASS=<strong-password>
RabbitMq__Password=<same-strong-password>
```

## 4. Build and run

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Check logs:

```bash
docker compose -f docker-compose.prod.yml --env-file .env logs -f
```

Open:

```text
https://po-integration-example.dirrini.tech/
```

HTTP also works, but Caddy will redirect it to HTTPS.

The VM can still be reached directly over HTTP if needed:

```text
http://64.236.155.201/
```

## 5. Azure Network Security Group

Open inbound:

- `22` for SSH
- `80` for HTTP and Let's Encrypt validation
- `443` for HTTPS

Do not expose RabbitMQ publicly unless you intentionally need it.

## 6. Updating later

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

## 7. GitHub Actions auto deploy

The workflow is in `.github/workflows/deploy-azure.yml`.

Create these GitHub Environment secrets in `Settings > Environments > production`:

```text
AZURE_VM_USER
AZURE_VM_SSH_KEY
RABBITMQ_DEFAULT_USER
RABBITMQ_DEFAULT_PASS
EXTERNAL_PRODUCTS_GRAPHQL_URL
EXTERNAL_PRODUCTS_API_KEY
```

`AZURE_VM_USER` is usually `azureuser` or `ubuntu`, depending on how the VM was created.

`AZURE_VM_SSH_KEY` must be the private key that matches a public key in the VM user's `~/.ssh/authorized_keys`.

Create these GitHub Environment variables in `Settings > Environments > production`:

```text
AZURE_VM_HOST=64.236.155.201
APP_DOMAIN=po-integration-example.dirrini.tech
HTTP_PORT=80
HTTPS_PORT=443
```

`AZURE_VM_HOST` is required by the workflow. It is an environment variable instead of being hardcoded in the workflow file.
`APP_DOMAIN` is required so Caddy can request the correct SSL certificate.

`HTTP_PORT` and `HTTPS_PORT` are optional because the workflow defaults to `80` and `443`, but adding them makes the deployment settings explicit.

If the workflow log shows empty values like this:

```text
env:
  AZURE_VM_HOST:
  DEPLOY_PATH: /home//dotnet-angular-app
```

then `AZURE_VM_HOST` was not created as an environment variable, or `AZURE_VM_USER` was not created as an environment secret in the `production` environment.

## 8. Custom domain

This deployment is configured for:

```text
po-integration-example.dirrini.tech
```

Your DNS provider should have an `A` record like this:

```text
po-integration-example.dirrini.tech -> 64.236.155.201
```

Azure must allow inbound traffic on ports `80` and `443`.

Caddy will request and renew the SSL certificate automatically. After changing DNS, Caddy config, or production compose, redeploy:

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Check the Caddy logs during the first certificate request:

```bash
docker compose -f docker-compose.prod.yml --env-file .env logs -f caddy
```

If you see this message:

```text
no such service: caddy
```

The VM still has an older copy of `docker-compose.prod.yml`. Update the project files on the VM, then redeploy:

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env config --services
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker compose -f docker-compose.prod.yml --env-file .env logs -f caddy
```

The `config --services` command should include:

```text
caddy
```

The workflow runs automatically on pushes to `main`, or manually from GitHub Actions with `workflow_dispatch`.

Before the first workflow run, make sure Docker is installed on the VM and that the VM user can run Docker:

```bash
docker --version
docker compose version
groups
```

If the user is not in the `docker` group:

```bash
sudo usermod -aG docker $USER
```

Then log out and back in.
