using LogisticsFleetOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace LogisticsFleetOperations.Infrastructure.Persistence;

/// <summary>
/// Shared EF Core mapping for the fleet schema. Two concrete subclasses (below) bind this same
/// mapping to two different connection strings/database identities: a writable one for authenticated
/// commands and migrations, and a read-only one for public GET endpoints.
/// </summary>
public abstract class FleetDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentException> Exceptions => Set<ShipmentException>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<DatasetMetaRow> DatasetMeta => Set<DatasetMetaRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>(b =>
        {
            b.ToTable("Vehicles");
            b.HasKey(v => v.Id);
            b.Property(v => v.Label).IsRequired().HasMaxLength(120);
            b.Property(v => v.Type).IsRequired().HasMaxLength(40);
            b.Property(v => v.Status).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<Driver>(b =>
        {
            b.ToTable("Drivers");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(120);
            b.Property(d => d.Territory).IsRequired().HasMaxLength(80);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(d => d.VehicleId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Depot>(b =>
        {
            b.ToTable("Depots");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(120);
        });

        modelBuilder.Entity<Route>(b =>
        {
            b.ToTable("Routes");
            b.HasKey(r => r.Id);
            b.Property(r => r.Label).IsRequired().HasMaxLength(120);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
            b.HasOne<Driver>().WithMany().HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Depot>().WithMany().HasForeignKey(r => r.DepotId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(r => r.Stops).WithOne().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RouteStop>(b =>
        {
            b.ToTable("RouteStops");
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.RouteId, s.Sequence });
        });

        modelBuilder.Entity<Shipment>(b =>
        {
            b.ToTable("Shipments");
            b.HasKey(s => s.Id);
            b.Property(s => s.CustomerLabel).IsRequired().HasMaxLength(120);
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(30);
            b.HasOne<Route>().WithMany().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Depot>().WithMany().HasForeignKey(s => s.OriginDepotId).OnDelete(DeleteBehavior.Restrict);
        });

        // RouteStop.ShipmentId is a nullable FK to Shipment, configured from the Shipment side to
        // avoid a second navigation collision with Route.Stops above.
        modelBuilder.Entity<RouteStop>(b =>
        {
            b.HasOne<Shipment>().WithMany().HasForeignKey(s => s.ShipmentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShipmentException>(b =>
        {
            b.ToTable("Exceptions");
            b.HasKey(e => e.Id);
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.Note).HasMaxLength(2000);
            b.HasOne<Shipment>().WithMany().HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleEntry>(b =>
        {
            b.ToTable("ScheduleEntries");
            b.HasKey(e => e.Id);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.HasOne<Driver>().WithMany().HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Route>().WithMany().HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DatasetMetaRow>(b =>
        {
            b.ToTable("DatasetMeta");
            b.HasKey(m => m.Id);
            b.Property(m => m.DatasetVersion).IsRequired().HasMaxLength(60);
        });
    }
}

/// <summary>Writable context (lfo_app identity): migrations, and every authenticated command.</summary>
public sealed class FleetWriteDbContext(DbContextOptions<FleetWriteDbContext> options) : FleetDbContext(options);

/// <summary>Read-only context (lfo_readonly identity): every public GET endpoint.</summary>
public sealed class FleetReadDbContext(DbContextOptions<FleetReadDbContext> options) : FleetDbContext(options);
