# Vitaent Medical System

Repository bootstrap for the Vitaent monorepo.

## Folder structure

```text
vitaent/
  backend/
    src/
    tests/
  frontend/
```

## PostgreSQL (Docker Compose)

Start PostgreSQL in detached mode:

```bash
docker compose up -d
```

Stop PostgreSQL (keep data volume):

```bash
docker compose down
```

Stop PostgreSQL and remove data volume (reset data):

```bash
docker compose down -v
```

Optional: view container logs:

```bash
docker compose logs -f postgres
```
