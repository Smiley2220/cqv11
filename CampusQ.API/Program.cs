using CampusQ.API.Hubs;
using CampusQ.Core.Services;
using CampusQ.MVP.Data;
using CampusQ.MVP.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CampusQ.Core.Infrastructure.Authentication;
using System.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("CAMPUSQ_JWT_KEY")
    ?? "";

if (builder.Environment.IsProduction() && jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "CAMPUSQ_JWT_KEY must be set to a random secret of at least 32 characters in production."
    );
}

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? "CampusQ.API";

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? "CampusQ.Web";


// ============================================================
// AUTHENTICATION
// ============================================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromSeconds(30)
            };

        options.Events =
            new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken =
                        context.Request.Query["access_token"];

                    var path =
                        context.HttpContext.Request.Path;

                    if (
                        !string.IsNullOrEmpty(accessToken)
                        && path.StartsWithSegments("/hubs/queue")
                    )
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
    });

builder.Services.AddAuthorization();


// ============================================================
// SERVICES
// ============================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddSingleton<
    IQueueEventPublisher,
    SignalRQueueEventPublisher
>();


// ============================================================
// RATE LIMITING
// ============================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddFixedWindowLimiter(
        "public",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 120;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        }
    );
});


// ============================================================
// DATABASE CONNECTION
// ============================================================

var connection =
    builder.Configuration.GetConnectionString("CampusQ")
    ?? Environment.GetEnvironmentVariable("CAMPUSQ_CONNECTION_STRING")
    ?? DbConfig.ConnectionString;


// ============================================================
// QUEUE SERVICE
// ============================================================

builder.Services.AddSingleton(
    new CampusQWebService(
        connection,
        builder.Configuration.GetValue<int?>(
            "TicketMonitoring:AverageMinutesPerTicket"
        ) ?? 5
    )
);


// ============================================================
// CORS
// ============================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Vercel",
        policy =>
        {
            var origins =
                builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>()
                ?? [];

            if (origins.Length == 0)
            {
                if (builder.Environment.IsProduction())
                {
                    throw new InvalidOperationException(
                        "Cors:AllowedOrigins must be configured in production."
                    );
                }

                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        }
    );
});


// ============================================================
// TEMPORARY ADMIN BOOTSTRAP
//
// Creates:
// Username: admin
// Password: admin123
// Role:     Admin
//
// REMOVE THIS ENTIRE SECTION AFTER THE ADMIN ACCOUNT
// HAS BEEN SUCCESSFULLY CREATED.
// ============================================================

try
{
    using var bootstrapConnection =
        new SqlConnection(connection);

    bootstrapConnection.Open();

    // --------------------------------------------------------
    // Make sure Office column exists
    // --------------------------------------------------------

    using (var schemaCommand =
        bootstrapConnection.CreateCommand())
    {
        schemaCommand.CommandText = """
            IF COL_LENGTH('dbo.Users', 'Office') IS NULL
            BEGIN
                ALTER TABLE dbo.Users
                ADD Office NVARCHAR(100) NULL;
            END
            """;

        schemaCommand.ExecuteNonQuery();
    }


    // --------------------------------------------------------
    // Make sure IsActive column exists
    // --------------------------------------------------------

    using (var schemaCommand =
        bootstrapConnection.CreateCommand())
    {
        schemaCommand.CommandText = """
            IF COL_LENGTH('dbo.Users', 'IsActive') IS NULL
            BEGIN
                ALTER TABLE dbo.Users
                ADD IsActive BIT NOT NULL
                    CONSTRAINT DF_Users_IsActive DEFAULT (1);
            END
            """;

        schemaCommand.ExecuteNonQuery();
    }


    // --------------------------------------------------------
    // Check if admin already exists
    // --------------------------------------------------------

    using var checkCommand =
        bootstrapConnection.CreateCommand();

    checkCommand.CommandText = """
        SELECT COUNT(*)
        FROM dbo.Users
        WHERE Username = @Username
        """;

    checkCommand.Parameters.Add(
        "@Username",
        SqlDbType.NVarChar,
        100
    ).Value = "admin";

    var exists =
        Convert.ToInt32(
            checkCommand.ExecuteScalar()
        ) > 0;


    // --------------------------------------------------------
    // Create admin account
    // --------------------------------------------------------

    if (!exists)
    {
        var passwordHash =
            PasswordHashService.HashPassword(
                "admin123"
            );

        using var insertCommand =
            bootstrapConnection.CreateCommand();

        insertCommand.CommandText = """
            INSERT INTO dbo.Users
            (
                Username,
                PasswordHash,
                Salt,
                Role,
                Office,
                CreatedAt,
                IsActive
            )
            VALUES
            (
                @Username,
                @PasswordHash,
                '',
                'Admin',
                NULL,
                SYSUTCDATETIME(),
                1
            )
            """;

        insertCommand.Parameters.Add(
            "@Username",
            SqlDbType.NVarChar,
            100
        ).Value = "admin";

        insertCommand.Parameters.Add(
            "@PasswordHash",
            SqlDbType.NVarChar,
            -1
        ).Value = passwordHash;

        insertCommand.ExecuteNonQuery();

        Console.WriteLine();
        Console.WriteLine(
            "============================================"
        );
        Console.WriteLine(
            "       CAMPUSQ ADMIN ACCOUNT CREATED"
        );
        Console.WriteLine(
            "============================================"
        );
        Console.WriteLine(
            "Username : admin"
        );
        Console.WriteLine(
            "Password : admin123"
        );
        Console.WriteLine(
            "Role     : Admin"
        );
        Console.WriteLine(
            "Office   : NULL"
        );
        Console.WriteLine(
            "Active   : Yes"
        );
        Console.WriteLine(
            "============================================"
        );
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine(
            "CampusQ admin account already exists."
        );
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine(
        "============================================"
    );
    Console.WriteLine(
        "       CAMPUSQ ADMIN BOOTSTRAP ERROR"
    );
    Console.WriteLine(
        "============================================"
    );
    Console.WriteLine(ex.Message);
    Console.WriteLine(
        "============================================"
    );
    Console.WriteLine();
}


// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();


// ============================================================
// SWAGGER
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// ============================================================
// MIDDLEWARE
// ============================================================

app.UseHttpsRedirection();

app.UseCors("Vercel");

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();


// ============================================================
// BASIC API
// ============================================================

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                name = "CampusQ API",
                status = "online",
                version = "1.0"
            }
        )
);

