using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Timezone).HasMaxLength(100).HasDefaultValue("UTC");
        builder.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("GBP");
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();
        builder.Property(e => e.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PasswordHash).IsRequired();
        builder.HasIndex(e => e.NormalizedEmail).IsUnique();
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class BusinessUserConfiguration : IEntityTypeConfiguration<BusinessUser>
{
    public void Configure(EntityTypeBuilder<BusinessUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.UserId, e.BusinessId }).IsUnique();
        builder.HasOne(e => e.User).WithMany(u => u.BusinessUsers).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Business).WithMany(b => b.BusinessUsers).HasForeignKey(e => e.BusinessId);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.BusinessId, e.Name }).IsUnique();
        builder.HasOne(e => e.Business).WithMany(b => b.Roles).HasForeignKey(e => e.BusinessId);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(e => new { e.RoleId, e.PermissionId });
        builder.HasOne(e => e.Role).WithMany(r => r.Permissions).HasForeignKey(e => e.RoleId);
        builder.HasOne(e => e.Permission).WithMany(p => p.RolePermissions).HasForeignKey(e => e.PermissionId);
    }
}

public class BusinessUserRoleConfiguration : IEntityTypeConfiguration<BusinessUserRole>
{
    public void Configure(EntityTypeBuilder<BusinessUserRole> builder)
    {
        builder.HasKey(e => new { e.BusinessUserId, e.RoleId });
        builder.HasOne(e => e.BusinessUser).WithMany(bu => bu.Roles).HasForeignKey(e => e.BusinessUserId);
        builder.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId);
    }
}

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.BusinessId, e.Name }).IsUnique();
        builder.HasOne(e => e.Business).WithMany(b => b.Zones).HasForeignKey(e => e.BusinessId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.HasIndex(e => new { e.BusinessId, e.Phone });
        builder.HasIndex(e => new { e.BusinessId, e.Email });
        builder.HasOne(e => e.Business).WithMany(b => b.Customers).HasForeignKey(e => e.BusinessId);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Registration).HasMaxLength(20);
        builder.Property(e => e.Vin).HasMaxLength(17);
        builder.Property(e => e.DisplayName).HasMaxLength(300);
        builder.HasIndex(e => new { e.BusinessId, e.Registration });
        builder.HasOne(e => e.Customer).WithMany(c => c.Vehicles).HasForeignKey(e => e.CustomerId);
        builder.HasOne(e => e.Business).WithMany().HasForeignKey(e => e.BusinessId);

        // Catalogue links. Restrict: a variant that vehicles reference must not be deletable.
        builder.HasOne(e => e.Variant).WithMany().HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Colour).WithMany().HasForeignKey(e => e.ColourId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ── Vehicle catalogue: global reference data, no BusinessId, no query filter ──

public class VehicleMakeConfiguration : IEntityTypeConfiguration<VehicleMake>
{
    public void Configure(EntityTypeBuilder<VehicleMake> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.VpicMakeId);
    }
}

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(150);
        // One model name per make — this is what makes the importer safely idempotent.
        builder.HasIndex(e => new { e.MakeId, e.Name }).IsUnique();
        builder.HasOne(e => e.Make).WithMany(m => m.Models).HasForeignKey(e => e.MakeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VehicleVariantConfiguration : IEntityTypeConfiguration<VehicleVariant>
{
    public void Configure(EntityTypeBuilder<VehicleVariant> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Trim).HasMaxLength(100);
        builder.Property(e => e.BodyStyle).HasMaxLength(60);
        builder.Property(e => e.EngineDisplacementL).HasPrecision(3, 1);
        builder.Property(e => e.FuelType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Transmission).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DriveType).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Market).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(e => new { e.ModelId, e.YearFrom, e.YearTo });
        builder.HasOne(e => e.Model).WithMany(m => m.Variants).HasForeignKey(e => e.ModelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VehicleColourConfiguration : IEntityTypeConfiguration<VehicleColour>
{
    public void Configure(EntityTypeBuilder<VehicleColour> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(60);
        builder.Property(e => e.HexCode).HasMaxLength(7);
        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.HasOne(e => e.Zone).WithMany(z => z.Bookings).HasForeignKey(e => e.ZoneId);
        builder.HasOne(e => e.Customer).WithMany(c => c.Bookings).HasForeignKey(e => e.CustomerId);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId);
        builder.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).IsRequired(false);
        builder.HasOne(e => e.Business).WithMany().HasForeignKey(e => e.BusinessId);
        builder.HasIndex(e => new { e.BusinessId, e.ZoneId, e.StartUtc, e.EndUtc });
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(500).IsRequired();
        builder.HasOne(e => e.Customer).WithMany(c => c.Jobs).HasForeignKey(e => e.CustomerId);
        builder.HasOne(e => e.Vehicle).WithMany(v => v.Jobs).HasForeignKey(e => e.VehicleId);
        builder.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId).IsRequired(false);
        builder.HasOne(e => e.AssignedZone).WithMany().HasForeignKey(e => e.AssignedZoneId).IsRequired(false);
        builder.HasOne(e => e.Business).WithMany().HasForeignKey(e => e.BusinessId);
        builder.HasIndex(e => new { e.BusinessId, e.Status });
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class JobAssignmentConfiguration : IEntityTypeConfiguration<JobAssignment>
{
    public void Configure(EntityTypeBuilder<JobAssignment> builder)
    {
        builder.HasKey(e => new { e.JobId, e.BusinessUserId });
        builder.HasOne(e => e.Job).WithMany(j => j.Assignments).HasForeignKey(e => e.JobId);
        builder.HasOne(e => e.BusinessUser).WithMany().HasForeignKey(e => e.BusinessUserId);
    }
}

