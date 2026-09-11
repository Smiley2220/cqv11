# CampusQ Configuration Guide

## Connection Strings

Connection strings are now managed securely through configuration instead of hardcoded values.

### Priority Order (highest to lowest):
1. **Environment Variable** `CAMPUSQ_CONNECTION_STRING` - Use for production deployments
2. **appsettings.json** - Use for development (local SQL Server)
3. **appsettings.{Environment}.json** - Environment-specific overrides

### Web Application (CampusQ.Web)
Connection string is read from `appsettings.json` under `ConnectionStrings:CampusQ`:

```json
{
  "ConnectionStrings": {
	"CampusQ": "Data Source=.\\SQLEXPRESS;Initial Catalog=CampusQ;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
  }
}
```

### Desktop Application (CampusQ)
The desktop app can use `DbConfig.ConnectionString` property or environment variables:

```csharp
// Option 1: Environment variable (recommended for production)
Environment.SetEnvironmentVariable("CAMPUSQ_CONNECTION_STRING", "your_connection_string");

// Option 2: Direct assignment
DbConfig.ConnectionString = "your_connection_string";

// Option 3: Via ConfigurationService (for DI scenarios)
var configService = new ConfigurationService(configuration);
DbConfig.Initialize(configService);
```

## Production Deployment

### For Windows Services/Hosted Applications:
Set the environment variable before starting the application:

```powershell
# Set system-wide (persistent)
[Environment]::SetEnvironmentVariable("CAMPUSQ_CONNECTION_STRING", "your_connection_string", "Machine")

# Or set per-application in web.config (IIS)
<aspNetCore processPath="..." arguments="">
  <environmentVariables>
	<environmentVariable name="CAMPUSQ_CONNECTION_STRING" value="Data Source=prod-server;..." />
  </environmentVariables>
</aspNetCore>
```

### Best Practices:
- ✅ Never commit sensitive connection strings to version control
- ✅ Use environment variables for production secrets
- ✅ Use Azure Key Vault or similar services for enterprise deployments
- ✅ Ensure connection strings use encrypted connections (`Encrypt=True`)
- ✅ Use Integrated Security where possible instead of service accounts
- ✅ Rotate database credentials regularly

## Configuration Service

The `ConfigurationService` class centralizes all configuration access:

```csharp
var configService = new ConfigurationService(configuration);
var connStr = configService.GetDatabaseConnectionString();
var setting = configService.GetSetting("MyKey", "defaultValue");
```

## Logging Configuration

Logging is configured in `appsettings.json` and can be adjusted per environment:

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft": "Warning"
	}
  }
}
```

For production, EventLog provider is automatically enabled.
