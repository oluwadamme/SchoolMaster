using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Infrastructure.Persistence;
using SchoolMaster.Tests.Integration.Helpers;
using Testcontainers.PostgreSql;
using Xunit;
using Moq;
namespace SchoolMaster.Tests.Integration;

/// <summary>
/// One PostgreSQL container is started per test class via IClassFixture.
/// Migrations are applied once when the factory initialises.
/// </summary>
public class SchoolMasterWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string FixedOtp = "0000";

    // The server uses JsonStringEnumConverter, so the test client must too.
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // -------------------------------------------------------------------------
    // IAsyncLifetime — container lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // EnsureCreated builds the schema directly from the current model.
        // This avoids EF Core 10's PendingModelChangesWarning, which fires when
        // entity changes exist that have not yet been captured in a migration.
        // Testcontainers always starts a blank database, so there is nothing to
        // migrate incrementally — we just need the schema to exist.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Test host configuration
    // -------------------------------------------------------------------------

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override settings before the app reads them.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // JWT must be ≥ 32 ASCII chars for HmacSha256
                ["Jwt:Key"] = "schoolmaster-test-secret-key-min-32-chars!!",
                ["Jwt:Issuer"] = "schoolmaster-test",
                ["Jwt:Audience"] = "schoolmaster-test",
                ["Jwt:ExpirationInMinutes"] = "60",
                ["EmailVerification:ExpirationInMinutes"] = "15",
                ["EmailSettings:SmtpServer"] = "localhost",
                ["EmailSettings:SmtpPort"] = "25",
                ["EmailSettings:SenderEmail"] = "test@schoolmaster.test",
                ["EmailSettings:SenderName"] = "SchoolMaster Test",
                ["EmailSettings:Password"] = "test-password",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace EmailService (would try a real SMTP connection) with a no-op mock.
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddScoped<IEmailService>(_ => Mock.Of<IEmailService>());

            // Replace the random OTP generator with one that always returns "0000".
            // This lets integration tests predict the OTP value stored in the DB.
            var otpDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IOtpService));
            if (otpDescriptor != null) services.Remove(otpDescriptor);
            services.AddScoped<IOtpService>(_ => new FixedOtpService(FixedOtp));

            // Hangfire is disabled in the "Testing" environment (see Program.cs), so
            // IBackgroundJobClient is not registered. Register a mock so AuthService
            // and OnboardingService can be constructed by the DI container.
            services.AddSingleton<IBackgroundJobClient>(_ => Mock.Of<IBackgroundJobClient>());
        });
    }
}
