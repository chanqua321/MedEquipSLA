using Microsoft.AspNetCore.Identity;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var value = password?.ToLowerInvariant() ?? "";
        var letters = new string(value.Where(char.IsLetter).ToArray());
        var common = new[] { "password", "pssword", "qwerty", "letmein", "admin" };
        return Task.FromResult(common.Contains(letters, StringComparer.Ordinal) || value == "123456789012"
            ? IdentityResult.Failed(new IdentityError { Code = "CommonPassword", Description = "Choose a password without common compromised patterns." })
            : IdentityResult.Success);
    }
}
