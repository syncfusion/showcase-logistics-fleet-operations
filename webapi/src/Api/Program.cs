using System.Reflection;
using System.Text;
using System.Text.Json;
using LogisticsFleetOperations.Api;
using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Services;
using LogisticsFleetOperations.Infrastructure.Auth;
using LogisticsFleetOperations.Infrastructure.Persistence;
using LogisticsFleetOperations.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// --- Database: two separate connection strings/identities, per the requirement that public GET
// endpoints run against a read-only database identity that cannot write, not just an API layer
// that chooses not to write. Real values sourced from user-secrets, never appsettings.json --
// appsettings.json declares the keys as empty placeholders for discoverability, so the check
// below must catch empty as well as missing: a bare "?? throw" only fires on a fully-absent key.
var appConnectionString = builder.Configuration.GetConnectionString("App");
if (string.IsNullOrWhiteSpace(appConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:App is not configured. Set it via dotnet user-secrets.");
}
var readOnlyConnectionString = builder.Configuration.GetConnectionString("ReadOnly");
if (string.IsNullOrWhiteSpace(readOnlyConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:ReadOnly is not configured. Set it via dotnet user-secrets.");
}

builder.Services.AddDbContext<FleetWriteDbContext>(options => options.UseNpgsql(appConnectionString));
builder.Services.AddDbContext<FleetReadDbContext>(options => options.UseNpgsql(readOnlyConnectionString));

builder.Services.AddScoped<IReadRepository, PostgresReadRepository>();
builder.Services.AddScoped<IWriteRepository, PostgresWriteRepository>();

// --- CORS: this API and its frontend deploy to two separate App Services, so cross-origin calls
// are the normal production case. Origins come from Cors:AllowedOrigins, never committed to
// appsettings*.json, and the policy fails closed outside Development. ---
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpaApp", policy =>
    {
        if (corsOrigins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                corsOrigins = ["http://localhost:5175", "http://127.0.0.1:5175"];
            }
            else
            {
                throw new InvalidOperationException("Production CORS origins are not configured. Set Cors:AllowedOrigins via Azure App Service Environment variables (Cors__AllowedOrigins__0).");
            }
        }

        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

// --- /health: every API-backed app exposes a real, dependency-checking health endpoint here --
// not a hardcoded "ok". Two separate DB checks deliberately mirror the app's two-identity
// architecture (write vs. read-only). ---
builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<DbConnectivityHealthCheck<FleetWriteDbContext>>(
        "postgresql-write", failureStatus: null, tags: [], args: ["PostgreSQL (write)"])
    .AddTypeActivatedCheck<DbConnectivityHealthCheck<FleetReadDbContext>>(
        "postgresql-readonly", failureStatus: null, tags: [], args: ["PostgreSQL (read-only)"]);
builder.Services.AddSingleton<IAuthService, DemoAuthProvider>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<FleetOverviewService>();
builder.Services.AddScoped<ReferenceDataService>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<ShipmentService>();
builder.Services.AddScoped<ExceptionService>();
builder.Services.AddScoped<ScheduleService>();

// --- JWT auth for the customer/authenticated profile. Showcase-scoped demo account, real JWT
// validation server-side (framework transport is not a security boundary). ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException("Jwt:SigningKey is not configured. Set it via dotnet user-secrets.");
}
var jwtIssuer = jwtSection["Issuer"] ?? "logistics-fleet-operations";
var jwtAudience = jwtSection["Audience"] ?? "logistics-fleet-operations-clients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowSpaApp");
app.UseAuthentication();
app.UseAuthorization();

// Per-dependency status, service/version/uptime, so an operator (or the
// React /health page's live fetch) gets more than a bare "ok".
var startedAt = DateTimeOffset.UtcNow;
var apiVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
var healthCheckOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var payload = new
        {
            status = report.Status.ToString(),
            service = "logistics-fleet-operations-api",
            version = apiVersion,
            environment = app.Environment.EnvironmentName,
            uptimeSeconds = (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
            timestamp = DateTimeOffset.UtcNow,
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message,
            }),
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    },
};
// Root /health: this API's own Azure App Service liveness/readiness probe target
// (matches the frontend static host's server.mjs convention at its own origin).
app.MapHealthChecks("/health", healthCheckOptions);
// Also under /api: the only path the React app's same-origin reverse-proxy setup
// forwards to this API (see vite.config.ts's dev proxy) -- the frontend's live
// /health page reads this one, not the root path.
app.MapHealthChecks("/api/health", healthCheckOptions);

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
