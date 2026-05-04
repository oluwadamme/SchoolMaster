using SchoolMaster.Api.Middlewares;
using SchoolMaster.Application.Services.Interfaces;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using SchoolMaster.Infrastructure.Options;
using SchoolMaster.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using SchoolMaster.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-logs.json") // The File Sink!
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog(); // Tell .NET to use Serilog instead of the default logger


    // Add services to the container.
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    // 1. Tell ASP.NET Core to auto-validate requests using FluentValidation
    builder.Services.AddFluentValidationAutoValidation();
    // 2. Tell DI to scan your project and register RegisterRequestValidator (and any others you make)
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.Configure<EmailVerificationOptions>(builder.Configuration.GetSection("EmailVerification"));

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
    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<SchoolMasterContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
        app.UseHangfireDashboard();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
            if (db.Database.IsRelational())
            {
                db.Database.Migrate();
            }
        }
    }


    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging(); // Add before UseAuthentication()

    app.UseAuthentication();   // ← BEFORE authorization
    app.UseAuthorization();    // ← AFTER authentication
                               // Configure the HTTP request pipeline.
    app.UseRateLimiter();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly");
}
finally
{
    Log.CloseAndFlush();
}