# CampusQ Production Deployment

## Architecture

- `CampusQ.Web`: Next.js frontend deployed to Vercel.
- `CampusQ.API`: ASP.NET Core 8 API deployed to a .NET-capable host/container.
- SQL Server: managed/hosted SQL Server reachable from the API host.
- SignalR: served by the same API over HTTPS at `/hubs/queue`.

Vercel is for the Next.js frontend. Do not put SQL Server credentials in Vercel client variables.

## 1. SQL Server

Run `CampusQ.API/CampusQ_DatabaseSetup.sql` against the production database. Create a least-privilege SQL login for the API. Do not use `sa`.

## 2. API environment variables

Required in production:

- `CAMPUSQ_CONNECTION_STRING`
- `CAMPUSQ_JWT_KEY` — random secret, at least 32 characters; use a secret manager.

The connection string can alternatively be placed under `ConnectionStrings:CampusQ` in production configuration.

Configure `Cors:AllowedOrigins` with the exact Vercel origin, e.g. `https://your-campusq.vercel.app`. Do not use `*` when credentials/authentication are enabled.

## 3. Build API

```bash
dotnet restore CampusQ.API/CampusQ.API.csproj
dotnet publish CampusQ.API/CampusQ.API.csproj -c Release -o ./publish
```

Health check:

```text
GET https://YOUR_API_HOST/health
```

Expected response contains `"status":"healthy"`.

## 4. Container deployment

The included `CampusQ.API/Dockerfile` builds the API on .NET 8 and listens on port `8080`.

```bash
docker build -t campusq-api ./CampusQ.API
docker run -p 8080:8080 \
  -e CAMPUSQ_CONNECTION_STRING='YOUR_CONNECTION_STRING' \
  -e CAMPUSQ_JWT_KEY='YOUR_RANDOM_SECRET' \
  campusq-api
```

Configure your hosting provider to expose HTTPS and forward traffic to port 8080.

## 5. Vercel

Deploy only `CampusQ.Web` as the Vercel project root.

Set this environment variable in Vercel:

```text
NEXT_PUBLIC_API_BASE_URL=https://YOUR_API_HOST
```

Redeploy after changing environment variables.

## 6. SignalR

The browser connects to:

```text
https://YOUR_API_HOST/hubs/queue
```

Ensure the API host supports WebSockets. If WebSockets are unavailable, the frontend's polling fallback should still keep queue information refreshed, but real-time updates will be less immediate.

## 7. Multi-PC setup

Every device uses the same Vercel URL:

- Kiosk PC: `/kiosk`
- Staff PC: `/login` then `/staff`
- Office monitor: `/monitor/registrar`, `/monitor/cashier`, `/monitor/admission`
- Student phone: scan the QR code to `/queue/{ticket}`

No separate database is installed on each PC.

## Security checklist before defense/production

- Replace all demo passwords.
- Set a strong random `CAMPUSQ_JWT_KEY`.
- Use HTTPS for both frontend and API.
- Restrict CORS to the Vercel domain.
- Use a least-privilege SQL login.
- Disable or protect Swagger in production.
- Do not commit `.env` files or secrets.
- Configure database backups.
- Test kiosk printing with the actual thermal printer.