app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "healthy",
                service = "CampusQ.API",
                timestamp = DateTimeOffset.UtcNow
            }
        )
);


// ============================================================
// AUTH LOGIN
// ============================================================

app.MapPost(
    "/api/auth/login",
    (LoginRequest req, IConfiguration config) =>
    {
        if (
            string.IsNullOrWhiteSpace(req.Username)
            || string.IsNullOrWhiteSpace(req.Password)
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "Username and password are required."
                }
            );
        }

        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                Username,
                PasswordHash,
                Role,
                ISNULL(Office, '')
            FROM dbo.Users
            WHERE Username = @u
              AND ISNULL(IsActive, 1) = 1
            """;

        cmd.Parameters.Add(
            "@u",
            SqlDbType.NVarChar,
            100
        ).Value = req.Username.Trim();

        using var r =
            cmd.ExecuteReader();

        if (
            !r.Read()
            || !PasswordHashService.VerifyPassword(
                req.Password,
                r.GetString(1)
            )
        )
        {
            return Results.Unauthorized();
        }

        var username =
            r.GetString(0);

        var role =
            r.GetString(2);

        var office =
            r.GetString(3);

        var claims =
            new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    username
                ),

                new(
                    ClaimTypes.Name,
                    username
                ),

                new(
                    ClaimTypes.Role,
                    role
                )
            };

        if (!string.IsNullOrWhiteSpace(office))
        {
            claims.Add(
                new Claim(
                    "office",
                    office
                )
            );
        }

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    config["Jwt:Key"]
                    ?? Environment.GetEnvironmentVariable(
                        "CAMPUSQ_JWT_KEY"
                    )
                    ?? "CHANGE_THIS_CAMPUSQ_SECRET_KEY_IN_PRODUCTION_32CHARS_MINIMUM"
                )
            );

        var creds =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

        var token =
            new JwtSecurityToken(
                issuer:
                    config["Jwt:Issuer"]
                    ?? "CampusQ.API",

                audience:
                    config["Jwt:Audience"]
                    ?? "CampusQ.Web",

                claims: claims,

                expires:
                    DateTime.UtcNow.AddHours(8),

                signingCredentials: creds
            );

        return Results.Ok(
            new
            {
                accessToken =
                    new JwtSecurityTokenHandler()
                        .WriteToken(token),

                username,

                role,

                office =
                    string.IsNullOrWhiteSpace(office)
                        ? null
                        : office,

                expiresAt =
                    token.ValidTo
            }
        );
    }
)
.RequireRateLimiting("public");


// ============================================================
// CURRENT USER
// ============================================================

app.MapGet(
    "/api/auth/me",
    (ClaimsPrincipal user) =>
        Results.Ok(
            new
            {
                username =
                    user.Identity?.Name,

                role =
                    user.FindFirstValue(
                        ClaimTypes.Role
                    ),

                office =
                    user.FindFirstValue(
                        "office"
                    )
            }
        )
)
.RequireAuthorization();


// ============================================================
// ADMIN - USERS
// ============================================================

app.MapGet(
    "/api/admin/users",
    (IConfiguration config) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                Username,
                Role,
                Office,
                CreatedAt,
                ISNULL(IsActive, 1)
            FROM dbo.Users
            ORDER BY Username
            """;

        using var r =
            cmd.ExecuteReader();

        var users =
            new List<object>();

        while (r.Read())
        {
            users.Add(
                new
                {
                    username =
                        r.GetString(0),

                    role =
                        r.GetString(1),

                    office =
                        r.IsDBNull(2)
                            ? null
                            : r.GetString(2),

                    createdAt =
                        r.GetDateTime(3),

                    isActive =
                        r.GetBoolean(4)
                }
            );
        }

        return Results.Ok(users);
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN - CREATE USER
// ============================================================

