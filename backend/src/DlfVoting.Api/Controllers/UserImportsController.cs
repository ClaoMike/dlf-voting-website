using System.Security.Claims;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>
/// Creating users in bulk from an uploaded Excel file. An upload starts a background import (see <see cref="ImportJobs"/>)
/// and returns 202 with its id; the browser polls GET /api/users/imports/{id} for progress and the result.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserImportsController : ControllerBase
{
    private readonly ImportJobs _jobs;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserImportsController(ImportJobs jobs)
    {
        _jobs = jobs;
    }

    private Guid CurrentAdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("bulk-import")]
    [RequestSizeLimit(ExcelWorkbooks.MaxFileBytes)]
    public Task<IActionResult> ImportEmails(IFormFile? file) =>
        StartAsync(file, (services, workbook, progress) =>
            services.GetRequiredService<EmailImportService>().ImportAsync(workbook, progress));

    [HttpPost("import-employees")]
    [RequestSizeLimit(ExcelWorkbooks.MaxFileBytes)]
    public Task<IActionResult> ImportEmployees(IFormFile? file) =>
        StartAsync(file, (services, workbook, progress) =>
            services.GetRequiredService<EmployeeImportService>().ImportAsync(workbook, progress));

    [HttpGet("imports/{id:guid}")]
    public IActionResult GetImport(Guid id)
    {
        var job = _jobs.Find(id, CurrentAdminId);
        return job is null ? NotFound(new { message = ImportJobResponse.GoneMessage }) : Ok(ImportJobResponse.From(job));
    }

    /// <summary>Forgets a finished import and its passwords (the browser calls this once it has saved the file).</summary>
    [HttpDelete("imports/{id:guid}")]
    public IActionResult DeleteImport(Guid id) =>
        _jobs.Remove(id, CurrentAdminId) ? NoContent() : NotFound(new { message = ImportJobResponse.GoneMessage });

    private async Task<IActionResult> StartAsync<TResult>(
        IFormFile? file, Func<IServiceProvider, ClosedXML.Excel.XLWorkbook, ImportProgress, Task<Common.OperationResult<TResult>>> import)
        where TResult : class
    {
        // Read the upload now, while the request is open; the job owns (and disposes) the workbook from here on.
        var workbook = await ExcelWorkbooks.OpenAsync(file);
        if (workbook is null) return BadRequest(new { message = ExcelWorkbooks.InvalidFileMessage });

        var job = _jobs.TryStart<TResult>(CurrentAdminId, async (services, progress) =>
        {
            using (workbook)
            {
                return await import(services, workbook, progress);
            }
        });

        if (job is null)
        {
            workbook.Dispose();
            return Conflict(new { message = ImportJobs.AlreadyRunningMessage });
        }

        return AcceptedAtAction(nameof(GetImport), new { id = job.Id }, ImportJobResponse.From(job));
    }
}
