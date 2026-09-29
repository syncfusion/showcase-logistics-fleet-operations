using LogisticsFleetOperations.Infrastructure.Persistence;
using LogisticsFleetOperations.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Standalone seed command. Never run from API startup or a browser request (per the blueprint).
// Reads the write connection string from the Api project's user-secrets store (shared UserSecretsId)
// so no credential is hardcoded or committed here. The actual dataset (delete + rebuild) lives in
// DemoDataSeeder, shared with the authenticated POST /api/admin/reset-data endpoint so the two never drift.
const string ApiUserSecretsId = "f12479f5-0cf2-4239-84ff-14d3728a28d1";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(ApiUserSecretsId)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("App")
    ?? configuration["ConnectionStrings:App"]
    ?? throw new InvalidOperationException(
        "ConnectionStrings:App not found. Run `dotnet user-secrets set \"ConnectionStrings:App\" \"...\"` " +
        "from src/Api, or set the ConnectionStrings__App environment variable.");

var options = new DbContextOptionsBuilder<FleetWriteDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new FleetWriteDbContext(options);

Console.WriteLine("Logistics Fleet & Delivery Operations — seed command");
Console.WriteLine("Clearing existing seeded rows (idempotent reseed)...");

var summary = await DemoDataSeeder.ResetAsync(db);

Console.WriteLine($"Dataset version: {summary.DatasetVersion}");
Console.WriteLine("Seed complete.");
Console.WriteLine($"  Depots:          {summary.Depots}");
Console.WriteLine($"  Vehicles:        {summary.Vehicles}");
Console.WriteLine($"  Drivers:         {summary.Drivers}");
Console.WriteLine($"  Routes:          {summary.Routes}");
Console.WriteLine($"  RouteStops:      {summary.RouteStops}");
Console.WriteLine($"  Shipments:       {summary.Shipments}");
Console.WriteLine($"  Exceptions:      {summary.Exceptions}");
Console.WriteLine($"  ScheduleEntries: {summary.ScheduleEntries}");
