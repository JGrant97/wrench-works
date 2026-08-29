namespace WrenchWorks.Domain.Entities;

public class BusinessUser : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public BusinessUserStatus Status { get; set; } = BusinessUserStatus.Active;

    public ICollection<BusinessUserRole> Roles { get; set; } = [];
}

public enum BusinessUserStatus
{
    Pending,
    Active,
    Disabled
}

public class Role : BusinessScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];
    public ICollection<BusinessUserRole> UserRoles { get; set; } = [];
}

public class Permission : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class BusinessUserRole
{
    public Guid BusinessUserId { get; set; }
    public BusinessUser BusinessUser { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
