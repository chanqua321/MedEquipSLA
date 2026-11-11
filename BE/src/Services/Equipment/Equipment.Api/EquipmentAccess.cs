using System.Security.Claims;

namespace MedEquipSLA.Equipment.Api;

internal static class EquipmentAccess
{
    public static bool TryScope(ClaimsPrincipal principal, out Guid? department)
    {
        department = null;
        if (principal.IsInRole("Admin") || principal.IsInRole("Technician"))
            return true;
        if (!principal.IsInRole("DepartmentStaff") ||
            !Guid.TryParse(principal.FindFirstValue("department_id"), out var id) || id == Guid.Empty)
            return false;
        department = id;
        return true;
    }
}


