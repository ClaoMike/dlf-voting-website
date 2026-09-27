using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace DlfVoting.Api.Imports;

public enum EmployeeColumn { EmployeeCode, FirstName, LastName, CompanyCode, EmploymentDate, Electability }

/// <summary>Where the employee data sits in an HR workbook, and how its cells are read.</summary>
public record EmployeeSheet(IXLWorksheet Sheet, int HeaderRow, int LastColumn, Dictionary<EmployeeColumn, int> Columns)
{
    // The employee file comes from HR with Danish column names.
    public static readonly Dictionary<string, EmployeeColumn> ColumnHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kode"] = EmployeeColumn.EmployeeCode,
        ["Fornavn"] = EmployeeColumn.FirstName,
        ["Efternavn"] = EmployeeColumn.LastName,
        ["Virksomhedskode"] = EmployeeColumn.CompanyCode,
        ["Ansættelsesdato"] = EmployeeColumn.EmploymentDate,
        ["Valgbarhed"] = EmployeeColumn.Electability,
    };

    // Danish Excel writes dates as dd-MM-yyyy; the others cover common re-saves and ISO dates.
    private static readonly string[] EmploymentDateFormats =
    [
        "d-M-yyyy", "d.M.yyyy", "d/M/yyyy", "yyyy-M-d",
        "d-M-yyyy H:mm", "d.M.yyyy H:mm", "d/M/yyyy H:mm", "yyyy-M-d H:mm",
        "d-M-yyyy H:mm:ss", "d.M.yyyy H:mm:ss", "d/M/yyyy H:mm:ss", "yyyy-M-d H:mm:ss",
    ];

    /// <summary>
    /// Finds the first row (in any sheet, within the first 20 rows) that contains at least one of the Danish column names.
    /// </summary>
    public static EmployeeSheet? Find(XLWorkbook workbook)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
            {
                continue;
            }

            var lastColumn = used.LastColumn().ColumnNumber();
            var firstRow = used.FirstRow().RowNumber();
            var lastCandidateRow = Math.Min(used.LastRow().RowNumber(), firstRow + 19);

            for (var r = firstRow; r <= lastCandidateRow; r++)
            {
                var columns = new Dictionary<EmployeeColumn, int>();
                for (var c = 1; c <= lastColumn; c++)
                {
                    var name = ExcelWorkbooks.CellText(sheet.Cell(r, c)).Normalize(NormalizationForm.FormC);
                    if (ColumnHeaders.TryGetValue(name, out var column))
                    {
                        columns.TryAdd(column, c);
                    }
                }

                if (columns.Count > 0)
                {
                    return new EmployeeSheet(sheet, r, lastColumn, columns);
                }
            }
        }

        return null;
    }

    /// <summary>The trimmed text of a column in a row, or null when the column is missing or the cell is empty.</summary>
    public string? Text(int row, EmployeeColumn column) =>
        Columns.TryGetValue(column, out var c) && ExcelWorkbooks.CellText(Sheet.Cell(row, c)) is { Length: > 0 } text ? text : null;

    public bool IsBlankRow(int row) => Sheet.Row(row).Cells(1, LastColumn).All(c => ExcelWorkbooks.CellText(c).Length == 0);

    /// <summary>Reads a real Excel date, a date serial number, or a date typed as text.</summary>
    public static DateOnly? ReadDate(IXLCell cell)
    {
        var value = cell.Value;

        if (value.IsDateTime)
        {
            return DateOnly.FromDateTime(value.GetDateTime());
        }

        // A real Excel date whose date formatting was lost shows up as its serial number (e.g. 43525).
        if (value.IsNumber && value.GetNumber() is >= 1 and < 2958466)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(value.GetNumber()));
        }

        return value.IsText
               && DateTime.TryParseExact(value.GetText().Trim(), EmploymentDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;
    }
}
