using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using Rhymers.Data.Database;

namespace Rhymers;

public sealed class LayoutIniStore
{
    public string LegacyLayoutFile { get; } = Path.Combine(AppContext.BaseDirectory, "VoteCounter.layouts.ini");
    public string LayoutStorage => LocalDatabase.DatabasePath + " :: Setting.Ini";

    public void Save(Form form, IReadOnlyDictionary<string, string>? appValues = null)
    {
        var ini = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var formSection = GetSection(ini, "Form");
        var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        formSection["WindowState"] = form.WindowState == FormWindowState.Minimized
            ? FormWindowState.Normal.ToString()
            : form.WindowState.ToString();
        formSection["Left"] = bounds.Left.ToString(CultureInfo.InvariantCulture);
        formSection["Top"] = bounds.Top.ToString(CultureInfo.InvariantCulture);
        formSection["Width"] = bounds.Width.ToString(CultureInfo.InvariantCulture);
        formSection["Height"] = bounds.Height.ToString(CultureInfo.InvariantCulture);

        if (appValues is not null)
        {
            var app = GetSection(ini, "App");
            foreach (var pair in appValues)
                app[pair.Key] = pair.Value ?? string.Empty;
        }

        foreach (Control control in EnumerateControls(form))
        {
            switch (control)
            {
                case SplitContainer split:
                    SaveSplitContainer(ini, split);
                    break;

                case XtraTabControl tabs:
                    SaveXtraTabs(ini, tabs);
                    break;

                case DataGridView grid:
                    SaveGrid(ini, grid);
                    break;

                case GridControl grid:
                    SaveGridControl(ini, grid);
                    break;
            }
        }

        LocalDatabase.SaveLayoutIni(WriteIni(ini));
    }

