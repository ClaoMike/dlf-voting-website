using DlfVoting.Api.Common;
using DlfVoting.Api.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>Creating users in bulk from an uploaded Excel file.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserImportsController : ControllerBase
{
    private readonly EmailImportService _emailImport;
    private readonly EmployeeImportService _employeeImport;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserImportsController(EmailImportService emailImport, EmployeeImportService employeeImport)
    {
        _emailImport = emailImport;
        _employeeImport = employeeImport;
    }

    [HttpPost("bulk-import")]
    [RequestSizeLimit(ExcelWorkbooks.MaxFileBytes)]
    public async Task<IActionResult> ImportEmails(IFormFile? file)
    {
        using var workbook = await ExcelWorkbooks.OpenAsync(file);
        return workbook is null
            ? BadRequest(new { message = ExcelWorkbooks.InvalidFileMessage })
            : this.ToActionResult(await _emailImport.ImportAsync(workbook));
    }

    [HttpPost("import-employees")]
    [RequestSizeLimit(ExcelWorkbooks.MaxFileBytes)]
    public async Task<IActionResult> ImportEmployees(IFormFile? file)
    {
        using var workbook = await ExcelWorkbooks.OpenAsync(file);
        return workbook is null
            ? BadRequest(new { message = ExcelWorkbooks.InvalidFileMessage })
            : this.ToActionResult(await _employeeImport.ImportAsync(workbook));
    }
}
