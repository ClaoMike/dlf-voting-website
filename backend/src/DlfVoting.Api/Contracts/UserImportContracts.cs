// ReSharper disable NotAccessedPositionalProperty.Global

using DlfVoting.Api.Imports;

namespace DlfVoting.Api.Contracts;

public record BulkImportedUser(string Email, string Username, string Password);

public record BulkImportSkippedEntry(string Email, string Reason);

// File is the result workbook (.xlsx) as base64, for the admin to download.
public record BulkImportResponse(List<BulkImportedUser> Created, List<BulkImportSkippedEntry> Skipped, string? File);

public record EmployeeImportWarning(int Row, string Message);

public record EmployeeImportResponse(int CreatedCount, int SkippedCount, List<EmployeeImportWarning> Warnings, string File);

/// <summary>
/// A background import: "running" (with progress), "succeeded" (with the import's result, including the file), or
/// "failed" (with a message). Processed/Total count the passwords hashed so far; Total is 0 while the file is read.
/// </summary>
public record ImportJobResponse(Guid Id, string Status, int Processed, int Total, object? Result, string? Message)
{
    public const string GoneMessage = "This import is no longer available.";

    public static ImportJobResponse From(ImportJob job) => new(
        job.Id,
        job.Status.ToString().ToLowerInvariant(),
        job.Progress.Done,
        job.Progress.Total,
        job.Status == ImportJobStatus.Succeeded ? job.Result : null,
        job.Status == ImportJobStatus.Failed ? job.FailureMessage : null);
}
