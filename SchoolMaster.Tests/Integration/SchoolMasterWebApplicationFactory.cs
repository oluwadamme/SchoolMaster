using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
/// : WebApplicationFactory<Program>: This tells the computer: 
/// "Take the real school app and make a temporary copy for me in memory to play with."
/// , IAsyncLifetime: This is a set of rules. It tells the computer how to Start the laboratory
///  and how to Clean it up when we are finished.
public class SchoolMasterWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string FixedOtp = "0000";

    // The server uses JsonStringEnumConverter, so the test client must too.
    // converts your c# objects to JSON when sending requests, and converts JSON back to C# objects when reading responses. The JsonStringEnumConverter makes sure that enum values are sent as their names (e.g., "Admin") instead of their numeric values (e.g., 1).
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    // building fake database
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // -------------------------------------------------------------------------
    // IAsyncLifetime — container lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // EnsureCreated builds the schema directly from the current model instead of using migrations
        // This avoids EF Core 10's PendingModelChangesWarning, which fires when
        // entity changes exist that have not yet been captured in a migration.
        // Testcontainers always starts a blank database for every test class, so there is nothing to
        // migrate incrementally — we just need the schema/c# model to exist.
        // creates "private tool box" to isolate and use a tool then destroy it afterwards. because the database tool is too heavy to just stay in memory, so we create a scope to use it and then destroy it after we are done.
        using var scope = Services.CreateScope();
        // db - instance of schoolmastercontext
        // does the work of the schoolmastercontext(creates tables and apply the 
        // specified restrictions in OnModelCreating in your database)
        var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        // disposes after every test class
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
            // Swap out the production NpgsqlDataSource (registered as a lazy singleton in
            // Program.cs) for one that points at the Testcontainers PostgreSQL instance.
            var dsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(NpgsqlDataSource));
            if (dsDescriptor != null) services.Remove(dsDescriptor);
            
            var testDsBuilder = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString());
            testDsBuilder.ConfigureJsonOptions(new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
            // machine that builds the physical connection between your API and your database(it knows the address of the fake database and is fixed to the database)
            // AddSingleton means make only one instance of this builder for the whole test class, and share it across all tests. This is important because each test needs to talk to the same database instance.
            services.AddSingleton(testDsBuilder.Build());

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
            // IBackgroundJobClient is not registered. Register a mock of IBackgroundJobClient so AuthService
            // and OnboardingService can be constructed by the DI container. because they depend on IBackgroundJobClient.
            services.AddSingleton<IBackgroundJobClient>(_ => Mock.Of<IBackgroundJobClient>());
        });
    }
}