app.MapPost(
    "/api/admin/users",
    (
        CreateUserRequest req,
        IConfiguration config
    ) =>
    {
        if (
            string.IsNullOrWhiteSpace(req.Username)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.Role)
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "Username, password and role are required."
                }
            );
        }

        if (
            req.Role is not ("Admin" or "Staff")
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "Role must be Admin or Staff."
                }
            );
        }

        if (
            req.Role == "Staff"
            && string.IsNullOrWhiteSpace(req.Office)
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "Staff users require an office."
                }
            );
        }

        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            IF EXISTS
            (
                SELECT 1
                FROM dbo.Users
                WHERE Username = @u
            )
                THROW 50002,
                    'Username already exists.',
                    1;

            INSERT dbo.Users
            (
                Username,
                PasswordHash,
                Salt,
                Role,
                Office,
                CreatedAt
            )
            VALUES
            (
                @u,
                @h,
                '',
                @r,
                @o,
                SYSUTCDATETIME()
            )
            """;

        cmd.Parameters.Add(
            "@u",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.Username.Trim();

        cmd.Parameters.Add(
            "@h",
            SqlDbType.NVarChar,
            -1
        ).Value =
            PasswordHashService.HashPassword(
                req.Password
            );

        cmd.Parameters.Add(
            "@r",
            SqlDbType.NVarChar,
            50
        ).Value =
            req.Role;

        cmd.Parameters.Add(
            "@o",
            SqlDbType.NVarChar,
            100
        ).Value =
            (object?)req.Office?.Trim()
            ?? DBNull.Value;

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqlException ex)
            when (ex.Number == 50002)
        {
            return Results.Conflict(
                new
                {
                    message =
                        "Username already exists."
                }
            );
        }

        return Results.Created(
            $"/api/admin/users/{req.Username.Trim()}",
            new
            {
                username =
                    req.Username.Trim(),

                role =
                    req.Role,

                office =
                    req.Office
            }
        );
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// OFFICES
// ============================================================

app.MapGet(
    "/api/offices",
    () =>
        Results.Ok(
            CampusQWebService.Offices
        )
)
.RequireRateLimiting("public");


// ============================================================
// QUEUE
// ============================================================

app.MapGet(
    "/api/queue",
    (CampusQWebService s) =>
        Results.Ok(s.GetAll())
)
.RequireRateLimiting("public");


app.MapGet(
    "/api/queue/{office}",
    (string office, CampusQWebService s) =>
        Results.Ok(
            s.GetByOffice(office)
        )
)
.RequireRateLimiting("public");


app.MapGet(
    "/api/queue/ticket/{ticketNumber:int}",
    (
        int ticketNumber,
        CampusQWebService s
    ) =>
        s.GetTicket(ticketNumber)
            is { } t
            ? Results.Ok(t)
            : Results.NotFound()
)
.RequireRateLimiting("public");


app.MapGet(
    "/api/ticket/{ticketNumber:int}/status",
    (
        int ticketNumber,
        CampusQWebService s
    ) =>
        s.GetStatus(ticketNumber)
            is { State: "NotFound" } r
            ? Results.NotFound(r)
            : Results.Ok(
                s.GetStatus(ticketNumber)
            )
)
.RequireRateLimiting("public");


// ============================================================
// ADD QUEUE TICKET
// ============================================================

app.MapPost(
    "/api/queue",
    async (
        AddQueueRequest req,
        CampusQWebService s,
        IQueueEventPublisher events,
        CancellationToken ct
    ) =>
    {
        var t =
            s.Add(req);

        await events.PublishAsync(
            new QueueEvent(
                QueueEventType.TicketAdded,
                t.Service,
                t.TicketNumber,
                DateTimeOffset.UtcNow
            ),
            ct
        );

        return Results.Ok(t);
    }
)
.RequireRateLimiting("public");


// ============================================================
// CALL NEXT
// ============================================================

app.MapPost(
    "/api/queue/{office}/next",
    async (
        string office,
        NextQueueRequest req,
        CampusQWebService s,
        IQueueEventPublisher events,
        ClaimsPrincipal user,
        CancellationToken ct
    ) =>
    {
        var role =
            user.FindFirstValue(
                ClaimTypes.Role
            );

        var assigned =
            user.FindFirstValue(
                "office"
            );

        if (
            role == "Staff"
            && !string.Equals(
                assigned,
                office,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Results.Forbid();
        }

        var t =
            s.Next(
                office,
                req.Window
            );

        if (t is null)
        {
            return Results.NoContent();
        }

        await events.PublishAsync(
            new QueueEvent(
                QueueEventType.QueueChanged,
                office,
                t.TicketNumber,
                DateTimeOffset.UtcNow
            ),
            ct
        );

        return Results.Ok(t);
    }
)
.RequireAuthorization()
.RequireRateLimiting("public");


// ============================================================
// COMPLETE TICKET
// ============================================================

app.MapPost(
    "/api/queue/{ticketNumber:int}/complete",
    async (
        int ticketNumber,
        CompleteQueueRequest req,
        CampusQWebService s,
        IQueueEventPublisher events,
        ClaimsPrincipal user,
        CancellationToken ct
    ) =>
    {
        var role =
            user.FindFirstValue(
                ClaimTypes.Role
            );

        var assigned =
            user.FindFirstValue(
                "office"
            );

        var current =
            s.GetTicket(ticketNumber);

        if (
            role == "Staff"
            && current is not null
            && !string.Equals(
                assigned,
                current.Service,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Results.Forbid();
        }

        var t =
            s.Complete(
                ticketNumber,
                req.Window
            );

        if (t is null)
        {
            return Results.Conflict(
                new
                {
                    message =
                        "Ticket is not currently being served."
                }
            );
        }

        await events.PublishAsync(
            new QueueEvent(
                QueueEventType.TicketServed,
                t.Service,
                t.TicketNumber,
                DateTimeOffset.UtcNow
            ),
            ct
        );

        return Results.Ok(t);
    }
)
.RequireAuthorization()
.RequireRateLimiting("public");


// ============================================================
// TRANSFER
// ============================================================

app.MapPost(
    "/api/queue/transfer",
    async (
        TransferApiRequest req,
        CampusQWebService s,
        IQueueEventPublisher events,
        ClaimsPrincipal user,
        CancellationToken ct
    ) =>
    {
        var role =
            user.FindFirstValue(
                ClaimTypes.Role
            );

        var assigned =
            user.FindFirstValue(
                "office"
            );

        var current =
            s.GetTicket(
                req.TicketNumber
            );

        if (
            role == "Staff"
            && current is not null
            && !string.Equals(
                assigned,
                current.Service,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Results.Forbid();
        }

        var actor =
            user.Identity?.Name
            ?? req.Actor;

        var r =
            s.Transfer(
                req with
                {
                    Actor = actor
                }
            );

        await events.PublishAsync(
            new QueueEvent(
                QueueEventType.TicketTransferred,
                r.SourceService,
                r.TicketNumber,
                r.TransferredAt,
                r.TargetService
            ),
            ct
        );

        return Results.Ok(r);
    }
)
.RequireAuthorization()
.RequireRateLimiting("public");


// ============================================================
// ADMIN REPORT - SUMMARY
// ============================================================

app.MapGet(
    "/api/admin/reports/summary",
    (
        string? office,
        DateTime? from,
        DateTime? to,
        IConfiguration config
    ) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(
                    SUM(
                        CASE
                            WHEN IsPriority = 1
                            THEN 1
                            ELSE 0
                        END
                    ),
                    0
                ),
                COALESCE(
                    AVG(
                        CAST(
                            DATEDIFF(
                                second,
                                TimeAdded,
                                ServedAt
                            ) AS float
                        )
                    ) / 60.0,
                    0
                ),
                COALESCE(
                    MIN(
                        DATEDIFF(
                            second,
                            TimeAdded,
                            ServedAt
                        )
                    ) / 60.0,
                    0
                ),
                COALESCE(
                    MAX(
                        DATEDIFF(
                            second,
                            TimeAdded,
                            ServedAt
                        )
                    ) / 60.0,
                    0
                )
            FROM dbo.QueueHistory
            WHERE
                (@office IS NULL OR Service = @office)
                AND
                (@from IS NULL OR ServedAt >= @from)
                AND
                (
                    @to IS NULL
                    OR ServedAt < DATEADD(day, 1, @to)
                )
            """;

        cmd.Parameters.Add(
            "@office",
            SqlDbType.NVarChar,
            100
        ).Value =
            (object?)office?.Trim()
            ?? DBNull.Value;

        cmd.Parameters.Add(
            "@from",
            SqlDbType.DateTime2
        ).Value =
            (object?)from
            ?? DBNull.Value;

        cmd.Parameters.Add(
            "@to",
            SqlDbType.DateTime2
        ).Value =
            (object?)to
            ?? DBNull.Value;

        using var r =
            cmd.ExecuteReader();

        r.Read();

        return Results.Ok(
            new
            {
                totalTickets =
                    r.GetInt32(0),

                priorityTickets =
                    r.GetInt32(1),

                avgWaitMinutes =
                    Convert.ToInt32(Math.Round(
                        Convert.ToDouble(r.GetValue(2)),
                        0,
                        MidpointRounding.AwayFromZero
                    )),

                minWaitMinutes =
                    Convert.ToInt32(Math.Round(
                        Convert.ToDouble(r.GetValue(3)),
                        0,
                        MidpointRounding.AwayFromZero
                    )),

                maxWaitMinutes =
                    Convert.ToInt32(Math.Round(
                        Convert.ToDouble(r.GetValue(4)),
                        0,
                        MidpointRounding.AwayFromZero
                    ))
            }
        );
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN REPORT - BY OFFICE
// ============================================================