    public IReadOnlyDictionary<string, string> Apply(Form form)
    {
        string iniText = LocalDatabase.LoadLayoutIni();
        if (string.IsNullOrWhiteSpace(iniText) && File.Exists(LegacyLayoutFile))
        {
            iniText = File.ReadAllText(LegacyLayoutFile, Encoding.UTF8);
            LocalDatabase.SaveLayoutIni(iniText);
        }

        var ini = ReadIniText(iniText);
        if (ini.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ApplyForm(form, ini);

        foreach (Control control in EnumerateControls(form))
        {
            switch (control)
            {
                case SplitContainer split:
                    ApplySplitContainer(ini, split);
                    break;

                case XtraTabControl tabs:
                    ApplyXtraTabs(ini, tabs);
                    break;

                case DataGridView grid:
                    ApplyGrid(ini, grid);
                    break;

                case GridControl grid:
                    ApplyGridControl(ini, grid);
                    break;
            }
        }

        return ini.TryGetValue("App", out var app)
            ? new Dictionary<string, string>(app, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Delete()
    {
        LocalDatabase.DeleteLayoutIni();
        if (File.Exists(LegacyLayoutFile))
            File.Delete(LegacyLayoutFile);
    }

    private static void SaveSplitContainer(IDictionary<string, SortedDictionary<string, string>> ini, SplitContainer split)
    {
        var section = GetSection(ini, "Split." + BuildControlKey(split));
        section["SplitterDistance"] = split.SplitterDistance.ToString(CultureInfo.InvariantCulture);
        section["Orientation"] = split.Orientation.ToString();
    }

    private static void ApplySplitContainer(IReadOnlyDictionary<string, SortedDictionary<string, string>> ini, SplitContainer split)
    {
        if (!ini.TryGetValue("Split." + BuildControlKey(split), out var section))
            return;

        if (!TryReadInt(section, "SplitterDistance", out int distance))
            return;

        try
        {
            int max = split.Orientation == Orientation.Vertical
                ? split.Width - split.Panel2MinSize - split.SplitterWidth
                : split.Height - split.Panel2MinSize - split.SplitterWidth;
            int min = split.Panel1MinSize;
            if (max > min)
                split.SplitterDistance = Math.Max(min, Math.Min(max, distance));
        }
        catch
        {
            // Layout must never break the application startup.
        }
    }

    private static void SaveXtraTabs(IDictionary<string, SortedDictionary<string, string>> ini, XtraTabControl tabs)
    {
        var section = GetSection(ini, "Tabs." + BuildControlKey(tabs));
        section["SelectedIndex"] = tabs.SelectedTabPageIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static void ApplyXtraTabs(IReadOnlyDictionary<string, SortedDictionary<string, string>> ini, XtraTabControl tabs)
    {
        if (!ini.TryGetValue("Tabs." + BuildControlKey(tabs), out var section))
            return;

        if (!TryReadInt(section, "SelectedIndex", out int index))
            return;

        if (index >= 0 && index < tabs.TabPages.Count)
            tabs.SelectedTabPageIndex = index;
    }

    private static void SaveGrid(IDictionary<string, SortedDictionary<string, string>> ini, DataGridView grid)
    {
        var gridSection = GetSection(ini, "Grid." + BuildControlKey(grid));
        gridSection["RowHeadersVisible"] = BoolText(grid.RowHeadersVisible);
        gridSection["AutoSizeColumnsMode"] = grid.AutoSizeColumnsMode.ToString();

        foreach (DataGridViewColumn column in grid.Columns)
        {
            string columnKey = GetColumnKey(column);
            var section = GetSection(ini, "GridColumn." + BuildControlKey(grid) + "." + columnKey);
            section["Width"] = column.Width.ToString(CultureInfo.InvariantCulture);
            section["DisplayIndex"] = column.DisplayIndex.ToString(CultureInfo.InvariantCulture);
            section["Visible"] = BoolText(column.Visible);
            section["FillWeight"] = column.FillWeight.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void ApplyGrid(IReadOnlyDictionary<string, SortedDictionary<string, string>> ini, DataGridView grid)
    {
        string gridKey = BuildControlKey(grid);
        if (ini.TryGetValue("Grid." + gridKey, out var gridSection))
        {
            if (TryReadBool(gridSection, "RowHeadersVisible", out bool rowHeadersVisible))
                grid.RowHeadersVisible = rowHeadersVisible;
        }

        foreach (DataGridViewColumn column in grid.Columns)
        {
            string sectionName = "GridColumn." + gridKey + "." + GetColumnKey(column);
            if (!ini.TryGetValue(sectionName, out var section))
                continue;

            if (TryReadFloat(section, "FillWeight", out float fillWeight) && fillWeight > 0)
                column.FillWeight = fillWeight;

            if (TryReadInt(section, "Width", out int width) && width > 20)
                column.Width = Math.Min(2000, width);

            if (TryReadBool(section, "Visible", out bool visible))
                column.Visible = visible;
        }

        var displayIndexes = new List<(DataGridViewColumn Column, int DisplayIndex)>();
        foreach (DataGridViewColumn column in grid.Columns)
        {
            string sectionName = "GridColumn." + gridKey + "." + GetColumnKey(column);
            if (ini.TryGetValue(sectionName, out var section) && TryReadInt(section, "DisplayIndex", out int displayIndex))
                displayIndexes.Add((column, displayIndex));
        }

        foreach (var item in displayIndexes.OrderBy(x => x.DisplayIndex))
        {
            try
            {
                int max = Math.Max(0, grid.Columns.Count - 1);
                item.Column.DisplayIndex = Math.Max(0, Math.Min(max, item.DisplayIndex));
            }
            catch
            {
                // Ignore impossible column orders after UI changes.
            }
        }
    }

    private static void SaveGridControl(IDictionary<string, SortedDictionary<string, string>> ini, GridControl grid)
    {
        if (grid.MainView is not GridView view)
            return;

        string gridKey = BuildControlKey(grid);
        var gridSection = GetSection(ini, "DxGrid." + gridKey);
        gridSection["ActiveFilterString"] = view.ActiveFilterString ?? string.Empty;

        foreach (GridColumn column in view.Columns)
        {
            string columnKey = GetColumnKey(column);
            var section = GetSection(ini, "DxGridColumn." + gridKey + "." + columnKey);
            section["Width"] = column.Width.ToString(CultureInfo.InvariantCulture);
            section["VisibleIndex"] = column.VisibleIndex.ToString(CultureInfo.InvariantCulture);
            section["Visible"] = BoolText(column.Visible);
            section["SortIndex"] = column.SortIndex.ToString(CultureInfo.InvariantCulture);
            section["SortOrder"] = column.SortOrder.ToString();
            section["GroupIndex"] = column.GroupIndex.ToString(CultureInfo.InvariantCulture);
            section["Fixed"] = column.Fixed.ToString();
        }
    }

    private static void ApplyGridControl(IReadOnlyDictionary<string, SortedDictionary<string, string>> ini, GridControl grid)
    {
        if (grid.MainView is not GridView view)
            return;

        string gridKey = BuildControlKey(grid);
        if (ini.TryGetValue("DxGrid." + gridKey, out var gridSection) &&
            gridSection.TryGetValue("ActiveFilterString", out string? filter))
        {
            try
            {
                view.ActiveFilterString = filter ?? string.Empty;
            }
            catch
            {
                // Ignore filters from older layouts after model changes.
            }
        }

        foreach (GridColumn column in view.Columns)
        {
            string sectionName = "DxGridColumn." + gridKey + "." + GetColumnKey(column);
            if (!ini.TryGetValue(sectionName, out var section))
                continue;

            if (TryReadInt(section, "Width", out int width) && width > 20)
                column.Width = Math.Min(2200, width);

            if (TryReadBool(section, "Visible", out bool visible))
                column.Visible = visible;

            if (section.TryGetValue("Fixed", out string? fixedText) && Enum.TryParse(fixedText, out FixedStyle fixedStyle))
                column.Fixed = fixedStyle;

            if (section.TryGetValue("SortOrder", out string? sortText) && Enum.TryParse(sortText, out ColumnSortOrder sortOrder))
                column.SortOrder = sortOrder;

            if (TryReadInt(section, "SortIndex", out int sortIndex) && sortIndex >= 0)
                column.SortIndex = sortIndex;

            if (TryReadInt(section, "GroupIndex", out int groupIndex))
                column.GroupIndex = groupIndex;
        }

        var visibleIndexes = new List<(GridColumn Column, int VisibleIndex)>();
        foreach (GridColumn column in view.Columns)
        {
            string sectionName = "DxGridColumn." + gridKey + "." + GetColumnKey(column);
            if (ini.TryGetValue(sectionName, out var section) && TryReadInt(section, "VisibleIndex", out int visibleIndex))
                visibleIndexes.Add((column, visibleIndex));
        }

        foreach (var item in visibleIndexes.OrderBy(x => x.VisibleIndex))
        {
            try
            {
                item.Column.VisibleIndex = item.VisibleIndex;
            }
            catch
            {
                // Ignore impossible column orders after UI changes.
            }
        }
    }


    private static void ApplyForm(Form form, IReadOnlyDictionary<string, SortedDictionary<string, string>> ini)
    {
        if (!ini.TryGetValue("Form", out var section))
            return;

        if (TryReadInt(section, "Left", out int left) &&
            TryReadInt(section, "Top", out int top) &&
            TryReadInt(section, "Width", out int width) &&
            TryReadInt(section, "Height", out int height) &&
            width >= 900 && height >= 600)
        {
            var bounds = new Rectangle(left, top, width, height);
            if (IsVisibleOnAnyScreen(bounds))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = bounds;
            }
        }

        if (section.TryGetValue("WindowState", out string? stateText) &&
            Enum.TryParse(stateText, out FormWindowState state) &&
            state != FormWindowState.Minimized)
        {
            form.WindowState = state;
        }
    }

    private static bool IsVisibleOnAnyScreen(Rectangle bounds)
    {
        foreach (Screen screen in Screen.AllScreens)
        {
            Rectangle area = screen.WorkingArea;
            if (area.IntersectsWith(bounds))
                return true;
        }

        return false;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in EnumerateControls(child))
                yield return nested;
        }
    }

    private static string BuildControlKey(Control control)
    {
        var parts = new Stack<string>();
        Control? current = control;
        while (current is not null && current is not Form)
        {
            string name = string.IsNullOrWhiteSpace(current.Name)
                ? current.GetType().Name + GetSiblingIndex(current).ToString(CultureInfo.InvariantCulture)
                : current.Name;
            parts.Push(CleanKey(name));
            current = current.Parent;
        }

        return string.Join(".", parts);
    }

    private static int GetSiblingIndex(Control control)
    {
        if (control.Parent is null)
            return 0;

        int index = 0;
        foreach (Control sibling in control.Parent.Controls)
        {
            if (ReferenceEquals(sibling, control))
                return index;
            if (sibling.GetType() == control.GetType())
                index++;
        }

        return index;
    }

    private static string GetColumnKey(DataGridViewColumn column)
    {
        string raw = !string.IsNullOrWhiteSpace(column.Name)
            ? column.Name
            : !string.IsNullOrWhiteSpace(column.DataPropertyName)
                ? column.DataPropertyName
                : column.HeaderText;
        return CleanKey(raw);
    }


    private static string GetColumnKey(GridColumn column)
    {
        string raw = !string.IsNullOrWhiteSpace(column.Name)
            ? column.Name
            : !string.IsNullOrWhiteSpace(column.FieldName)
                ? column.FieldName
                : column.Caption;
        return CleanKey(raw);
    }

    private static string CleanKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static SortedDictionary<string, string> GetSection(
        IDictionary<string, SortedDictionary<string, string>> ini,
        string section)
    {
        if (!ini.TryGetValue(section, out var values))
        {
            values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ini[section] = values;
        }

        return values;
    }

    private static string WriteIni(SortedDictionary<string, SortedDictionary<string, string>> ini)
    {
        var builder = new StringBuilder();
        builder.AppendLine("; VoteCounter layouts");
        builder.AppendLine("; Stored in SQLite table Setting, BLOB column Ini");
        builder.AppendLine("; Database file stays in VoteCounter/Database/VoteCounter.db");
        builder.AppendLine();

        foreach (var section in ini)
        {
            builder.Append('[').Append(section.Key).AppendLine("]");
            foreach (var pair in section.Value)
                builder.Append(pair.Key).Append('=').AppendLine(pair.Value ?? string.Empty);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static SortedDictionary<string, SortedDictionary<string, string>> ReadIniText(string text)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        string currentSection = string.Empty;
        using var reader = new StringReader(text);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!string.IsNullOrWhiteSpace(currentSection))
                    GetSection(result, currentSection);
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentSection))
                continue;

            int equal = line.IndexOf('=');
            if (equal <= 0)
                continue;

            string key = line[..equal].Trim();
            string value = line[(equal + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                GetSection(result, currentSection)[key] = value;
        }

        return result;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, string> section, string key, out int value)
    {
        value = 0;
        return section.TryGetValue(key, out string? text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadFloat(IReadOnlyDictionary<string, string> section, string key, out float value)
    {
        value = 0;
        return section.TryGetValue(key, out string? text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBool(IReadOnlyDictionary<string, string> section, string key, out bool value)
    {
        value = false;
        if (!section.TryGetValue(key, out string? text))
            return false;

        if (bool.TryParse(text, out value))
            return true;

        if (text == "1")
        {
            value = true;
            return true;
        }

        if (text == "0")
        {
            value = false;
            return true;
        }

        return false;
    }

    private static string BoolText(bool value) => value ? "true" : "false";
}
