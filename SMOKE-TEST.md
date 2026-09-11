# CampusQ v10 Smoke Test

Run this after starting the API and frontend. Replace the URLs with your local/hosted values.

## 1. API health
- Open `GET /health`.
- Expect HTTP 200 and `status: healthy`.

## 2. Database
- Run `CampusQ.API/CampusQ_DatabaseSetup.sql` against the CampusQ database.
- Confirm `Users`, `Queue`, `QueueHistory`, `CurrentCalls`, `ServiceWindows`, and `QueueTransfers` exist.
- Confirm `ServiceWindows` contains windows 1-4.

## 3. Authentication
- Sign in as Admin.
- Sign in as a Staff account assigned to one office.
- Verify Staff cannot call the next ticket for another office.
- Verify inactive accounts cannot log in.

## 4. Kiosk → ticket
- Open `/kiosk`.
- Select each office and purpose.
- Create one Regular and one Priority ticket.
- Verify the ticket is stored in SQL Server.
- Verify the printed ticket contains the QR code.
- Scan the QR code and confirm it opens `/queue/{ticketNumber}`.

## 5. Priority ordering
- Create a Regular ticket first.
- Create a Priority ticket second.
- Staff calls Next.
- Confirm the Priority ticket is called first.

## 6. Window control
- Disable a service window in Admin.
- Confirm Staff cannot call Next from that window.
- Re-enable it and verify it works again.

## 7. Serving / completion
- Staff calls Next.
- Confirm the office monitor changes to Serving.
- Confirm the student's virtual queue changes to Serving.
- Complete the ticket.
- Confirm it appears in QueueHistory and is removed from the active queue.

## 8. Transfer
- Create/call a ticket.
- Transfer it to another office with a reason.
- Confirm the target office receives it.
- Confirm QueueTransfers contains the audit record.
- Confirm the current source window is released.

## 9. Multi-client test
Use separate browser windows/devices:
- Kiosk
- Staff
- Office monitor
- Student phone/browser

Generate a ticket and verify queue changes propagate across all clients.

## 10. Production checks
- Set `CAMPUSQ_JWT_KEY` to a strong random secret.
- Set `CAMPUSQ_CONNECTION_STRING` securely on the API host.
- Set `Cors:AllowedOrigins` to the exact Vercel URL.
- Set `NEXT_PUBLIC_API_BASE_URL` to the HTTPS API URL.
- Confirm HTTPS and SignalR/WebSockets are supported by the API host.
- Never commit `.env` files or real credentials.
