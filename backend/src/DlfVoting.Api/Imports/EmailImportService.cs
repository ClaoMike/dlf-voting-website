using ClosedXML.Excel;
using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Validation;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Imports;

/// <summary>
/// "Import email addresses": one email per row in the first column. Each new email gets a generated
/// username and password; the result workbook lists them, plus the skipped rows and why.
/// </summary>
public class EmailImportService
{
    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public EmailImportService(DlfVotingDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<BulkImportResponse>> ImportAsync(XLWorkbook workbook)
    {
        var skipped = new List<BulkImportSkippedEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        foreach (var email in ReadEmails(workbook.Worksheets.First()))
        {
            if (!IdentityRules.IsValidEmail(email))
                skipped.Add(new BulkImportSkippedEntry(email, "Invalid email format"));
            else if (!seen.Add(email))
                skipped.Add(new BulkImportSkippedEntry(email, "Duplicate in file"));
            else
                candidates.Add(email);
        }

        var existingEmails = candidates.Count == 0
            ? []
            : await _db.Users
                .Where(u => u.Email != null && candidates.Contains(u.Email))
                .Select(u => u.Email!)
                .ToListAsync();
        
        var existingSet = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);
        skipped.AddRange(existingEmails.Select(email => new BulkImportSkippedEntry(email, "Already exists")));

        var emailsToCreate = candidates.Where(e => !existingSet.Contains(e)).ToList();
        var usernames = await UsernameGenerator.GenerateUniqueAsync(_db, emailsToCreate.Count);
        var passwords = emailsToCreate.Select(_ => SecurePasswordGenerator.Generate()).ToList();
        var hashes = PasswordHashing.HashMany(passwords);

        var created = new List<BulkImportedUser>();
        for (var i = 0; i < emailsToCreate.Count; i++)
        {
            _db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = usernames[i],
                Email = emailsToCreate[i],
                PasswordHash = hashes[i],
                CreatedAt = DateTime.UtcNow
            });
            created.Add(new BulkImportedUser(emailsToCreate[i], usernames[i], passwords[i]));
        }

        if (created.Count > 0)
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                return OperationResult.Conflict(
                    "One or more emails were created by someone else at the same moment. Please re-upload the file to retry the remaining entries.");
            }
        }

        var resultFile = created.Count > 0 || skipped.Count > 0 ? BuildResultWorkbook(created, skipped) : null;
        return OperationResult<BulkImportResponse>.Success(new BulkImportResponse(created, skipped, resultFile));
    }

    /// <summary>Non-empty values of the first used column, skipping an "email" header row.</summary>
    private static IEnumerable<string> ReadEmails(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed();
        if (used is null) yield break;

        var column = used.FirstColumn().ColumnNumber();
        var firstRow = used.FirstRow().RowNumber();
        for (var r = firstRow; r <= used.LastRow().RowNumber(); r++)
        {
            var value = ExcelWorkbooks.CellText(sheet.Cell(r, column));
            var isHeaderRow = r == firstRow && value.Equals("email", StringComparison.OrdinalIgnoreCase);
            if (!isHeaderRow && !string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static string BuildResultWorkbook(List<BulkImportedUser> created, List<BulkImportSkippedEntry> skipped)
    {
        using var workbook = new XLWorkbook();

        ExcelWorkbooks.AddTable(workbook.Worksheets.Add("Created users"), ["Email", "Username", "Password"],
            created.Select(c => new[] { c.Email, c.Username, c.Password }));

        if (skipped.Count > 0)
        {
            ExcelWorkbooks.AddTable(workbook.Worksheets.Add("Skipped"), ["Email", "Reason"],
                skipped.Select(s => new[] { s.Email, s.Reason }));
        }

        return ExcelWorkbooks.SaveToBase64(workbook);
    }
}
