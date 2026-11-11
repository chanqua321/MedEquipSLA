using System.Text;
using MedEquipSLA.Maintenance.Application.Features.Reports;

namespace MedEquipSLA.Maintenance.Api;

internal static class OperationalReportEndpoints
{
    internal static void MapOperationalReports(this WebApplication app)
    {
        app.MapGet("/api/reports/maintenance", async (Guid? departmentId, string? status, DateTimeOffset? fromUtc,
            DateTimeOffset? untilUtc, int? page, string? format, HttpContext http, OperationalReportQuery query, CancellationToken ct) =>
        {
            if (page is < 1 or > 1_000_000 || status?.Length > 30 || untilUtc <= fromUtc || format is not (null or "json" or "csv")) return Results.BadRequest();
            var result = await query.HandleAsync(new(departmentId, status, fromUtc, untilUtc, page ?? 1), ct);
            http.Response.Headers.CacheControl = "private, no-store";
            if (format != "csv") return Results.Ok(result);
            http.Response.Headers["X-Has-Next-Page"] = result.HasNext ? "true" : "false";
            http.Response.Headers["X-Data-Scope"] = "owner-local; see JSON report metadata";
            static string Cell(string? value)
            {
                value ??= "";
                if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            var csv = new StringBuilder("Id,Code,Name,DepartmentId,Status,AtUtc,ResponseOutcome,ResolutionOutcome\r\n");
            foreach (var row in result.Items) csv.AppendLine(string.Join(",", new[] { row.Id.ToString(), row.Code, row.Name,
                row.DepartmentId.ToString(), row.Status, row.AtUtc.ToString("O"), row.ResponseOutcome, row.ResolutionOutcome }.Select(Cell)));
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "maintenance-report-page-" + (page ?? 1) + ".csv");
        }).RequireAuthorization(p => p.RequireRole("Admin")).WithTags("Administrative reports");
    }
}
