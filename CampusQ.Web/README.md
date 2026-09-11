# CampusQ Web

Vercel-ready Next.js frontend based on the latest CampusQ desktop project.

## Included
- Responsive CampusQ landing page
- Kiosk flow: office -> purpose -> generate ticket
- Virtual queue ticket page
- Staff queue dashboard with Call Next / Complete
- Office live monitor
- Admin dashboard
- Browser-tab demo synchronization via localStorage
- Original CampusQ logo asset
- API-ready environment variable: `NEXT_PUBLIC_API_BASE_URL`

## Run locally
```bash
npm install
npm run dev
```
Open http://localhost:3000.

## Deploy to Vercel
Push this folder to GitHub, import the repository into Vercel, and deploy. No server configuration is required for the demo frontend.

## Production integration
The current app uses localStorage so the UI can be demonstrated immediately. For the real capstone deployment, replace the functions in `lib/store.ts` with calls to the ASP.NET Core CampusQ API. Keep SQL Server behind the API; the browser should never connect directly to SQL Server. SignalR can then replace the browser-tab event for real-time updates.

## Authentication (Step 4)
The current web demo includes role-based route protection for Staff and Admin. Demo accounts:
- admin / admin123 -> Admin
- staff / staff123 -> Staff / Registrar
- cashier / cashier123 -> Staff / Cashier
- admission / admission123 -> Staff / Admission

This client-side demo authentication is for UI/prototype validation. Before production deployment, replace it with API-issued JWT or secure cookie authentication and enforce authorization on the ASP.NET Core API endpoints as well.
