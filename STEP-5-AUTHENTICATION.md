# CampusQ Step 5 - API Authentication

Authentication is now handled by ASP.NET Core JWT authentication.

## Local setup
1. Run CampusQ_DatabaseSetup.sql against SQL Server.
2. Set Jwt:Key in appsettings.json or CAMPUSQ_JWT_KEY environment variable.
3. Set CAMPUSQ_CONNECTION_STRING or ConnectionStrings:CampusQ.
4. Start the API.
5. Set NEXT_PUBLIC_API_BASE_URL in the Next.js app to the API URL.
6. Login through /login.

## Demo accounts
These are for development/capstone testing only. Change them before deployment.
- admin / admin123
- staff / staff123 (Registrar)
- cashier / cashier123 (Cashier)
- admission / admission123 (Admission)

## Authorization
- GET queue endpoints are public for student/monitor use.
- POST queue creation is public for the kiosk.
- Call Next, Complete, and Transfer require authentication.
- Staff accounts are restricted to their assigned office.
- Admin accounts can operate across offices.

For a production deployment, prefer secure HttpOnly cookie-based session handling if the frontend/API are configured under a suitable same-site domain.
