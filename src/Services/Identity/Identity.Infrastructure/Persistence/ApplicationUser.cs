using Microsoft.AspNetCore.Identity;
namespace MedEquipSLA.Identity.Infrastructure.Persistence;
public sealed class ApplicationUser : IdentityUser<Guid>
{ public long AuthorizationVersion { get; set; } = 1; }
