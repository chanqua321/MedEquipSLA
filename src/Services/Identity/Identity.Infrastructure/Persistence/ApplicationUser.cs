using Microsoft.AspNetCore.Identity;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; private set; } = string.Empty;
    public Guid? DepartmentId { get; private set; }
    public string? DepartmentNameSnapshot { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long AuthorizationVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
