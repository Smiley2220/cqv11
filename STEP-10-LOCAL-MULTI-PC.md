# CampusQ — Step 10 Local Multi-PC Test

## Goal
Run one CampusQ API/database on the host PC and connect kiosk, staff, monitor, and phones over the same LAN before production deployment.

## 1. Host PC
Install:
- SQL Server Developer/Express
- SQL Server Management Studio
- .NET 8 SDK
- Node.js LTS

Run the database script in `CampusQ.API/CampusQ_DatabaseSetup.sql`.

Set `CampusQ.API/appsettings.Development.json` or environment variables for the SQL Server instance.

Start API:
```powershell
cd CampusQ.API
dotnet restore
dotnet run --urls http://0.0.0.0:5000
```
Verify on the host:
`http://localhost:5000/health`

Find the host LAN IP:
```powershell
ipconfig
```
Example: `192.168.1.20`.

## 2. Frontend PC / host
Set `CampusQ.Web/.env.local`:
```env
NEXT_PUBLIC_API_BASE_URL=http://192.168.1.20:5000
```
Start:
```powershell
cd CampusQ.Web
npm install
npm run dev -- --hostname 0.0.0.0
```
The frontend is then available at:
`http://192.168.1.20:3000`

## 3. Windows Firewall
On the API host, allow inbound TCP 5000 and frontend TCP 3000 for the Private network only.

PowerShell (run as Administrator):
```powershell
New-NetFirewallRule -DisplayName "CampusQ API 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private
New-NetFirewallRule -DisplayName "CampusQ Web 3000" -Direction Inbound -Protocol TCP -LocalPort 3000 -Action Allow -Profile Private
```
Remove these rules after the test if they are no longer needed.

## 4. Test devices
Use the host IP, never `localhost`, from other PCs/phones:
- Kiosk: `http://192.168.1.20:3000/kiosk`
- Staff: `http://192.168.1.20:3000/login`
- Registrar monitor: `http://192.168.1.20:3000/monitor/registrar`
- Cashier monitor: `http://192.168.1.20:3000/monitor/cashier`
- Admission monitor: `http://192.168.1.20:3000/monitor/admission`

## 5. End-to-end test
1. Create a regular ticket at the kiosk.
2. Create a priority ticket.
3. Scan the QR from a phone.
4. Log in as the correct staff account.
5. Call Next.
6. Confirm the office monitor changes.
7. Confirm the student queue page changes.
8. Complete the ticket.
9. Transfer a ticket to another office.
10. Confirm the target office receives it.
11. Disable a window in Admin and confirm Staff cannot call from it.
12. Check Admin reporting/history.

## 6. Important production rule
Do not expose SQL Server directly to the internet. Only the ASP.NET API should reach SQL Server. For production, use HTTPS and a hosted SQL Server/managed database accessible by the API.
