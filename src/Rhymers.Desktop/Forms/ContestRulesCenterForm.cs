using System.Text;
using Rhymers.Core.Models;
using DevExpress.XtraEditors;
using Form = DevExpress.XtraEditors.XtraForm;
using Button = DevExpress.XtraEditors.SimpleButton;
using CheckBox = DevExpress.XtraEditors.CheckEdit;
using MessageBox = DevExpress.XtraEditors.XtraMessageBox;

namespace Rhymers;

public sealed class ContestRulesCenterForm : Form
{
    private readonly NumericUpDown nudVoteLimit;
    private readonly NumericUpDown nudBaseVote;
    private readonly NumericUpDown nudMaxVote;
    private readonly NumericUpDown nudLimitMaxVote;
    private readonly CheckBox chkLimitMaxVoteByTopic;
    private readonly CheckBox chkOneMaxVotePerTopic;
    private readonly CheckBox chkDowngradeExtraMaxVote;
    private readonly CheckBox chkAllowZeroVotes;
    private readonly CheckBox chkSelfVoteZero;
    private readonly CheckBox chkHostKnowsAuthors;
    private readonly TextBox txtPreview;

    public ContestRulesCenterForm(Contest contest)
    {
        Text = "Центр правил конкурса";
        StartPosition = FormStartPosition.CenterParent;
        Width = 760;
        Height = 560;
        MinimumSize = new Size(700, 500);
        Font = new Font("Segoe UI", 10F);

        nudVoteLimit = CreateNumber(0, 999, contest.VoteLimit);
        nudBaseVote = CreateNumber(0, 9, contest.BaseVote);
        nudMaxVote = CreateNumber(1, 9, contest.MaxVote);
        nudLimitMaxVote = CreateNumber(0, 999, contest.LimitMaxVote);

        chkLimitMaxVoteByTopic = CreateCheck("Лимит максимальной оценки считается отдельно в каждой теме", contest.LimitMaxVoteByTopic);
        chkOneMaxVotePerTopic = CreateCheck("Одна максимальная оценка в одной теме", contest.OneMaxVotePerTopic);
        chkDowngradeExtraMaxVote = CreateCheck("Если лимит исчерпан - принимать базовую оценку, оригинал сохранять", contest.DowngradeExtraMaxVoteToBase);
        chkAllowZeroVotes = CreateCheck("Принимать 0 как обычную оценку", contest.AllowZeroVotes);
        chkSelfVoteZero = CreateCheck("Голос автора за свою работу автоматически принимать как 0", contest.TreatSelfVoteAsZero);
        chkHostKnowsAuthors = CreateCheck("Таблицу ведёт ведущий - авторы работ известны", contest.HostKnowsAuthors);

        txtPreview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            BorderStyle = BorderStyle.FixedSingle
        };

