using System.Diagnostics;
using System.Runtime.InteropServices;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed partial class ExcelResultBuilder
{
    private const int NumberColumn = 1;    // A
    private const int TitleColumn = 2;     // B
    private const int AuthorColumn = 3;    // C
    private const int TotalColumn = 4;     // D
    private const int FirstVoteColumn = 5; // E

    private const int XlUp = -4162;
    private const int XlToLeft = -4159;
    private const int XlPasteFormats = -4122;
    private const int XlCalculationAutomatic = -4105;
    private const int XlCenter = -4108;
    private const int XlLeft = -4131;
    private const int XlTop = -4160;


    public string BuildAndOpen(string templatePath, string outputFolder, Contest contest, IReadOnlyCollection<VoteEntry> votes)
    {
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            throw new FileNotFoundException("Файл-образец не найден.", templatePath);

        Directory.CreateDirectory(outputFolder);
        var safeName = BuildResultFileName(contest);
        var outputPath = Path.Combine(outputFolder, safeName);

        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); }
            catch (Exception ex)
            {
                throw new IOException($"Не могу перезаписать итоговый файл. Закрой его в Excel и попробуй снова: {outputPath}", ex);
            }
        }

        File.Copy(templatePath, outputPath, overwrite: true);

        object? excel = null;
        object? workbook = null;
        object? worksheet = null;
        var wasSaved = false;

        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType is null)
                throw new InvalidOperationException("Microsoft Excel не найден. Для генерации нужен установленный Excel.");

            excel = Activator.CreateInstance(excelType);
            if (excel is null)
                throw new InvalidOperationException("Не удалось запустить Microsoft Excel.");

            dynamic xl = excel;
            xl.Visible = false;
            xl.DisplayAlerts = false;

            workbook = xl.Workbooks.Open(Path.GetFullPath(outputPath));
            dynamic wb = workbook;
            worksheet = wb.Worksheets[1];

            FillWorksheet(worksheet, contest, votes);
            AddServiceSheets(workbook, contest, votes);
            TryActivateFirstSheet(workbook);

            try { xl.Calculation = XlCalculationAutomatic; } catch { }
            try { wb.RefreshAll(); } catch { }
            wb.Save();
            wb.Close(true);
            wasSaved = true;
            xl.Quit();
        }
        finally
        {
            ReleaseComObject(worksheet);
            ReleaseComObject(workbook);
            if (excel is not null)
            {
                try { ((dynamic)excel).Quit(); } catch { }
                ReleaseComObject(excel);
            }
        }

        if (wasSaved)
            OpenResultFile(outputPath);

        return outputPath;
    }

    private static void FillWorksheet(object worksheetObject, Contest contest, IReadOnlyCollection<VoteEntry> votes)
    {
        dynamic worksheet = worksheetObject;

        IReadOnlyList<ContestWork> works = contest.Works
            .Where(x => x.Number > 0)
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Title)
            .ToList();

        Dictionary<int, int> workRows = works.Count > 0
            ? WriteConfiguredWorks(worksheetObject, works)
            : DetectWorkRows(worksheetObject);

        if (workRows.Count == 0)
            throw new InvalidOperationException("В настройках конкурса нет работ, а в образце не найдены строки работ в колонке A.");

        int lastRow = workRows.Values.Max();
        List<VoterInfo> orderedVoters = BuildOrderedVoters(contest, votes);
        Dictionary<string, int> voterColumns = WriteConfiguredVoters(worksheetObject, orderedVoters, lastRow);
        int lastCol = orderedVoters.Count == 0 ? TotalColumn : FirstVoteColumn + orderedVoters.Count - 1;

        ClearVoteArea(worksheetObject, workRows.Values, lastCol);

        foreach (VoteEntry vote in votes.OrderBy(x => x.UpdatedAt))
        {
            int row;
            if (!workRows.TryGetValue(vote.WorkNo, out row))
                continue;

            int col;
            if (!voterColumns.TryGetValue(vote.VoterKey, out col))
                continue;

            string author = Convert.ToString(worksheet.Cells[row, AuthorColumn].Value2) ?? string.Empty;
            bool isSelfVote = contest.TreatSelfVoteAsZero && NameNormalizer.Same(vote.VoterName, author);

            object cell = worksheet.Cells[row, col];
            dynamic excelCell = cell;
            excelCell.Value2 = isSelfVote ? 0 : Convert.ToDouble(vote.Score);

            if (!string.IsNullOrWhiteSpace(vote.Comment))
                TrySetComment(cell, vote.Comment);

            if (isSelfVote)
                TryMarkSelfVote(cell);
        }

        List<int> formulaRows = workRows.Values.OrderBy(x => x).ToList();
        ApplyTotalFormulas(worksheetObject, formulaRows, lastCol);
        TryAutoFit(worksheetObject, Math.Max(lastCol, TotalColumn), lastRow);
    }

    private static Dictionary<int, int> WriteConfiguredWorks(object worksheetObject, IReadOnlyList<ContestWork> works)
    {
        dynamic worksheet = worksheetObject;
        int oldLastRow = Math.Max(2, GetLastRow(worksheetObject));
        int oldLastCol = Math.Max(TotalColumn, GetLastColumn(worksheetObject));
        int targetLastRow = Math.Max(oldLastRow, 1 + works.Count);

        TrySetHeader(worksheetObject);
        ClearRangeContents(worksheetObject, 2, NumberColumn, targetLastRow, oldLastCol);

        var result = new Dictionary<int, int>();
        for (int i = 0; i < works.Count; i++)
        {
            int row = 2 + i;
            ContestWork work = works[i];

            if (row > 2)
                CopyRowFormat(worksheetObject, 2, row);

            object numberCell = worksheet.Cells[row, NumberColumn];
            dynamic number = numberCell;
            try { number.NumberFormat = "@"; } catch { }
            number.Value2 = work.Number.ToString("00");

            worksheet.Cells[row, TitleColumn].Value2 = work.Title;
            worksheet.Cells[row, AuthorColumn].Value2 = work.Author;
            result[work.Number] = row;
        }

        return result;
    }

    private static void TrySetHeader(object worksheetObject)
    {
        dynamic worksheet = worksheetObject;
        try { worksheet.Cells[1, NumberColumn].Value2 = "№"; } catch { }
        try { worksheet.Cells[1, TitleColumn].Value2 = "Работа"; } catch { }
        try { worksheet.Cells[1, AuthorColumn].Value2 = "Автор"; } catch { }
        try { worksheet.Cells[1, TotalColumn].Value2 = "Итог"; } catch { }
    }

    private static List<VoterInfo> BuildOrderedVoters(Contest contest, IReadOnlyCollection<VoteEntry> votes)
    {
        var result = new List<VoterInfo>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (VoterSetting voter in contest.Voters)
        {
            string key = NameNormalizer.Normalize(voter.Name);
            if (!string.IsNullOrWhiteSpace(key) && used.Add(key))
                result.Add(new VoterInfo(voter.Name.Trim(), key));
        }

        var voteVoters = votes
            .OrderBy(x => x.UpdatedAt)
            .GroupBy(x => x.VoterKey)
            .Select(g => g.Last())
            .ToList();

        foreach (VoteEntry vote in voteVoters)
        {
            string key = NameNormalizer.Normalize(vote.VoterName);
            if (!string.IsNullOrWhiteSpace(key) && used.Add(key))
                result.Add(new VoterInfo(vote.VoterName.Trim(), key));
        }

        return result;
    }

    private static Dictionary<string, int> WriteConfiguredVoters(object worksheetObject, IReadOnlyList<VoterInfo> voters, int lastRow)
    {
        dynamic worksheet = worksheetObject;
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int oldLastCol = Math.Max(FirstVoteColumn - 1, GetLastColumn(worksheetObject));
        int targetLastCol = Math.Max(oldLastCol, voters.Count == 0 ? FirstVoteColumn - 1 : FirstVoteColumn + voters.Count - 1);

        if (targetLastCol >= FirstVoteColumn)
            ClearRangeContents(worksheetObject, 1, FirstVoteColumn, lastRow, targetLastCol);

        for (int i = 0; i < voters.Count; i++)
        {
            int col = FirstVoteColumn + i;
            VoterInfo voter = voters[i];

            if (col > FirstVoteColumn)
                CopyColumnFormat(worksheetObject, FirstVoteColumn, col);

            object headerCell = worksheet.Cells[1, col];
            dynamic header = headerCell;
            header.Value2 = voter.VoterName;
            try { header.Orientation = 90; } catch { }
            try { header.HorizontalAlignment = XlCenter; } catch { }
            try { worksheet.Columns[col].ColumnWidth = 4.5; } catch { }

            result[voter.VoterKey] = col;
        }

        return result;
    }

    private static void ClearVoteArea(object worksheetObject, IEnumerable<int> rows, int lastCol)
    {
        if (lastCol < FirstVoteColumn)
            return;

        dynamic worksheet = worksheetObject;
        foreach (int row in rows)
        {
            for (int col = FirstVoteColumn; col <= lastCol; col++)
            {
                object cell = worksheet.Cells[row, col];
                dynamic excelCell = cell;
                try { excelCell.ClearContents(); } catch { excelCell.Value2 = null; }
                TryClearComment(cell);
                TryClearFill(cell);
            }
        }
    }

    private static Dictionary<int, int> DetectWorkRows(object worksheetObject)
    {
        dynamic worksheet = worksheetObject;
        var result = new Dictionary<int, int>();
        int lastRow = Math.Max(2, GetLastRow(worksheetObject));

        for (int row = 2; row <= lastRow; row++)
        {
            object? value = worksheet.Cells[row, NumberColumn].Value2;
            int workNo;
            if (TryParseWorkNo(value, out workNo))
                result[workNo] = row;
        }

        return result;
    }

    private static int GetLastRow(object worksheetObject)
    {
        dynamic worksheet = worksheetObject;
        try { return Convert.ToInt32(worksheet.Cells[worksheet.Rows.Count, NumberColumn].End(XlUp).Row); }
        catch { return Convert.ToInt32(worksheet.UsedRange.Rows.Count); }
    }

    private static int GetLastColumn(object worksheetObject)
    {
        dynamic worksheet = worksheetObject;
        try { return Convert.ToInt32(worksheet.Cells[1, worksheet.Columns.Count].End(XlToLeft).Column); }
        catch { return Convert.ToInt32(worksheet.UsedRange.Columns.Count); }
    }

    private static bool TryParseWorkNo(object? value, out int workNo)
    {
        workNo = 0;
        if (value is null)
            return false;

        if (value is double d)
        {
            workNo = Convert.ToInt32(d);
            return workNo > 0;
        }

        string text = Convert.ToString(value)?.Trim() ?? string.Empty;
        text = text.TrimStart('№', '#').Trim();
        return int.TryParse(text, out workNo) && workNo > 0;
    }

    private static void CopyRowFormat(object worksheetObject, int sourceRow, int targetRow)
    {
        dynamic worksheet = worksheetObject;
        try
        {
            worksheet.Rows[sourceRow].Copy();
            worksheet.Rows[targetRow].PasteSpecial(XlPasteFormats);
        }
        catch
        {
            // Если шаблон совсем пустой - просто оставляем стандартное оформление.
        }
    }

    private static void CopyColumnFormat(object worksheetObject, int sourceCol, int targetCol)
    {
        dynamic worksheet = worksheetObject;
        try
        {
            worksheet.Columns[sourceCol].Copy();
            worksheet.Columns[targetCol].PasteSpecial(XlPasteFormats);
        }
        catch
        {
            try { worksheet.Columns[targetCol].ColumnWidth = 4.5; } catch { }
        }
    }

    private static void ClearRangeContents(object worksheetObject, int row1, int col1, int row2, int col2)
    {
        if (row2 < row1 || col2 < col1)
            return;

        dynamic worksheet = worksheetObject;
        try
        {
            object first = worksheet.Cells[row1, col1];
            object last = worksheet.Cells[row2, col2];
            object range = worksheet.Range[first, last];
            dynamic excelRange = range;
            excelRange.ClearContents();
        }
        catch
        {
            for (int row = row1; row <= row2; row++)
            {
                for (int col = col1; col <= col2; col++)
                {
                    try { worksheet.Cells[row, col].ClearContents(); } catch { }
                }
            }
        }
    }

    private static void ApplyTotalFormulas(object worksheetObject, IReadOnlyCollection<int> rows, int lastCol)
    {
        dynamic worksheet = worksheetObject;
        if (lastCol < FirstVoteColumn)
        {
            foreach (int row in rows)
                worksheet.Cells[row, TotalColumn].Formula = "=0";
            return;
        }

        string lastLetter = ColumnLetter(lastCol);
        foreach (int row in rows)
        {
            worksheet.Cells[row, TotalColumn].Formula =
                $"=IFERROR(SUMIFS($E{row}:${lastLetter}{row},$E$1:${lastLetter}$1,\"<>\"&$C{row}),0)";
        }
    }

    private static string ColumnLetter(int column)
    {
        int dividend = column;
        string columnName = string.Empty;
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }
        return columnName;
    }

    private static void TryClearComment(object cellObject)
    {
        dynamic cell = cellObject;
        try { cell.ClearComments(); } catch { }
        try { cell.Comment.Delete(); } catch { }
    }

    private static void TrySetComment(object cellObject, string text)
    {
        dynamic cell = cellObject;
        try
        {
            TryClearComment(cellObject);
            cell.AddComment(text);
            return;
        }
        catch { }

        try { cell.NoteText(text); } catch { }
    }

    private static void TryMarkSelfVote(object cellObject)
    {
        dynamic cell = cellObject;
        try { cell.Interior.Color = 10092543; } catch { } // светло-жёлтый
    }

    private static void TryClearFill(object cellObject)
    {
        dynamic cell = cellObject;
        try { cell.Interior.Pattern = -4142; } catch { } // xlPatternNone
    }

    private static void TryAutoFit(object worksheetObject, int lastCol, int lastRow)
    {
        dynamic worksheet = worksheetObject;
        try
        {
            object first = worksheet.Cells[1, 1];
            object last = worksheet.Cells[lastRow, lastCol];
            worksheet.Range[first, last].Columns.AutoFit();
        }
        catch { }

        // После AutoFit имена голосующих могут раздуть колонки. Возвращаем компактный вид.
        try
        {
            for (int col = FirstVoteColumn; col <= lastCol; col++)
                worksheet.Columns[col].ColumnWidth = 4.5;
        }
        catch { }
    }


    private static void AddServiceSheets(object workbookObject, Contest contest, IReadOnlyCollection<VoteEntry> votes)
    {
        var resultsService = new ContestResultsService();
        var auditService = new VoteAuditService();
        ContestResultsReport resultsReport = resultsService.BuildReport(contest, votes);
        ContestAuditReport auditReport = auditService.BuildReport(contest, votes);

        WriteRatingSheet(workbookObject, contest, resultsReport);
        WriteProtocolSheet(workbookObject, contest, resultsReport, resultsService.BuildFinalText(contest, resultsReport));
        WriteControlSheet(workbookObject, auditReport);
    }

    private static void WriteRatingSheet(object workbookObject, Contest contest, ContestResultsReport report)
    {
        object sheetObject = RecreateSheet(workbookObject, "Рейтинг");
        dynamic sheet = sheetObject;

        sheet.Cells[1, 1].Value2 = "Рейтинг конкурса";
        sheet.Cells[2, 1].Value2 = string.IsNullOrWhiteSpace(contest.Number) ? contest.Name : $"{contest.Name} ({contest.Number})";

        string[] headers = { "Место", "№", "Работа", "Автор", "Тема", "Итог", "Голосов", "Средн.", "Max", "Самоголос" };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[4, i + 1].Value2 = headers[i];

        int row = 5;
        foreach (ContestRatingRow item in report.Rows.OrderBy(x => x.PlaceNo).ThenBy(x => x.WorkNo))
        {
            sheet.Cells[row, 1].Value2 = item.PlaceText;
            sheet.Cells[row, 2].Value2 = item.WorkNoText;
            sheet.Cells[row, 3].Value2 = item.Title;
            sheet.Cells[row, 4].Value2 = item.Author;
            sheet.Cells[row, 5].Value2 = item.Topic;
            sheet.Cells[row, 6].Value2 = Convert.ToDouble(item.Rate);
            sheet.Cells[row, 7].Value2 = item.AcceptedVotes;
            sheet.Cells[row, 8].Value2 = item.AverageText;
            sheet.Cells[row, 9].Value2 = item.MaxVotes;
            sheet.Cells[row, 10].Value2 = item.SelfVotes;
            row++;
        }

        int lastRow = Math.Max(5, row - 1);
        FormatTableSheet(sheetObject, 1, 10, lastRow);
        TryColorResultRows(sheetObject, 5, lastRow);
        ReleaseComObject(sheetObject);
    }

    private static void WriteProtocolSheet(object workbookObject, Contest contest, ContestResultsReport report, string finalText)
    {
        object sheetObject = RecreateSheet(workbookObject, "Протокол");
        dynamic sheet = sheetObject;

        sheet.Cells[1, 1].Value2 = "Итоговый протокол";
        sheet.Cells[2, 1].Value2 = string.IsNullOrWhiteSpace(contest.Number) ? contest.Name : $"{contest.Name} ({contest.Number})";
        sheet.Cells[4, 1].Value2 = "Статистика";
        sheet.Cells[5, 1].Value2 = "Проголосовало судей";
        sheet.Cells[5, 2].Value2 = report.VoterCount;
        sheet.Cells[6, 1].Value2 = "Работ";
        sheet.Cells[6, 2].Value2 = report.WorkCount;
        sheet.Cells[7, 1].Value2 = "Принято голосов";
        sheet.Cells[7, 2].Value2 = report.AcceptedVoteCount;
        sheet.Cells[8, 1].Value2 = "Самоголосов в 0";
        sheet.Cells[8, 2].Value2 = report.SelfVoteCount;

        sheet.Cells[10, 1].Value2 = "Готовый текст для публикации";
        string[] lines = finalText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int row = 11;
        foreach (string line in lines)
        {
            sheet.Cells[row, 1].Value2 = line;
            row++;
        }

        try { sheet.Columns[1].ColumnWidth = 92; } catch { }
        try { sheet.Columns[2].ColumnWidth = 18; } catch { }
        try { sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, 2]].Font.Bold = true; } catch { }
        try { sheet.Range[sheet.Cells[4, 1], sheet.Cells[4, 2]].Font.Bold = true; } catch { }
        try { sheet.Range[sheet.Cells[10, 1], sheet.Cells[10, 1]].Font.Bold = true; } catch { }
        try { sheet.Range[sheet.Cells[11, 1], sheet.Cells[Math.Max(11, row - 1), 1]].WrapText = true; } catch { }
        ReleaseComObject(sheetObject);
    }

    private static void WriteControlSheet(object workbookObject, ContestAuditReport report)
    {
        object sheetObject = RecreateSheet(workbookObject, "Контроль");
        dynamic sheet = sheetObject;

        sheet.Cells[1, 1].Value2 = "Контроль голосования";
        sheet.Cells[2, 1].Value2 = "Работ";
        sheet.Cells[2, 2].Value2 = report.WorkCount;
        sheet.Cells[3, 1].Value2 = "Принято голосов";
        sheet.Cells[3, 2].Value2 = report.AcceptedVoteCount;
        sheet.Cells[4, 1].Value2 = "Должников";
        sheet.Cells[4, 2].Value2 = report.Debtors;
        sheet.Cells[5, 1].Value2 = "Неизвестных голосующих";
        sheet.Cells[5, 2].Value2 = report.UnknownVoters;

        string[] headers = { "Голосующий", "Статус", "Обяз.", "Голосов", "Пропущено", "Лишние №", "Самоголос", "Примечание" };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[7, i + 1].Value2 = headers[i];

        int row = 8;
        foreach (VoterStatusRow item in report.Rows)
        {
            sheet.Cells[row, 1].Value2 = item.VoterName;
            sheet.Cells[row, 2].Value2 = item.Status;
            sheet.Cells[row, 3].Value2 = item.RequiredToVote ? "да" : "";
            sheet.Cells[row, 4].Value2 = item.AcceptedVotes;
            sheet.Cells[row, 5].Value2 = item.MissingWorks;
            sheet.Cells[row, 6].Value2 = item.UnknownWorks;
            sheet.Cells[row, 7].Value2 = item.SelfVotes;
            sheet.Cells[row, 8].Value2 = item.Note;
            row++;
        }

        int lastRow = Math.Max(8, row - 1);
        FormatTableSheet(sheetObject, 1, 8, lastRow, headerRow: 7);
        TryColorControlRows(sheetObject, report.Rows, 8);
        ReleaseComObject(sheetObject);
    }

    private static object RecreateSheet(object workbookObject, string sheetName)
    {
        dynamic workbook = workbookObject;
        DeleteSheetIfExists(workbookObject, sheetName);

        object after = workbook.Worksheets[workbook.Worksheets.Count];
        object sheetObject = workbook.Worksheets.Add(Type.Missing, after, Type.Missing, Type.Missing);
        dynamic sheet = sheetObject;
        try { sheet.Name = sheetName; } catch { }
        return sheetObject;
    }

    private static void DeleteSheetIfExists(object workbookObject, string sheetName)
    {
        dynamic workbook = workbookObject;
        object? sheetObject = null;
        try
        {
            sheetObject = workbook.Worksheets[sheetName];
            dynamic sheet = sheetObject;
            sheet.Delete();
        }
        catch
        {
        }
        finally
        {
            ReleaseComObject(sheetObject);
        }
    }

    private static void FormatTableSheet(object sheetObject, int firstColumn, int lastColumn, int lastRow, int headerRow = 4)
    {
        dynamic sheet = sheetObject;
        try
        {
            object firstHeader = sheet.Cells[headerRow, firstColumn];
            object lastHeader = sheet.Cells[headerRow, lastColumn];
            object headerRange = sheet.Range[firstHeader, lastHeader];
            dynamic header = headerRange;
            header.Font.Bold = true;
            header.Interior.Color = 14277081; // мягкий серо-голубой
            header.HorizontalAlignment = XlCenter;
            ReleaseComObject(headerRange);
        }
        catch { }

        try
        {
            object first = sheet.Cells[headerRow, firstColumn];
            object last = sheet.Cells[lastRow, lastColumn];
            object tableRange = sheet.Range[first, last];
            dynamic range = tableRange;
            range.Borders.LineStyle = 1;
            range.VerticalAlignment = XlTop;
            ReleaseComObject(tableRange);
        }
        catch { }

        try { sheet.Columns.AutoFit(); } catch { }
        try { sheet.Rows[1].Font.Bold = true; } catch { }
        try { sheet.Rows[1].Font.Size = 14; } catch { }
        try { sheet.Application.ActiveWindow.SplitRow = headerRow; sheet.Application.ActiveWindow.FreezePanes = true; } catch { }
    }

    private static void TryColorResultRows(object sheetObject, int firstRow, int lastRow)
    {
        dynamic sheet = sheetObject;
        for (int row = firstRow; row <= lastRow; row++)
        {
            string place = Convert.ToString(sheet.Cells[row, 1].Value2) ?? string.Empty;
            int color = place.StartsWith("1", StringComparison.Ordinal) ? 13434879
                : place.StartsWith("2", StringComparison.Ordinal) ? 15658734
                : place.StartsWith("3", StringComparison.Ordinal) ? 13431551
                : 16777215;
            try { sheet.Range[sheet.Cells[row, 1], sheet.Cells[row, 10]].Interior.Color = color; } catch { }
        }
    }

    private static void TryColorControlRows(object sheetObject, IEnumerable<VoterStatusRow> rows, int firstRow)
    {
        dynamic sheet = sheetObject;
        int rowIndex = firstRow;
        foreach (VoterStatusRow item in rows)
        {
            int color = item.IsUnknownVoter ? 11854079
                : item.IsDebtor ? 14540287
                : item.AcceptedVotes > 0 ? 14876637
                : 16777215;
            try { sheet.Range[sheet.Cells[rowIndex, 1], sheet.Cells[rowIndex, 8]].Interior.Color = color; } catch { }
            rowIndex++;
        }
    }

    private static void TryActivateFirstSheet(object workbookObject)
    {
        dynamic workbook = workbookObject;
        try { workbook.Worksheets[1].Activate(); } catch { }
    }

    private static string BuildResultFileName(Contest contest)
    {
        string title = CleanContestTitle(contest.Name);
        string number = CleanContestNumber(contest.Number);

        if (string.IsNullOrWhiteSpace(title))
            title = "Contest";

        if (string.IsNullOrWhiteSpace(number))
            return title + ".xlsx";

        return $"{title}({number}).xlsx";
    }

    private static string CleanContestNumber(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string CleanContestTitle(string value)
    {
        string text = TransliterateToLatin(value ?? string.Empty);
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        var chars = new List<char>(text.Length);

        foreach (char ch in text)
        {
            if (invalidFileNameChars.Contains(ch) || char.IsControl(ch))
                continue;

            if (IsAllowedResultFileNameChar(ch))
                chars.Add(ch);
        }

        string cleaned = new string(chars.ToArray());
        cleaned = RegexSpaces().Replace(cleaned, " ").Trim();
        cleaned = RegexContestNumberTail().Replace(cleaned, string.Empty).Trim();
        cleaned = RegexSpacesBeforePunctuation().Replace(cleaned, "$1");
        cleaned = RegexRepeatedPunctuation().Replace(cleaned, "$1");
        cleaned = cleaned.Trim(' ', '-', '_', '.', ',');

        return cleaned;
    }

    private static string TransliterateToLatin(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length * 2);
        foreach (char ch in value)
        {
            string mapped = ch switch
            {
                'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'е' => "e", 'ё' => "yo",
                'ж' => "zh", 'з' => "z", 'и' => "i", 'й' => "y", 'к' => "k", 'л' => "l", 'м' => "m",
                'н' => "n", 'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s", 'т' => "t", 'у' => "u",
                'ф' => "f", 'х' => "kh", 'ц' => "ts", 'ч' => "ch", 'ш' => "sh", 'щ' => "shch",
                'ы' => "y", 'э' => "e", 'ю' => "yu", 'я' => "ya", 'ь' => string.Empty, 'ъ' => string.Empty,

                'А' => "A", 'Б' => "B", 'В' => "V", 'Г' => "G", 'Д' => "D", 'Е' => "E", 'Ё' => "Yo",
                'Ж' => "Zh", 'З' => "Z", 'И' => "I", 'Й' => "Y", 'К' => "K", 'Л' => "L", 'М' => "M",
                'Н' => "N", 'О' => "O", 'П' => "P", 'Р' => "R", 'С' => "S", 'Т' => "T", 'У' => "U",
                'Ф' => "F", 'Х' => "Kh", 'Ц' => "Ts", 'Ч' => "Ch", 'Ш' => "Sh", 'Щ' => "Shch",
                'Ы' => "Y", 'Э' => "E", 'Ю' => "Yu", 'Я' => "Ya", 'Ь' => string.Empty, 'Ъ' => string.Empty,
                _ => ch.ToString()
            };

            sb.Append(mapped);
        }

        return sb.ToString();
    }

    private static bool IsAllowedResultFileNameChar(char ch)
    {
        if (ch >= '0' && ch <= '9')
            return true;

        if (ch >= 'A' && ch <= 'Z')
            return true;

        if (ch >= 'a' && ch <= 'z')
            return true;

        if (char.IsWhiteSpace(ch))
            return true;

        return ch is ',' or '.' or '!' or '?' or '(' or ')' or '-' or '_';
    }

    private static void OpenResultFile(string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string? folder = Path.GetDirectoryName(fullPath);
        System.Threading.Thread.Sleep(250);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = folder ?? string.Empty,
                UseShellExecute = true
            });
            return;
        }
        catch
        {
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "excel.exe",
                Arguments = QuoteArgument(fullPath),
                UseShellExecute = true
            });
            return;
        }
        catch
        {
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select," + QuoteArgument(fullPath),
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    [System.Text.RegularExpressions.GeneratedRegex("\\s+")]
    private static partial System.Text.RegularExpressions.Regex RegexSpaces();

    [System.Text.RegularExpressions.GeneratedRegex("\\s+([,.!?])")]
    private static partial System.Text.RegularExpressions.Regex RegexSpacesBeforePunctuation();

    [System.Text.RegularExpressions.GeneratedRegex("([,.!?])\\1+")]
    private static partial System.Text.RegularExpressions.Regex RegexRepeatedPunctuation();

    [System.Text.RegularExpressions.GeneratedRegex("\\s*\\(\\d+\\)\\s*$")]
    private static partial System.Text.RegularExpressions.Regex RegexContestNumberTail();

    private static void ReleaseComObject(object? value)
    {
        if (value is null)
            return;

        try
        {
            if (Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
        catch
        {
        }
    }

    private sealed class VoterInfo
    {
        public VoterInfo(string voterName, string voterKey)
        {
            VoterName = voterName;
            VoterKey = voterKey;
        }

        public string VoterName { get; }
        public string VoterKey { get; }
    }
}
