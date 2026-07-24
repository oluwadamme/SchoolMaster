using SchoolMaster.Api.Middlewares;
using SchoolMaster.Api.Filters;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Infrastructure.Repositories;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using SchoolMaster.Infrastructure.Options;
using SchoolMaster.Application.Services;
using SchoolMaster.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Hangfire;
using Hangfire.PostgreSql;
using SchoolMaster.Infrastructure.Persistence;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System.Text.Json;
using SchoolMaster.Api.Converters;
using SchoolMaster.Infrastructure.Jobs;
using SchoolMaster.Infrastructure.EventHandlers;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// Console only. Container filesystems are ephemeral, so the old file sink lost every log on
// restart, and writing to logs/ fails outright once the container runs as a non-root user.
// The hosting platform captures stdout, so that is the only sink worth having in production.
// This must stay assigned: builder.Host.UseSerilog() with no argument binds to this static
// logger, and leaving it unset silently swaps in a no-op logger that writes nothing at all.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

// Load .env file but DO NOT overwrite existing environment variables (like those set by Docker)
DotNetEnv.Env.NoClobber().Load();

try
{
    var builder = WebApplication.CreateBuilder(args);
    // Immediately after WebApplication.CreateBuilder(args)
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    builder.Host.UseSerilog(); // Tell .NET to use Serilog instead of the default logger

    // where you register the services you will use
    // Add services to the container.
    builder.Services.AddControllers(options =>
        {
            // Commits the Unit of Work after each action but before the result is serialized,
            // so a failed SaveChangesAsync surfaces as a catchable exception (see UnitOfWorkFilter).
            options.Filters.Add<UnitOfWorkFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.JsonSerializerOptions.Converters.Add(new OptionalConverterFactory());
        });
    // 1. Tell ASP.NET Core to auto-validate requests using FluentValidation
    builder.Services.AddFluentValidationAutoValidation();
    // 2. Tell DI to scan your project and register RegisterRequestValidator (and any others you make)
    builder.Services.AddValidatorsFromAssembly(typeof(OnboardTenantRequestValidator).Assembly);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
    builder.Services.AddScoped<IOnboardingService, OnboardingService>();
    builder.Services.AddScoped<IStaffService, StaffService>(); // Register the StaffService
    builder.Services.AddScoped<IStudentService, StudentService>();
    builder.Services.AddScoped<ITenantRepository, TenantRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IStaffRepository, StaffRepository>(); // Register the StaffRepository
    builder.Services.AddScoped<IStudentRepository, StudentRepository>(); // Register the StudentRepository
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IJwtService, JwtService>(); // This line was already there, just showing context
    builder.Services.AddScoped<IOtpService, OtpService>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IAcademicService, AcademicService>();
    builder.Services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
    builder.Services.AddScoped<IClassRepository, ClassRepository>();
    builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
    builder.Services.AddScoped<IPeriodRepository, PeriodRepository>();

    // MediatR — register from both the API assembly and the assembly holding the domain-event
    // handlers. Explicit so that splitting Infrastructure into its own project later cannot
    // silently drop handler discovery and quietly stop firing absence notifications.
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblyContaining<Program>();
        cfg.RegisterServicesFromAssemblyContaining<StudentMarkedAbsentEventHandler>();
    });

    // Attendance
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<IAttendanceService, AttendanceService>();
    builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
    builder.Services.AddScoped<IStudentRepository, StudentRepository>();
    builder.Services.AddScoped<IAbsenceNotificationJob, AbsenceNotificationJob>();



    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.Configure<EmailVerificationOptions>(builder.Configuration.GetSection("EmailVerification"));
    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("EmailSettings"));

    // Fail fast on a missing or too-short signing key. HmacSha256 needs at least 256 bits (32 bytes);
    // a short key produces weak, forgeable signatures. This guard cannot measure entropy, only length.
    // Skipped under "Testing": the test host injects its key later via ConfigureAppConfiguration, so it
    // is not yet visible at this point in startup (and tests always supply a valid 32+ char key).
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var jwtKey = builder.Configuration.GetSection("Jwt")["Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing or shorter than 32 bytes. Configure a strong, random 256-bit (or longer) key.");
        }
    }

    builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
       {
           var jwtSettings = builder.Configuration.GetSection("Jwt");
           options.TokenValidationParameters = new TokenValidationParameters
           {
               ValidateIssuer = true,
               ValidateAudience = true,
               ValidateLifetime = true,
               ValidateIssuerSigningKey = true,
               ValidIssuer = jwtSettings["Issuer"],
               ValidAudience = jwtSettings["Audience"],
               IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]??""))
           };

           // After signature/lifetime checks pass, re-validate the user server-side: the security stamp
           // in the token must still match the stored one, and the account must still be active. This is
           // what makes password reset and deactivation revoke already-issued access tokens immediately.
           options.Events = new JwtBearerEvents
           {
               OnTokenValidated = async context =>
               {
                   var principal = context.Principal;
                   var userIdValue = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                   var tenantValue = principal?.FindFirst("tenant_id")?.Value;
                   var stampValue = principal?.FindFirst("security_stamp")?.Value;

                   if (!Guid.TryParse(userIdValue, out var userId)
                       || !Guid.TryParse(tenantValue, out var tenantId)
                       || string.IsNullOrEmpty(stampValue))
                   {
                       context.Fail("Invalid token claims.");
                       return;
                   }

                   var userRepository = context.HttpContext.RequestServices
                       .GetRequiredService<IUserRepository>();
                   var user = await userRepository.GetUserByIdAsync(userId, tenantId);

                   if (user is null || user.SecurityStamp.ToString() != stampValue)
                   {
                       context.Fail("Session is no longer valid.");
                   }
               }
           };
       });
    builder.Services.AddAuthorization(
        options =>
        {
            // Dynamically create a policy for each permission in the Permission enum
            foreach (var permission in Enum.GetValues<Permission>())
            {
                options.AddPolicy(permission.ToString(), policy =>
                    policy.AddRequirements(new HasPermissionRequirement(permission)));
            }
        }
    );
    builder.Services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();

    // Registered as a singleton factory so it is built lazily at first resolve —
    // after the DI container is fully configured. This lets WebApplicationFactory
    // swap it out with a Testcontainers data source before any test runs.
    builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection");
        var dsBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dsBuilder.ConfigureJsonOptions(new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
        return dsBuilder.Build();
    });

    builder.Services.AddDbContext<SchoolMasterContext>((sp, options) =>
        options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

    // Tagged "ready" so it runs for the readiness probe only. AddDbContextCheck calls
    // CanConnectAsync, which opens a connection without querying an entity, so the tenant
    // global query filters are never involved and no tenant context is needed.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<SchoolMasterContext>("database", tags: ["ready"]);

    builder.Services.AddRateLimiter(options =>
        {
            // If they get blocked, send back a 429 Too Many Requests
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // "AuthLimit" is partitioned per client IP so one abusive client cannot exhaust a single
            // shared bucket and lock everyone out (and so the limit actually throttles a brute-forcer).
            // NOTE: behind a reverse proxy, enable ForwardedHeaders so RemoteIpAddress is the real client.
            options.AddPolicy("AuthLimit", httpContext =>
            {
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        // Only allow 5 requests per IP address every 1 minute.
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        // Don't queue extra requests, just block them instantly.
                        QueueLimit = 0
                    });
            });
        });

    var isTesting = builder.Environment.IsEnvironment("Testing");

    // Swap job scheduler for a no-op in tests — no Hangfire server or storage needed
    if (!isTesting)
        builder.Services.AddScoped<IAttendanceJobScheduler, HangfireAttendanceJobScheduler>();
    else
        builder.Services.AddScoped<IAttendanceJobScheduler, NoOpAttendanceJobScheduler>();

    // Skip real Hangfire and server in testing environment
    if (!isTesting)
    {
        // 1. Tell Hangfire to use your existing PostgreSQL database
        builder.Services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
        // 2. Add the Hangfire Server (the background worker that processes jobs)
        builder.Services.AddHangfireServer();
    }

    builder.Services.AddSwaggerGen(options =>
    {
        options.OperationFilter<SchoolMaster.Api.Swagger.TenantHeaderOperationFilter>();

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter: Bearer {your JWT token}"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    });
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    // PaaS edge IPs are dynamic, so the default known-proxy allowlist cannot be used.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});


    // CORS: only the origins listed under "Cors:AllowedOrigins" may call the API from a browser.
    // With none configured the policy allows no cross-origin access at all (safe default for an API
    // that has no browser SPA wired up yet).
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultCors", policy =>
        {
            if (corsOrigins.Length > 0)
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        });
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    // 1. First Aid Station (Catch all errors)
    app.UseMiddleware<ExceptionMiddleware>();
    // 2. Check-in Desk (Identify the School)
    app.UseMiddleware<TenantResolverMiddleware>();
    // Unit of Work now commits via UnitOfWorkFilter (an MVC action filter), not middleware,
    // so a failed commit can still be turned into the correct error response.

    // HSTS only outside Development so we never pin localhost to HTTPS in browsers.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();

    // Baseline security response headers on every response.
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";   // don't MIME-sniff responses
        headers["X-Frame-Options"] = "DENY";              // disallow framing (clickjacking)
        headers["Referrer-Policy"] = "no-referrer";       // don't leak URLs to other origins
        await next();
    });

    app.UseCors("DefaultCors");
    app.UseSerilogRequestLogging(); // Add before UseAuthentication()

    app.UseAuthentication();   // ← BEFORE authorization
    app.UseAuthorization();    // ← AFTER authentication
                               // Configure the HTTP request pipeline.
    if (!isTesting) app.UseRateLimiter();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!isTesting)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new HangfireDashboardAuthorizationFilter()] });

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
            if (db.Database.IsRelational())
            {
                db.Database.Migrate();
            }
        }
    }


    // Liveness: is the process up. Deliberately runs no checks (Predicate false), so a database
    // outage never makes the platform kill and restart an otherwise healthy container, which
    // would turn a short database blip into a restart loop.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    // Readiness: is it safe to route traffic here. Runs everything tagged "ready".
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).AllowAnonymous();

    app.MapControllers();
    app.Run();

    // app.Run() returns on a graceful shutdown, which is a success.
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The application failed to start correctly");

    // Non-zero so the hosting platform fails the deploy instead of promoting a container
    // that logged a fatal error and then exited looking successful.
    return 1;
}
finally
{
    Log.CloseAndFlush();
}


// Required so WebApplicationFactory<Program> in integration tests can access this type.
public partial class Program { }