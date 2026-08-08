using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/sections/reports")]
[Authorize(Policy = AuthorizationPolicies.CanViewSections)]
public sealed class SectionOperationalReportsController : ControllerBase
{
    private readonly ISectionOperationalReportService _reports;

    public SectionOperationalReportsController(ISectionOperationalReportService reports) => _reports = reports;

    /// <summary>
    /// Export section operational reports. kind: section-capacity | section-occupancy | merge-history |
    /// split-history | section-lifecycle | readiness. format: csv | excel | pdf
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string kind = "section-capacity",
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var bytes = await _reports.ExportAsync(kind, format, cancellationToken);
        var (contentType, fileName) = format.Trim().ToLowerInvariant() switch
        {
            "xlsx" or "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{kind}.xlsx"),
            "pdf" => ("application/pdf", $"{kind}.pdf"),
            _ => ("text/csv", $"{kind}.csv"),
        };
        return File(bytes, contentType, fileName);
    }
}