public class JobLaborLineConfiguration : IEntityTypeConfiguration<JobLaborLine>
{
    public void Configure(EntityTypeBuilder<JobLaborLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Hours).HasPrecision(10, 2);
        builder.Property(e => e.Rate).HasPrecision(10, 2);
        builder.HasOne(e => e.Job).WithMany(j => j.LaborLines).HasForeignKey(e => e.JobId);
    }
}

public class JobPartLineConfiguration : IEntityTypeConfiguration<JobPartLine>
{
    public void Configure(EntityTypeBuilder<JobPartLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasPrecision(10, 2);
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2);
        builder.HasOne(e => e.Job).WithMany(j => j.PartLines).HasForeignKey(e => e.JobId);
        builder.HasOne(e => e.InventoryItem).WithMany(i => i.JobPartLines).HasForeignKey(e => e.InventoryItemId);
    }
}

public class InventoryCategoryConfiguration : IEntityTypeConfiguration<InventoryCategory>
{
    public void Configure(EntityTypeBuilder<InventoryCategory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Sku).HasMaxLength(100);
        builder.Property(e => e.UnitCost).HasPrecision(10, 2);
        builder.Property(e => e.RetailPrice).HasPrecision(10, 2);
        builder.HasIndex(e => new { e.BusinessId, e.Sku }).IsUnique().HasFilter("\"Sku\" IS NOT NULL");
        builder.HasOne(e => e.Category).WithMany(c => c.Items).HasForeignKey(e => e.CategoryId).IsRequired(false);
        builder.HasOne(e => e.Business).WithMany(b => b.InventoryItems).HasForeignKey(e => e.BusinessId);

        // StockOnHand is read, checked, then written by both AddPartAsync and
        // AdjustStockAsync. Without a concurrency token two simultaneous part-adds read the
        // same value, both pass the "enough stock?" guard, and the second write silently
        // overwrites the first — stock drifts from the StockMovement trail that exists to
        // reconstruct it, and can go negative. InventoryItem inherits RowVersion from
        // BaseEntity like the five entities that already map it, but was the one left
        // unmapped, so the column was never incremented and never checked.
        // See docs/review-findings.md finding 6.
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.InventoryItem).WithMany(i => i.StockMovements).HasForeignKey(e => e.InventoryItemId);
        builder.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).IsRequired(false);
        builder.HasOne(e => e.Business).WithMany().HasForeignKey(e => e.BusinessId);
        builder.HasIndex(e => new { e.BusinessId, e.InventoryItemId });
    }
}

public class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.To).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500);
        builder.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).IsRequired(false);
        builder.HasOne(e => e.Job).WithMany(j => j.Messages).HasForeignKey(e => e.JobId).IsRequired(false);
        builder.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId).IsRequired(false);
        builder.HasOne(e => e.Business).WithMany().HasForeignKey(e => e.BusinessId);
    }
}

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(e => e.Business).WithMany(b => b.MessageTemplates).HasForeignKey(e => e.BusinessId);
    }
}

public class BusinessSubscriptionConfiguration : IEntityTypeConfiguration<BusinessSubscription>
{
    public void Configure(EntityTypeBuilder<BusinessSubscription> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.BusinessId).IsUnique();
        builder.HasIndex(e => e.StripeCustomerId).IsUnique().HasFilter("\"StripeCustomerId\" IS NOT NULL");
        builder.HasOne(e => e.Business).WithOne(b => b.Subscription).HasForeignKey<BusinessSubscription>(e => e.BusinessId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.BusinessId, e.CreatedAtUtc });
    }
}
