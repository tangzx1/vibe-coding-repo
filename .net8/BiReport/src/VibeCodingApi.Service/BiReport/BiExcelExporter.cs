using ClosedXML.Excel;

namespace VibeCodingApi.Service.BiReport;

/// <summary>
/// 把查询结果写成 xlsx 工作簿。
/// <para>
/// 用 ClosedXML（MIT）而不是 EPPlus：EPPlus 5 起是商业许可，这个项目没理由为此付费。
/// </para>
/// <para>
/// 放在业务层而不是表现层：「把结果组织成工作簿」是纯数据转换，不需要 <c>HttpContext</c>；
/// 文件名、Content-Type 这些 HTTP 细节留给控制器。
/// </para>
/// </summary>
public class BiExcelExporter
{
    /// <summary>数据 sheet 名。</summary>
    private const string DataSheetName = "数据";

    /// <summary>说明 sheet 名。</summary>
    private const string NoteSheetName = "说明";

    /// <summary>Excel 单元格的文本上限，超出会被截断（ClosedXML 直接写会抛异常）。</summary>
    private const int MaxCellTextLength = 32_767;

    /// <summary>估算列宽时最多看前多少行数据。</summary>
    private const int WidthSampleRows = 100;

    private const int MinColumnWidth = 8;
    private const int MaxColumnWidth = 50;

    /// <summary>
    /// 生成工作簿。
    /// </summary>
    /// <param name="columns">列名（就是用户在查询里定义的别名），同时作为表头与每行的取值键。</param>
    /// <param name="rows">数据行。</param>
    /// <param name="note">附加说明（如「结果已截断」）；为空时不生成说明 sheet。</param>
    public byte[] Build(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string? note = null)
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.Worksheets.Add(DataSheetName);
        WriteHeader(sheet, columns, rows.Count);
        WriteRows(sheet, columns, rows);
        ApplyColumnWidths(sheet, columns, rows);

        if (!string.IsNullOrWhiteSpace(note))
        {
            workbook.Worksheets.Add(NoteSheetName).Cell(1, 1).Value = note;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> columns, int dataRowCount)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = columns[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }

        // 冻结表头：数据行多的时候，往下翻还能看到列名
        sheet.SheetView.FreezeRows(1);

        if (columns.Count > 0)
        {
            // 自动筛选让用户拿到文件后能自己再筛一遍，不必回系统重查。
            // 范围要含数据行——只框住表头的话下拉箭头点开是空的
            sheet.Range(1, 1, Math.Max(1, dataRowCount + 1), columns.Count).SetAutoFilter();
        }
    }

    private static void WriteRows(
        IXLWorksheet sheet,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                row.TryGetValue(columns[columnIndex], out var value);
                WriteCell(sheet.Cell(rowIndex + 2, columnIndex + 1), value);
            }
        }
    }

    /// <summary>
    /// 决定列宽。
    /// <para>
    /// <b>故意不用 <c>AdjustToContents()</c></b>：它会逐单元格做字体度量（依赖 SixLabors.Fonts），
    /// 两万行的导出上耗时可能超过查询本身。按字符数估算已经把中文宽度算进去了，够用。
    /// </para>
    /// </summary>
    private static void ApplyColumnWidths(
        IXLWorksheet sheet,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var sampleCount = Math.Min(rows.Count, WidthSampleRows);

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var width = DisplayWidth(columns[columnIndex]);

            for (var rowIndex = 0; rowIndex < sampleCount; rowIndex++)
            {
                rows[rowIndex].TryGetValue(columns[columnIndex], out var value);
                if (value is not null)
                {
                    width = Math.Max(width, DisplayWidth(ToDisplayText(value)));
                }
            }

            sheet.Column(columnIndex + 1).Width = Math.Clamp(width + 2, MinColumnWidth, MaxColumnWidth);
        }
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            // 空单元格：不要写成字符串 "null"，那样用户在 Excel 里筛空值会筛不出来
            case null:
                break;

            case string text:
                // 超长文本（如备注类字段）会直接把 Excel 的单元格限制撑爆，宁可截断并标明
                cell.Value = text.Length > MaxCellTextLength
                    ? text[..MaxCellTextLength] + "…（内容过长已截断）"
                    : text;
                break;

            case bool flag:
                cell.Value = flag;
                break;

            case DateTime dateTime:
                WriteDateTime(cell, dateTime);
                break;

            case TimeSpan timeSpan:
                cell.Value = timeSpan;
                break;

            // 二进制列（blob）降级成 base64：直接写字节数组会变成乱码
            case byte[] bytes:
                cell.Value = Convert.ToBase64String(bytes);
                break;

            case sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal:
                cell.Value = XLCellValue.FromObject(value);
                break;

            // 兜底：类型没预料到就写成文本，宁可难看也不要因为一个罕见类型让整个导出失败
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// 写日期时间，并显式指定格式。
    /// <para>
    /// <b>必须显式设格式</b>：Excel 里日期本质是数字，不设格式的话单元格显示的是
    /// OADate 序列号（用户看到 <c>46000.5</c> 这种数字）。
    /// </para>
    /// <para>
    /// MySQL 的零值日期（<c>0000-00-00</c>）在 ADO.NET 驱动里会变成
    /// <see cref="DateTime.MinValue"/>，而 Excel 的日期从 1900-01-01 起算，
    /// 直接写会抛异常——这类值降级成文本。
    /// </para>
    /// </summary>
    private static void WriteDateTime(IXLCell cell, DateTime dateTime)
    {
        if (dateTime < ExcelEpoch)
        {
            cell.Value = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            return;
        }

        cell.Value = dateTime;
        // 时分秒全为 0 时只显示日期：日期列带上 "00:00:00" 纯属噪音
        cell.Style.DateFormat.Format = dateTime.TimeOfDay == TimeSpan.Zero
            ? "yyyy-mm-dd"
            : "yyyy-mm-dd hh:mm:ss";
    }

    /// <summary>Excel 能表示的最早日期。比它更早的值（如零值日期）只能当文本写。</summary>
    private static readonly DateTime ExcelEpoch = new(1900, 1, 1);

    private static string ToDisplayText(object value) => value switch
    {
        DateTime dateTime when dateTime < ExcelEpoch => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// 估算一串文本要占几个字符宽。中日韩字符在 Excel 的默认字体下占两格，
    /// 都按 1 算会让中文列名被截断成「矿卡自编」。
    /// </summary>
    private static int DisplayWidth(string text)
    {
        var width = 0;
        foreach (var ch in text)
        {
            width += IsWideCharacter(ch) ? 2 : 1;
        }

        return width;
    }

    private static bool IsWideCharacter(char ch) =>
        ch is >= 'ᄀ' and <= 'ᅟ'   // 谚文字母
            or >= '⺀' and <= '꓏'   // 中日韩部首、假名、注音、汉字
            or >= '가' and <= '힣'   // 谚文音节
            or >= '豈' and <= '﫿'   // 中日韩兼容汉字
            or >= '︰' and <= '﹯'   // 中日韩兼容形式
            or >= '＀' and <= '｠'   // 全角字符
            or >= '￠' and <= '￦';  // 全角符号
}
