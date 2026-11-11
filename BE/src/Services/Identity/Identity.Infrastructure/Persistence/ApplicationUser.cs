using Microsoft.AspNetCore.Identity;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public static ApplicationUser Invite(string email, string name, Guid? department, string? departmentName, Guid actor) => new()
    {
        Id = Guid.NewGuid(), UserName = email, Email = email, FullName = name, DepartmentId = department,
        DepartmentNameSnapshot = departmentName, IsActive = false, EmailConfirmed = false, LockoutEnabled = true,
        CreatedByUserId = actor, UpdatedByUserId = actor, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };
    public void Activate(Guid actor)
    {
        if (IsInvitationSuspended) throw new InvalidOperationException("Administrator suspended this invitation.");
        EmailConfirmed = true; IsActive = true; AuthorizationVersion++; UpdatedByUserId = actor; UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    public bool IsInvitationSuspended { get; private set; }
    public void SetInvitationSuspension(bool suspended, Guid actor)
    {
        if (EmailConfirmed || PasswordHash is not null) throw new InvalidOperationException("Only an unactivated invitation may be suspended/resumed.");
        IsInvitationSuspended = suspended; IsActive = false; AuthorizationVersion++;
        UpdatedByUserId = actor; UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    public void ChangeEmployeeProfile(string name, Guid? department, string? departmentName, Guid actor)
    {
        FullName = name; DepartmentId = department; DepartmentNameSnapshot = departmentName;
        SetEmployeeAccess(IsActive, actor);
    }
    public static ApplicationUser CreateAdmin(string userName, string email)
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            LockoutEnabled = true,
            FullName = userName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void SetEmployeeAccess(bool active, Guid actor)
    {
        IsActive = active;
        AuthorizationVersion++;
        UpdatedByUserId = actor;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

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
