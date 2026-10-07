using Hangfire;
using Hangfire.PostgreSql;
using Jusoor.Api.Middleware;
using Jusoor.Application;
using Jusoor.Infrastructure;
using Jusoor.Infrastructure.Identity;
using Jusoor.Infrastructure.Ingestion;
using Jusoor.Infrastructure.Persistence;
using Jusoor.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS is intentionally narrow: only the Next.js origin(s) from config are
// allowed, never AllowAnyOrigin — the API will eventually carry Atmaen data.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Hangfire storage is configured HERE — after Build(), reading app.Configuration
// (fully merged: real env vars in production/dev, or WebApplicationFactory's
// test overrides under integration tests) — not inside AddInfrastructure,
// which runs before Build() and would hit the same eager-config-read bug
// already fixed there for the DbContext/Jwt sections. See DependencyInjection.cs's
// comment on the Hangfire registration for the full explanation.
var hangfireConnectionString = app.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing (required for Hangfire storage).");
GlobalConfiguration.Configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnectionString));

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Auto-migrate + seed roles on boot. Always in Development, so `docker compose up`
// satisfies the Phase 0 DoD ("new dev running locally in under an hour") without
// a manual migration step. Staging/production apply migrations explicitly during
// deploy (CI/CD job), never automatically on boot — EXCEPT a hosted client-trial
// environment (e.g. Railway), which has no separate deploy job and opts in
// explicitly with Bootstrap__RunOnStartup=true. Swagger stays Development-only.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Bootstrap:RunOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await RoleSeeder.SeedAsync(roleManager);

    // Optional newsroom logins, driven purely by DevSeed:* configuration (no-op when
    // unset). Used for local dev and for the client-trial environment's accounts.
    await DevelopmentNewsroomSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        app.Configuration,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentNewsroomSeeder"));
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Hangfire Dashboard — KNOWN LIMITATION, not solved in this slice: our API
// uses JWT bearer authentication, but a browser navigating to /hangfire
// has no way to attach an Authorization header to a page load. Hangfire's
// default dashboard authorization (MapHangfireDashboard with no explicit
// filters) falls back to local-requests-only, which is safe for local dev
// but is NOT an access control suitable for a deployed environment.
// Production-grade dashboard access (e.g. a separate cookie-based admin
// login, or restricting the route at the network/reverse-proxy level)
// is Planned, not implemented here — flagging this explicitly rather than
// shipping a dashboard that looks secured but isn't.
app.MapHangfireDashboard("/hangfire");

using (var scope = app.Services.CreateScope())
{
    var ingestionOptions = scope.ServiceProvider.GetRequiredService<IOptions<IngestionOptions>>().Value;
    var cronExpression = $"*/{ingestionOptions.ScheduleIntervalMinutes} * * * *";

    RecurringJob.AddOrUpdate<SourceIngestionSweepJob>(
        "source-ingestion-sweep",
        job => job.RunAsync(JobCancellationToken.Null),
        cronExpression);

    // Slice 24: the schedule timestamp on each approved article is durable in
    // PostgreSQL. A one-minute sweep makes retries and process restarts safe;
    // the worker performs an idempotent status-checked transition plus audit.
    RecurringJob.AddOrUpdate<ScheduledEditorialPublicationJob>(
        ScheduledEditorialPublicationJob.JobId,
        job => job.RunAsync(JobCancellationToken.Null),
        Cron.Minutely());
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
