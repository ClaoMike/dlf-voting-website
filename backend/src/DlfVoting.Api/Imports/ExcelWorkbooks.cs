using System.Globalization;
using ClosedXML.Excel;

namespace DlfVoting.Api.Imports;

/// <summary>Reading uploaded .xlsx files and producing .xlsx results for download.</summary>
public static class ExcelWorkbooks
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const string InvalidFileMessage = "Please upload an Excel file (.xlsx).";

    /// <summary>Opens the upload, or returns null when it is missing or not a readable .xlsx.</summary>
    public static async Task<XLWorkbook?> OpenAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // ClosedXML keeps reading from the source stream until the workbook is saved, so buffer it.
        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;

        try
        {
            return new XLWorkbook(buffer);
        }
        catch (Exception)
        {
            // Not a readable .xlsx (e.g. an old .xls or a renamed CSV).
            return null;
        }
    }

    /// <summary>
    /// The cell as the admin sees it in Excel: text as-is, numbers/dates via their display format
    /// (so an employee code shown as "00123" keeps its leading zeros).
    /// </summary>
    public static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank || value.IsError)
        {
            return string.Empty;
        }

        return (value.IsText ? value.GetText() : cell.GetFormattedString(CultureInfo.InvariantCulture)).Trim();
    }

    /// <summary>Writes a bold header row and the rows below it, all as text.</summary>
    public static void AddTable(IXLWorksheet sheet, string[] headers, IEnumerable<string[]> rows)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).SetValue(headers[c]).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                sheet.Cell(r, c + 1).SetValue(row[c]);
            }
            r++;
        }

        sheet.Columns().AdjustToContents();
    }

    public static string SaveToBase64(XLWorkbook workbook)
    {
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return Convert.ToBase64String(output.ToArray());
    }
}
