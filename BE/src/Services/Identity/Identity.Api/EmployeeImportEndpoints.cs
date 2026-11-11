using System.IO.Compression;
using System.Security.Claims;
using System.Xml;
using System.Xml.Linq;
using MedEquipSLA.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Identity.Api;

internal static class EmployeeImportEndpoints
{
    internal static void MapEmployeeImport(this WebApplication app)
    {
        app.MapPost("/api/users/import", ImportAsync).RequireAuthorization(p => p.RequireRole("Admin"))
            .WithTags("Employee provisioning").DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(2 * 1024 * 1024 + 65536));
    }
    private static async Task<IResult> ImportAsync(IFormFile file, bool? preview, HttpContext http,
        IAccountProvisioning accounts, IIdentitySessions sessions, IHttpClientFactory clients, CancellationToken ct)
    {
        var actor = SessionClaims.ReadClaims(http.User);
        if (actor is null || !await sessions.IsCurrentAsync(actor, ct)) return Results.Unauthorized();
        if (file.Length is < 1 or > 2097152 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
        IReadOnlyList<ImportRow> rows;
        try { using var input = file.OpenReadStream(); rows = Read(input); }
        catch (Exception error) when (error is InvalidDataException or XmlException or FormatException or ArgumentException)
        { return Results.BadRequest(new { message = "Workbook .xlsx không hợp lệ. Dùng cột Email, FullName, Role, DepartmentId; tối đa 1000 dòng; không dùng công thức." }); }
        var output = new List<object>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var departments = new Dictionary<Guid, string?>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var email = row.Email.Trim(); string? error = null; string? departmentName = null;
            Guid? department = null;
            if (row.DepartmentId.Length > 0)
            {
                if (!Guid.TryParse(row.DepartmentId, out var id) || id == Guid.Empty) error = "InvalidDepartmentId";
                else
                {
                    department = id;
                    if (!departments.TryGetValue(id, out departmentName))
                    { departmentName = await AccountProvisioningEndpoints.DepartmentAsync(id, http, clients, ct); departments[id] = departmentName; }
                    if (departmentName is null) error = "DepartmentUnavailable";
                }
            }
            if (row.Role is not ("Technician" or "DepartmentStaff") || row.Role == "DepartmentStaff" && department is null) error ??= "InvalidRoleOrDepartment";
            if (!System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email || email.Length > 256 || row.FullName.Trim().Length is < 1 or > 200) error ??= "InvalidEmailOrName";
            if (!seen.Add(email)) error ??= "DuplicateInWorkbook";
            if (error is not null) { output.Add(new { row.Row, status = error, userId = (Guid?)null }); continue; }
            var result = preview == true ? new ProvisionResult("Valid") : await accounts.InviteAsync(new(email, row.FullName.Trim(), row.Role,
                department, departmentName), actor.Id, ct);
            output.Add(new { row.Row, status = result.Status, userId = result.UserId });
        }
        return Results.Ok(new { preview = preview == true, rows = output });
    }
    private sealed record ImportRow(int Row, string Email, string FullName, string Role, string DepartmentId);
    private static IReadOnlyList<ImportRow> Read(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        XDocument ReadXml(ZipArchiveEntry entry)
        {
            if (entry.Length > 2_000_000) throw new InvalidDataException();
            using var stream = entry.Open(); using var reader = XmlReader.Create(stream,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
            return XDocument.Load(reader);
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var strings = archive.GetEntry("xl/sharedStrings.xml") is { } shared ? ReadXml(shared).Descendants(ns + "si")
            .Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToArray() : [];
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException();
        var elements = ReadXml(sheet).Descendants(ns + "row").Take(1002).ToArray();
        if (elements.Length is < 1 or > 1001) throw new InvalidDataException();
        string[] Cells(XElement row)
        {
            var result = new string[4]; Array.Fill(result, "");
            foreach (var cell in row.Elements(ns + "c"))
            {
                if (cell.Element(ns + "f") is not null) throw new InvalidDataException();
                var reference = (string?)cell.Attribute("r") ?? "";
                var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                var index = Array.IndexOf(new[] { "A", "B", "C", "D" }, column);
                if (index < 0) continue;
                var value = cell.Element(ns + "v")?.Value ?? string.Concat(cell.Descendants(ns + "t").Select(x => x.Value));
                if ((string?)cell.Attribute("t") == "s")
                { if (!int.TryParse(value, out var key) || key < 0 || key >= strings.Length) throw new InvalidDataException(); value = strings[key]; }
                if (value.Length > 1024) throw new InvalidDataException(); result[index] = value.Trim();
            }
            return result;
        }
        if (!Cells(elements[0]).SequenceEqual(new[] { "Email", "FullName", "Role", "DepartmentId" })) throw new InvalidDataException();
        return elements.Skip(1).Select((row, i) => (Values: Cells(row), Number: i + 2)).Where(x => x.Values.Any(s => s.Length > 0))
            .Select(x => new ImportRow(x.Number, x.Values[0], x.Values[1], x.Values[2], x.Values[3])).ToArray();
    }
}
