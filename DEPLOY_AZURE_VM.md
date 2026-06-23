# Azure VM Docker Deployment

This deployment is for `dotnet-angular-app`.

The production stack uses:

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
http://64.236.155.201/
```

## 5. Azure Network Security Group

Open inbound:

- `22` for SSH
- `80` for the web app

Do not expose RabbitMQ publicly unless you intentionally need it.

## 6. Updating later

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

## 7. GitHub Actions auto deploy

The workflow is in `.github/workflows/deploy-azure.yml`.

Create these GitHub repository secrets in `Settings > Secrets and variables > Actions`:

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

Optional repository variable:

```text
AZURE_VM_HOST=64.236.155.201
WEB_PORT=80
```

`AZURE_VM_HOST` is required by the workflow. It is a repository variable instead of being hardcoded in the workflow file.

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
