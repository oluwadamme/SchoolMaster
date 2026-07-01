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
using MediatR;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-logs.json") // The File Sink!
    .CreateLogger();

// Load .env file but DO NOT overwrite existing environment variables (like those set by Docker)
DotNetEnv.Env.NoClobber().Load();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog(); // Tell .NET to use Serilog instead of the default logger

    // where you register the services you will use
    // Add services to the container.
    builder.Services.AddControllers(options =>
        {
            // runs after every controller action
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
               IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
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

    builder.Services.AddRateLimiter(options =>
        {
            // If they get blocked, send back a 429 Too Many Requests
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Define a strict policy named "AuthLimit"
            options.AddFixedWindowLimiter("AuthLimit", config =>
            {
                // Only allow 5 requests per IP address...
                config.PermitLimit = 5;
                // ...every 1 minute.
                config.Window = TimeSpan.FromMinutes(1);
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                // Don't queue extra requests, just block them instantly.
                config.QueueLimit = 0;
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

    var app = builder.Build();

    if (!isTesting)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
            if (db.Database.IsRelational())
            {
                db.Database.Migrate();
            }
        }
    }

    // 1. First Aid Station (Catch all errors)
    app.UseMiddleware<ExceptionMiddleware>();
    // 2. Check-in Desk (Identify the School)
    app.UseMiddleware<TenantResolverMiddleware>();
    // Unit of Work now commits via UnitOfWorkFilter (an MVC action filter), not middleware,
    // so a failed commit can still be turned into the correct error response.
    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(); // Add before UseAuthentication()

    app.UseAuthentication();   // ← BEFORE authorization
    app.UseAuthorization();    // ← AFTER authentication
                               // Configure the HTTP request pipeline.
    var disableRateLimit = builder.Configuration.GetValue<bool>("RateLimiting:Disable", false);
    if (!isTesting && !disableRateLimit) app.UseRateLimiter();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }


    app.MapControllers();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The application failed to start correctly");
}
finally
{
    Log.CloseAndFlush();
}

// Required so WebApplicationFactory<Program> in integration tests can access this type.
public partial class Program { }

public class AllowAllDashboardAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        return true;
    }
}