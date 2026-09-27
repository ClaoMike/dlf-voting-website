using ClosedXML.Excel;
using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Imports;

/// <summary>
/// "Import employees": the HR workbook (Kode, Fornavn, Efternavn, Virksomhedskode, Ansættelsesdato, Valgbarhed).
/// Creates a user with a random username and password per row and returns the same workbook with
/// "Username" and "Password" columns appended.
/// </summary>
public class EmployeeImportService
{
    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public EmployeeImportService(DlfVotingDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<EmployeeImportResponse>> ImportAsync(XLWorkbook workbook)
    {
        var layout = EmployeeSheet.Find(workbook);
        if (layout is null)
        {
            return OperationResult.Invalid(
                $"Could not find a header row with any of the expected columns ({string.Join(", ", EmployeeSheet.ColumnHeaders.Keys)}).");
        }

        var sheet = layout.Sheet;
        var lastRow = sheet.LastRowUsed()!.RowNumber();
        var dataRows = Enumerable.Range(layout.HeaderRow + 1, Math.Max(lastRow - layout.HeaderRow, 0))
            .Where(r => !layout.IsBlankRow(r))
            .ToList();

        var usernames = await UsernameGenerator.GenerateUniqueAsync(_db, dataRows.Count);
        var warnings = new List<EmployeeImportWarning>();
        var newUsers = new List<(User User, int Row, string Password)>();

        foreach (var row in dataRows)
        {
            var user = ReadUser(layout, row, warnings);
            if (user is null) continue;

            user.Username = usernames[newUsers.Count];
            newUsers.Add((user, row, SecurePasswordGenerator.Generate()));
        }

        var usernameColumn = layout.LastColumn + 1;
        var passwordColumn = layout.LastColumn + 2;
        var headerStyle = sheet.Cell(layout.HeaderRow, layout.LastColumn).Style;
        sheet.Cell(layout.HeaderRow, usernameColumn).SetValue("Username").Style = headerStyle;
        sheet.Cell(layout.HeaderRow, passwordColumn).SetValue("Password").Style = headerStyle;

        var hashes = PasswordHashing.HashMany(newUsers.Select(n => n.Password));
        for (var i = 0; i < newUsers.Count; i++)
        {
            var (user, row, password) = newUsers[i];
            user.PasswordHash = hashes[i];
            _db.Users.Add(user);
            // SetValue with a string stores text, so Excel never reinterprets e.g. a password starting with "=".
            sheet.Cell(row, usernameColumn).SetValue(user.Username);
            sheet.Cell(row, passwordColumn).SetValue(password);
        }

        if (newUsers.Count > 0)
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                return OperationResult.Conflict(
                    "A generated username was taken by someone else at the same moment. Nothing was imported; please upload the file again.");
            }
        }

        sheet.Column(usernameColumn).AdjustToContents();
        sheet.Column(passwordColumn).AdjustToContents();

        return OperationResult<EmployeeImportResponse>.Success(new EmployeeImportResponse(
            newUsers.Count, dataRows.Count - newUsers.Count, warnings, ExcelWorkbooks.SaveToBase64(workbook)));
    }

    /// <summary>
    /// Builds the user for one row (without username/password), or returns null when the row can't be imported.
    /// Problems are reported through <paramref name="warnings"/>.
    /// </summary>
    private static User? ReadUser(EmployeeSheet layout, int row, List<EmployeeImportWarning> warnings)
    {
        var tooLong = layout.Columns
            .Where(pair => pair.Key != EmployeeColumn.EmploymentDate && layout.Text(row, pair.Key)?.Length > User.EmployeeFieldMaxLength)
            .Select(pair => ExcelWorkbooks.CellText(layout.Sheet.Cell(layout.HeaderRow, pair.Value)))
            .ToList();
        if (tooLong.Count > 0)
        {
            warnings.Add(new EmployeeImportWarning(row,
                $"Not imported: {string.Join(", ", tooLong)} is longer than {User.EmployeeFieldMaxLength} characters."));
            return null;
        }

        DateOnly? employmentDate = null;
        if (layout.Columns.TryGetValue(EmployeeColumn.EmploymentDate, out var dateColumn)
            && layout.Sheet.Cell(row, dateColumn) is { Value.IsBlank: false } dateCell)
        {
            employmentDate = EmployeeSheet.ReadDate(dateCell);
            if (employmentDate is null)
            {
                warnings.Add(new EmployeeImportWarning(row,
                    $"Imported, but the employment date \"{ExcelWorkbooks.CellText(dateCell)}\" was not recognised and was left empty."));
            }
        }

        return new User
        {
            Id = Guid.NewGuid(),
            EmployeeCode = layout.Text(row, EmployeeColumn.EmployeeCode),
            FirstName = layout.Text(row, EmployeeColumn.FirstName),
            LastName = layout.Text(row, EmployeeColumn.LastName),
            CompanyCode = layout.Text(row, EmployeeColumn.CompanyCode),
            EmploymentDate = employmentDate,
            Electability = layout.Text(row, EmployeeColumn.Electability),
            CreatedAt = DateTime.UtcNow
        };
    }
}