        Controls.Add(BuildRoot(contest));
        WirePreviewUpdates();
        UpdatePreview();
    }

    public void ApplyTo(Contest contest)
    {
        contest.VoteLimit = (int)nudVoteLimit.Value;
        contest.BaseVote = (int)nudBaseVote.Value;
        contest.MaxVote = (int)nudMaxVote.Value;
        contest.LimitMaxVote = (int)nudLimitMaxVote.Value;
        contest.LimitMaxVoteByTopic = chkLimitMaxVoteByTopic.Checked;
        contest.OneMaxVotePerTopic = chkOneMaxVotePerTopic.Checked;
        contest.DowngradeExtraMaxVoteToBase = chkDowngradeExtraMaxVote.Checked;
        contest.AllowZeroVotes = chkAllowZeroVotes.Checked;
        contest.TreatSelfVoteAsZero = chkSelfVoteZero.Checked;
        contest.HostKnowsAuthors = chkHostKnowsAuthors.Checked;

        if (contest.MaxVote < contest.BaseVote)
            contest.BaseVote = contest.MaxVote;

        if (contest.OneMaxVotePerTopic && contest.LimitMaxVote <= 0)
            contest.LimitMaxVote = 1;

        contest.UpdatedAt = DateTime.Now;
    }

    private Control BuildRoot(Contest contest)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"№{contest.Number} - {contest.Name}\r\nЗдесь собраны правила, которые влияют на автоприём голосов и расчёт итогов."
        }, 0, 0);

        root.Controls.Add(BuildRulesPanel(), 0, 1);

        var previewGroup = new GroupControl { Text = "Как правила будут применяться", Dock = DockStyle.Fill };
        previewGroup.Controls.Add(txtPreview);
        root.Controls.Add(previewGroup, 0, 2);

        root.Controls.Add(BuildPresetPanel(), 0, 3);
        root.Controls.Add(BuildButtonsPanel(), 0, 4);
        return root;
    }

    private Control BuildRulesPanel()
    {
        var group = new GroupControl { Text = "Основные параметры", Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddLabel(panel, "Макс. оценок:", 0, 0);
        panel.Controls.Add(nudVoteLimit, 1, 0);
        AddLabel(panel, "Базовая оценка:", 2, 0);
        panel.Controls.Add(nudBaseVote, 3, 0);

        AddLabel(panel, "Максимальная:", 0, 1);
        panel.Controls.Add(nudMaxVote, 1, 1);
        AddLabel(panel, "Лимит максимальных:", 2, 1);
        panel.Controls.Add(nudLimitMaxVote, 3, 1);

        panel.Controls.Add(chkOneMaxVotePerTopic, 0, 2);
        panel.SetColumnSpan(chkOneMaxVotePerTopic, 2);
        panel.Controls.Add(chkLimitMaxVoteByTopic, 2, 2);
        panel.SetColumnSpan(chkLimitMaxVoteByTopic, 2);

        panel.Controls.Add(chkDowngradeExtraMaxVote, 0, 3);
        panel.SetColumnSpan(chkDowngradeExtraMaxVote, 2);
        panel.Controls.Add(chkSelfVoteZero, 2, 3);
        panel.SetColumnSpan(chkSelfVoteZero, 2);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildPresetPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = true
        };

        var btnOkRules = new Button { Text = "ОК-конкурс 1-4", Width = 155, Height = 34 };
        btnOkRules.Click += (_, _) => ApplyOkContestPreset();

        var btnNoLimit = new Button { Text = "Без лимита 4", Width = 140, Height = 34 };
        btnNoLimit.Click += (_, _) => ApplyNoMaxLimitPreset();

        var btnHost = new Button { Text = "Ведущий знает авторов", Width = 190, Height = 34 };
        btnHost.Click += (_, _) => { chkHostKnowsAuthors.Checked = true; UpdatePreview(); };

        var btnCounter = new Button { Text = "Счётчик до раскрытия", Width = 190, Height = 34 };
        btnCounter.Click += (_, _) => { chkHostKnowsAuthors.Checked = false; UpdatePreview(); };

        panel.Controls.AddRange(new Control[] { btnOkRules, btnNoLimit, btnHost, btnCounter, chkAllowZeroVotes, chkHostKnowsAuthors });
        return panel;
    }

    private Control BuildButtonsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };

        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        var btnOk = new Button { Text = "Применить", Width = 130, Height = 34 };
        btnOk.Click += (_, _) => AcceptRules();

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        panel.Controls.AddRange(new Control[] { btnCancel, btnOk });
        return panel;
    }

    private void ApplyOkContestPreset()
    {
        nudVoteLimit.Value = 0;
        nudBaseVote.Value = 3;
        nudMaxVote.Value = 4;
        nudLimitMaxVote.Value = 1;
        chkOneMaxVotePerTopic.Checked = true;
        chkLimitMaxVoteByTopic.Checked = true;
        chkDowngradeExtraMaxVote.Checked = true;
        chkAllowZeroVotes.Checked = false;
        chkSelfVoteZero.Checked = true;
        UpdatePreview();
    }

    private void ApplyNoMaxLimitPreset()
    {
        nudVoteLimit.Value = 0;
        nudBaseVote.Value = 3;
        nudMaxVote.Value = 4;
        nudLimitMaxVote.Value = 0;
        chkOneMaxVotePerTopic.Checked = false;
        chkLimitMaxVoteByTopic.Checked = false;
        chkDowngradeExtraMaxVote.Checked = true;
        chkAllowZeroVotes.Checked = false;
        chkSelfVoteZero.Checked = true;
        UpdatePreview();
    }

    private void AcceptRules()
    {
        if (nudMaxVote.Value < nudBaseVote.Value)
        {
            MessageBox.Show(this, "Максимальная оценка не может быть меньше базовой.", "Правила конкурса", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (chkOneMaxVotePerTopic.Checked && nudLimitMaxVote.Value <= 0)
            nudLimitMaxVote.Value = 1;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void WirePreviewUpdates()
    {
        nudVoteLimit.ValueChanged += (_, _) => UpdatePreview();
        nudBaseVote.ValueChanged += (_, _) => UpdatePreview();
        nudMaxVote.ValueChanged += (_, _) => UpdatePreview();
        nudLimitMaxVote.ValueChanged += (_, _) => UpdatePreview();
        chkLimitMaxVoteByTopic.CheckedChanged += (_, _) => UpdatePreview();
        chkOneMaxVotePerTopic.CheckedChanged += (_, _) => UpdatePreview();
        chkDowngradeExtraMaxVote.CheckedChanged += (_, _) => UpdatePreview();
        chkAllowZeroVotes.CheckedChanged += (_, _) => UpdatePreview();
        chkSelfVoteZero.CheckedChanged += (_, _) => UpdatePreview();
        chkHostKnowsAuthors.CheckedChanged += (_, _) => UpdatePreview();
    }

    private void UpdatePreview()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Диапазон оценок: {(chkAllowZeroVotes.Checked ? "0, " : string.Empty)}1..{nudMaxVote.Value}.");
        sb.AppendLine($"Базовая оценка для автопонижений: {nudBaseVote.Value}.");
        sb.AppendLine("3+ всегда считается как 3.5 и сохраняется дробно.");

        if (nudVoteLimit.Value > 0)
            sb.AppendLine($"У одного голосующего принимаются только первые {nudVoteLimit.Value} оценок.");
        else
            sb.AppendLine("Количество оценок одного голосующего не ограничено.");

        if (chkOneMaxVotePerTopic.Checked || nudLimitMaxVote.Value > 0)
        {
            string scope = chkLimitMaxVoteByTopic.Checked ? "в каждой теме" : "во всём конкурсе";
            sb.AppendLine($"Максимальная оценка {nudMaxVote.Value}: лимит {Math.Max(1, (int)nudLimitMaxVote.Value)} {scope}.");
            sb.AppendLine(chkDowngradeExtraMaxVote.Checked
                ? $"Если лимит исчерпан, оригинал сохраняется, а принятая оценка становится {nudBaseVote.Value}."
                : "Если лимит исчерпан, оценка будет отмечена предупреждением без автопонижения.");
        }
        else
        {
            sb.AppendLine("Лимит максимальных оценок отключён.");
        }

        sb.AppendLine(chkSelfVoteZero.Checked
            ? "Голос автора за свою работу принимается как 0, оригинал сохраняется."
            : "Самоголосы не обнуляются автоматически.");

        sb.AppendLine(chkHostKnowsAuthors.Checked
            ? "Режим авторства: ведущий знает авторов, можно проверять самоголосы."
            : "Режим авторства: счётчик до раскрытия, авторы скрыты.");

        txtPreview.Text = sb.ToString();
    }

    private static NumericUpDown CreateNumber(int minimum, int maximum, int value)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            Dock = DockStyle.Fill,
            Width = 80
        };
    }

    private static CheckBox CreateCheck(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
    }

    private static void AddLabel(TableLayoutPanel panel, string text, int column, int row)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        }, column, row);
    }
}
