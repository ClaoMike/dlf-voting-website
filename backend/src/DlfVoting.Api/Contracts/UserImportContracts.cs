// ReSharper disable NotAccessedPositionalProperty.Global

namespace DlfVoting.Api.Contracts;

public record BulkImportedUser(string Email, string Username, string Password);

public record BulkImportSkippedEntry(string Email, string Reason);

// File is the result workbook (.xlsx) as base64, for the admin to download.
public record BulkImportResponse(List<BulkImportedUser> Created, List<BulkImportSkippedEntry> Skipped, string? File);

public record EmployeeImportWarning(int Row, string Message);

public record EmployeeImportResponse(int CreatedCount, int SkippedCount, List<EmployeeImportWarning> Warnings, string File);
