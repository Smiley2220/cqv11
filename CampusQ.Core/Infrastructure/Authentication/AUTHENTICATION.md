# CampusQ Authentication & Authorization Guide

## Current Status

The CampusQ.Web application currently serves as a **public queue viewer** with no authentication.

The desktop application (CampusQ) uses **Windows Integrated Authentication** for user identification.

## Future Authentication Implementation

When adding admin features to the web app, follow this roadmap:

### Phase 1: Basic Password Authentication (Short Term)

1. **Create Admin Controller/Pages**:
   ```csharp
   // CampusQ.Web/Pages/Admin/Login.cshtml.cs
   [HttpPost]
   public IActionResult Login(string username, string password)
   {
	   var user = userRepository.GetByUsername(username);
	   if (user != null && PasswordHashService.VerifyPassword(password, user.PasswordHash))
	   {
		   // Set authentication cookie
		   HttpContext.SignInAsync(/* authentication scheme */, 
			   new ClaimsPrincipal(/* identity */));

		   return RedirectToPage("Dashboard");
	   }
	   ModelState.AddModelError("", "Invalid credentials");
	   return Page();
   }
   ```

2. **Add Authentication Scheme**:
   ```csharp
   // In Program.cs
   builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	   .AddCookie(options =>
	   {
		   options.LoginPath = "/admin/login";
		   options.LogoutPath = "/admin/logout";
		   options.SlidingExpiration = true;
		   options.ExpireTimeSpan = TimeSpan.FromHours(8);
	   });
   ```

3. **Secure Pages with Authorize Attribute**:
   ```csharp
   [Authorize]
   public class DashboardModel : PageModel
   {
	   public void OnGet()
	   {
		   // Only authenticated users can access
	   }
   }
   ```

### Phase 2: Azure AD / OAuth2 (Medium Term)

Recommended for enterprise deployments:

```csharp
// Add to Program.cs
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
	.AddOpenIdConnect(options =>
	{
		options.ClientId = builder.Configuration["AzureAd:ClientId"];
		options.Authority = builder.Configuration["AzureAd:Authority"];
		options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
	});
```

### Phase 3: Role-Based Access Control (RBAC)

```csharp
// Add roles to database Users table
public enum UserRole
{
	Admin,      // Full system access
	Supervisor, // Queue monitoring and reports
	Staff       // Desk operator
}

// Use role-based authorization
[Authorize(Roles = "Admin")]
public IActionResult DeleteQueue(int id)
{
	// Only admins can delete
}
```

## Password Hashing

The `PasswordHashService` uses **PBKDF2** with SHA-256:

```csharp
using CampusQ.Core.Infrastructure.Authentication;

// Hash password during registration
string hashedPassword = PasswordHashService.HashPassword(userInput);
user.PasswordHash = hashedPassword;
userRepository.Add(user);

// Verify during login
bool isValid = PasswordHashService.VerifyPassword(userInput, storedHash);
```

Features:
- ✅ OWASP recommended (10,000 iterations)
- ✅ Random salt per password (16 bytes)
- ✅ Constant-time comparison (prevents timing attacks)
- ✅ SHA-256 algorithm

## Security Headers

All responses include security headers via `SecurityHeadersMiddleware`:

```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: default-src 'self'; ...
```

These prevent:
- Clickjacking attacks
- MIME sniffing
- XSS attacks
- Information leakage

## CORS Configuration

Currently configured for localhost in development:

```csharp
options.AddPolicy("AllowLocal", policy =>
{
	policy.WithOrigins("http://localhost:*", "https://localhost:*")
		  .AllowAnyMethod()
		  .AllowAnyHeader();
});
```

For production, restrict to specific domains:
```csharp
policy.WithOrigins("https://campusq.example.com", "https://admin.example.com")
```

## API Endpoint Security

### Public Endpoints (No Auth Required)
- `GET /api/ticket/{ticketNumber}/status` - Public queue status
- `GET /api/office/{service}/queue` - Public queue counts

### Admin Endpoints (Auth Required) - To Be Added
- `POST /admin/api/queue/add`
- `DELETE /admin/api/queue/{ticketNumber}`
- `GET /admin/dashboard`

Add `[Authorize]` attribute to admin endpoints.

## Session Management

When implementing cookie-based auth:

```csharp
options.ExpireTimeSpan = TimeSpan.FromHours(8);  // Session timeout
options.SlidingExpiration = true;                 // Auto-renew on activity
options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
options.Cookie.HttpOnly = true;                  // JS cannot access
options.Cookie.SameSite = SameSiteMode.Strict;   // CSRF protection
```

## Audit Logging

Log all authentication events:

```csharp
logger.LogInformation("User {Username} logged in successfully", username);
logger.LogWarning("Failed login attempt for user {Username}", username);
logger.LogInformation("User {Username} logged out", username);
```

## Testing Authentication

Create test users in development:

```csharp
// In Program.cs startup
if (app.Environment.IsDevelopment())
{
	var testUser = new UserAccount
	{
		Username = "admin",
		PasswordHash = PasswordHashService.HashPassword("dev-password-123"),
		Role = "Admin",
		CreatedAt = DateTime.Now
	};
	userRepository.Add(testUser);
}
```

## Best Practices Checklist

Before deploying authentication to production:

- ✅ Use HTTPS only (Secure flag on cookies)
- ✅ Implement rate limiting on login attempts
- ✅ Log all authentication attempts
- ✅ Use strong password requirements
- ✅ Implement multi-factor authentication (MFA) for admins
- ✅ Regular security audit
- ✅ Keep dependencies updated
- ✅ Implement CSRF tokens on forms
- ✅ Use secure session management
- ✅ Never log passwords or tokens

## References

- OWASP Authentication Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- Microsoft Identity Platform: https://docs.microsoft.com/en-us/azure/active-directory/develop/
- ASP.NET Core Security: https://docs.microsoft.com/en-us/aspnet/core/security/