app.MapGet(
    "/api/admin/reports/by-office",
    (
        DateTime? from,
        DateTime? to,
        IConfiguration config
    ) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                Service,
                COUNT(*),
                SUM(
                    CASE
                        WHEN IsPriority = 1
                        THEN 1
                        ELSE 0
                    END
                ),
                COALESCE(
                    AVG(
                        CAST(
                            DATEDIFF(
                                second,
                                TimeAdded,
                                ServedAt
                            ) AS float
                        )
                    ) / 60.0,
                    0
                )
            FROM dbo.QueueHistory
            WHERE
                (@from IS NULL OR ServedAt >= @from)
                AND
                (
                    @to IS NULL
                    OR ServedAt < DATEADD(day, 1, @to)
                )
            GROUP BY Service
            ORDER BY COUNT(*) DESC
            """;

        cmd.Parameters.Add(
            "@from",
            SqlDbType.DateTime2
        ).Value =
            (object?)from
            ?? DBNull.Value;

        cmd.Parameters.Add(
            "@to",
            SqlDbType.DateTime2
        ).Value =
            (object?)to
            ?? DBNull.Value;

        using var r =
            cmd.ExecuteReader();

        var rows =
            new List<object>();

        while (r.Read())
        {
            rows.Add(
                new
                {
                    office =
                        r.GetString(0),

                    tickets =
                        r.GetInt32(1),

                    priorityTickets =
                        r.IsDBNull(2)
                            ? 0
                            : r.GetInt32(2),

                    avgWaitMinutes =
                        Convert.ToInt32(Math.Round(
                            Convert.ToDouble(r.GetValue(3)),
                            0,
                            MidpointRounding.AwayFromZero
                            ))
                }
            );
        }

        return Results.Ok(rows);
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN REPORT - DAILY
// ============================================================

app.MapGet(
    "/api/admin/reports/daily",
    (
        DateTime? from,
        DateTime? to,
        IConfiguration config
    ) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                CAST(ServedAt AS date),
                COUNT(*),
                SUM(
                    CASE
                        WHEN IsPriority = 1
                        THEN 1
                        ELSE 0
                    END
                ),
                COALESCE(
                    AVG(
                        CAST(
                            DATEDIFF(
                                second,
                                TimeAdded,
                                ServedAt
                            ) AS float
                        )
                    ) / 60.0,
                    0
                )
            FROM dbo.QueueHistory
            WHERE
                (@from IS NULL OR ServedAt >= @from)
                AND
                (
                    @to IS NULL
                    OR ServedAt < DATEADD(day, 1, @to)
                )
            GROUP BY
                CAST(ServedAt AS date)
            ORDER BY
                CAST(ServedAt AS date) DESC
            """;

        cmd.Parameters.Add(
            "@from",
            SqlDbType.DateTime2
        ).Value =
            (object?)from
            ?? DBNull.Value;

        cmd.Parameters.Add(
            "@to",
            SqlDbType.DateTime2
        ).Value =
            (object?)to
            ?? DBNull.Value;

        using var r =
            cmd.ExecuteReader();

        var rows =
            new List<object>();

        while (r.Read())
        {
            rows.Add(
                new
                {
                    day =
                        r.GetDateTime(0)
                            .ToString("yyyy-MM-dd"),

                    tickets =
                        r.GetInt32(1),

                    priorityTickets =
                        r.IsDBNull(2)
                            ? 0
                            : r.GetInt32(2),

                    avgWaitMinutes =
                    Convert.ToInt32(Math.Round(
                        Convert.ToDouble(r.GetValue(3)),
                        0,
                        MidpointRounding.AwayFromZero
                    ))
                }
            );
        }

        return Results.Ok(rows);
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN - TRANSFERS
// ============================================================

app.MapGet(
    "/api/admin/transfers",
    (
        int? limit,
        IConfiguration config
    ) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT TOP (@limit)
                TransferId,
                TicketNumber,
                SourceService,
                TargetService,
                Reason,
                Actor,
                TransferredAt
            FROM dbo.QueueTransfers
            ORDER BY TransferredAt DESC
            """;

        cmd.Parameters.Add(
            "@limit",
            SqlDbType.Int
        ).Value =
            Math.Clamp(
                limit ?? 50,
                1,
                200
            );

        using var r =
            cmd.ExecuteReader();

        var rows =
            new List<object>();

        while (r.Read())
        {
            rows.Add(
                new
                {
                    transferId =
                        r.GetInt64(0),

                    ticketNumber =
                        r.GetInt32(1),

                    sourceOffice =
                        r.GetString(2),

                    targetOffice =
                        r.GetString(3),

                    reason =
                        r.GetString(4),

                    actor =
                        r.GetString(5),

                    transferredAt =
                        r.GetDateTime(6)
                }
            );
        }

        return Results.Ok(rows);
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN - ACTIVATE / DEACTIVATE USER
// ============================================================

app.MapPut(
    "/api/admin/users/{username}/status",
    (
        string username,
        UserStatusRequest req,
        IConfiguration config,
        ClaimsPrincipal actor
    ) =>
    {
        if (
            string.Equals(
                username,
                actor.Identity?.Name,
                StringComparison.OrdinalIgnoreCase
            )
            && !req.IsActive
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "You cannot deactivate your own account."
                }
            );
        }

        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            UPDATE dbo.Users
            SET IsActive = @a
            WHERE Username = @u
            """;

        cmd.Parameters.Add(
            "@a",
            SqlDbType.Bit
        ).Value =
            req.IsActive;

        cmd.Parameters.Add(
            "@u",
            SqlDbType.NVarChar,
            100
        ).Value =
            username;

        return cmd.ExecuteNonQuery() == 0
            ? Results.NotFound()
            : Results.Ok(
                new
                {
                    username,
                    isActive =
                        req.IsActive
                }
            );
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN - WINDOWS
// ============================================================

app.MapGet(
    "/api/admin/windows",
    (IConfiguration config) =>
    {
        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                WindowNumber,
                IsActive,
                UpdatedAt
            FROM dbo.ServiceWindows
            ORDER BY WindowNumber
            """;

        using var r =
            cmd.ExecuteReader();

        var rows =
            new List<object>();

        while (r.Read())
        {
            rows.Add(
                new
                {
                    windowNumber =
                        r.GetInt32(0),

                    isActive =
                        r.GetBoolean(1),

                    updatedAt =
                        r.GetDateTime(2)
                }
            );
        }

        return Results.Ok(rows);
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// ADMIN - WINDOW STATUS
// ============================================================

app.MapPut(
    "/api/admin/windows/{window:int}/status",
    (
        int window,
        WindowStatusRequest req,
        IConfiguration config
    ) =>
    {
        if (
            window < 1
            || window > 4
        )
        {
            return Results.BadRequest(
                new
                {
                    message =
                        "Invalid window."
                }
            );
        }

        var cs =
            config.GetConnectionString("CampusQ")
            ?? Environment.GetEnvironmentVariable(
                "CAMPUSQ_CONNECTION_STRING"
            )
            ?? DbConfig.ConnectionString;

        using var c =
            new SqlConnection(cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            UPDATE dbo.ServiceWindows
            SET
                IsActive = @a,
                UpdatedAt = SYSUTCDATETIME()
            WHERE WindowNumber = @w
            """;

        cmd.Parameters.Add(
            "@a",
            SqlDbType.Bit
        ).Value =
            req.IsActive;

        cmd.Parameters.Add(
            "@w",
            SqlDbType.Int
        ).Value =
            window;

        return cmd.ExecuteNonQuery() == 0
            ? Results.NotFound()
            : Results.Ok(
                new
                {
                    windowNumber =
                        window,

                    isActive =
                        req.IsActive
                }
            );
    }
)
.RequireAuthorization(
    policy =>
        policy.RequireRole("Admin")
);


// ============================================================
// SIGNALR
// ============================================================

app.MapHub<QueueHub>(
    "/hubs/queue"
);


// ============================================================
// RUN
// ============================================================

app.Run();


// ============================================================
// REQUEST RECORDS
// ============================================================

public sealed record LoginRequest(
    string Username,
    string Password
);

public sealed record CreateUserRequest(
    string Username,
    string Password,
    string Role,
    string? Office
);

public sealed record UserStatusRequest(
    bool IsActive
);

public sealed record WindowStatusRequest(
    bool IsActive
);

public sealed record AddQueueRequest(
    string Purpose,
    string Service,
    bool IsPriority = false
);

public sealed record NextQueueRequest(
    int Window
);

public sealed record CompleteQueueRequest(
    int Window
);

public sealed record TransferApiRequest(
    int TicketNumber,
    string TargetService,
    string Reason,
    string Actor,
    int? Window = null
);


// ============================================================
// CAMPUSQ WEB SERVICE
// ============================================================

public sealed class CampusQWebService
{
    public static readonly object[] Offices =
    {
        new
        {
            name = "Registrar",
            purposes = new[]
            {
                "Enrollment",
                "Credentials",
                "Other Inquiries"
            },
            windows = new[]
            {
                1, 2, 3, 4
            }
        },

        new
        {
            name = "Cashier",
            purposes = new[]
            {
                "Tuition Fee",
                "Miscellaneous Fee",
                "Other Payments"
            },
            windows = new[]
            {
                1, 2, 3, 4
            }
        },

        new
        {
            name = "Admission",
            purposes = new[]
            {
                "Application Status",
                "Document Verification",
                "General Inquiry"
            },
            windows = new[]
            {
                1, 2
            }
        }
    };

    private readonly string _cs;
    private readonly int _avg;

    public CampusQWebService(
        string cs,
        int avg
    )
    {
        _cs = cs;
        _avg = avg;

        EnsureWebTables();
    }

    static bool Known(string s) =>
        new[]
        {
            "Registrar",
            "Cashier",
            "Admission"
        }
        .Any(
            x =>
                x.Equals(
                    s.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
        );


    // ========================================================
    // GET ALL
    // ========================================================

    public List<WebTicket> GetAll() =>
        GetByOffice(null);


    // ========================================================
    // GET BY OFFICE
    // ========================================================

    public List<WebTicket> GetByOffice(
        string? office
    )
    {
        using var c =
            new SqlConnection(_cs);

        c.Open();

        var list =
            new List<WebTicket>();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                q.TicketNumber,
                q.ServiceTicketNumber,
                q.Purpose,
                q.Service,
                q.TimeAdded,
                cc.WindowNumber,
                cc.CalledAt,
                CASE
                    WHEN cc.TicketNumber IS NULL
                    THEN 'Waiting'
                    ELSE 'Serving'
                END Status,
                q.IsPriority
            FROM dbo.Queue q
            LEFT JOIN dbo.CurrentCalls cc
                ON cc.TicketNumber = q.TicketNumber
            WHERE
                (
                    @office IS NULL
                    OR q.Service = @office
                )
            ORDER BY
                CASE
                    WHEN cc.TicketNumber IS NOT NULL
                    THEN 0
                    ELSE 1
                END,
                q.IsPriority DESC,
                q.TimeAdded,
                q.TicketNumber
            """;

        cmd.Parameters.Add(
            "@office",
            SqlDbType.NVarChar,
            100
        ).Value =
            (object?)office?.Trim()
            ?? DBNull.Value;

        using var r =
            cmd.ExecuteReader();

        while (r.Read())
        {
            list.Add(
                Read(r)
            );
        }

        return list;
    }


    // ========================================================
    // GET TICKET
    // ========================================================

    public WebTicket? GetTicket(
        int n
    )
    {
        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                q.TicketNumber,
                q.ServiceTicketNumber,
                q.Purpose,
                q.Service,
                q.TimeAdded,
                cc.WindowNumber,
                cc.CalledAt,
                CASE
                    WHEN cc.TicketNumber IS NULL
                    THEN 'Waiting'
                    ELSE 'Serving'
                END Status,
                q.IsPriority
            FROM dbo.Queue q
            LEFT JOIN dbo.CurrentCalls cc
                ON cc.TicketNumber = q.TicketNumber
            WHERE
                q.TicketNumber = @n
            """;

        cmd.Parameters.Add(
            "@n",
            SqlDbType.Int
        ).Value = n;

        using var r =
            cmd.ExecuteReader();

        return r.Read()
            ? Read(r)
            : null;
    }


    // ========================================================
    // ADD TICKET
    // ========================================================

    public WebTicket Add(
        AddQueueRequest req
    )
    {
        if (!Known(req.Service))
        {
            throw new ArgumentException(
                "Unknown office."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                req.Purpose
            )
        )
        {
            throw new ArgumentException(
                "Purpose is required."
            );
        }

        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var tx =
            c.BeginTransaction(
                IsolationLevel.Serializable
            );

        using var cmd =
            c.CreateCommand();

        cmd.Transaction = tx;

        cmd.CommandText = """
            SELECT
                ISNULL(
                    MAX(ServiceTicketNumber),
                    0
                ) + 1
            FROM dbo.Queue
            WHERE
                Service = @s
                AND Purpose = @p
            """;

        cmd.Parameters.Add(
            "@s",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.Service.Trim();

        cmd.Parameters.Add(
            "@p",
            SqlDbType.NVarChar,
            200
        ).Value =
            req.Purpose.Trim();

        var seq =
            (int)cmd.ExecuteScalar()!;

        cmd.Parameters.Clear();

        cmd.CommandText = """
            INSERT dbo.Queue
            (
                ServiceTicketNumber,
                Purpose,
                Service,
                TimeAdded,
                IsPriority
            )
            OUTPUT INSERTED.TicketNumber
            VALUES
            (
                @seq,
                @p,
                @s,
                @t,
                @priority
            )
            """;

        cmd.Parameters.Add(
            "@seq",
            SqlDbType.Int
        ).Value = seq;

        cmd.Parameters.Add(
            "@p",
            SqlDbType.NVarChar,
            200
        ).Value =
            req.Purpose.Trim();

        cmd.Parameters.Add(
            "@s",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.Service.Trim();

        cmd.Parameters.Add(
            "@t",
            SqlDbType.DateTime2
        ).Value =
            DateTime.Now;

        cmd.Parameters.Add(
            "@priority",
            SqlDbType.Bit
        ).Value =
            req.IsPriority;

        var id =
            (int)cmd.ExecuteScalar()!;

        tx.Commit();

        return GetTicket(id)!;
    }


    // ========================================================
    // CALL NEXT
    // ========================================================

    public WebTicket? Next(
        string office,
        int window
    )
    {
        if (
            !Known(office)
            || window < 1
            || window > 4
        )
        {
            throw new ArgumentException(
                "Invalid office or window."
            );
        }

        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var tx =
            c.BeginTransaction(
                IsolationLevel.Serializable
            );

        using var cmd =
            c.CreateCommand();

        cmd.Transaction = tx;

        cmd.CommandText = """
            SELECT IsActive
            FROM dbo.ServiceWindows
            WHERE WindowNumber = @w
            """;

        cmd.Parameters.Add(
            "@w",
            SqlDbType.Int
        ).Value = window;

        var active =
            cmd.ExecuteScalar();

        if (
            active is null
            || active is bool b && !b
        )
        {
            throw new InvalidOperationException(
                $"Window {window} is disabled."
            );
        }

        cmd.Parameters.Clear();

        cmd.CommandText = """
            SELECT TOP 1
                TicketNumber
            FROM dbo.Queue
            WHERE
                Service = @s
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.CurrentCalls cc
                    WHERE
                        cc.TicketNumber =
                            dbo.Queue.TicketNumber
                )
            ORDER BY
                IsPriority DESC,
                TimeAdded,
                TicketNumber
            """;

        cmd.Parameters.Add(
            "@s",
            SqlDbType.NVarChar,
            100
        ).Value =
            office.Trim();

        var id =
            cmd.ExecuteScalar();

        if (id is null)
        {
            tx.Rollback();
            return null;
        }

        cmd.Parameters.Clear();

        cmd.CommandText = """
            IF EXISTS
            (
                SELECT 1
                FROM dbo.CurrentCalls
                WHERE
                    Service = @s
                    AND WindowNumber = @w
            )
                THROW 50001,
                    'Window is busy.',
                    1;

            INSERT dbo.CurrentCalls
            (
                TicketNumber,
                Service,
                WindowNumber,
                CalledAt
            )
            VALUES
            (
                @t,
                @s,
                @w,
                SYSUTCDATETIME()
            )
            """;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value =
            (int)id;

        cmd.Parameters.Add(
            "@s",
            SqlDbType.NVarChar,
            100
        ).Value =
            office.Trim();

        cmd.Parameters.Add(
            "@w",
            SqlDbType.Int
        ).Value =
            window;

        cmd.ExecuteNonQuery();

        tx.Commit();

        return GetTicket(
            (int)id
        );
    }


    // ========================================================
    // COMPLETE
    // ========================================================

    public WebTicket? Complete(
        int ticket,
        int window
    )
    {
        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var tx =
            c.BeginTransaction(
                IsolationLevel.Serializable
            );

        using var cmd =
            c.CreateCommand();

        cmd.Transaction = tx;

        cmd.CommandText = """
            SELECT
                q.TicketNumber,
                q.ServiceTicketNumber,
                q.Purpose,
                q.Service,
                q.TimeAdded,
                cc.WindowNumber,
                cc.CalledAt,
                q.IsPriority
            FROM dbo.Queue q
            JOIN dbo.CurrentCalls cc
                ON cc.TicketNumber =
                    q.TicketNumber
            WHERE
                q.TicketNumber = @t
                AND cc.WindowNumber = @w
            """;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value = ticket;

        cmd.Parameters.Add(
            "@w",
            SqlDbType.Int
        ).Value = window;

        WebTicket? current = null;

        using (var r =
            cmd.ExecuteReader())
        {
            if (r.Read())
            {
                current =
                    new WebTicket(
                        r.GetInt32(0),
                        r.GetInt32(1),
                        r.GetString(2),
                        r.GetString(3),
                        r.GetDateTime(4),
                        r.GetInt32(5),
                        r.GetDateTime(6),
                        "Serving",
                        r.GetBoolean(7),
                        Label(
                            r.GetString(3),
                            r.GetString(2),
                            r.GetInt32(1)
                        )
                    );
            }
        }

        if (current is null)
        {
            tx.Rollback();
            return null;
        }

        cmd.Parameters.Clear();

        cmd.CommandText = """
            DELETE FROM dbo.Queue
            OUTPUT
                deleted.TicketNumber,
                deleted.ServiceTicketNumber,
                deleted.Purpose,
                deleted.Service,
                deleted.TimeAdded,
                @at,
                deleted.IsPriority
            INTO dbo.QueueHistory
            (
                TicketNumber,
                ServiceTicketNumber,
                Purpose,
                Service,
                TimeAdded,
                ServedAt,
                IsPriority
            )
            WHERE TicketNumber = @t;

            DELETE FROM dbo.CurrentCalls
            WHERE TicketNumber = @t;
            """;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value = ticket;

        cmd.Parameters.Add(
            "@at",
            SqlDbType.DateTime2
        ).Value =
            DateTime.Now;

        cmd.ExecuteNonQuery();

        tx.Commit();

        return current with
        {
            Status = "Completed"
        };
    }


    // ========================================================
    // TRANSFER
    // ========================================================

    public TransferTicketResult Transfer(
        TransferApiRequest req
    )
    {
        if (!Known(req.TargetService))
        {
            throw new ArgumentException(
                "Unknown target office."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                req.Reason
            )
        )
        {
            throw new ArgumentException(
                "Transfer reason is required."
            );
        }

        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var tx =
            c.BeginTransaction(
                IsolationLevel.Serializable
            );

        using var cmd =
            c.CreateCommand();

        cmd.Transaction = tx;

        cmd.CommandText = """
            SELECT
                q.Service,
                q.Purpose,
                q.IsPriority,
                cc.WindowNumber
            FROM dbo.Queue q
            LEFT JOIN dbo.CurrentCalls cc
                ON cc.TicketNumber =
                    q.TicketNumber
            WHERE
                q.TicketNumber = @t
            """;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value =
            req.TicketNumber;

        string? source = null;
        string? purpose = null;
        bool priority = false;
        int? currentWindow = null;

        using (var r =
            cmd.ExecuteReader())
        {
            if (r.Read())
            {
                source =
                    r.GetString(0);

                purpose =
                    r.GetString(1);

                priority =
                    r.GetBoolean(2);

                currentWindow =
                    r.IsDBNull(3)
                        ? null
                        : r.GetInt32(3);
            }
        }

        if (
            string.IsNullOrWhiteSpace(source)
        )
        {
            throw new InvalidOperationException(
                "Ticket was not found or is no longer active."
            );
        }

        if (
            currentWindow.HasValue
            && req.Window.HasValue
            && currentWindow.Value != req.Window.Value
        )
        {
            throw new InvalidOperationException(
                "Ticket is assigned to a different window."
            );
        }

        cmd.Parameters.Clear();

        cmd.CommandText = """
            SELECT
                ISNULL(
                    MAX(ServiceTicketNumber),
                    0
                ) + 1
            FROM dbo.Queue
            WHERE
                Service = @target
                AND Purpose = @p
            """;

        cmd.Parameters.Add(
            "@target",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.TargetService.Trim();

        cmd.Parameters.Add(
            "@p",
            SqlDbType.NVarChar,
            200
        ).Value =
            purpose!;

        var newSeq =
            (int)cmd.ExecuteScalar()!;

        cmd.Parameters.Clear();

        cmd.CommandText = """
            UPDATE dbo.Queue
            SET
                Service = @target,
                ServiceTicketNumber = @seq
            WHERE
                TicketNumber = @t
            """;

        cmd.Parameters.Add(
            "@target",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.TargetService.Trim();

        cmd.Parameters.Add(
            "@seq",
            SqlDbType.Int
        ).Value =
            newSeq;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value =
            req.TicketNumber;

        cmd.ExecuteNonQuery();

        if (currentWindow.HasValue)
        {
            cmd.Parameters.Clear();

            cmd.CommandText = """
                DELETE FROM dbo.CurrentCalls
                WHERE TicketNumber = @t
                """;

            cmd.Parameters.Add(
                "@t",
                SqlDbType.Int
            ).Value =
                req.TicketNumber;

            cmd.ExecuteNonQuery();
        }

        cmd.Parameters.Clear();

        cmd.CommandText = """
            INSERT dbo.QueueTransfers
            (
                TicketNumber,
                SourceService,
                TargetService,
                Reason,
                Actor,
                TransferredAt
            )
            VALUES
            (
                @t,
                @s,
                @target,
                @reason,
                @actor,
                SYSUTCDATETIME()
            )
            """;

        cmd.Parameters.Add(
            "@t",
            SqlDbType.Int
        ).Value =
            req.TicketNumber;

        cmd.Parameters.Add(
            "@s",
            SqlDbType.NVarChar,
            100
        ).Value =
            source;

        cmd.Parameters.Add(
            "@target",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.TargetService.Trim();

        cmd.Parameters.Add(
            "@reason",
            SqlDbType.NVarChar,
            500
        ).Value =
            req.Reason.Trim();

        cmd.Parameters.Add(
            "@actor",
            SqlDbType.NVarChar,
            100
        ).Value =
            req.Actor.Trim();

        cmd.ExecuteNonQuery();

        tx.Commit();

        return new TransferTicketResult(
            req.TicketNumber,
            source,
            req.TargetService.Trim(),
            DateTimeOffset.UtcNow
        );
    }


    // ========================================================
    // STATUS
    // ========================================================

    public TicketStatus GetStatus(
        int n
    )
    {
        var t =
            GetTicket(n);

        if (t is not null)
        {
            var ahead =
                GetByOffice(t.Service)
                    .Count(
                        x =>
                            x.Status == "Waiting"
                            &&
                            (
                                x.IsPriority
                                || !t.IsPriority
                            )
                            &&
                            (
                                x.IsPriority != t.IsPriority
                                    ? x.IsPriority
                                    : x.TimeAdded < t.TimeAdded
                                      ||
                                      (
                                          x.TimeAdded == t.TimeAdded
                                          &&
                                          x.TicketNumber <
                                              t.TicketNumber
                                      )
                            )
                    );

            return new TicketStatus(
                t.Status,
                n,
                t.TicketLabel,
                ahead,
                ahead + 1,
                t.Status == "Serving"
                    ? 0
                    : ahead * _avg
            );
        }

        using var c =
            new SqlConnection(_cs);

        c.Open();

        using var cmd =
            c.CreateCommand();

        cmd.CommandText = """
            SELECT
                ServiceTicketNumber,
                Purpose,
                Service
            FROM dbo.QueueHistory
            WHERE TicketNumber = @n
            """;

        cmd.Parameters.Add(
            "@n",
            SqlDbType.Int
        ).Value = n;

        using var r =
            cmd.ExecuteReader();

        if (r.Read())
        {
            var seq =
                r.GetInt32(0);

            var p =
                r.GetString(1);

            var svc =
                r.GetString(2);

            return new TicketStatus(
                "Completed",
                n,
                Label(
                    svc,
                    p,
                    seq
                ),
                0,
                0,
                0
            );
        }

        return new TicketStatus(
            "NotFound",
            n,
            "",
            0,
            0,
            0
        );
    }


    // ========================================================
    // READ TICKET
    // ========================================================

    private static WebTicket Read(
        SqlDataReader r
    )
    {
        return new WebTicket(
            r.GetInt32(0),
            r.GetInt32(1),
            r.GetString(2),
            r.GetString(3),
            r.GetDateTime(4),
            r.IsDBNull(5)
                ? null
                : r.GetInt32(5),
            r.IsDBNull(6)
                ? null
                : r.GetDateTime(6),
            r.GetString(7),
            r.GetBoolean(8),
            Label(
                r.GetString(3),
                r.GetString(2),
                r.GetInt32(1)
            )
        );
    }


    // ========================================================
    // TICKET LABEL
    // ========================================================

    static string Label(
        string service,
        string purpose,
        int seq
    )
    {
        char a =
            service.FirstOrDefault(
                char.IsLetter
            );

        char b =
            purpose.FirstOrDefault(
                char.IsLetter
            );

        return
            $"{char.ToUpperInvariant(a)}{char.ToUpperInvariant(b)}-{seq:D3}";
    }


    // ========================================================
    // ENSURE WEB TABLES
    // ========================================================

    void EnsureWebTables()
    {
        try
        {
            using var c =
                new SqlConnection(_cs);

            c.Open();

            using var cmd =
                c.CreateCommand();

            cmd.CommandText = """
                IF OBJECT_ID(
                    'dbo.CurrentCalls'
                ) IS NULL
                CREATE TABLE dbo.CurrentCalls
                (
                    TicketNumber INT NOT NULL PRIMARY KEY,
                    Service NVARCHAR(100) NOT NULL,
                    WindowNumber INT NOT NULL,
                    CalledAt DATETIME2 NOT NULL
                );

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE
                        name =
                            'UX_CurrentCalls_Service_Window'
                        AND object_id =
                            OBJECT_ID(
                                'dbo.CurrentCalls'
                            )
                )
                CREATE UNIQUE INDEX
                    UX_CurrentCalls_Service_Window
                ON dbo.CurrentCalls
                (
                    Service,
                    WindowNumber
                );

                IF COL_LENGTH(
                    'dbo.QueueHistory',
                    'ServedWindow'
                ) IS NULL
                ALTER TABLE dbo.QueueHistory
                ADD ServedWindow INT NULL;
                """;

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"CampusQ schema check skipped: {ex.Message}"
            );
        }
    }
}


// ============================================================
// RESPONSE RECORDS
// ============================================================

public sealed record WebTicket(
    int TicketNumber,
    int ServiceTicketNumber,
    string Purpose,
    string Service,
    DateTime TimeAdded,
    int? WindowNumber,
    DateTime? CalledAt,
    string Status,
    bool IsPriority,
    string TicketLabel
);

public sealed record TicketStatus(
    string State,
    int TicketNumber,
    string TicketLabel,
    int PeopleAhead,
    int PositionInLine,
    int EstimatedWaitMinutes
);