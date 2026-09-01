using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly Guid? _currentBusinessId;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _currentBusinessId = tenantProvider.BusinessId;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<BusinessUserRole> BusinessUserRoles => Set<BusinessUserRole>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobLaborLine> JobLaborLines => Set<JobLaborLine>();
    public DbSet<JobPartLine> JobPartLines => Set<JobPartLine>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<TaxRateComponent> TaxRateComponents => Set<TaxRateComponent>();
    public DbSet<TaxRateCategory> TaxRateCategories => Set<TaxRateCategory>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<BusinessSubscription> BusinessSubscriptions => Set<BusinessSubscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Vehicle catalogue — global reference data, deliberately NOT tenant-filtered.
    public DbSet<VehicleMake> VehicleMakes => Set<VehicleMake>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<VehicleVariant> VehicleVariants => Set<VehicleVariant>();
    public DbSet<VehicleColour> VehicleColours => Set<VehicleColour>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filters for multi-tenant scoping.
        // EF Core evaluates _currentBusinessId at query time (not model creation time).
        // When null (unauthenticated), all records pass through.
        // When set, only matching business records are returned.
        modelBuilder.Entity<Zone>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        // Filters are registered one entity at a time, so a new BusinessScopedEntity has NO
        // isolation until its line exists. TaxRate is one.
        modelBuilder.Entity<TaxRate>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<TaxRateCategory>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<Booking>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<Job>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<InventoryItem>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<OutboundMessage>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<MessageTemplate>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<Role>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<BusinessUser>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
        modelBuilder.Entity<BusinessSubscription>().HasQueryFilter(e => _currentBusinessId == null || e.BusinessId == _currentBusinessId);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

public interface ITenantProvider
{
    Guid? BusinessId { get; }
    Guid? UserId { get; }
}
