# CampusQ Web — Vercel + ASP.NET Core API

This is the web migration foundation of the latest CampusQ desktop project.

## Structure
- `CampusQ.Web` — Next.js/React/TypeScript frontend. Deploy this folder to Vercel.
- `CampusQ.API` — ASP.NET Core 8 API + SignalR. Deploy this separately to an ASP.NET-capable host (Azure App Service, IIS, Render, Railway, etc.).
- `CampusQ.Core` — shared SQL Server data/repository code from the existing CampusQ project.

## Run frontend
```bash
cd CampusQ.Web
npm install
npm run dev
```
Set `NEXT_PUBLIC_API_BASE_URL` to the public HTTPS URL of CampusQ.API. Example: `https://api.example.com`.

## Run API locally
Requires .NET 8 SDK and SQL Server/SQL Express.
```bash
dotnet restore CampusQ.API/CampusQ.API.csproj
dotnet run --project CampusQ.API/CampusQ.API.csproj
```
Set `CAMPUSQ_CONNECTION_STRING` or `ConnectionStrings:CampusQ` for the database.

## Production architecture
Browser -> Vercel Next.js -> HTTPS -> ASP.NET Core API -> SQL Server
                                   \\-> SignalR real-time updates

Do not put the SQL Server connection string in the Next.js/Vercel environment as a `NEXT_PUBLIC_*` variable. Only the API should know the database credentials.

## Important
The current API adds `CurrentCalls` so a ticket remains in the queue while it is being served. This is required for the live monitor and virtual queue behavior. Existing queue/history tables remain compatible.

## Step 2 — Live API connection

Set `CampusQ.Web/.env.local`:

`NEXT_PUBLIC_API_BASE_URL=https://your-api-domain.example.com`

Run API:

`dotnet run --project CampusQ.API`

Run web:

`cd CampusQ.Web`
`npm install`
`npm run dev`

Open `/kiosk` to generate a ticket, `/staff` to call/complete tickets, `/monitor/registrar` for the live monitor, and `/queue/<ticketNumber>` for the student virtual queue.

## Production deployment

See `DEPLOYMENT.md` for the Vercel + ASP.NET Core + SQL Server deployment procedure.
