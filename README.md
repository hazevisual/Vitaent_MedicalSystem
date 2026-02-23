# Vitaent Medical System

Repository bootstrap for the Vitaent monorepo.

## Folder structure

```text
vitaent/
  backend/
    src/
      Vitaent.Api/
      Vitaent.Application/
      Vitaent.Domain/
      Vitaent.Infrastructure/
    tests/
      Vitaent.Api.Tests/
      Vitaent.Infrastructure.Tests/
  frontend/
```

## Docker-first (recommended)

Build and run the full stack (Postgres + Backend + Frontend):

```bash
docker compose up -d --build
```

Then open:

- Frontend: `http://localhost:5173`
- Backend API (direct): `http://localhost:5080`
- Swagger (direct backend): `http://localhost:5080/swagger`

Default login credentials:

- Email: `admin@clinic1.local`
- Password: `Admin123!`

Stop stack:

```bash
docker compose down
```

### What happens automatically in Docker

On backend startup, the API now automatically:

1. Applies EF Core migrations to the Postgres database.
2. Seeds a default tenant `clinic1` (idempotent).
3. Seeds default branding for `clinic1` so `/api/tenant/branding?tenant=clinic1` returns a valid payload.

No manual host-side `dotnet ef` command is required.

Stop and reset database volume:

```bash
docker compose down -v
```

## API routing approach

This project uses **same-origin proxy (Option A)** for frontend API calls:

- Frontend calls relative paths like `/api/auth/sign-in?tenant=clinic1`.
- In Docker, Nginx (frontend container) proxies `/api/*` to `http://backend:5080`.
- In local Vite dev mode, `vite.config.ts` proxies `/api/*` to `http://localhost:5080`.

This avoids CORS issues and keeps cookie-based refresh flows working consistently.


## Offline / No-Docker mode (Cursor-friendly)

If Docker is unavailable and npm install is blocked, you can still run backend + fallback UI:

```bash
cd vitaent/backend
dotnet run --project src/Vitaent.Api
```

Open fallback UI:

- `http://localhost:5080/static-fallback`

The fallback page is plain HTML/JS (no npm dependencies) and supports branding load, sign-in, tenant me, refresh, and sign-out.

## Local non-Docker development

Backend:

```bash
cd vitaent/backend
dotnet build
dotnet test
dotnet run --project src/Vitaent.Api
```

Frontend:

```bash
cd vitaent/frontend
npm i
npm run dev
```

Frontend env:

- By default, no `VITE_API_URL` is needed (same-origin `/api` usage).
- Optional override can be set in `.env` if needed.

## Smoke test (no Docker)

Run the backend first, then execute:

```bash
API_URL=http://localhost:5080 bash vitaent/scripts/smoke-auth.sh
```

The script verifies `/health`, tenant resolution, sign-in, bearer-authenticated `/api/tenant/me`, refresh, and sign-out.

## API/Auth curl examples

```bash
# sign-in (stores refresh cookie in cookies.txt)
curl -i -c cookies.txt -H "Content-Type: application/json" \
  -d '{"email":"admin@clinic1.local","password":"Admin123!"}' \
  "http://localhost:5080/api/auth/sign-in?tenant=clinic1"

# refresh
curl -i -b cookies.txt -c cookies.txt -X POST \
  "http://localhost:5080/api/auth/refresh?tenant=clinic1"

# sign-out (requires Bearer access token from sign-in)
curl -i -b cookies.txt -X POST \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  "http://localhost:5080/api/auth/sign-out?tenant=clinic1"
```

## Appointment API (MVP)

All appointment endpoints require JWT and tenant fallback query param in local/dev examples.

Create appointment (valid):

```bash
curl -i -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"doctorId":"<DOCTOR_ID>","patientName":"Peter Parker","startsAt":"2026-03-01T10:00:00Z","endsAt":"2026-03-01T10:30:00Z"}' \
  "http://localhost:5080/api/appointments?tenant=clinic1"
```

Example validation error (400 ProblemDetails):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Appointment validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "patientName": ["patientName must be between 2 and 120 characters."],
    "duration": ["duration must be between 5 and 120 minutes."]
  }
}
```

Example doctor-not-found error (404 ProblemDetails):

```json
{
  "title": "Doctor not found",
  "status": 404,
  "detail": "Doctor not found"
}
```

Example duplicate-slot error (409 ProblemDetails):

```json
{
  "title": "Slot already booked",
  "status": 409,
  "detail": "Slot already booked"
}
```

## Tenant troubleshooting

- For local/dev usage, always include query fallback `?tenant=clinic1` on `/api/*` routes if you are not using subdomain hostnames.
- Subdomain-based resolution (e.g. `clinic1.localhost`) also works when your local DNS/hosts setup supports it.

## Integration test notes

`Vitaent.Api.Tests` uses `WebApplicationFactory` + EF InMemory for auth integration tests.

```bash
cd vitaent/backend
dotnet test
```


## Docker verification commands

After `docker compose up -d --build`, verify everything is initialized:

```bash
# verify containers
docker compose ps

# verify DB tables (including tenants)
docker compose exec postgres psql -U vitaent -d vitaent -c "\dt"

# verify seeded tenant exists
docker compose exec postgres psql -U vitaent -d vitaent -c "select slug, name, is_active from tenants;"

# branding endpoint through frontend proxy
curl -i "http://localhost:5173/api/tenant/branding?tenant=clinic1"
```

Expected result:

- `tenants` table exists.
- `clinic1` row exists.
- branding endpoint responds `200 OK` with JSON payload for `clinic1`.

## Docker troubleshooting

- If startup was interrupted, retry with:

```bash
docker compose down
docker compose up -d --build
```

- To reset to a clean database state:

```bash
docker compose down -v
docker compose up -d --build
```

- Backend logs (migration/seed status):

```bash
docker compose logs -f backend
```


## Acceptance validation steps

Run the exact fresh-volume validation flow:

```bash
docker compose down -v
docker compose up -d --build
docker compose exec postgres psql -U vitaent -d vitaent -c "\dt"
docker compose exec postgres psql -U vitaent -d vitaent -c "select slug from tenants where slug='clinic1';"
curl -i "http://localhost:5173/api/tenant/branding?tenant=clinic1"
```

Expected:

- tables exist (including `tenants`).
- `clinic1` exists in `tenants`.
- branding endpoint returns `200` for `clinic1`.
- frontend login page loads at `http://localhost:5173` without hanging loader.
