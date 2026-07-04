using System.Text;
using VoteCounter.Models;
using VoteCounter.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Helpers;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraEditors.Repository;
using Button = DevExpress.XtraEditors.SimpleButton;
using CheckBox = DevExpress.XtraEditors.CheckEdit;
using Form = DevExpress.XtraEditors.XtraForm;
using GroupBox = DevExpress.XtraEditors.GroupControl;
using MessageBox = DevExpress.XtraEditors.XtraMessageBox;
using TabControl = DevExpress.XtraTab.XtraTabControl;
using TabPage = DevExpress.XtraTab.XtraTabPage;


namespace VoteCounter;

public sealed class MainForm : RibbonForm
{
    private readonly LocalStore _store = new();
    private readonly VoteParser _parser = new();
    private readonly ExcelResultBuilder _builder = new();
    private readonly VoteAuditService _audit = new();
    private readonly VoteRuleService _rules = new();
    private readonly ContestResultsService _results = new();
    private readonly WorkTextImporter _workImporter = new();
    private readonly SingleWorkSubmissionImporter _singleWorkImporter = new();
    private readonly PrivateMessageWorkImporter _privateMessageWorkImporter = new();
    private readonly ContestTextImporter _contestImporter = new();
    private readonly RhymeMachineStore _rmStore = new();
    private readonly WorkSpellChecker _spellChecker = new();
    private readonly FirebirdLegacyImporter _firebirdImporter = new();
    private readonly ContestReportExportService _reportExporter = new();
    private readonly ContestRulesAutoFixService _rulesAutoFix = new();
    private readonly VoteImportReportService _voteImportReports = new();
    private readonly LayoutIniStore _layoutStore = new();
    private readonly ToolTip _hints = new()
    {
        AutoPopDelay = 15000,
        InitialDelay = 450,
        ReshowDelay = 120,
        ShowAlways = true
    };
    private readonly Dictionary<Control, string> _helpAnchors = new();

    private List<Contest> _contests = new();
    private AppSettings _settings = new();
    private BindingList<ContestListRow> _contestListBinding = new();
    private BindingList<ContestWork> _worksBinding = new();
    private BindingList<VoterSetting> _votersBinding = new();
    private BindingList<VoterStatusRow> _statusBinding = new();
    private BindingList<ContestRatingRow> _ratingBinding = new();
    private BindingList<PersonDirectoryRow> _peopleBinding = new();
    private bool _syncingContestList;
    private bool _syncingNavigation;
    private bool _syncingWorkflowContest;

    private TabControl mainTabs = null!;
    private AccordionControl sideNavigation = null!;
    private readonly List<AccordionControlElement> _navigationElements = new();
    private RibbonControl ribbonWorkflow = null!;
    private RibbonPage rpCurrent = null!;
    private BarEditItem beiWorkflowContest = null!;
    private RepositoryItemComboBox repositoryWorkflowContests = null!;
    private BarStaticItem bsiWorkflowStatus = null!;
    private BarStaticItem bsiWorkflowHint = null!;
    private SkinDropDownButtonItem skinDropDownButtonItem = null!;
    private SkinPaletteDropDownButtonItem skinPaletteDropDownButtonItem = null!;
    private BarCheckItem bciOriginalPalette = null!;
    private BarCheckItem bciTrackWindowsAccentColor = null!;
    private BarButtonItem bbiCustomColors = null!;
    private BarButtonItem bbiCustomColors2 = null!;
    private BarCheckItem bciTrackWindowsAppMode = null!;
    private GridControl gridContests = null!;
    private GridView viewContests = null!;
    private GridControl gridContestWorksPreview = null!;
    private GridView viewContestWorksPreview = null!;
    private GridControl gridContestVotersPreview = null!;
    private GridView viewContestVotersPreview = null!;
    private TextBox txtContestCard = null!;
    private System.Windows.Forms.ComboBox cboContests = null!;
    private TextBox txtContestNo = null!;
    private TextBox txtContestName = null!;
    private TextBox txtTemplate = null!;
    private TextBox txtOutput = null!;
    private TextBox txtVotes = null!;
    private NumericUpDown nudVoteLimit = null!;
    private NumericUpDown nudBaseVote = null!;
    private NumericUpDown nudMaxVote = null!;
    private NumericUpDown nudLimitMaxVote = null!;
    private CheckBox chkLimitMaxVoteByTopic = null!;
    private CheckBox chkOneMaxVotePerTopic = null!;
    private CheckBox chkDowngradeExtraMaxVote = null!;
    private CheckBox chkAllowZeroVotes = null!;
    private CheckBox chkSelfVoteZero = null!;
    private CheckBox chkHostKnowsAuthors = null!;
    private GridControl grid = null!;
    private GridView viewVotes = null!;
    private GridControl gridWorks = null!;
    private GridView viewWorks = null!;
    private GridControl gridVoters = null!;
    private GridView viewVoters = null!;
    private GridControl gridStatus = null!;
    private GridView viewStatus = null!;
    private GridControl gridResults = null!;
    private GridView viewResults = null!;
    private GridControl gridRuleChanges = null!;
    private GridView viewRuleChanges = null!;
    private GridControl gridPeople = null!;
    private GridView viewPeople = null!;
    private TextBox txtRuleChangeSummary = null!;
    private TextBox txtFinalReport = null!;
    private TextBox txtLog = null!;

    public MainForm()
    {
        InitializeComponent();
        LoadState();
        Shown += (_, _) => ApplyLayoutFromSettingsBlob(log: false);
        FormClosing += (_, _) => SaveLayoutToSettingsBlob(log: false);
    }

    private Contest? CurrentContest => cboContests.SelectedItem as Contest;

    private void InitializeComponent()
    {
        Name = "MainForm";
        Text = "VoteCounter - автозаполнение голосований";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1260;
        Height = 880;
        MinimumSize = new Size(1040, 740);
        Font = new Font("Segoe UI", 10F);
        Icon = LoadIconAsset("votecounter.ico");

        mainTabs = new TabControl
        {
            Name = "mainTabs",
            Dock = DockStyle.Fill,
            ShowTabHeader = DefaultBoolean.False
        };
        
        mainTabs.SelectedPageChanged += MainTabsSelectedPageChanged;

        var contestsPage = CreateTabPage("Конкурсы");
        var votePage = CreateTabPage("Голосование");
        var settingsPage = CreateTabPage("Настройки конкурса");
        var controlPage = CreateTabPage("Контроль голосования");
        var resultsPage = CreateTabPage("Итоги");
        var ruleChangesPage = CreateTabPage("Правки правил");
        var peoplePage = CreateTabPage("Участники");

        contestsPage.Controls.Add(BuildContestListPage());
        votePage.Controls.Add(BuildVotePage());
        settingsPage.Controls.Add(BuildSettingsPage());
        controlPage.Controls.Add(BuildControlPage());
        resultsPage.Controls.Add(BuildResultsPage());
        ruleChangesPage.Controls.Add(BuildRuleChangesPage());
        peoplePage.Controls.Add(BuildPeoplePage());

        mainTabs.TabPages.Add(contestsPage);
        mainTabs.TabPages.Add(votePage);
        mainTabs.TabPages.Add(settingsPage);
        mainTabs.TabPages.Add(controlPage);
        mainTabs.TabPages.Add(resultsPage);
        mainTabs.TabPages.Add(ruleChangesPage);
        mainTabs.TabPages.Add(peoplePage);

        BuildContestWorkflowPanel();
        BuildSideNavigationPanel();

        mainTabs.Dock = DockStyle.Fill;
        sideNavigation.Dock = DockStyle.Left;
        sideNavigation.Width = 255;
        sideNavigation.MinimumSize = new Size(0, 768);

        Controls.Add(mainTabs);
        Controls.Add(sideNavigation);
        Controls.Add(ribbonWorkflow);
        Ribbon = ribbonWorkflow;
        NavigationControl = sideNavigation;
        NavigationControlLayoutMode = RibbonFormNavigationControlLayoutMode.StretchToFormTitle;

        InstallGridPopupMenus();
        ApplyDevExpressPolish(this);
        InstallHelpAndHints();
        InitAccentColors();
        SyncNavigationFromSelectedPage();
    }

    private void MainTabsSelectedPageChanged(object? sender, TabPageChangedEventArgs e)
    {
        SyncNavigationFromSelectedPage();
    }

    private void BuildSideNavigationPanel()
    {
        sideNavigation = new AccordionControl
        {
            Name = "navigationAccordion",
            AllowItemSelection = true,
            ScrollBarMode = ScrollBarMode.Hidden
        };

        var viewsGroup = new AccordionControlElement
        {
            Name = "navigationAccordionViewsGroup",
            Text = "Views",
            Style = ElementStyle.Group,
            Expanded = true
        };
        sideNavigation.Elements.Add(viewsGroup);
        _navigationElements.Clear();
        AddNavigationElement(viewsGroup, "Конкурсы", 0);
        AddNavigationElement(viewsGroup, "Голосование", 1);
        AddNavigationElement(viewsGroup, "Настройки конкурса", 2);
        AddNavigationElement(viewsGroup, "Контроль голосования", 3);
        AddNavigationElement(viewsGroup, "Итоги", 4);
        AddNavigationElement(viewsGroup, "Правки правил", 5);
        AddNavigationElement(viewsGroup, "Участники", 6);

        sideNavigation.SelectedElementChanged += SideNavigationSelectedElementChanged;

    }

    private void SideNavigationSelectedElementChanged(object? sender, SelectedElementChangedEventArgs e)
    {
        if (_syncingNavigation)
            return;
        if (e.Element?.Tag is int index)
            NavigateToSection(index);
    }

    private void AddNavigationElement(AccordionControlElement parent, string text, int sectionIndex)
    {
        var element = new AccordionControlElement
        {
            Text = text,
            Style = ElementStyle.Item,
            Tag = sectionIndex
        };
        parent.Elements.Add(element);
        _navigationElements.Add(element);
    }

    private void BuildContestWorkflowPanel()
    {
        ribbonWorkflow = new RibbonControl
        {
            Name = "ribbonControl1",
            MdiMergeStyle = RibbonMdiMergeStyle.Always,
            ShowToolbarCustomizeItem = false
        };
        ribbonWorkflow.Toolbar.ShowCustomizeItem = false;

        repositoryWorkflowContests = new RepositoryItemComboBox
        {
            TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor
        };
        ribbonWorkflow.RepositoryItems.Add(repositoryWorkflowContests);

        skinDropDownButtonItem = new SkinDropDownButtonItem
        {
            Id = 1,
            Name = "skinDropDownButtonItem1"
        };
        skinPaletteDropDownButtonItem = new SkinPaletteDropDownButtonItem
        {
            ActAsDropDown = true,
            ButtonStyle = BarButtonStyle.DropDown,
            Id = 2,
            Name = "skinPaletteDropDownButtonItem1"
        };
        bciOriginalPalette = new BarCheckItem
        {
            Caption = "Original Palette",
            Id = 3,
            Name = "bciOriginalPalette"
        };
        bciOriginalPalette.ImageOptions.ImageUri.Uri = "arrows/stop";
        SetBarItemVisual(bciOriginalPalette, "arrows/stop", "Вернуть исходную палитру выбранной темы оформления.");
        bciTrackWindowsAccentColor = new BarCheckItem
        {
            Caption = "Track Window Accent Color",
            Id = 4,
            Name = "bciTrackWindowsAccentColor"
        };
        bciTrackWindowsAccentColor.ImageOptions.ImageUri.Uri = "arrows/stop";
        SetBarItemVisual(bciTrackWindowsAccentColor, "arrows/stop", "Подстраивать акцентный цвет приложения под цвет Windows.");
        bbiCustomColors = new BarButtonItem
        {
            Caption = "Custom Colors",
            Id = 5,
            Name = "bbiCustomColors"
        };
        bbiCustomColors.ImageOptions.ImageUri.Uri = "arrows/stop";
        SetBarItemVisual(bbiCustomColors, "actions/edit", "Выбрать пользовательский акцентный цвет интерфейса.");
        bbiCustomColors2 = new BarButtonItem
        {
            Caption = "Custom Colors 2",
            Id = 6,
            Name = "bbiCustomColors2"
        };
        bbiCustomColors2.ImageOptions.ImageUri.Uri = "arrows/stop";
        SetBarItemVisual(bbiCustomColors2, "actions/edit", "Выбрать второй пользовательский акцентный цвет интерфейса.");
        bciTrackWindowsAppMode = new BarCheckItem
        {
            Caption = "Track Window App Mode",
            Id = 7,
            Name = "bciTrackWindowsAppMode"
        };
        bciTrackWindowsAppMode.ImageOptions.ImageUri.Uri = "arrows/stop";
        SetBarItemVisual(bciTrackWindowsAppMode, "actions/check", "Следовать светлому или тёмному режиму Windows.");
        SetBarItemVisual(skinDropDownButtonItem, "paint/brush", "Выбрать тему оформления DevExpress.");
        SetBarItemVisual(skinPaletteDropDownButtonItem, "paint/palette", "Выбрать палитру текущей темы оформления.");

        beiWorkflowContest = new BarEditItem
        {
            Name = "beiWorkflowContest",
            Caption = "Текущий конкурс",
            Edit = repositoryWorkflowContests,
            Width = 310
        };
        SetBarItemVisual(beiWorkflowContest, "business%20objects/bo_project", "Выбрать текущий конкурс для работы.");
        beiWorkflowContest.EditValueChanged += (_, _) =>
        {
            if (_syncingWorkflowContest)
                return;
            if (beiWorkflowContest.EditValue is ContestComboItem item)
                SelectContestById(item.Id);
        };

        bsiWorkflowStatus = new BarStaticItem
        {
            Name = "bsiWorkflowStatus",
            Caption = "конкурс не выбран",
            ItemAppearance = { Normal = { Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold) } }
        };
        SetBarItemVisual(bsiWorkflowStatus, "actions/info", "Текущая стадия выбранного конкурса.");

        bsiWorkflowHint = new BarStaticItem
        {
            Name = "bsiWorkflowHint",
            Caption = "Создай или выбери конкурс. После этого сверху можно вести конкурс по стадиям 1-5.",
            AutoSize = BarStaticItemSize.Spring
        };
        SetBarItemVisual(bsiWorkflowHint, "support/info", "Короткая подсказка по следующему рабочему шагу.");

        ribbonWorkflow.Items.AddRange(new BarItem[]
        {
            skinDropDownButtonItem,
            skinPaletteDropDownButtonItem,
            bciOriginalPalette,
            bciTrackWindowsAccentColor,
            bbiCustomColors,
            bbiCustomColors2,
            bciTrackWindowsAppMode,
            beiWorkflowContest,
            bsiWorkflowStatus,
            bsiWorkflowHint
        });
        ribbonWorkflow.MaxItemId = 20;

        var ribbonPageAppearance = new RibbonPage
        {
            Name = "ribbonPageAppearance",
            Text = "Appearance"
        };
        var ribbonPageGroupAppearance = new RibbonPageGroup
        {
            Name = "ribbonPageGroupAppearance",
            Text = "Appearance"
        };
        ribbonPageGroupAppearance.ItemLinks.Add(skinDropDownButtonItem);
        ribbonPageGroupAppearance.ItemLinks.Add(skinPaletteDropDownButtonItem);

        var ribbonPageGroupAccentColors = new RibbonPageGroup
        {
            Name = "ribbonPageGroupAccentColors",
            Text = "Accent Colors"
        };
        ribbonPageGroupAccentColors.ItemLinks.Add(bciTrackWindowsAppMode);
        ribbonPageGroupAccentColors.ItemLinks.Add(bciOriginalPalette);
        ribbonPageGroupAccentColors.ItemLinks.Add(bciTrackWindowsAccentColor);
        ribbonPageGroupAccentColors.ItemLinks.Add(bbiCustomColors);
        ribbonPageGroupAccentColors.ItemLinks.Add(bbiCustomColors2);
        ribbonPageAppearance.Groups.AddRange(new[] { ribbonPageGroupAppearance, ribbonPageGroupAccentColors });
        ribbonWorkflow.Pages.Add(ribbonPageAppearance);

        rpCurrent = new RibbonPage
        {
            Name = "rpCurrent",
            Text = "Текущий конкурс"
        };
        ribbonWorkflow.Pages.Add(rpCurrent);

        var contestGroup = new RibbonPageGroup("Конкурс") { Name = "rpgCurrentContest" };
        contestGroup.ItemLinks.Add(beiWorkflowContest);
        rpCurrent.Groups.Add(contestGroup);

        var stageGroup = new RibbonPageGroup("Стадия") { Name = "rpgCurrentStage" };
        AddStageButton(stageGroup, ContestStage.TopicReception, "1 Темы", "actions/add", "Перевести конкурс на стадию приёма тем.");
        AddStageButton(stageGroup, ContestStage.WorkReception, "2 Работы", "richedit/insertpagebreak", "Перевести конкурс на стадию приёма работ.");
        AddStageButton(stageGroup, ContestStage.VotingOpen, "3 Голосование", "actions/check", "Открыть стадию голосования.");
        AddStageButton(stageGroup, ContestStage.VotingClosed, "4 Итоги", "business%20objects/bo_report", "Закрыть голосование и перейти к итогам.");
        AddStageButton(stageGroup, ContestStage.Finished, "5 Закрытие", "actions/close", "Перевести конкурс в закрытое состояние.");
        rpCurrent.Groups.Add(stageGroup);

        var actionGroup = new RibbonPageGroup("Действия") { Name = "rpgCurrentActions" };
        AddWorkflowAction(actionGroup, "Импорт тем", ImportTopicsDialog, "actions/download", "Вставить список тем, проверить дубли и импортировать в конкурс.");
        AddWorkflowAction(actionGroup, "Импорт работ из лички", ImportWorksFromPrivateMessagesDialog, "mail/forward", "Импортировать авторские сообщения из лички с предпросмотром.");
        AddWorkflowAction(actionGroup, "Форма голосования", () => NavigateToSection(1), "navigation/next", "Открыть рабочую форму приёма и проверки голосов.");
        AddWorkflowAction(actionGroup, "Импорт авторов", ImportWorksFromTextDialog, "business%20objects/bo_contact", "Применить или импортировать авторов работ из текста.");
        AddWorkflowAction(actionGroup, "Подвести итоги", () => { SetCurrentContestStage(ContestStage.VotingClosed); NavigateToSection(4); RefreshResults(log: true); }, "business%20objects/bo_report", "Посчитать результаты и открыть таблицу итогов.");
        AddWorkflowAction(actionGroup, "Дипломы HTML", GenerateDiplomaHtml, "export/exporttohtml", "Сформировать HTML-дипломы по текущим итогам.");
        AddWorkflowAction(actionGroup, "Закрыть + новый", CloseContestAndOpenNextDialog, "actions/new", "Закрыть текущий конкурс и открыть следующий.");
        rpCurrent.Groups.Add(actionGroup);

        var stateGroup = new RibbonPageGroup("Состояние") { Name = "rpgCurrentState" };
        stateGroup.ItemLinks.Add(bsiWorkflowStatus);
        stateGroup.ItemLinks.Add(bsiWorkflowHint);
        rpCurrent.Groups.Add(stateGroup);
    }

    private void AddStageButton(RibbonPageGroup group, ContestStage stage, string text, string imageUri, string tip)
    {
        var item = new BarButtonItem(ribbonWorkflow.Manager, text)
        {
            Name = "bbiStage" + (int)stage,
            Tag = stage
        };
        SetBarItemVisual(item, imageUri, tip);
        item.ItemClick += (_, _) => SetCurrentContestStage(stage);
        group.ItemLinks.Add(item);
    }

    private void AddWorkflowAction(RibbonPageGroup group, string text, Action action, string imageUri, string tip)
    {
        var item = new BarButtonItem(ribbonWorkflow.Manager, text)
        {
            Name = "bbi" + SanitizeIdentifier(text)
        };
        SetBarItemVisual(item, imageUri, tip);
        item.ItemClick += (_, _) => action();
        group.ItemLinks.Add(item);
    }

    private static void SetBarItemVisual(BarItem item, string imageUri, string tip)
    {
        item.ImageOptions.ImageUri.Uri = imageUri;
        item.SuperTip = new SuperToolTip();
        item.SuperTip.Items.Add(tip);
    }

    private void InitAccentColors()
    {
        SkinHelper.InitTrackWindowsAppMode(bciTrackWindowsAppMode);
        bciTrackWindowsAppMode.SuperTip = new SuperToolTip();
        bciTrackWindowsAppMode.SuperTip.Items.Add("Следовать светлому или тёмному режиму Windows.");
        bciTrackWindowsAppMode.SuperTip.Items[0].Appearance.FontStyleDelta = FontStyle.Bold;
        SkinHelper.InitResetToOriginalPalette(bciOriginalPalette);
        SkinHelper.InitTrackWindowsAccentColor(bciTrackWindowsAccentColor);
        SkinHelper.InitCustomAccentColor(Ribbon.Manager, bbiCustomColors);
        bbiCustomColors.SuperTip = new SuperToolTip();
        bbiCustomColors.SuperTip.Items.Add("Выбрать пользовательский акцентный цвет интерфейса.");
        bbiCustomColors.SuperTip.Items[0].Appearance.FontStyleDelta = FontStyle.Bold;
        SkinHelper.InitCustomAccentColor2(Ribbon.Manager, bbiCustomColors2);
        bbiCustomColors2.SuperTip = new SuperToolTip();
        bbiCustomColors2.SuperTip.Items.Add("Выбрать второй пользовательский акцентный цвет интерфейса.");
        bbiCustomColors2.SuperTip.Items[0].Appearance.FontStyleDelta = FontStyle.Bold;
    }

    private void NavigateToSection(int index)
    {
        if (mainTabs is null || index < 0 || index >= mainTabs.TabPages.Count)
            return;

        mainTabs.SelectedTabPageIndex = index;
        SyncNavigationFromSelectedPage();
    }

    private void SyncNavigationFromSelectedPage()
    {
        if (sideNavigation is null || mainTabs is null)
            return;

        int index = mainTabs.SelectedTabPageIndex;
        if (index < 0 || index >= _navigationElements.Count)
            return;

        _syncingNavigation = true;
        try
        {
            AccordionControlElement element = _navigationElements[index];
            if (sideNavigation.SelectedElement != element)
                sideNavigation.SelectedElement = element;
        }
        finally
        {
            _syncingNavigation = false;
        }
    }


    private void InstallHelpAndHints()
    {
        KeyPreview = true;
        _helpAnchors[this] = "overview";
        HelpRequested += MainHelpRequested;
        KeyDown -= MainHelpKeyDown;
        KeyDown += MainHelpKeyDown;

        foreach (Control control in EnumerateControls(this))
        {
            string anchor = ResolveHelpAnchor(control);
            _helpAnchors[control] = anchor;
            control.HelpRequested -= MainHelpRequested;
            control.HelpRequested += MainHelpRequested;
            control.KeyDown -= MainHelpKeyDown;
            control.KeyDown += MainHelpKeyDown;

            string hint = ResolveControlHint(control);
            if (!string.IsNullOrWhiteSpace(hint))
                _hints.SetToolTip(control, hint);
        }
    }

    private void MainHelpRequested(object? sender, HelpEventArgs e)
    {
        Control? control = sender as Control ?? ActiveControl;
        string anchor = FindHelpAnchor(control);
        OpenHelp(anchor);
        e.Handled = true;
    }

    private void MainHelpKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.F1)
            return;

        Control? control = sender as Control ?? ActiveControl;
        OpenHelp(FindHelpAnchor(control));
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private string FindHelpAnchor(Control? control)
    {
        while (control is not null)
        {
            if (_helpAnchors.TryGetValue(control, out string? anchor) && !string.IsNullOrWhiteSpace(anchor))
                return anchor;
            control = control.Parent;
        }

        return mainTabs?.SelectedTabPageIndex switch
        {
            0 => "contests",
            1 => "voting",
            2 => "settings",
            3 => "control",
            4 => "results",
            5 => "rule-changes",
            _ => "overview"
        };
    }

    private static string ResolveHelpAnchor(Control control)
    {
        string name = control.Name ?? string.Empty;
        string text = GetControlText(control);

        if (name.Contains("sideNavigation", StringComparison.OrdinalIgnoreCase))
            return "navigation";
        if (name.Contains("Workflow", StringComparison.OrdinalIgnoreCase) || text.Contains("стад", StringComparison.OrdinalIgnoreCase))
            return "workflow";
        if (name.Contains("Contests", StringComparison.OrdinalIgnoreCase) || name.Contains("ContestCard", StringComparison.OrdinalIgnoreCase))
            return "contests";
        if (name.Contains("Votes", StringComparison.OrdinalIgnoreCase) || text.Contains("голос", StringComparison.OrdinalIgnoreCase))
            return "voting";
        if (name.Contains("Works", StringComparison.OrdinalIgnoreCase) || text.Contains("работ", StringComparison.OrdinalIgnoreCase))
            return "works";
        if (name.Contains("Voters", StringComparison.OrdinalIgnoreCase) || text.Contains("суд", StringComparison.OrdinalIgnoreCase))
            return "settings";
        if (name.Contains("Status", StringComparison.OrdinalIgnoreCase) || text.Contains("контроль", StringComparison.OrdinalIgnoreCase))
            return "control";
        if (name.Contains("Results", StringComparison.OrdinalIgnoreCase) || name.Contains("FinalReport", StringComparison.OrdinalIgnoreCase) || text.Contains("итог", StringComparison.OrdinalIgnoreCase))
            return "results";
        if (name.Contains("RuleChanges", StringComparison.OrdinalIgnoreCase) || text.Contains("правк", StringComparison.OrdinalIgnoreCase))
            return "rule-changes";
        if (text.Contains("диплом", StringComparison.OrdinalIgnoreCase))
            return "diplomas";
        if (text.Contains("тем", StringComparison.OrdinalIgnoreCase))
            return "topics";
        if (text.Contains("layout", StringComparison.OrdinalIgnoreCase) || text.Contains("AutoWidth", StringComparison.OrdinalIgnoreCase))
            return "layout";

        return "overview";
    }

    private static string ResolveControlHint(Control control)
    {
        string text = GetControlText(control).Trim();
        if (control is AccordionControl)
            return "Боковая accordion-навигация по разделам. F1 - справка по навигации.";
        if (control is GridControl)
            return "Таблица DevExpress: правая кнопка мыши открывает меню. F1 - справка по разделу.";
        if (control is TextBox textBox && textBox.Multiline)
            return "Многострочное поле ввода/просмотра. Можно вставлять текст из буфера. F1 - справка.";
        if (control is TextBox)
            return "Текстовое поле. F1 - справка по этому разделу.";
        if (control is System.Windows.Forms.ComboBox)
            return "Выбор текущего конкурса. F1 - справка.";
        if (control is NumericUpDown)
            return "Числовое правило конкурса. F1 - справка по настройкам.";
        if (control is CheckBox)
            return "Флажок правила или режима. F1 - справка по настройкам.";
        if (control is Button)
            return string.IsNullOrWhiteSpace(text) ? "Команда. F1 - справка." : $"Команда: {text}. F1 - справка.";
        if (control is GroupBox)
            return string.IsNullOrWhiteSpace(text) ? "Блок формы. F1 - справка." : $"Блок: {text}. F1 - справка.";

        return string.IsNullOrWhiteSpace(text) ? string.Empty : text + ". F1 - справка.";
    }

    private static string GetControlText(Control control)
    {
        try
        {
            return control.Text ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void OpenHelp(string anchor)
    {
        try
        {
            string file = ResolveHelpFile();
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                MessageBox.Show(this, "Файл справки не найден.", "Справка", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string url = new Uri(file).AbsoluteUri + "#" + Uri.EscapeDataString(anchor);
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка открытия справки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ResolveHelpFile()
    {
        string output = Path.Combine(AppContext.BaseDirectory, "Help", "index.html");
        if (File.Exists(output))
            return output;

        string project = Path.Combine(LocalDatabase.ProjectFolder, "Help", "index.html");
        return project;
    }

    private static TabPage CreateTabPage(string text) => new() { Text = text };

    private static Icon? LoadIconAsset(string fileName)
    {
        string file = ResolveImageAsset(fileName);
        return File.Exists(file) ? new Icon(file) : null;
    }

    private static Image? LoadImageAsset(string fileName)
    {
        string file = ResolveImageAsset(fileName);
        if (!File.Exists(file))
            return null;

        using var stream = new MemoryStream(File.ReadAllBytes(file));
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static string ResolveImageAsset(string fileName)
    {
        string output = Path.Combine(AppContext.BaseDirectory, "Img", fileName);
        if (File.Exists(output))
            return output;

        output = Path.Combine(AppContext.BaseDirectory, "Resources", "Images", fileName);
        if (File.Exists(output))
            return output;

        string project = Path.Combine(LocalDatabase.ProjectFolder, "Img", fileName);
        if (File.Exists(project))
            return project;

        return Path.Combine(LocalDatabase.ProjectFolder, "Resources", "Images", fileName);
    }

    private static void ApplyDevExpressPolish(Control root)
    {
        foreach (Control control in EnumerateControls(root))
        {
            switch (control)
            {
                case GridControl gridControl:
                    PolishDevExpressGrid(gridControl);
                    break;
                case TextBox textBox:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case TableLayoutPanel table:
                    table.BackColor = Color.FromArgb(247, 249, 252);
                    break;
                case FlowLayoutPanel flow:
                    flow.BackColor = Color.FromArgb(247, 249, 252);
                    break;
                case Label label:
                    label.ForeColor = Color.FromArgb(45, 55, 72);
                    break;
            }
        }
    }

    private static IEnumerable<Control> EnumerateControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in EnumerateControls(child))
                yield return nested;
        }
    }

    private static void PolishDevExpressGrid(GridControl gridControl)
    {
        gridControl.LookAndFeel.UseDefaultLookAndFeel = true;
        if (gridControl.MainView is GridView view)
            PolishGridView(view);
    }

    private static GridControl CreateGrid(string name, out GridView view, bool readOnly = true, bool multiSelect = false, bool allowNewRows = false)
    {
        var grid = new GridControl
        {
            Name = name,
            Dock = DockStyle.Fill
        };

        view = new GridView(grid)
        {
            Name = "view" + name,
            FocusRectStyle = DrawFocusRectStyle.RowFullFocus
        };

        grid.MainView = view;
        grid.ViewCollection.Add(view);
        PolishGridView(view);

        view.OptionsBehavior.Editable = !readOnly;
        view.OptionsBehavior.ReadOnly = readOnly;
        view.OptionsBehavior.AllowAddRows = allowNewRows ? DefaultBoolean.True : DefaultBoolean.False;
        view.OptionsBehavior.AllowDeleteRows = allowNewRows ? DefaultBoolean.True : DefaultBoolean.False;
        view.OptionsSelection.MultiSelect = multiSelect;
        view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
        view.OptionsView.NewItemRowPosition = allowNewRows ? NewItemRowPosition.Bottom : NewItemRowPosition.None;

        return grid;
    }

    private static void PolishGridView(GridView view)
    {
        view.OptionsView.ShowGroupPanel = false;
        view.OptionsView.ShowIndicator = false;
        view.OptionsView.EnableAppearanceEvenRow = true;
        view.OptionsView.EnableAppearanceOddRow = true;
        view.OptionsView.ColumnAutoWidth = false;
        view.OptionsFind.AlwaysVisible = true;
        view.OptionsFind.FindNullPrompt = "Поиск в таблице...";
        view.OptionsCustomization.AllowColumnMoving = true;
        view.OptionsCustomization.AllowColumnResizing = true;
        view.OptionsCustomization.AllowFilter = true;
        view.OptionsCustomization.AllowSort = true;
        view.OptionsMenu.EnableColumnMenu = true;
        view.OptionsSelection.EnableAppearanceFocusedCell = false;
        view.Appearance.HeaderPanel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        view.Appearance.Row.Font = new Font("Segoe UI", 9F);
        view.Appearance.EvenRow.BackColor = Color.FromArgb(249, 251, 254);
        view.Appearance.FocusedRow.BackColor = Color.FromArgb(221, 235, 255);
        view.Appearance.FocusedRow.ForeColor = Color.FromArgb(20, 28, 40);
    }

    private static GridColumn AddGridColumn(GridView view, string fieldName, string caption, int width, bool editable = false)
    {
        GridColumn column = view.Columns.AddVisible(fieldName, caption);
        column.MinWidth = Math.Min(width, 70);
        column.Width = width;
        column.OptionsColumn.AllowEdit = editable;
        column.OptionsColumn.ReadOnly = !editable;
        return column;
    }

    private static T? GetFocusedRow<T>(GridView? view) where T : class
    {
        return view?.GetFocusedRow() as T;
    }

    private void InstallGridPopupMenus()
    {
        AttachGridPopup(gridContests, viewContests, "Конкурсы",
            GridMenuCommand.Item("Открыть выбранный конкурс", () => SelectContestFromList(switchToWorkPage: true)),
            GridMenuCommand.Item("Перейти в настройки конкурса", () =>
            {
                SelectContestFromList(switchToWorkPage: false);
                if (mainTabs.TabPages.Count > 2)
                    NavigateToSection(2);
            }),
            GridMenuCommand.Item("Центр правил", OpenContestRulesCenter),
            GridMenuCommand.Item("Перейти в итоги", () =>
            {
                SelectContestFromList(switchToWorkPage: false);
                if (mainTabs.TabPages.Count > 4)
                    NavigateToSection(4);
            }),
            GridMenuCommand.Separator(),
            GridMenuCommand.Item("Новый конкурс", CreateContest),
            GridMenuCommand.Item("Импорт конкурса из текста", ImportContestFromTextDialog),
            GridMenuCommand.Item("Импорт старой *.fdb", ImportLegacyFirebirdDialog),
            GridMenuCommand.Item("Обновить список", RefreshContestList),
            GridMenuCommand.Item("Удалить конкурс", DeleteSelectedContest));

        AttachGridPopup(gridContestWorksPreview, viewContestWorksPreview, "Работы / рейтинг",
            GridMenuCommand.Item("Открыть конкурс", () => SelectContestFromList(switchToWorkPage: true)),
            GridMenuCommand.Item("Перейти в итоги", () => NavigateToSection(4)),
            GridMenuCommand.Item("Обновить карточку", () => RefreshContestCard(CurrentContest)),
            GridMenuCommand.Item("Сохранить пакет отчётов", ExportCurrentContestReportPackage));

        AttachGridPopup(gridContestVotersPreview, viewContestVotersPreview, "Голосующие / контроль",
            GridMenuCommand.Item("Перейти в контроль голосования", () => NavigateToSection(3)),
            GridMenuCommand.Item("Обновить карточку", () => RefreshContestCard(CurrentContest)),
            GridMenuCommand.Item("Скопировать должников", CopyDebtorsToClipboard),
            GridMenuCommand.Item("Добавить неизвестных в судьи", AddUnknownVotersToSettings));

        AttachGridPopup(grid, viewVotes, "Голоса конкурса",
            GridMenuCommand.Item("Проверить текст", PreviewVotes),
            GridMenuCommand.Item("Принять в базу", ImportVotes),
            GridMenuCommand.Item("Авточек правил", () => SaveVotePreviewReport(openFolder: true)),
            GridMenuCommand.Item("Показать базу конкурса", LoadGridFromStore),
            GridMenuCommand.Item("Очистить голоса конкурса", ClearContestVotes),
            GridMenuCommand.Item("Сформировать Excel", GenerateExcel));

        AttachGridPopup(gridWorks, viewWorks, "Работы конкурса",
            GridMenuCommand.Item("Добавить работу", AddWorkRow),
            GridMenuCommand.Item("Удалить выбранные работы", DeleteSelectedWorkRows),
            GridMenuCommand.Item("№ работ из голосов", AddWorkNumbersFromVotes),
            GridMenuCommand.Item("Импорт работ из текста", ImportWorksFromTextDialog),
            GridMenuCommand.Item("Принять одну работу", ReceiveSingleWorkDialog),
            GridMenuCommand.Item("Центр правил", OpenContestRulesCenter),
            GridMenuCommand.Item("Сохранить настройки конкурса", () => SaveContestSettings(log: true)));

        AttachGridPopup(gridVoters, viewVoters, "Голосующие / судьи",
            GridMenuCommand.Item("Добавить голосующего", () => _votersBinding.Add(new VoterSetting())),
            GridMenuCommand.Item("Голосующие из базы", AddVotersFromStoredVotes),
            GridMenuCommand.Item("Авторов в судьи", AddAuthorsToVoters),
            GridMenuCommand.Item("Центр правил", OpenContestRulesCenter),
            GridMenuCommand.Item("Сохранить настройки конкурса", () => SaveContestSettings(log: true)));

        AttachGridPopup(gridStatus, viewStatus, "Контроль голосования",
            GridMenuCommand.Item("Обновить контроль", () => RefreshVoteStatus(log: true)),
            GridMenuCommand.Item("Скопировать должников", CopyDebtorsToClipboard),
            GridMenuCommand.Item("Сохранить отчёт контроля", ExportCurrentContestReportPackage),
            GridMenuCommand.Item("Добавить неизвестных в судьи", AddUnknownVotersToSettings),
            GridMenuCommand.Item("Сохранить настройки", () => SaveContestSettings(log: true)));

        AttachGridPopup(gridResults, viewResults, "Итоги",
            GridMenuCommand.Item("Обновить итоги", () => RefreshResults(log: true)),
            GridMenuCommand.Item("Скопировать протокол", CopyFinalReportToClipboard),
            GridMenuCommand.Item("Скопировать победителей", CopyWinnersToClipboard),
            GridMenuCommand.Item("Сформировать Excel", GenerateExcel),
            GridMenuCommand.Item("Сохранить пакет отчётов", ExportCurrentContestReportPackage),
            GridMenuCommand.Item("HTML-протокол", () => ExportCurrentContestHtml(openPrintVersion: false)),
            GridMenuCommand.Item("Печать HTML", () => ExportCurrentContestHtml(openPrintVersion: true)),
            GridMenuCommand.Item("Открыть отчёты", OpenReportsFolder));

        AttachGridPopup(gridRuleChanges, viewRuleChanges, "Правки правил",
            GridMenuCommand.Item("Обновить правки", () => RefreshRuleChanges(log: true)),
            GridMenuCommand.Item("Скопировать правки", CopyRuleChangesToClipboard),
            GridMenuCommand.Item("Сохранить CSV", () => ExportRuleChangesCsv(openFolder: true)));

        AttachGridPopup(gridPeople, viewPeople, "Участники",
            GridMenuCommand.Item("Обновить участников", () => RefreshPeopleDirectory(log: true)),
            GridMenuCommand.Item("Назначить ведущим", () => AssignSelectedPersonAsHost(nextHost: false)),
            GridMenuCommand.Item("Назначить следующим ведущим", () => AssignSelectedPersonAsHost(nextHost: true)),
            GridMenuCommand.Item("Добавить в обязательные судьи", AddSelectedPeopleToVoters),
            GridMenuCommand.Item("Авторов в судьи", () =>
            {
                AddAuthorsToVoters();
                SaveContestSettings(log: false);
                RefreshPeopleDirectory(log: true);
            }));
    }

    private void AttachGridPopup(GridControl? gridControl, GridView? view, string title, params GridMenuCommand[] commands)
    {
        if (gridControl is null || view is null)
            return;

        var menu = new ContextMenuStrip
        {
            Name = "popup" + gridControl.Name,
            ShowImageMargin = false
        };

        menu.Items.Add(new ToolStripMenuItem(title) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        AddGridSpecificPopupCommands(menu, commands);

        menu.Items.Add(new ToolStripSeparator());
        AddGridCommonPopupCommands(menu, view, title);
        gridControl.ContextMenuStrip = menu;

        view.MouseDown += (_, e) => FocusGridRowOnRightClick(view, e);
    }

    private void AddGridSpecificPopupCommands(ContextMenuStrip menu, IReadOnlyList<GridMenuCommand> commands)
    {
        int actionCount = commands.Count(x => !x.IsSeparator);
        if (actionCount <= 4)
        {
            AddGridCommandItems(menu.Items, commands);
            return;
        }

        var primary = new List<GridMenuCommand>();
        var extra = new List<GridMenuCommand>();
        int visibleActions = 0;

        foreach (var command in commands)
        {
            if (!command.IsSeparator && visibleActions < 3)
            {
                primary.Add(command);
                visibleActions++;
                continue;
            }

            if (command.IsSeparator && visibleActions < 3)
            {
                // не засоряем верхнее меню разделителями до минимума команд
                continue;
            }

            extra.Add(command);
        }

        AddGridCommandItems(menu.Items, primary);

        var more = new ToolStripMenuItem("Ещё команды...");
        AddGridCommandItems(more.DropDownItems, TrimSeparators(extra));
        if (more.DropDownItems.Count > 0)
            menu.Items.Add(more);
    }

    private void AddGridCommandItems(ToolStripItemCollection items, IEnumerable<GridMenuCommand> commands)
    {
        foreach (var command in TrimSeparators(commands))
        {
            if (command.IsSeparator)
            {
                items.Add(new ToolStripSeparator());
                continue;
            }

            var item = new ToolStripMenuItem(command.Text);
            item.Click += (_, _) => RunGridPopupCommand(command.Action);
            items.Add(item);
        }
    }

    private static IReadOnlyList<GridMenuCommand> TrimSeparators(IEnumerable<GridMenuCommand> commands)
    {
        var list = commands.ToList();
        while (list.Count > 0 && list[0].IsSeparator)
            list.RemoveAt(0);
        while (list.Count > 0 && list[^1].IsSeparator)
            list.RemoveAt(list.Count - 1);

        for (int i = list.Count - 1; i > 0; i--)
        {
            if (list[i].IsSeparator && list[i - 1].IsSeparator)
                list.RemoveAt(i);
        }

        return list;
    }

    private void AddGridCommonPopupCommands(ContextMenuStrip menu, GridView view, string title)
    {
        var copyRow = new ToolStripMenuItem("Копировать строку");
        copyRow.Click += (_, _) => RunGridPopupCommand(() => CopyFocusedGridRow(view));
        menu.Items.Add(copyRow);

        var exportCsv = new ToolStripMenuItem("Экспорт CSV...");
        exportCsv.Click += (_, _) => RunGridPopupCommand(() => ExportGridCsv(view, title));
        menu.Items.Add(exportCsv);

        var bestFit = new ToolStripMenuItem("Автоширина колонок");
        bestFit.Click += (_, _) => RunGridPopupCommand(view.BestFitColumns);
        menu.Items.Add(bestFit);

        var more = new ToolStripMenuItem("Ещё...");
        AddSimplePopupItem(more.DropDownItems, "Копировать ячейку", () => CopyFocusedGridCell(view));
        AddSimplePopupItem(more.DropDownItems, "Копировать выбранные строки", () => CopyGridRows(view, selectedOnly: true));
        AddSimplePopupItem(more.DropDownItems, "Копировать все видимые строки", () => CopyGridRows(view, selectedOnly: false));
        more.DropDownItems.Add(new ToolStripSeparator());
        AddSimplePopupItem(more.DropDownItems, "Сбросить фильтр", () => view.ActiveFilter.Clear());
        AddSimplePopupItem(more.DropDownItems, "Сбросить сортировку", view.ClearSorting);
        AddSimplePopupItem(more.DropDownItems, "Сбросить группировку", () => ClearGridGrouping(view));
        AddSimplePopupItem(more.DropDownItems, "Развернуть группы", view.ExpandAllGroups);
        AddSimplePopupItem(more.DropDownItems, "Свернуть группы", view.CollapseAllGroups);
        more.DropDownItems.Add(new ToolStripSeparator());
        AddSimplePopupItem(more.DropDownItems, "Сохранить layout всех таблиц", () => SaveLayoutToSettingsBlob(log: true));
        AddSimplePopupItem(more.DropDownItems, "Сбросить layout всех таблиц", ResetLayoutSettingsBlob);
        menu.Items.Add(more);
    }

    private void AddSimplePopupItem(ToolStripItemCollection items, string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => RunGridPopupCommand(action);
        items.Add(item);
    }

    private void RunGridPopupCommand(Action? action)
    {
        if (action is null)
            return;

        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Команда таблицы", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка команды таблицы: " + ex.Message);
        }
    }

    private static void FocusGridRowOnRightClick(GridView view, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        var hit = view.CalcHitInfo(e.Location);
        if (!hit.InRow && !hit.InRowCell)
            return;

        view.FocusedRowHandle = hit.RowHandle;
        if (!view.OptionsSelection.MultiSelect || !view.IsRowSelected(hit.RowHandle))
        {
            view.ClearSelection();
            view.SelectRow(hit.RowHandle);
        }
    }

    private static void CopyFocusedGridCell(GridView view)
    {
        if (view.FocusedColumn is null || !view.IsDataRow(view.FocusedRowHandle))
            return;

        string text = view.GetRowCellDisplayText(view.FocusedRowHandle, view.FocusedColumn) ?? string.Empty;
        Clipboard.SetText(text);
    }

    private static void CopyFocusedGridRow(GridView view)
    {
        if (!view.IsDataRow(view.FocusedRowHandle))
            return;

        Clipboard.SetText(BuildGridText(view, new[] { view.FocusedRowHandle }, separator: "\t", csv: false));
    }

    private static void CopyGridRows(GridView view, bool selectedOnly)
    {
        List<int> rows = selectedOnly ? GetSelectedDataRows(view) : GetVisibleDataRows(view);
        if (rows.Count == 0)
            return;

        Clipboard.SetText(BuildGridText(view, rows, separator: "\t", csv: false));
    }

    private void ExportGridCsv(GridView view, string title)
    {
        List<int> rows = GetVisibleDataRows(view);
        if (rows.Count == 0)
        {
            MessageBox.Show(this, "В таблице нет видимых строк для экспорта.", "Экспорт CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт таблицы в CSV",
            Filter = "CSV UTF-8 (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
            FileName = $"{SanitizeFilePart(title)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllText(dialog.FileName, BuildGridText(view, rows, separator: ";", csv: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Log($"Таблица \"{title}\" экспортирована: {dialog.FileName}");
    }

    private static List<int> GetSelectedDataRows(GridView view)
    {
        var rows = view.GetSelectedRows()
            .Where(view.IsDataRow)
            .Distinct()
            .ToList();

        if (rows.Count == 0 && view.IsDataRow(view.FocusedRowHandle))
            rows.Add(view.FocusedRowHandle);

        return rows;
    }

    private static List<int> GetVisibleDataRows(GridView view)
    {
        var rows = new List<int>();
        for (int i = 0; i < view.DataRowCount; i++)
        {
            int rowHandle = view.GetVisibleRowHandle(i);
            if (view.IsDataRow(rowHandle))
                rows.Add(rowHandle);
        }

        return rows;
    }

    private static List<GridColumn> GetVisibleGridColumns(GridView view)
    {
        var columns = new List<GridColumn>();
        for (int i = 0; i < view.Columns.Count; i++)
        {
            GridColumn column = view.Columns[i];
            if (column.Visible && column.VisibleIndex >= 0)
                columns.Add(column);
        }

        return columns.OrderBy(x => x.VisibleIndex).ToList();
    }

    private static string BuildGridText(GridView view, IEnumerable<int> rowHandles, string separator, bool csv)
    {
        var columns = GetVisibleGridColumns(view);
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(separator, columns.Select(x => FormatGridValue(x.Caption, csv))));

        foreach (int rowHandle in rowHandles)
        {
            var cells = columns.Select(column => FormatGridValue(view.GetRowCellDisplayText(rowHandle, column), csv));
            sb.AppendLine(string.Join(separator, cells));
        }

        return sb.ToString();
    }

    private static string FormatGridValue(string? value, bool csv)
    {
        string text = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        return csv ? EscapeCsv(text) : text;
    }

    private static void ClearGridGrouping(GridView view)
    {
        view.BeginUpdate();
        try
        {
            for (int i = 0; i < view.Columns.Count; i++)
                view.Columns[i].GroupIndex = -1;
        }
        finally
        {
            view.EndUpdate();
        }
    }

    private sealed class GridMenuCommand
    {
        private GridMenuCommand(string text, Action? action, bool isSeparator)
        {
            Text = text;
            Action = action;
            IsSeparator = isSeparator;
        }

        public string Text { get; }
        public Action? Action { get; }
        public bool IsSeparator { get; }

        public static GridMenuCommand Item(string text, Action action) => new(text, action, isSeparator: false);
        public static GridMenuCommand Separator() => new(string.Empty, null, isSeparator: true);
    }



    private Control BuildContestListPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Список всех конкурсов. Один клик выбирает конкурс, справа/снизу сразу видна карточка: работы, судьи, голоса и готовность конкурса."
        }, 0, 0);

        var split = new SplitContainer
        {
            Name = "splitContestListPreview",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 315
        };

        gridContests = CreateGrid("gridContests", out viewContests, readOnly: true, multiSelect: false);
        viewContests.FocusedRowChanged += (_, _) => SelectContestFromList(switchToWorkPage: false);
        viewContests.DoubleClick += (_, _) => SelectContestFromList(switchToWorkPage: true);

        AddGridColumn(viewContests, nameof(ContestListRow.Number), "№", 70);
        AddGridColumn(viewContests, nameof(ContestListRow.Name), "Название конкурса", 300);
        AddGridColumn(viewContests, nameof(ContestListRow.ActiveText), "Статус", 90);
        AddGridColumn(viewContests, nameof(ContestListRow.StageText), "Стадия", 185);
        AddGridColumn(viewContests, nameof(ContestListRow.TopicCount), "Тем", 65);
        AddGridColumn(viewContests, nameof(ContestListRow.WorkCount), "Работ", 75);
        AddGridColumn(viewContests, nameof(ContestListRow.VoterCount), "Судей", 75);
        AddGridColumn(viewContests, nameof(ContestListRow.VoteCount), "Принято голосов", 125);
        AddGridColumn(viewContests, nameof(ContestListRow.AuthorMode), "Режим", 190);
        AddGridColumn(viewContests, nameof(ContestListRow.UpdatedText), "Обновлён", 140);

        split.Panel1.Controls.Add(gridContests);
        split.Panel2.Controls.Add(BuildContestPreviewPanel());
        root.Controls.Add(split, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        var btnOpen = new Button { Text = "Открыть выбранный", Width = 175, Height = 34 };
        btnOpen.Click += (_, _) => SelectContestFromList(switchToWorkPage: true);

        var btnNew = new Button { Text = "Новый конкурс", Width = 145, Height = 34 };
        btnNew.Click += (_, _) => CreateContest();

        var btnImportContest = new Button { Text = "Импорт конкурса из текста", Width = 225, Height = 34 };
        btnImportContest.Click += (_, _) => ImportContestFromTextDialog();

        var btnImportFdb = new Button { Text = "Импорт старой *.fdb", Width = 185, Height = 34 };
        btnImportFdb.Click += (_, _) => ImportLegacyFirebirdDialog();

        var btnRefresh = new Button { Text = "Обновить список", Width = 150, Height = 34 };
        btnRefresh.Click += (_, _) => RefreshContestList();

        var btnDelete = new Button { Text = "Удалить конкурс", Width = 150, Height = 34 };
        btnDelete.Click += (_, _) => DeleteSelectedContest();

        var btnSettings = new Button { Text = "Настройки конкурса", Width = 180, Height = 34 };
        btnSettings.Click += (_, _) =>
        {
            SelectContestFromList(switchToWorkPage: false);
            if (mainTabs.TabPages.Count > 2)
                NavigateToSection(2);
        };

        var btnRulesCenter = new Button { Text = "Центр правил", Width = 140, Height = 34 };
        btnRulesCenter.Click += (_, _) =>
        {
            SelectContestFromList(switchToWorkPage: false);
            OpenContestRulesCenter();
        };

        var btnResults = new Button { Text = "Итоги", Width = 110, Height = 34 };
        btnResults.Click += (_, _) =>
        {
            SelectContestFromList(switchToWorkPage: false);
            if (mainTabs.TabPages.Count > 4)
                NavigateToSection(4);
        };

        var btnSaveLayout = new Button { Text = "Сохранить layout", Width = 155, Height = 34 };
        btnSaveLayout.Click += (_, _) => SaveLayoutToSettingsBlob(log: true);

        var btnResetLayout = new Button { Text = "Сбросить layout", Width = 145, Height = 34 };
        btnResetLayout.Click += (_, _) => ResetLayoutSettingsBlob();

        buttons.Controls.AddRange(new Control[] { btnOpen, btnNew, btnImportContest, btnImportFdb, btnRefresh, btnDelete, btnSettings, btnRulesCenter, btnResults, btnSaveLayout, btnResetLayout });
        root.Controls.Add(buttons, 0, 2);
        return root;
    }

    private Control BuildContestPreviewPanel()
    {
        var group = new GroupBox { Text = "Карточка выбранного конкурса", Dock = DockStyle.Fill };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        group.Controls.Add(root);

        txtContestCard = new TextBox
        {
            Name = "txtContestCard",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true
        };
        root.Controls.Add(txtContestCard, 0, 0);

        var tabs = new TabControl { Name = "tabsContestPreview", Dock = DockStyle.Fill };
        var worksPage = CreateTabPage("Работы / рейтинг");
        var votersPage = CreateTabPage("Голосующие / контроль");

        gridContestWorksPreview = CreateGrid("gridContestWorksPreview", out viewContestWorksPreview, readOnly: true, multiSelect: false);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.PlaceText), "Место", 75);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.WorkNoText), "№", 60);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.Title), "Работа", 260);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.Author), "Автор", 200);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.Topic), "Тема", 150);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.Rate), "Итог", 70);
        AddGridColumn(viewContestWorksPreview, nameof(ContestWorkPreviewRow.AcceptedVotes), "Гол.", 65);
        worksPage.Controls.Add(gridContestWorksPreview);

        gridContestVotersPreview = CreateGrid("gridContestVotersPreview", out viewContestVotersPreview, readOnly: true, multiSelect: false);
        AddGridColumn(viewContestVotersPreview, nameof(ContestVoterPreviewRow.VoterName), "Голосующий", 220);
        AddGridColumn(viewContestVotersPreview, nameof(ContestVoterPreviewRow.Status), "Статус", 155);
        AddGridColumn(viewContestVotersPreview, nameof(ContestVoterPreviewRow.AcceptedVotes), "Голосов", 80);
        AddGridColumn(viewContestVotersPreview, nameof(ContestVoterPreviewRow.MissingWorks), "Пропущено", 150);
        AddGridColumn(viewContestVotersPreview, nameof(ContestVoterPreviewRow.Note), "Примечание", 260);
        votersPage.Controls.Add(gridContestVotersPreview);

        tabs.TabPages.Add(worksPage);
        tabs.TabPages.Add(votersPage);
        root.Controls.Add(tabs, 0, 1);
        return group;
    }

    private Control BuildVotePage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));

        root.Controls.Add(BuildContestPanel(), 0, 0);
        root.Controls.Add(BuildPastePanel(), 0, 1);
        root.Controls.Add(BuildCommandPanel(), 0, 2);
        root.Controls.Add(BuildGridPanel(), 0, 3);
        root.Controls.Add(BuildLogPanel(), 0, 4);
        return root;
    }

    private Control BuildContestPanel()
    {
        var group = new GroupBox { Text = "Конкурс и файлы", Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 4,
            Padding = new Padding(8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        group.Controls.Add(panel);

        cboContests = new System.Windows.Forms.ComboBox { Name = "cboContests", Dock = DockStyle.Fill, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
        cboContests.SelectedIndexChanged += (_, _) => ApplySelectedContest();

        txtContestNo = new TextBox { Dock = DockStyle.Fill };
        txtContestName = new TextBox { Dock = DockStyle.Fill };
        txtTemplate = new TextBox { Dock = DockStyle.Fill };
        txtOutput = new TextBox { Dock = DockStyle.Fill };

        var btnNew = new Button { Text = "Новый конкурс", Dock = DockStyle.Fill };
        btnNew.Click += (_, _) => CreateContest();

        var btnSave = new Button { Text = "Сохранить", Dock = DockStyle.Fill };
        btnSave.Click += (_, _) => SaveContest();

        var btnTemplate = new Button { Text = "Образец...", Dock = DockStyle.Fill };
        btnTemplate.Click += (_, _) => SelectTemplate();

        var btnOutput = new Button { Text = "Куда сохранить...", Dock = DockStyle.Fill };
        btnOutput.Click += (_, _) => SelectOutputFolder();

        panel.Controls.Add(new Label { Text = "Конкурс:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        panel.Controls.Add(cboContests, 1, 0);
        panel.SetColumnSpan(cboContests, 3);
        panel.Controls.Add(btnNew, 4, 0);
        panel.Controls.Add(btnSave, 5, 0);

        panel.Controls.Add(new Label { Text = "№ конкурса:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        panel.Controls.Add(txtContestNo, 1, 1);
        panel.Controls.Add(new Label { Text = "Название:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
        panel.Controls.Add(txtContestName, 3, 1);
        panel.SetColumnSpan(txtContestName, 3);

        panel.Controls.Add(new Label { Text = "Файл-образец:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        panel.Controls.Add(txtTemplate, 1, 2);
        panel.SetColumnSpan(txtTemplate, 3);
        panel.Controls.Add(btnTemplate, 4, 2);
        panel.SetColumnSpan(btnTemplate, 2);

        panel.Controls.Add(new Label { Text = "Итоговая папка:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        panel.Controls.Add(txtOutput, 1, 3);
        panel.SetColumnSpan(txtOutput, 3);
        panel.Controls.Add(btnOutput, 4, 3);
        panel.SetColumnSpan(btnOutput, 2);

        return group;
    }

    private Control BuildPastePanel()
    {
        var group = new GroupBox { Text = "Вставь голосование сюда: один голосующий или сразу вся лента", Dock = DockStyle.Fill };
        txtVotes = new TextBox
        {
            Name = "txtVotes",
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false
        };
        group.Controls.Add(txtVotes);
        return group;
    }

    private Control BuildCommandPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = true,
            AutoScroll = true
        };

        var btnPreview = new Button { Text = "Проверить текст", Width = 150, Height = 34 };
        btnPreview.Click += (_, _) => PreviewVotes();

        var btnImport = new Button { Text = "Принять в базу", Width = 150, Height = 34 };
        btnImport.Click += (_, _) => ImportVotes();

        var btnPreviewReport = new Button { Text = "Авточек правил", Width = 150, Height = 34 };
        btnPreviewReport.Click += (_, _) => SaveVotePreviewReport(openFolder: true);

        var btnGenerate = new Button { Text = "Сформировать Excel и открыть", Width = 230, Height = 34 };
        btnGenerate.Click += (_, _) => GenerateExcel();

        var btnLoad = new Button { Text = "Показать базу конкурса", Width = 180, Height = 34 };
        btnLoad.Click += (_, _) => LoadGridFromStore();

        var btnClear = new Button { Text = "Очистить голоса конкурса", Width = 200, Height = 34 };
        btnClear.Click += (_, _) => ClearContestVotes();

        var btnRmDb = new Button { Text = "Открыть базу проекта", Width = 180, Height = 34 };
        btnRmDb.Click += (_, _) => OpenRhymeMachineDatabase();

        panel.Controls.AddRange(new Control[] { btnPreview, btnImport, btnPreviewReport, btnGenerate, btnLoad, btnClear, btnRmDb });
        return panel;
    }

    private Control BuildGridPanel()
    {
        var group = new GroupBox { Text = "Данные конкурса", Dock = DockStyle.Fill };
        grid = CreateGrid("gridVotes", out viewVotes, readOnly: true, multiSelect: false);
        AddGridColumn(viewVotes, nameof(GridRow.VoterName), "Голосующий", 220);
        AddGridColumn(viewVotes, nameof(GridRow.WorkNo), "№ работы", 90);
        AddGridColumn(viewVotes, nameof(GridRow.VotedScoreText), "Проголосовал", 105);
        AddGridColumn(viewVotes, nameof(GridRow.AcceptedScoreText), "Принято", 90);
        AddGridColumn(viewVotes, nameof(GridRow.Score), "Итог", 70);
        AddGridColumn(viewVotes, nameof(GridRow.RuleNote), "Правило", 220);
        AddGridColumn(viewVotes, nameof(GridRow.Comment), "Комментарий", 260);
        AddGridColumn(viewVotes, nameof(GridRow.UpdatedAt), "Обновлено", 150);
        group.Controls.Add(grid);
        return group;
    }

    private Control BuildLogPanel()
    {
        var group = new GroupBox { Text = "Журнал", Dock = DockStyle.Fill };
        txtLog = new TextBox
        {
            Name = "txtLog",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        };
        group.Controls.Add(txtLog);
        return group;
    }

    private Control BuildSettingsPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        var info = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Здесь задаются правила, работы и голосующие. Excel-образец может быть пустой: программа сама заполнит A - №, B - работа, C - автор, D - итог, E... - голосующие."
        };
        root.Controls.Add(info, 0, 0);
        root.Controls.Add(BuildContestRulesPanel(), 0, 1);

        var split = new SplitContainer
        {
            Name = "splitSettingsWorksVoters",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300
        };
        split.Panel1.Controls.Add(BuildWorksPanel());
        split.Panel2.Controls.Add(BuildVotersPanel());
        root.Controls.Add(split, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = true
        };

        var btnAddWork = new Button { Text = "Добавить работу", Width = 130, Height = 34 };
        btnAddWork.Click += (_, _) => AddWorkRow();

        var btnDeleteWork = new Button { Text = "Удалить работу", Width = 130, Height = 34 };
        btnDeleteWork.Click += (_, _) => DeleteSelectedWorkRows();

        var btnWorksFromVotes = new Button { Text = "№ работ из голосов", Width = 150, Height = 34 };
        btnWorksFromVotes.Click += (_, _) => AddWorkNumbersFromVotes();

        var btnImportWorks = new Button { Text = "Импорт работ из текста", Width = 190, Height = 34 };
        btnImportWorks.Click += (_, _) => ImportWorksFromTextDialog();

        var btnReceiveWork = new Button { Text = "Принять работу", Width = 150, Height = 34 };
        btnReceiveWork.Click += (_, _) => ReceiveSingleWorkDialog();

        var btnAddVoter = new Button { Text = "Добавить голосующего", Width = 165, Height = 34 };
        btnAddVoter.Click += (_, _) => _votersBinding.Add(new VoterSetting());

        var btnVotersFromVotes = new Button { Text = "Голосующие из базы", Width = 165, Height = 34 };
        btnVotersFromVotes.Click += (_, _) => AddVotersFromStoredVotes();

        var btnAuthorsToVoters = new Button { Text = "Авторов в судьи", Width = 150, Height = 34 };
        btnAuthorsToVoters.Click += (_, _) => AddAuthorsToVoters();

        var btnRulesCenter = new Button { Text = "Центр правил", Width = 140, Height = 34 };
        btnRulesCenter.Click += (_, _) => OpenContestRulesCenter();

        var btnSaveSettings = new Button { Text = "Сохранить настройки конкурса", Width = 220, Height = 34 };
        btnSaveSettings.Click += (_, _) => SaveContestSettings();

        buttons.Controls.AddRange(new Control[]
        {
            btnRulesCenter,
            btnAddWork,
            btnDeleteWork,
            btnWorksFromVotes,
            btnImportWorks,
            btnReceiveWork,
            btnAddVoter,
            btnVotersFromVotes,
            btnAuthorsToVoters,
            btnSaveSettings
        });
        root.Controls.Add(buttons, 0, 3);

        return root;
    }


    private Control BuildControlPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Контроль сравнивает список голосующих из настроек с фактическими голосами в базе. Должники подсвечиваются розовым, полный голос - зелёным, неизвестные голосующие - жёлтым."
        }, 0, 0);

        gridStatus = CreateGrid("gridStatus", out viewStatus, readOnly: true, multiSelect: false);
        viewStatus.RowStyle += (_, e) => PaintStatusRow(e);

        AddGridColumn(viewStatus, nameof(VoterStatusRow.VoterName), "Голосующий", 220);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.Status), "Статус", 170);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.RequiredToVote), "Обяз.", 75);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.AcceptedVotes), "Голосов", 85);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.MissingWorks), "Пропущено", 150);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.UnknownWorks), "Лишние №", 110);
        AddGridColumn(viewStatus, nameof(VoterStatusRow.Note), "Примечание", 280);


        root.Controls.Add(gridStatus, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };

        var btnRefresh = new Button { Text = "Обновить контроль", Width = 170, Height = 34 };
        btnRefresh.Click += (_, _) => RefreshVoteStatus();

        var btnCopyDebtors = new Button { Text = "Скопировать должников", Width = 210, Height = 34 };
        btnCopyDebtors.Click += (_, _) => CopyDebtorsToClipboard();

        var btnExportAudit = new Button { Text = "Сохранить отчёт контроля", Width = 210, Height = 34 };
        btnExportAudit.Click += (_, _) => ExportCurrentContestReportPackage();

        var btnAddUnknown = new Button { Text = "Добавить неизвестных в судьи", Width = 235, Height = 34 };
        btnAddUnknown.Click += (_, _) => AddUnknownVotersToSettings();

        var btnSaveSettings = new Button { Text = "Сохранить настройки", Width = 180, Height = 34 };
        btnSaveSettings.Click += (_, _) => SaveContestSettings();

        buttons.Controls.AddRange(new Control[] { btnRefresh, btnCopyDebtors, btnExportAudit, btnAddUnknown, btnSaveSettings });
        root.Controls.Add(buttons, 0, 2);
        return root;
    }


    private Control BuildRuleChangesPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        txtRuleChangeSummary = new TextBox
        {
            Name = "txtRuleChangeSummary",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Text = "Выбери конкурс, чтобы увидеть все оценки, которые автоправила приняли иначе: лимит 4, самоголосы, 3+ = 3.5 и прочие корректировки."
        };
        root.Controls.Add(txtRuleChangeSummary, 0, 0);

        gridRuleChanges = CreateGrid("gridRuleChanges", out viewRuleChanges, readOnly: true, multiSelect: false);
        viewRuleChanges.RowStyle += (_, e) => PaintRuleChangeRow(e);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.VoterName), "Голосующий", 220);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.WorkNoText), "№", 70);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.WorkTitle), "Работа", 240);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.Topic), "Тема", 180);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.OriginalScoreText), "Оригинал", 90);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.AcceptedScoreText), "Принято", 90);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.DeltaText), "Δ", 70);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.RuleNote), "Причина", 360);
        AddGridColumn(viewRuleChanges, nameof(RuleChangeRow.Comment), "Комментарий", 260);
        root.Controls.Add(gridRuleChanges, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };

        var btnRefresh = new Button { Text = "Обновить правки", Width = 170, Height = 34 };
        btnRefresh.Click += (_, _) => RefreshRuleChanges(log: true);

        var btnCopy = new Button { Text = "Скопировать правки", Width = 190, Height = 34 };
        btnCopy.Click += (_, _) => CopyRuleChangesToClipboard();

        var btnExport = new Button { Text = "Сохранить CSV", Width = 150, Height = 34 };
        btnExport.Click += (_, _) => ExportRuleChangesCsv(openFolder: true);

        buttons.Controls.AddRange(new Control[] { btnRefresh, btnCopy, btnExport });
        root.Controls.Add(buttons, 0, 2);
        return root;
    }


    private Control BuildResultsPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };

        var btnRefresh = new Button { Text = "Обновить итоги", Width = 160, Height = 34 };
        btnRefresh.Click += (_, _) => RefreshResults();

        var btnCopyReport = new Button { Text = "Скопировать протокол", Width = 190, Height = 34 };
        btnCopyReport.Click += (_, _) => CopyFinalReportToClipboard();

        var btnCopyWinners = new Button { Text = "Скопировать победителей", Width = 210, Height = 34 };
        btnCopyWinners.Click += (_, _) => CopyWinnersToClipboard();

        var btnGenerate = new Button { Text = "Сформировать Excel и открыть", Width = 230, Height = 34 };
        btnGenerate.Click += (_, _) => GenerateExcel();

        var btnExportPackage = new Button { Text = "Сохранить пакет отчётов", Width = 210, Height = 34 };
        btnExportPackage.Click += (_, _) => ExportCurrentContestReportPackage();

        var btnHtml = new Button { Text = "HTML-протокол", Width = 160, Height = 34 };
        btnHtml.Click += (_, _) => ExportCurrentContestHtml(openPrintVersion: false);

        var btnPrint = new Button { Text = "Печать HTML", Width = 140, Height = 34 };
        btnPrint.Click += (_, _) => ExportCurrentContestHtml(openPrintVersion: true);

        var btnOpenReports = new Button { Text = "Открыть отчёты", Width = 140, Height = 34 };
        btnOpenReports.Click += (_, _) => OpenReportsFolder();

        buttons.Controls.AddRange(new Control[] { btnRefresh, btnCopyReport, btnCopyWinners, btnGenerate, btnExportPackage, btnHtml, btnPrint, btnOpenReports });
        root.Controls.Add(buttons, 0, 0);

        gridResults = CreateGrid("gridResults", out viewResults, readOnly: true, multiSelect: false);
        viewResults.RowStyle += (_, e) => PaintResultRow(e);

        AddGridColumn(viewResults, nameof(ContestRatingRow.PlaceText), "Место", 75);
        AddGridColumn(viewResults, nameof(ContestRatingRow.WorkNoText), "№", 60);
        AddGridColumn(viewResults, nameof(ContestRatingRow.Title), "Работа", 280);
        AddGridColumn(viewResults, nameof(ContestRatingRow.Author), "Автор", 210);
        AddGridColumn(viewResults, nameof(ContestRatingRow.Topic), "Тема", 150);
        AddGridColumn(viewResults, nameof(ContestRatingRow.Rate), "Итог", 75);
        AddGridColumn(viewResults, nameof(ContestRatingRow.AcceptedVotes), "Гол.", 70);
        AddGridColumn(viewResults, nameof(ContestRatingRow.AverageText), "Средн.", 80);
        AddGridColumn(viewResults, nameof(ContestRatingRow.MaxVotes), "Max", 65);
        AddGridColumn(viewResults, nameof(ContestRatingRow.SelfVotes), "Сам", 65);


        root.Controls.Add(gridResults, 0, 1);

        txtFinalReport = new TextBox
        {
            Name = "txtFinalReport",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false
        };
        root.Controls.Add(txtFinalReport, 0, 2);

        return root;
    }

    private Control BuildContestRulesPanel()
    {
        var group = new GroupBox { Text = "Правила конкурса", Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 3,
            Padding = new Padding(8)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        nudVoteLimit = CreateSmallNumber(0, 999, 0);
        nudBaseVote = CreateSmallNumber(0, 9, 3);
        nudMaxVote = CreateSmallNumber(1, 9, 4);
        nudLimitMaxVote = CreateSmallNumber(0, 999, 0);

        chkAllowZeroVotes = new CheckBox { Text = "Принимать 0", AutoSize = true, Dock = DockStyle.Fill };
        chkSelfVoteZero = new CheckBox { Text = "Голос за себя = 0", Checked = true, AutoSize = true, Dock = DockStyle.Fill };
        chkLimitMaxVoteByTopic = new CheckBox { Text = "Лимит по темам", AutoSize = true, Dock = DockStyle.Fill };
        chkOneMaxVotePerTopic = new CheckBox { Text = "Одна максимальная в теме", AutoSize = true, Dock = DockStyle.Fill };
        chkDowngradeExtraMaxVote = new CheckBox { Text = "Лишние максимальные -> базовая", Checked = true, AutoSize = true, Dock = DockStyle.Fill };
        chkHostKnowsAuthors = new CheckBox { Text = "Учет ведет ведущий: авторы известны", Checked = true, AutoSize = true, Dock = DockStyle.Fill };

        AddRuleLabel(panel, "Макс. оценок:", 0, 0);
        panel.Controls.Add(nudVoteLimit, 1, 0);
        AddRuleLabel(panel, "Базовая:", 2, 0);
        panel.Controls.Add(nudBaseVote, 3, 0);
        AddRuleLabel(panel, "Максимальная:", 4, 0);
        panel.Controls.Add(nudMaxVote, 5, 0);
        AddRuleLabel(panel, "Лимит максим.:", 6, 0);
        panel.Controls.Add(nudLimitMaxVote, 7, 0);

        panel.Controls.Add(chkAllowZeroVotes, 0, 1);
        panel.SetColumnSpan(chkAllowZeroVotes, 2);
        panel.Controls.Add(chkSelfVoteZero, 2, 1);
        panel.SetColumnSpan(chkSelfVoteZero, 2);
        panel.Controls.Add(chkOneMaxVotePerTopic, 4, 1);
        panel.SetColumnSpan(chkOneMaxVotePerTopic, 2);
        panel.Controls.Add(chkLimitMaxVoteByTopic, 6, 1);
        panel.Controls.Add(chkDowngradeExtraMaxVote, 7, 1);

        panel.Controls.Add(chkHostKnowsAuthors, 0, 2);
        panel.SetColumnSpan(chkHostKnowsAuthors, 8);

        group.Controls.Add(panel);
        return group;
    }

    private static NumericUpDown CreateSmallNumber(int minimum, int maximum, int value)
    {
        return new NumericUpDown
        {
            Width = 68,
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Dock = DockStyle.Fill
        };
    }

    private static void AddRuleLabel(TableLayoutPanel panel, string text, int column, int row)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, column, row);
    }

    private Control BuildWorksPanel()
    {
        var group = new GroupBox { Text = "Работы конкурса", Dock = DockStyle.Fill };
        gridWorks = CreateGrid("gridWorks", out viewWorks, readOnly: false, multiSelect: true, allowNewRows: true);
        AddGridColumn(viewWorks, nameof(ContestWork.Number), "№", 80, editable: true);
        AddGridColumn(viewWorks, nameof(ContestWork.Title), "Название работы", 320, editable: true);
        AddGridColumn(viewWorks, nameof(ContestWork.Author), "Автор", 240, editable: true);
        AddGridColumn(viewWorks, nameof(ContestWork.Topic), "Тема", 170, editable: true);


        group.Controls.Add(gridWorks);
        return group;
    }

    private Control BuildVotersPanel()
    {
        var group = new GroupBox { Text = "Голосующие / судьи", Dock = DockStyle.Fill };
        gridVoters = CreateGrid("gridVoters", out viewVoters, readOnly: false, multiSelect: true, allowNewRows: true);
        AddGridColumn(viewVoters, nameof(VoterSetting.Name), "Имя голосующего", 360, editable: true);
        AddGridColumn(viewVoters, nameof(VoterSetting.MustVote), "Обязан голосовать", 150, editable: true);

        group.Controls.Add(gridVoters);
        return group;
    }

    private Control BuildPeoplePage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Сводный список людей текущего конкурса: ведущий, следующий ведущий, авторы работ, судьи и неизвестные голосующие."
        }, 0, 0);

        gridPeople = CreateGrid("gridPeople", out viewPeople, readOnly: true, multiSelect: true);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.Name), "Имя", 260);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.Roles), "Роли", 260);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.Works), "Работы", 220);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.AcceptedVotes), "Голосов", 80);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.MissingWorks), "Пропущено", 150);
        AddGridColumn(viewPeople, nameof(PersonDirectoryRow.Note), "Примечание", 320);
        root.Controls.Add(gridPeople, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        var btnRefresh = new Button { Text = "Обновить", Width = 115, Height = 34 };
        btnRefresh.Click += (_, _) => RefreshPeopleDirectory(log: true);

        var btnHost = new Button { Text = "Назначить ведущим", Width = 170, Height = 34 };
        btnHost.Click += (_, _) => AssignSelectedPersonAsHost(nextHost: false);

        var btnNextHost = new Button { Text = "Назначить следующим", Width = 185, Height = 34 };
        btnNextHost.Click += (_, _) => AssignSelectedPersonAsHost(nextHost: true);

        var btnVoter = new Button { Text = "В обязательные судьи", Width = 185, Height = 34 };
        btnVoter.Click += (_, _) => AddSelectedPeopleToVoters();

        var btnAuthors = new Button { Text = "Авторов в судьи", Width = 150, Height = 34 };
        btnAuthors.Click += (_, _) =>
        {
            AddAuthorsToVoters();
            SaveContestSettings(log: false);
            RefreshPeopleDirectory(log: true);
        };

        buttons.Controls.AddRange(new Control[] { btnRefresh, btnHost, btnNextHost, btnVoter, btnAuthors });
        root.Controls.Add(buttons, 0, 2);
        return root;
    }

    private void LoadState()
    {
        _settings = _store.LoadSettings();
        if (string.IsNullOrWhiteSpace(_settings.TemplatePath))
        {
            string[] bundledTemplates =
            {
                Path.Combine(AppContext.BaseDirectory, "Samples", "vote_counter_template.xlsx"),
                Path.Combine(AppContext.BaseDirectory, "Samples", "old_firebird_protocol_sample_1.xlsx"),
                Path.Combine(AppContext.BaseDirectory, "Samples", "old_firebird_protocol_sample_2.xlsx")
            };

            string? bundled = bundledTemplates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(bundled))
                _settings.TemplatePath = bundled;
        }

        if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            _settings.OutputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VoteCounter",
                "Results");
        }

        txtTemplate.Text = _settings.TemplatePath;
        txtOutput.Text = _settings.OutputFolder;

        ReloadContests();
        Log("Готово. Выбери конкурс, настрой работы/голосующих, вставь голосование и нажми \"Принять в базу\".");
    }

    private void ReloadContests(string? selectId = null)
    {
        _contests = _store.LoadContests();
        if (NormalizeContestLifecycle())
            _store.SaveContests(_contests);

        selectId ??= _contests.FirstOrDefault(x => x.IsActive)?.Id;
        cboContests.DataSource = null;
        cboContests.DataSource = _contests;
        SyncWorkflowContestList(selectId);

        if (!string.IsNullOrWhiteSpace(selectId))
        {
            var index = _contests.FindIndex(x => x.Id == selectId);
            if (index >= 0)
                cboContests.SelectedIndex = index;
        }

        ApplySelectedContest();
        RefreshContestList();
    }

    private bool NormalizeContestLifecycle()
    {
        if (_contests.Count == 0)
            return false;

        bool changed = false;
        foreach (Contest contest in _contests)
        {
            if (contest.StartedAt == default)
            {
                contest.StartedAt = contest.CreatedAt == default ? DateTime.Now : contest.CreatedAt;
                changed = true;
            }

            if (contest.StageUpdatedAt == default)
            {
                contest.StageUpdatedAt = contest.StartedAt;
                changed = true;
            }

            if (GetContestStage(contest) == ContestStage.Finished)
            {
                if (contest.IsActive)
                {
                    contest.IsActive = false;
                    changed = true;
                }

                if (contest.ClosedAt is null)
                {
                    contest.ClosedAt = contest.StageUpdatedAt;
                    changed = true;
                }
            }
        }

        Contest? active = _contests
            .Where(x => x.IsActive && GetContestStage(x) != ContestStage.Finished)
            .OrderByDescending(x => x.StageUpdatedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault()
            ?? _contests
                .Where(x => GetContestStage(x) != ContestStage.Finished)
                .OrderByDescending(x => x.StageUpdatedAt)
                .ThenByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

        foreach (Contest contest in _contests)
        {
            bool shouldBeActive = active is not null && string.Equals(contest.Id, active.Id, StringComparison.OrdinalIgnoreCase);
            if (contest.IsActive != shouldBeActive)
            {
                contest.IsActive = shouldBeActive;
                changed = true;
            }
        }

        return changed;
    }

    private void SetOnlyContestActive(Contest activeContest)
    {
        foreach (Contest contest in _contests)
            contest.IsActive = string.Equals(contest.Id, activeContest.Id, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySelectedContest()
    {
        var contest = CurrentContest;
        if (contest is null)
        {
            txtContestNo.Text = string.Empty;
            txtContestName.Text = string.Empty;
            _worksBinding = new BindingList<ContestWork>();
            _votersBinding = new BindingList<VoterSetting>();
            gridWorks.DataSource = _worksBinding;
            gridVoters.DataSource = _votersBinding;
            txtVotes.Text = string.Empty;
            RefreshResults(log: false);
            RefreshRuleChanges(log: false);
            RefreshContestCard(null);
            RefreshPeopleDirectory(log: false);
            UpdateWorkflowPanel(null);
            return;
        }

        txtContestNo.Text = contest.Number;
        txtContestName.Text = contest.Name;
        BindContestSettings(contest);
        LoadGridFromStore();
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        RefreshContestCard(contest);
        RefreshPeopleDirectory(log: false);
        UpdateWorkflowPanel(contest);
    }


    private void RefreshContestList()
    {
        if (gridContests is null)
            return;

        string? selectedId = CurrentContest?.Id;
        var rows = _contests
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => ParseContestNumberForSort(x.Number))
            .ThenBy(x => x.Name)
            .Select(x => new ContestListRow(x, _store.LoadVotes(x.Id).Count))
            .ToList();

        _syncingContestList = true;
        try
        {
            _contestListBinding = new BindingList<ContestListRow>(rows);
            gridContests.DataSource = _contestListBinding;

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                int index = rows.FindIndex(x => string.Equals(x.Id, selectedId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    int rowHandle = viewContests.GetRowHandle(index);
                    if (rowHandle >= 0)
                    {
                        viewContests.ClearSelection();
                        viewContests.FocusedRowHandle = rowHandle;
                        viewContests.SelectRow(rowHandle);
                    }
                }
            }
        }
        finally
        {
            _syncingContestList = false;
        }

        RefreshContestCard(CurrentContest);
    }

    private void RefreshContestCard(Contest? contest)
    {
        if (txtContestCard is null || gridContestWorksPreview is null || gridContestVotersPreview is null)
            return;

        if (contest is null)
        {
            txtContestCard.Text = "Конкурс не выбран.";
            gridContestWorksPreview.DataSource = new List<ContestWorkPreviewRow>();
            gridContestVotersPreview.DataSource = new List<ContestVoterPreviewRow>();
            return;
        }

        List<VoteEntry> votes = _store.LoadVotes(contest.Id);
        ContestResultsReport resultsReport = _results.BuildReport(contest, votes);
        ContestAuditReport auditReport = _audit.BuildReport(contest, votes);

        int required = auditReport.Rows.Count(x => x.RequiredToVote);
        int debtors = auditReport.Rows.Count(x => x.IsDebtor);
        int unknownVoters = auditReport.Rows.Count(x => x.IsUnknownVoter);
        string authorMode = contest.HostKnowsAuthors ? "ведущий знает авторов" : "сторонний счётчик, авторы скрыты";
        string updated = contest.UpdatedAt.ToString("dd.MM.yyyy HH:mm");
        string activeText = contest.IsActive ? "активный" : "архивный";
        string started = contest.StartedAt.ToString("dd.MM.yyyy HH:mm");
        string closed = contest.ClosedAt is null ? "не закрыт" : contest.ClosedAt.Value.ToString("dd.MM.yyyy HH:mm");

        txtContestCard.Text =
            $"№{contest.Number} - {contest.Name}{Environment.NewLine}" +
            $"Статус: {activeText}; стадия: {(int)GetContestStage(contest)} - {GetContestStageTitle(GetContestStage(contest))}; тем: {contest.Topics.Count}; режим: {authorMode}{Environment.NewLine}" +
            $"Начат: {started}; закрыт: {closed}; обновлён: {updated}{Environment.NewLine}" +
            $"Работ: {contest.Works.Count}; судей в настройках: {contest.Voters.Count}; обязательных: {required}; должников: {debtors}; неизвестных голосующих: {unknownVoters}{Environment.NewLine}" +
            $"Проголосовало судей: {resultsReport.VoterCount}; принято голосов: {resultsReport.AcceptedVoteCount}; самоголосов в 0: {resultsReport.SelfVoteCount}.";

        var workRows = resultsReport.Rows
            .OrderBy(x => x.PlaceNo)
            .ThenBy(x => x.WorkNo)
            .Select(x => new ContestWorkPreviewRow(x))
            .ToList();

        var voterRows = auditReport.Rows
            .OrderByDescending(x => x.IsDebtor)
            .ThenByDescending(x => x.IsUnknownVoter)
            .ThenBy(x => x.VoterName)
            .Select(x => new ContestVoterPreviewRow(x))
            .ToList();

        gridContestWorksPreview.DataSource = workRows;
        gridContestVotersPreview.DataSource = voterRows;
    }

    private void SelectContestFromList(bool switchToWorkPage)
    {
        if (_syncingContestList || gridContests is null)
            return;

        ContestListRow? row = GetFocusedRow<ContestListRow>(viewContests);
        if (row is null)
            return;

        SelectContestById(row.Id);
        if (switchToWorkPage && mainTabs is not null && mainTabs.TabPages.Count > 1)
            NavigateToSection(1);
    }

    private void SelectContestById(string contestId)
    {
        if (string.IsNullOrWhiteSpace(contestId) || cboContests is null)
            return;

        for (int i = 0; i < _contests.Count; i++)
        {
            if (_contests[i].Id == contestId)
            {
                if (cboContests.SelectedIndex != i)
                    cboContests.SelectedIndex = i;
                else
                    ApplySelectedContest();
                return;
            }
        }
    }

    private void DeleteSelectedContest()
    {
        ContestListRow? row = GetFocusedRow<ContestListRow>(viewContests);
        if (row is null)
            return;

        var answer = MessageBox.Show(
            this,
            $"Удалить конкурс \"{row.Number} - {row.Name}\" и его сохранённые голоса?",
            "Удаление конкурса",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return;

        Contest? contest = _contests.FirstOrDefault(x => x.Id == row.Id);
        if (contest is null)
            return;

        _store.ClearVotes(contest.Id);
        _contests.Remove(contest);
        _store.SaveContests(_contests);
        ReloadContests(_contests.FirstOrDefault()?.Id);
        Log($"Удалён конкурс: №{contest.Number} - {contest.Name}");
    }

    private static int ParseContestNumberForSort(string? value)
    {
        return int.TryParse(value, out int number) ? number : int.MaxValue;
    }



    private void SyncWorkflowContestList(string? selectId = null)
    {
        if (repositoryWorkflowContests is null || beiWorkflowContest is null)
            return;

        _syncingWorkflowContest = true;
        try
        {
            string? selectedId = selectId ?? CurrentContest?.Id;
            var items = _contests
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => ParseContestNumberForSort(x.Number))
                .ThenBy(x => x.Name)
                .Select(x => new ContestComboItem(x))
                .ToList();

            repositoryWorkflowContests.BeginUpdate();
            repositoryWorkflowContests.Items.Clear();
            repositoryWorkflowContests.Items.AddRange(items);

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                foreach (ContestComboItem item in items)
                {
                    if (string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        beiWorkflowContest.EditValue = item;
                        break;
                    }
                }
            }

            if (beiWorkflowContest.EditValue is null && items.Count > 0)
                beiWorkflowContest.EditValue = items[0];
        }
        finally
        {
            repositoryWorkflowContests.EndUpdate();
            _syncingWorkflowContest = false;
        }
    }

    private void SyncWorkflowContestSelection(Contest? selected)
    {
        if (repositoryWorkflowContests is null || beiWorkflowContest is null || selected is null)
            return;

        _syncingWorkflowContest = true;
        try
        {
            foreach (object? candidate in repositoryWorkflowContests.Items)
            {
                if (candidate is ContestComboItem item && string.Equals(item.Id, selected.Id, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ReferenceEquals(beiWorkflowContest.EditValue, item))
                        beiWorkflowContest.EditValue = item;
                    break;
                }
            }
        }
        finally
        {
            _syncingWorkflowContest = false;
        }
    }

    private void UpdateWorkflowPanel(Contest? contest)
    {
        if (bsiWorkflowStatus is null || bsiWorkflowHint is null)
            return;

        if (repositoryWorkflowContests is not null && repositoryWorkflowContests.Items.Count == 0 && _contests.Count > 0)
            SyncWorkflowContestList(contest?.Id);

        if (contest is null)
        {
            bsiWorkflowStatus.Caption = "конкурс не выбран";
            bsiWorkflowHint.Caption = "Создай или выбери конкурс. После этого сверху можно вести конкурс по стадиям 1-5.";
            return;
        }

        bsiWorkflowStatus.Caption = $"{(int)GetContestStage(contest)} - {GetContestStageTitle(GetContestStage(contest))}";
        bsiWorkflowHint.Caption = BuildWorkflowHint(contest);
        SyncWorkflowContestSelection(contest);
    }

    private static ContestStage GetContestStage(Contest contest)
    {
        int stage = contest.Stage;
        return Enum.IsDefined(typeof(ContestStage), stage)
            ? (ContestStage)stage
            : ContestStage.TopicReception;
    }

    private static string GetContestStageTitle(ContestStage stage) => stage switch
    {
        ContestStage.TopicReception => "приём тем",
        ContestStage.WorkReception => "приём работ",
        ContestStage.VotingOpen => "голосование открыто",
        ContestStage.VotingClosed => "голосование закрыто, итоги",
        ContestStage.Finished => "конкурс закрыт",
        _ => "неизвестно"
    };

    private static string BuildWorkflowHint(Contest contest)
    {
        int topicCount = contest.Topics?.Count ?? 0;
        string host = string.IsNullOrWhiteSpace(contest.HostName) ? "ведущий не указан" : $"ведущий: {contest.HostName}";
        string next = string.IsNullOrWhiteSpace(contest.NextHostName) ? "следующий ведущий не назначен" : $"следующий ведущий: {contest.NextHostName}";
        return GetContestStage(contest) switch
        {
            ContestStage.TopicReception => $"Открыт приём тем. Тем в базе: {topicCount}. {host}.",
            ContestStage.WorkReception => $"Идёт приём работ из лички. Работ в базе: {contest.Works.Count}. Авторы известны ведущему: {(contest.HostKnowsAuthors ? "да" : "нет")}.",
            ContestStage.VotingOpen => $"Голосование открыто. Форма голосования уже доступна во вкладке/разделе 'Голосование'.",
            ContestStage.VotingClosed => $"Голосование закрыто. Можно импортировать авторов, обновить итоги и сформировать HTML-дипломы.",
            ContestStage.Finished => $"Конкурс закрыт. {next}.",
            _ => string.Empty
        };
    }

    private void SetCurrentContestStage(ContestStage stage)
    {
        Contest? contest = CurrentContest;
        if (contest is null)
        {
            MessageBox.Show(this, "Сначала выбери или создай конкурс.", "Нет конкурса", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        contest.Stage = (int)stage;
        contest.StageUpdatedAt = DateTime.Now;
        contest.UpdatedAt = DateTime.Now;
        if (stage == ContestStage.Finished)
        {
            contest.IsActive = false;
            contest.ClosedAt ??= contest.StageUpdatedAt;
        }
        else
        {
            contest.ClosedAt = null;
            contest.StartedAt = contest.StartedAt == default ? DateTime.Now : contest.StartedAt;
            SetOnlyContestActive(contest);
        }
        _store.SaveContests(_contests);
        RefreshContestList();
        UpdateWorkflowPanel(contest);

        if (stage == ContestStage.WorkReception)
            NavigateToSection(2);
        else if (stage == ContestStage.VotingOpen)
            NavigateToSection(1);
        else if (stage == ContestStage.VotingClosed)
        {
            NavigateToSection(4);
            RefreshResults(log: false);
        }
        else if (stage == ContestStage.Finished)
            NavigateToSection(0);

        Log($"Стадия конкурса №{contest.Number} изменена: {(int)stage} - {GetContestStageTitle(stage)}.");
    }

    private void ImportTopicsDialog()
    {
        Contest? contest = CurrentContest;
        if (contest is null)
        {
            MessageBox.Show(this, "Сначала выбери или создай конкурс.", "Нет конкурса", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new Form
        {
            Text = "Импорт тем конкурса",
            StartPosition = FormStartPosition.CenterParent,
            Width = 980,
            Height = 640,
            MinimumSize = new Size(820, 520)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        form.Controls.Add(root);

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Темы не проверены."
        };
        root.Controls.Add(statusLabel, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 440
        };
        root.Controls.Add(split, 0, 1);

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            Text = SafeGetClipboardText()
        };
        split.Panel1.Controls.Add(textBox);

        var previewGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false
        };
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopicImportPreviewRow.SourceLine), HeaderText = "Строка", Width = 60 });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopicImportPreviewRow.Number), HeaderText = "№", Width = 55 });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopicImportPreviewRow.Title), HeaderText = "Тема", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopicImportPreviewRow.Status), HeaderText = "Статус", Width = 150 });
        split.Panel2.Controls.Add(previewGrid);

        var replaceCheck = new CheckBox
        {
            Dock = DockStyle.Fill,
            Text = "Заменить существующие темы конкурса",
            Checked = contest.Topics.Count == 0
        };
        root.Controls.Add(replaceCheck, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnImport = new Button { Text = "Импортировать", Width = 140, Height = 34 };
        var btnCheck = new Button { Text = "Проверить", Width = 110, Height = 34 };
        var btnPaste = new Button { Text = "Вставить из буфера", Width = 145, Height = 34 };
        var btnClear = new Button { Text = "Очистить", Width = 100, Height = 34 };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnImport);
        buttons.Controls.Add(btnCheck);
        buttons.Controls.Add(btnPaste);
        buttons.Controls.Add(btnClear);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 3);
        form.AcceptButton = btnCheck;
        form.CancelButton = btnCancel;

        TopicImportPreview preview = BuildTopicImportPreview(contest, textBox.Text, replaceCheck.Checked);
        RefreshTopicImportPreview(previewGrid, statusLabel, preview);

        void CheckPreview()
        {
            preview = BuildTopicImportPreview(contest, textBox.Text, replaceCheck.Checked);
            RefreshTopicImportPreview(previewGrid, statusLabel, preview);
        }

        btnPaste.Click += (_, _) =>
        {
            textBox.Text = SafeGetClipboardText();
            CheckPreview();
        };
        btnCheck.Click += (_, _) => CheckPreview();
        btnClear.Click += (_, _) =>
        {
            textBox.Clear();
            CheckPreview();
        };
        replaceCheck.CheckedChanged += (_, _) => CheckPreview();
        textBox.TextChanged += (_, _) =>
        {
            statusLabel.Text = "Текст изменён. Нажми \"Проверить\", чтобы обновить предпросмотр.";
        };

        btnImport.Click += (_, _) =>
        {
            CheckPreview();
            if (preview.AcceptedTopics.Count == 0)
            {
                MessageBox.Show(this, "Нет тем для импорта. Проверь текст или убери дубли.", "Импорт тем", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        ApplyImportedTopics(contest, preview.AcceptedTopics, replaceCheck.Checked);
        contest.Stage = (int)ContestStage.TopicReception;
        contest.StageUpdatedAt = DateTime.Now;
        contest.UpdatedAt = DateTime.Now;
        _store.SaveContests(_contests);
        RefreshContestList();
        UpdateWorkflowPanel(contest);
        Log($"Импорт тем: найдено {preview.FoundCount}, принято {preview.AcceptedTopics.Count}, дублей {preview.DuplicateCount}, всего тем в конкурсе {contest.Topics.Count}.");
    }

    private static List<ContestTopic> ParseTopics(string? text)
    {
        return ParseTopicImportLines(text)
            .Where(x => x.Topic is not null)
            .Select(x => x.Topic!.Clone())
            .GroupBy(x => x.Number)
            .Select(x => x.Last())
            .OrderBy(x => x.Number)
            .ToList();
    }

    private static List<TopicImportLine> ParseTopicImportLines(string? text)
    {
        var result = new List<TopicImportLine>();
        int autoNo = 1;
        int sourceLine = 0;
        foreach (string raw in (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            sourceLine++;
            string line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line == "." || line.All(ch => ch == '-' || char.IsWhiteSpace(ch)))
                continue;

            int number = 0;
            string title = line;
            int pos = 0;
            while (pos < line.Length && char.IsDigit(line[pos]))
                pos++;

            if (pos > 0 && int.TryParse(line[..pos], out int parsed))
            {
                string tail = line[pos..].TrimStart();
                if (tail.StartsWith("-") || tail.StartsWith(".") || tail.StartsWith(")") || tail.StartsWith("–") || tail.StartsWith("—"))
                {
                    number = parsed;
                    title = tail[1..].Trim();
                }
            }

            if (number <= 0)
                number = autoNo;

            if (string.IsNullOrWhiteSpace(title))
                continue;

            result.Add(new TopicImportLine(sourceLine, new ContestTopic { Number = number, Title = title }));
            autoNo = Math.Max(autoNo + 1, number + 1);
        }

        return result;
    }

    private static TopicImportPreview BuildTopicImportPreview(Contest contest, string? text, bool replaceExisting)
    {
        List<TopicImportLine> parsed = ParseTopicImportLines(text);
        var rows = new List<TopicImportPreviewRow>();
        var accepted = new List<ContestTopic>();
        var importTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importNumbers = new HashSet<int>();
        var existingTitles = replaceExisting
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : contest.Topics.Select(x => NormalizeTopicTitle(x.Title)).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNumbers = replaceExisting
            ? new HashSet<int>()
            : contest.Topics.Where(x => x.Number > 0).Select(x => x.Number).ToHashSet();

        foreach (TopicImportLine line in parsed)
        {
            ContestTopic topic = line.Topic!;
            string titleKey = NormalizeTopicTitle(topic.Title);
            bool duplicateNumber = topic.Number <= 0 || !importNumbers.Add(topic.Number);
            bool duplicateTitle = titleKey.Length == 0 || !importTitles.Add(titleKey) || existingTitles.Contains(titleKey);
            string status;
            bool acceptedRow;

            if (duplicateNumber)
            {
                status = "дубль номера";
                acceptedRow = false;
            }
            else if (duplicateTitle)
            {
                status = "дубль темы";
                acceptedRow = false;
            }
            else
            {
                status = existingNumbers.Contains(topic.Number) ? "обновит номер" : "будет добавлено";
                acceptedRow = true;
                accepted.Add(topic.Clone());
            }

            rows.Add(new TopicImportPreviewRow(line.SourceLine, topic.Number, topic.Title, status, acceptedRow));
        }

        return new TopicImportPreview(rows, accepted);
    }

    private static string NormalizeTopicTitle(string? title)
    {
        string value = (title ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        bool previousSpace = false;
        foreach (char ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousSpace)
                    sb.Append(' ');
                previousSpace = true;
            }
            else
            {
                sb.Append(ch);
                previousSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static void RefreshTopicImportPreview(DataGridView previewGrid, Label statusLabel, TopicImportPreview preview)
    {
        previewGrid.DataSource = new BindingList<TopicImportPreviewRow>(preview.Rows);
        foreach (DataGridViewRow row in previewGrid.Rows)
        {
            if (row.DataBoundItem is TopicImportPreviewRow item && !item.Accepted)
                row.DefaultCellStyle.ForeColor = Color.Firebrick;
        }

        statusLabel.Text = $"Найдено: {preview.FoundCount}; принято: {preview.AcceptedTopics.Count}; дублей: {preview.DuplicateCount}.";
    }

    private static void ApplyImportedTopics(Contest contest, IEnumerable<ContestTopic> importedTopics, bool replaceExisting)
    {
        if (replaceExisting)
            contest.Topics.Clear();

        var byNumber = contest.Topics
            .Where(x => x.Number > 0)
            .GroupBy(x => x.Number)
            .ToDictionary(x => x.Key, x => x.Last());

        foreach (ContestTopic topic in importedTopics.OrderBy(x => x.Number))
        {
            if (byNumber.TryGetValue(topic.Number, out ContestTopic? existing))
            {
                existing.Title = topic.Title.Trim();
            }
            else
            {
                var clone = topic.Clone();
                contest.Topics.Add(clone);
                byNumber[clone.Number] = clone;
            }
        }

        contest.Topics = contest.Topics
            .Where(x => x.Number > 0 && !string.IsNullOrWhiteSpace(x.Title))
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private void CloseContestAndOpenNextDialog()
    {
        Contest? contest = CurrentContest;
        if (contest is null)
        {
            MessageBox.Show(this, "Сначала выбери конкурс.", "Нет конкурса", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new Form
        {
            Text = "Закрыть конкурс и открыть следующий",
            StartPosition = FormStartPosition.CenterParent,
            Width = 620,
            Height = 300,
            MinimumSize = new Size(540, 260)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        form.Controls.Add(root);

        var closeInfoLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"Текущий конкурс №{contest.Number} будет закрыт. Новый конкурс создаётся сразу в стадии 1 - приём тем."
        };
        root.Controls.Add(closeInfoLabel, 0, 0);
        root.SetColumnSpan(closeInfoLabel, 2);

        var txtNextNo = new TextBox { Dock = DockStyle.Fill, Text = NextContestNumber() };
        var txtNextName = new TextBox { Dock = DockStyle.Fill, Text = "Новый конкурс" };
        var txtNextHost = new TextBox { Dock = DockStyle.Fill, Text = contest.NextHostName };
        var chkCopyRules = new CheckBox { Dock = DockStyle.Fill, Text = "Скопировать правила голосования из текущего конкурса", Checked = true };

        root.Controls.Add(new Label { Text = "№ нового конкурса:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        root.Controls.Add(txtNextNo, 1, 1);
        root.Controls.Add(new Label { Text = "Название:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        root.Controls.Add(txtNextName, 1, 2);
        root.Controls.Add(new Label { Text = "Следующий ведущий:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        root.Controls.Add(txtNextHost, 1, 3);
        root.Controls.Add(chkCopyRules, 0, 4);
        root.SetColumnSpan(chkCopyRules, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnOk = new Button { Text = "Закрыть и открыть", Width = 160, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 5);
        root.SetColumnSpan(buttons, 2);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        contest.Stage = (int)ContestStage.Finished;
        contest.StageUpdatedAt = DateTime.Now;
        contest.NextHostName = txtNextHost.Text.Trim();
        contest.IsActive = false;
        contest.ClosedAt = contest.StageUpdatedAt;
        contest.UpdatedAt = DateTime.Now;

        var next = new Contest
        {
            Number = string.IsNullOrWhiteSpace(txtNextNo.Text) ? NextContestNumber() : txtNextNo.Text.Trim(),
            Name = string.IsNullOrWhiteSpace(txtNextName.Text) ? "Новый конкурс" : txtNextName.Text.Trim(),
            Stage = (int)ContestStage.TopicReception,
            StageUpdatedAt = DateTime.Now,
            HostName = txtNextHost.Text.Trim(),
            StartedAt = DateTime.Now,
            ClosedAt = null,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        if (chkCopyRules.Checked)
            CopyContestRules(contest, next);

        _contests.Add(next);
        SetOnlyContestActive(next);
        _store.SaveContests(_contests);
        ReloadContests(next.Id);
        NavigateToSection(0);
        Log($"Конкурс №{contest.Number} закрыт. Открыт новый конкурс №{next.Number}." + (string.IsNullOrWhiteSpace(next.HostName) ? string.Empty : $" Ведущий: {next.HostName}."));
    }

    private static void CopyContestRules(Contest source, Contest target)
    {
        target.VoteLimit = source.VoteLimit;
        target.BaseVote = source.BaseVote;
        target.MaxVote = source.MaxVote;
        target.LimitMaxVote = source.LimitMaxVote;
        target.LimitMaxVoteByTopic = source.LimitMaxVoteByTopic;
        target.OneMaxVotePerTopic = source.OneMaxVotePerTopic;
        target.DowngradeExtraMaxVoteToBase = source.DowngradeExtraMaxVoteToBase;
        target.AllowZeroVotes = source.AllowZeroVotes;
        target.TreatSelfVoteAsZero = source.TreatSelfVoteAsZero;
        target.HostKnowsAuthors = source.HostKnowsAuthors;
    }

    private void GenerateDiplomaHtml()
    {
        Contest? contest = CurrentContest;
        ContestResultsReport? report = RefreshResults(log: false);
        if (contest is null || report is null)
            return;

        var winners = report.Rows
            .Where(x => x.PlaceNo is >= 1 and <= 3)
            .OrderBy(x => x.PlaceNo)
            .ThenBy(x => x.WorkNo)
            .ToList();

        if (winners.Count == 0)
        {
            MessageBox.Show(this, "Нет строк итогов для дипломов. Сначала подведи итоги.", "Дипломы", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string folder = Path.Combine(LocalDatabase.DatabaseFolder, "Reports", $"{DateTime.Now:yyyyMMdd_HHmmss}_diplomas_{SanitizeFilePart(contest.Number)}");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "diplomas.html");
        File.WriteAllText(file, BuildDiplomaHtml(contest, winners), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
        Log("HTML-дипломы сформированы: " + file);
    }

    private static string BuildDiplomaHtml(Contest contest, IReadOnlyList<ContestRatingRow> winners)
    {
        static string H(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\"><title>Дипломы</title>");
        sb.AppendLine("<style>@page{size:A4 landscape;margin:14mm}body{font-family:Segoe UI,Arial,sans-serif;background:#eef2f7;margin:0}.page{page-break-after:always;height:180mm;box-sizing:border-box;background:white;border:2px solid #d4af37;border-radius:18px;padding:28mm 22mm;text-align:center;display:flex;flex-direction:column;justify-content:center}.title{font-size:46px;font-weight:700;letter-spacing:3px}.place{font-size:34px;margin-top:12px}.who{font-size:30px;margin-top:18px}.work{font-size:24px;margin-top:10px}.contest{font-size:18px;margin-top:24px;color:#4b5563}.rate{font-size:18px;margin-top:8px}.footer{font-size:14px;margin-top:32px;color:#6b7280}@media print{body{background:white}.page{border-color:#c09a25}}</style>");
        sb.AppendLine("</head><body>");
        foreach (ContestRatingRow row in winners)
        {
            string medal = row.PlaceNo == 1 ? "I место" : row.PlaceNo == 2 ? "II место" : "III место";
            string author = string.IsNullOrWhiteSpace(row.Author) ? "Автор не указан" : row.Author;
            sb.AppendLine("<section class=\"page\">");
            sb.AppendLine("<div class=\"title\">ДИПЛОМ</div>");
            sb.AppendLine($"<div class=\"place\">{H(medal)}</div>");
            sb.AppendLine($"<div class=\"who\">{H(author)}</div>");
            sb.AppendLine($"<div class=\"work\">за работу №{row.WorkNoText} - {H(row.Title)}</div>");
            if (!string.IsNullOrWhiteSpace(row.Topic))
                sb.AppendLine($"<div class=\"work\">Тема: {H(row.Topic)}</div>");
            sb.AppendLine($"<div class=\"contest\">Конкурс №{H(contest.Number)} - {H(contest.Name)}</div>");
            sb.AppendLine($"<div class=\"rate\">Итог: {row.Rate:0.##}; голосов: {row.AcceptedVotes}</div>");
            sb.AppendLine($"<div class=\"footer\">Сформировано VoteCounter: {DateTime.Now:dd.MM.yyyy HH:mm}</div>");
            sb.AppendLine("</section>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private void ImportLegacyFirebirdDialog()
    {
        using var fileDialog = new OpenFileDialog
        {
            Title = "Выбери старую Firebird-базу конкурса",
            Filter = "Firebird database (*.fdb)|*.fdb|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
            return;

        using var form = new Form
        {
            Text = "Импорт старой *.fdb - предпросмотр и выбор конкурсов",
            StartPosition = FormStartPosition.CenterParent,
            Width = 940,
            Height = 700,
            MinimumSize = new Size(780, 560)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Безопасный импорт старых RhymeMachine/Firebird баз: сначала предпросмотр, потом выбор конкурсов, резервная копия VoteCounter.db, и только после этого запись в рабочую базу проекта."
        }, 0, 0);

        var connectionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3
        };
        connectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        connectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        connectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.Controls.Add(connectionPanel, 0, 1);

        var txtFile = new TextBox { Dock = DockStyle.Fill, Text = fileDialog.FileName, ReadOnly = true };
        var txtUser = new TextBox { Dock = DockStyle.Fill, Text = "SYSDBA" };
        var txtPassword = new TextBox { Dock = DockStyle.Fill, Text = "masterkey", UseSystemPasswordChar = true };

        connectionPanel.Controls.Add(new Label { Text = "*.fdb:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        connectionPanel.Controls.Add(txtFile, 1, 0);
        connectionPanel.SetColumnSpan(txtFile, 3);
        connectionPanel.Controls.Add(new Label { Text = "Пользователь:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        connectionPanel.Controls.Add(txtUser, 1, 1);
        connectionPanel.Controls.Add(new Label { Text = "Пароль:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);
        connectionPanel.Controls.Add(txtPassword, 3, 1);

        var databaseLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Рабочая база: " + _store.DatabaseFile
        };
        connectionPanel.Controls.Add(databaseLabel, 0, 2);
        connectionPanel.SetColumnSpan(databaseLabel, 4);

        var optionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(optionsPanel, 0, 2);

        var chkVotes = new CheckBox
        {
            Text = "Читать и импортировать старые голоса",
            Checked = true,
            Dock = DockStyle.Fill
        };
        var chkMergeSameNumberName = new CheckBox
        {
            Text = "Если совпали № и название - обновлять существующий конкурс",
            Checked = true,
            Dock = DockStyle.Fill
        };
        var chkReplaceVotes = new CheckBox
        {
            Text = "Заменять голоса выбранных конкурсов импортированными",
            Checked = true,
            Dock = DockStyle.Fill
        };
        var chkApplySelfVoteRule = new CheckBox
        {
            Text = "При импорте применить правило: голос автора за себя = 0",
            Checked = true,
            Dock = DockStyle.Fill
        };
        var btnPreview = new Button { Text = "Предпросмотр", Width = 145, Height = 34 };

        optionsPanel.Controls.Add(chkVotes, 0, 0);
        optionsPanel.Controls.Add(chkMergeSameNumberName, 1, 0);
        optionsPanel.Controls.Add(chkReplaceVotes, 0, 1);
        optionsPanel.Controls.Add(chkApplySelfVoteRule, 1, 1);
        optionsPanel.Controls.Add(btnPreview, 0, 2);
        optionsPanel.SetColumnSpan(btnPreview, 2);

        var checkedList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            HorizontalScrollbar = true
        };
        root.Controls.Add(checkedList, 0, 3);

        var txtPreview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false
        };
        root.Controls.Add(txtPreview, 0, 4);

        FirebirdImportReport? previewReport = null;
        List<FirebirdImportPreviewItem> previewItems = new();

        void ResetPreview()
        {
            previewReport = null;
            previewItems = new List<FirebirdImportPreviewItem>();
            checkedList.Items.Clear();
            txtPreview.Text = "Нажми \"Предпросмотр\", чтобы прочитать старую базу без записи в VoteCounter.db.";
        }

        bool BuildPreview()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                previewReport = _firebirdImporter.Import(txtFile.Text, txtUser.Text, txtPassword.Text, chkVotes.Checked);
                Cursor = Cursors.Default;

                previewItems = previewReport.Contests
                    .OrderBy(x => ParseContestNumberForSort(x.Number))
                    .ThenBy(x => x.Name)
                    .Select(x => new FirebirdImportPreviewItem(
                        x,
                        previewReport.VotesByContestId.TryGetValue(x.Id, out List<VoteEntry>? votes) ? votes.Count : 0,
                        FindExistingContestIndex(x, matchByNumberAndName: false) >= 0,
                        FindExistingContestIndex(x, matchByNumberAndName: true) >= 0))
                    .ToList();

                checkedList.Items.Clear();
                foreach (FirebirdImportPreviewItem item in previewItems)
                    checkedList.Items.Add(item, true);

                txtPreview.Text = BuildFirebirdPreviewText(previewReport, previewItems, chkMergeSameNumberName.Checked);
                if (previewReport.Contests.Count == 0)
                    MessageBox.Show(form, "В старой базе конкурсы не найдены.", "Импорт *.fdb", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                previewReport = null;
                previewItems = new List<FirebirdImportPreviewItem>();
                checkedList.Items.Clear();
                txtPreview.Text = "Ошибка предпросмотра: " + ex.Message;
                MessageBox.Show(form, ex.Message, "Ошибка чтения *.fdb", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        chkVotes.CheckedChanged += (_, _) => ResetPreview();
        chkMergeSameNumberName.CheckedChanged += (_, _) =>
        {
            if (previewReport is not null)
                txtPreview.Text = BuildFirebirdPreviewText(previewReport, previewItems, chkMergeSameNumberName.Checked);
        };
        btnPreview.Click += (_, _) => BuildPreview();
        ResetPreview();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var btnImport = new Button { Text = "Импортировать выбранные", Width = 190, Height = 34 };
        var btnSelectAll = new Button { Text = "Выбрать все", Width = 120, Height = 34 };
        var btnClear = new Button { Text = "Снять выбор", Width = 120, Height = 34 };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnImport);
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnClear);
        buttons.Controls.Add(btnSelectAll);
        root.Controls.Add(buttons, 0, 5);
        form.CancelButton = btnCancel;

        btnSelectAll.Click += (_, _) =>
        {
            for (int i = 0; i < checkedList.Items.Count; i++)
                checkedList.SetItemChecked(i, true);
        };
        btnClear.Click += (_, _) =>
        {
            for (int i = 0; i < checkedList.Items.Count; i++)
                checkedList.SetItemChecked(i, false);
        };

        List<string> selectedIds = new();
        bool acceptedImportVotes = true;
        bool acceptedReplaceVotes = true;
        bool acceptedMergeByNumberName = true;
        bool acceptedApplySelfVoteRule = true;
        FirebirdImportReport? acceptedReport = null;

        btnImport.Click += (_, _) =>
        {
            if (previewReport is null && !BuildPreview())
                return;

            selectedIds = checkedList.CheckedItems
                .Cast<FirebirdImportPreviewItem>()
                .Select(x => x.Contest.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedIds.Count == 0)
            {
                MessageBox.Show(form, "Выбери хотя бы один конкурс для импорта.", "Импорт *.fdb", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            acceptedReport = previewReport;
            acceptedImportVotes = chkVotes.Checked;
            acceptedReplaceVotes = chkReplaceVotes.Checked;
            acceptedMergeByNumberName = chkMergeSameNumberName.Checked;
            acceptedApplySelfVoteRule = chkApplySelfVoteRule.Checked;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        if (form.ShowDialog(this) != DialogResult.OK || acceptedReport is null)
            return;

        ApplyFirebirdImportReport(
            acceptedReport,
            selectedIds,
            acceptedImportVotes,
            acceptedReplaceVotes,
            acceptedMergeByNumberName,
            acceptedApplySelfVoteRule);
    }

    private void ApplyFirebirdImportReport(
        FirebirdImportReport report,
        List<string> selectedOriginalIds,
        bool importVotes,
        bool replaceVotes,
        bool mergeByNumberAndName,
        bool applySelfVoteRule)
    {
        var selected = new HashSet<string>(selectedOriginalIds, StringComparer.OrdinalIgnoreCase);
        var selectedContests = report.Contests.Where(x => selected.Contains(x.Id)).ToList();
        if (selectedContests.Count == 0)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            string backupPath = BackupProjectDatabaseBeforeImport();

            int added = 0;
            int updated = 0;
            int savedVotes = 0;
            string? firstSelectedId = null;
            var votesToSave = new Dictionary<string, List<VoteEntry>>(StringComparer.OrdinalIgnoreCase);
            var finalVotesByContestId = new Dictionary<string, List<VoteEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (Contest imported in selectedContests)
            {
                string originalId = imported.Id;
                List<VoteEntry> importedVotes = report.VotesByContestId.TryGetValue(originalId, out List<VoteEntry>? sourceVotes)
                    ? sourceVotes.Select(CloneVote).ToList()
                    : new List<VoteEntry>();

                int index = FindExistingContestIndex(imported, mergeByNumberAndName);
                if (index >= 0)
                {
                    Contest existing = _contests[index];
                    imported.Id = existing.Id;
                    imported.CreatedAt = existing.CreatedAt == default ? imported.CreatedAt : existing.CreatedAt;
                    imported.UpdatedAt = DateTime.Now;
                    _contests[index] = imported;
                    updated++;
                }
                else
                {
                    imported.UpdatedAt = DateTime.Now;
                    _contests.Add(imported);
                    added++;
                }

                foreach (VoteEntry vote in importedVotes)
                {
                    vote.ContestId = imported.Id;
                    vote.VoterKey = string.IsNullOrWhiteSpace(vote.VoterKey) ? NameNormalizer.Normalize(vote.VoterName) : vote.VoterKey;
                }

                if (applySelfVoteRule)
                    ApplySelfVoteZeroRule(imported, importedVotes);

                if (importVotes && importedVotes.Count > 0)
                {
                    List<VoteEntry> finalVotes = replaceVotes
                        ? importedVotes
                        : MergeVotes(_store.LoadVotes(imported.Id), importedVotes);
                    votesToSave[imported.Id] = finalVotes;
                    finalVotesByContestId[imported.Id] = finalVotes;
                    savedVotes += importedVotes.Count;
                }
                else
                {
                    finalVotesByContestId[imported.Id] = _store.LoadVotes(imported.Id);
                }

                if (firstSelectedId is null)
                    firstSelectedId = imported.Id;
            }

            _store.SaveContests(_contests);
            foreach (KeyValuePair<string, List<VoteEntry>> pair in votesToSave)
                _store.SaveVotes(pair.Key, pair.Value);

            foreach (Contest contest in selectedContests)
            {
                List<VoteEntry> storedVotes = _store.LoadVotes(contest.Id);
                finalVotesByContestId[contest.Id] = storedVotes;
                _rmStore.Sync(contest, storedVotes);
            }

            ContestReportExportResult export = _reportExporter.ExportFirebirdImportSession(
                Path.Combine(_store.RootFolder, "Reports"),
                report,
                selectedContests,
                finalVotesByContestId,
                backupPath,
                added,
                updated,
                savedVotes,
                replaceVotes,
                mergeByNumberAndName,
                applySelfVoteRule);

            Cursor = Cursors.Default;
            ReloadContests(firstSelectedId);
            if (!string.IsNullOrWhiteSpace(firstSelectedId))
                SelectContestById(firstSelectedId);

            string summary =
                $"Импорт *.fdb завершён. Режим подключения: {report.ConnectionMode}. " +
                $"Выбрано конкурсов: {selectedContests.Count}; добавлено: {added}; обновлено: {updated}; импортировано голосов: {savedVotes}.";
            Log(summary);
            if (!string.IsNullOrWhiteSpace(backupPath))
                Log("Резервная копия базы перед импортом: " + backupPath);
            Log("Отчёт импорта сохранён: " + export.Folder);
            foreach (string warning in report.Warnings)
                Log("⚠ " + warning);
            MessageBox.Show(
                this,
                summary + Environment.NewLine + Environment.NewLine +
                "Backup: " + (string.IsNullOrWhiteSpace(backupPath) ? "не требовался" : backupPath) + Environment.NewLine +
                "Отчёт: " + export.Folder,
                "Импорт *.fdb",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, ex.Message, "Ошибка импорта *.fdb", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка импорта *.fdb: " + ex.Message);
        }
    }

    private int FindExistingContestIndex(Contest imported, bool matchByNumberAndName)
    {
        int byId = _contests.FindIndex(x => x.Id.Equals(imported.Id, StringComparison.OrdinalIgnoreCase));
        if (byId >= 0 || !matchByNumberAndName)
            return byId;

        string number = (imported.Number ?? string.Empty).Trim();
        string name = NormalizeContestTitle(imported.Name);
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
            return -1;

        return _contests.FindIndex(x =>
            (x.Number ?? string.Empty).Trim().Equals(number, StringComparison.OrdinalIgnoreCase) &&
            NormalizeContestTitle(x.Name).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeContestTitle(string? value)
    {
        string source = value ?? string.Empty;
        return string.Join(" ", source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim().ToLowerInvariant();
    }

    private static string BuildFirebirdPreviewText(FirebirdImportReport report, List<FirebirdImportPreviewItem> items, bool mergeByNumberAndName)
    {
        var lines = new List<string>
        {
            $"Режим подключения: {report.ConnectionMode}",
            $"Найдено конкурсов: {report.Contests.Count}",
            $"Найдено работ: {report.WorkCount}",
            $"Найдено голосующих: {report.VoterCount}",
            $"Найдено голосов: {report.VoteCount}",
            $"Защита от дублей по №/названию: {(mergeByNumberAndName ? "включена" : "выключена")}",
            string.Empty,
            "Список конкурсов:"
        };

        foreach (FirebirdImportPreviewItem item in items)
            lines.Add("- " + item.ToPreviewLine(mergeByNumberAndName));

        if (report.Warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Предупреждения:");
            lines.AddRange(report.Warnings.Select(x => "⚠ " + x));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BackupProjectDatabaseBeforeImport()
    {
        string databaseFile = _store.DatabaseFile;
        if (!File.Exists(databaseFile))
            return string.Empty;

        string backupFolder = Path.Combine(Path.GetDirectoryName(databaseFile) ?? AppContext.BaseDirectory, "Backups");
        Directory.CreateDirectory(backupFolder);
        string backupFile = Path.Combine(backupFolder, $"VoteCounter_{DateTime.Now:yyyyMMdd_HHmmss}_before_fdb_import.db");
        File.Copy(databaseFile, backupFile, overwrite: false);
        return backupFile;
    }

    private static VoteEntry CloneVote(VoteEntry vote)
    {
        return new VoteEntry
        {
            ContestId = vote.ContestId,
            VoterName = vote.VoterName,
            VoterKey = vote.VoterKey,
            WorkNo = vote.WorkNo,
            ScoreText = vote.ScoreText,
            Score = vote.Score,
            OriginalScore = vote.OriginalScore,
            OriginalScoreText = vote.OriginalScoreText,
            VotedScore = vote.VotedScore,
            VotedScoreText = vote.VotedScoreText,
            AcceptedScore = vote.AcceptedScore,
            AcceptedScoreText = vote.AcceptedScoreText,
            WasChangedByRules = vote.WasChangedByRules,
            RuleNote = vote.RuleNote,
            Comment = vote.Comment,
            SourceLine = vote.SourceLine,
            UpdatedAt = vote.UpdatedAt
        };
    }

    private static List<VoteEntry> MergeVotes(IEnumerable<VoteEntry> existingVotes, IEnumerable<VoteEntry> importedVotes)
    {
        var map = new Dictionary<string, VoteEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (VoteEntry vote in existingVotes.Concat(importedVotes))
        {
            string key = (string.IsNullOrWhiteSpace(vote.VoterKey) ? NameNormalizer.Normalize(vote.VoterName) : vote.VoterKey) + "|" + vote.WorkNo.ToString("000");
            map[key] = vote;
        }

        return map.Values
            .OrderBy(x => x.VoterName)
            .ThenBy(x => x.WorkNo)
            .ToList();
    }

    private static void ApplySelfVoteZeroRule(Contest contest, List<VoteEntry> votes)
    {
        if (!contest.TreatSelfVoteAsZero)
            return;

        var authorsByWorkNo = contest.Works
            .Where(x => x.Number > 0 && !string.IsNullOrWhiteSpace(x.Author))
            .GroupBy(x => x.Number)
            .ToDictionary(x => x.Key, x => NameNormalizer.Normalize(x.First().Author));

        foreach (VoteEntry vote in votes)
        {
            if (!authorsByWorkNo.TryGetValue(vote.WorkNo, out string? authorKey))
                continue;

            string voterKey = string.IsNullOrWhiteSpace(vote.VoterKey) ? NameNormalizer.Normalize(vote.VoterName) : vote.VoterKey;
            if (string.IsNullOrWhiteSpace(voterKey) || !voterKey.Equals(authorKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (vote.Score != 0m)
            {
                vote.OriginalScore = vote.OriginalScore == 0m ? vote.Score : vote.OriginalScore;
                vote.OriginalScoreText = string.IsNullOrWhiteSpace(vote.OriginalScoreText) ? vote.ScoreText : vote.OriginalScoreText;
                vote.VotedScore = vote.VotedScore == 0m ? vote.OriginalScore : vote.VotedScore;
                vote.VotedScoreText = string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.OriginalScoreText : vote.VotedScoreText;
                vote.Score = 0;
                vote.ScoreText = "0";
                vote.AcceptedScore = 0;
                vote.AcceptedScoreText = "0";
            }
            vote.WasChangedByRules = true;
            vote.RuleNote = string.IsNullOrWhiteSpace(vote.RuleNote) ? "самоголос импортирован как 0" : vote.RuleNote + "; самоголос импортирован как 0";
        }
    }

    private void ImportContestFromTextDialog()
    {
        using var form = new Form
        {
            Text = "Импорт конкурса из текста",
            StartPosition = FormStartPosition.CenterParent,
            Width = 980,
            Height = 760,
            MinimumSize = new Size(820, 620)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Вставь сюда объявление конкурса, список работ, раскрытие авторства или уже собранную ленту голосов. Программа создаст отдельный конкурс, заполнит работы, судей и при необходимости сразу примет найденные голоса."
        }, 0, 0);

        var txtSource = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            Text = string.IsNullOrWhiteSpace(txtVotes?.Text) ? SafeGetClipboardText() : txtVotes.Text
        };
        root.Controls.Add(txtSource, 0, 1);

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var rbHost = new RadioButton
        {
            Text = "Я ведущий: авторы известны, брать авторов из текста",
            Checked = chkHostKnowsAuthors?.Checked ?? true,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var rbHidden = new RadioButton
        {
            Text = "Счётчик до раскрытия: авторы пока скрыты",
            Checked = !(chkHostKnowsAuthors?.Checked ?? true),
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var chkImportVotes = new CheckBox
        {
            Text = "Если в тексте есть голосования - сразу принять их в базу нового конкурса",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var chkAuthorsAsVoters = new CheckBox
        {
            Text = "Авторов автоматически добавить в обязательные судьи",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        var btnPreview = new Button { Text = "Предпросмотр", Width = 145, Height = 34 };
        var previewPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        previewPanel.Controls.Add(btnPreview);

        options.Controls.Add(rbHost, 0, 0);
        options.Controls.Add(rbHidden, 1, 0);
        options.Controls.Add(chkImportVotes, 0, 1);
        options.SetColumnSpan(chkImportVotes, 2);
        options.Controls.Add(chkAuthorsAsVoters, 0, 2);
        options.SetColumnSpan(chkAuthorsAsVoters, 2);
        options.Controls.Add(previewPanel, 0, 3);
        options.SetColumnSpan(previewPanel, 2);
        root.Controls.Add(options, 0, 2);

        var txtPreview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false
        };
        root.Controls.Add(txtPreview, 0, 3);

        ContestTextImportResult? lastPreview = null;
        ContestTextImportResult BuildPreview()
        {
            lastPreview = _contestImporter.Parse(
                txtSource.Text,
                NextContestNumber(),
                rbHost.Checked,
                chkImportVotes.Checked,
                chkAuthorsAsVoters.Checked);
            txtPreview.Text = lastPreview.BuildPreviewText();
            return lastPreview;
        }

        btnPreview.Click += (_, _) => BuildPreview();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnOk = new Button { Text = "Создать конкурс", Width = 150, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 4);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        BuildPreview();

        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        ContestTextImportResult import = lastPreview is null ? BuildPreview() : BuildPreview();
        Contest contest = import.Contest;
        if (string.IsNullOrWhiteSpace(contest.Number))
            contest.Number = NextContestNumber();
        if (string.IsNullOrWhiteSpace(contest.Name))
            contest.Name = "Новый конкурс из текста";

        contest.CreatedAt = DateTime.Now;
        contest.UpdatedAt = DateTime.Now;
        _contests.Add(contest);
        _store.SaveContests(_contests);

        if (import.Votes.Count > 0)
        {
            foreach (VoteEntry vote in import.Votes)
            {
                vote.ContestId = contest.Id;
                vote.UpdatedAt = DateTime.Now;
            }
            _store.SaveVotes(contest.Id, import.Votes);
        }

        _rmStore.Sync(contest, import.Votes);
        ReloadContests(contest.Id);
        NavigateToSection(1);

        Log($"Импорт конкурса из текста: создан {contest}; работ {contest.Works.Count}, судей {contest.Voters.Count}, голосов {import.Votes.Count}.");
        if (!string.IsNullOrWhiteSpace(import.HostName))
            Log("Ведущий из текста: " + import.HostName);
        if (!string.IsNullOrWhiteSpace(import.DeadlineText))
            Log("Срок из текста: " + import.DeadlineText);
        foreach (string warning in import.Warnings)
            Log("⚠ " + warning);
    }

    private void OpenContestRulesCenter()
    {
        var contest = CurrentContest;
        if (contest is null)
        {
            MessageBox.Show(this, "Сначала выбери конкурс.", "Центр правил", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ContestRulesCenterForm(contest);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        form.ApplyTo(contest);
        _store.SaveContests(_contests);
        SaveSettings();
        _rmStore.Sync(contest, _store.LoadVotes(contest.Id));

        BindContestSettings(contest);
        RefreshVoteStatus(log: false);
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        RefreshContestList();
        RefreshContestCard(contest);

        Log($"Правила конкурса обновлены: max={contest.MaxVote}, base={contest.BaseVote}, лимит max={contest.LimitMaxVote}, 3+ = 3.5.");
    }

    private void BindContestSettings(Contest contest)
    {
        _worksBinding = new BindingList<ContestWork>(
            contest.Works
                .OrderBy(x => x.Number)
                .ThenBy(x => x.Title)
                .Select(x => x.Clone())
                .ToList());

        _votersBinding = new BindingList<VoterSetting>(
            contest.Voters
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Clone())
                .ToList());

        if (gridWorks is not null)
            gridWorks.DataSource = _worksBinding;

        if (gridVoters is not null)
            gridVoters.DataSource = _votersBinding;

        if (nudVoteLimit is not null)
            nudVoteLimit.Value = Math.Clamp(contest.VoteLimit, (int)nudVoteLimit.Minimum, (int)nudVoteLimit.Maximum);

        if (nudBaseVote is not null)
            nudBaseVote.Value = Math.Clamp(contest.BaseVote, (int)nudBaseVote.Minimum, (int)nudBaseVote.Maximum);

        if (nudMaxVote is not null)
            nudMaxVote.Value = Math.Clamp(contest.MaxVote, (int)nudMaxVote.Minimum, (int)nudMaxVote.Maximum);

        if (nudLimitMaxVote is not null)
            nudLimitMaxVote.Value = Math.Clamp(contest.LimitMaxVote, (int)nudLimitMaxVote.Minimum, (int)nudLimitMaxVote.Maximum);

        if (chkAllowZeroVotes is not null)
            chkAllowZeroVotes.Checked = contest.AllowZeroVotes;

        if (chkSelfVoteZero is not null)
            chkSelfVoteZero.Checked = contest.TreatSelfVoteAsZero;

        if (chkLimitMaxVoteByTopic is not null)
            chkLimitMaxVoteByTopic.Checked = contest.LimitMaxVoteByTopic;

        if (chkOneMaxVotePerTopic is not null)
            chkOneMaxVotePerTopic.Checked = contest.OneMaxVotePerTopic;

        if (chkDowngradeExtraMaxVote is not null)
            chkDowngradeExtraMaxVote.Checked = contest.DowngradeExtraMaxVoteToBase;

        if (chkHostKnowsAuthors is not null)
            chkHostKnowsAuthors.Checked = contest.HostKnowsAuthors;
    }

    private void CreateContest()
    {
        var contest = new Contest
        {
            Number = string.IsNullOrWhiteSpace(txtContestNo.Text) ? NextContestNumber() : txtContestNo.Text.Trim(),
            Name = string.IsNullOrWhiteSpace(txtContestName.Text) ? "Новый конкурс" : txtContestName.Text.Trim(),
            Stage = (int)ContestStage.TopicReception,
            StageUpdatedAt = DateTime.Now,
            StartedAt = DateTime.Now,
            ClosedAt = null,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _contests.Add(contest);
        SetOnlyContestActive(contest);
        _store.SaveContests(_contests);
        ReloadContests(contest.Id);
        Log($"Создан конкурс: {contest}");
    }

    private string NextContestNumber()
    {
        int max = 0;
        foreach (Contest contest in _contests)
        {
            int number;
            if (int.TryParse(contest.Number, out number) && number > max)
                max = number;
        }

        return (max + 1).ToString("000");
    }

    private void SaveContest()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        contest.Number = txtContestNo.Text.Trim();
        contest.Name = txtContestName.Text.Trim();
        contest.UpdatedAt = DateTime.Now;
        _store.SaveContests(_contests);
        SaveSettings();
        ReloadContests(contest.Id);
        UpdateWorkflowPanel(contest);
        Log($"Сохранено: {contest}");
    }

    private void SelectTemplate()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выбери Excel-файл образца",
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            FileName = txtTemplate.Text
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        txtTemplate.Text = dialog.FileName;
        SaveSettings();
    }

    private void SelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Куда сохранять итоговые Excel-файлы",
            SelectedPath = Directory.Exists(txtOutput.Text) ? txtOutput.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        txtOutput.Text = dialog.SelectedPath;
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.TemplatePath = txtTemplate.Text.Trim();
        _settings.OutputFolder = txtOutput.Text.Trim();
        _store.SaveSettings(_settings);
    }

    private void SaveLayoutToSettingsBlob(bool log)
    {
        try
        {
            _layoutStore.Save(
                this,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SelectedContestId"] = CurrentContest?.Id ?? string.Empty,
                    ["SelectedSectionIndex"] = mainTabs?.SelectedTabPageIndex.ToString() ?? "0"
                });

            if (log)
            {
                Log("Layout сохранён: " + _layoutStore.LayoutStorage);
                MessageBox.Show(
                    this,
                    "Layout сохранён в базе:" + Environment.NewLine + _layoutStore.LayoutStorage,
                    "Layout в базе",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            if (log)
                MessageBox.Show(this, ex.Message, "Ошибка сохранения layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка сохранения layout: " + ex.Message);
        }
    }

    private void ApplyLayoutFromSettingsBlob(bool log)
    {
        try
        {
            IReadOnlyDictionary<string, string> app = _layoutStore.Apply(this);
            if (app.TryGetValue("SelectedContestId", out string? selectedContestId) && !string.IsNullOrWhiteSpace(selectedContestId))
            {
                int index = _contests.FindIndex(x => string.Equals(x.Id, selectedContestId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && index < cboContests.Items.Count)
                    cboContests.SelectedIndex = index;
            }

            if (app.TryGetValue("SelectedSectionIndex", out string? selectedSectionText) && int.TryParse(selectedSectionText, out int selectedSection))
                NavigateToSection(selectedSection);

            if (log)
                Log("Layout загружен: " + _layoutStore.LayoutStorage);
        }
        catch (Exception ex)
        {
            Log("Layout не применён: " + ex.Message);
        }
    }

    private void ResetLayoutSettingsBlob()
    {
        try
        {
            _layoutStore.Delete();
            Log("Layout в базе очищен. При следующем запуске откроется стандартная раскладка.");
            MessageBox.Show(
                this,
                "Layout в базе очищен." + Environment.NewLine + "При следующем запуске откроется стандартная раскладка.",
                "Layout в базе",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка сброса layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка сброса layout: " + ex.Message);
        }
    }

    private ImportResult? ParseCurrentText()
    {
        var contest = CurrentContest;
        if (contest is null)
        {
            MessageBox.Show(this, "Сначала выбери или создай конкурс.", "Нет конкурса", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var autoWarnings = new List<string>();
        bool rulesChanged = _rulesAutoFix.EnsureRules(contest, txtVotes.Text, autoWarnings);
        if (rulesChanged)
        {
            _store.SaveContests(_contests);
            BindContestSettings(contest);
        }

        var result = _parser.Parse(txtVotes.Text, contest.Id, contest);
        foreach (string warning in autoWarnings)
            result.Warnings.Add(warning);
        _rules.Apply(contest, result);
        if (result.VoteCount == 0)
        {
            MessageBox.Show(this, "Оценки не найдены. Проверь, что строки вида: 01 - 3", "Нет оценок", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return result;
    }

    private void PreviewVotes()
    {
        var result = ParseCurrentText();
        if (result is null)
            return;

        var rows = result.Blocks
            .SelectMany(block => block.Votes.Select(v => new GridRow(v)))
            .OrderBy(x => x.VoterName)
            .ThenBy(x => x.WorkNo)
            .ToList();

        grid.DataSource = rows;
        Log(BuildImportSummary("Распознано", result));
        foreach (var warning in result.Warnings)
            Log("⚠ " + warning);
    }

    private void ImportVotes()
    {
        var contest = CurrentContest;
        var result = ParseCurrentText();
        if (contest is null || result is null)
            return;

        var votes = _store.LoadVotes(contest.Id);
        VoteImportChangeSummary changes = _voteImportReports.BuildChangeSummary(result, votes);

        string? reportFolder = null;
        try
        {
            reportFolder = _voteImportReports.SavePreview(contest, result, votes);
        }
        catch (Exception ex)
        {
            Log("Не удалось сохранить отчёт авточека, импорт продолжается: " + ex.Message);
        }

        foreach (var block in result.Blocks)
        {
            var key = NameNormalizer.Normalize(block.VoterName);
            votes.RemoveAll(x => x.VoterKey == key);

            foreach (var vote in block.Votes)
            {
                vote.ContestId = contest.Id;
                vote.VoterKey = key;
                vote.UpdatedAt = DateTime.Now;
                votes.Add(vote);
            }
        }

        _store.SaveVotes(contest.Id, votes);
        EnsureContestSettingsFromImport(contest, result);
        _store.SaveContests(_contests);
        string rmDbPath = _rmStore.Sync(contest, votes);
        BindContestSettings(contest);
        LoadGridFromStore();
        RefreshVoteStatus(log: false);
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        Log(BuildImportSummary("Принято в базу после авточека", result) + " Повторы заменены последними данными.");
        Log($"Авточек правил: исправлено {changes.ChangedByRules}, самоголосов в 0 {changes.SelfVotesToZero}, заменено старых голосов {changes.ReplacedVotes}.");
        if (!string.IsNullOrWhiteSpace(reportFolder))
            Log("Отчёт авточека сохранён: " + reportFolder);
        Log("SQLite-база обновлена: " + rmDbPath);
    }

    private void SaveVotePreviewReport(bool openFolder)
    {
        var contest = CurrentContest;
        var result = ParseCurrentText();
        if (contest is null || result is null)
            return;

        try
        {
            string folder = _voteImportReports.SavePreview(contest, result, _store.LoadVotes(contest.Id));
            Log("Отчёт авточека правил сохранён: " + folder);
            if (openFolder)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка отчёта авточека", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка отчёта авточека: " + ex.Message);
        }
    }

    private void GenerateExcel()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        SaveSettings();
        SaveContestSettings(log: false);

        var votes = _store.LoadVotes(contest.Id);
        if (votes.Count == 0 && contest.Voters.Count == 0)
        {
            MessageBox.Show(this, "В базе этого конкурса пока нет голосов и в настройках нет голосующих.", "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (contest.Works.Count == 0)
        {
            MessageBox.Show(this, "В настройках конкурса нет работ. Заполни вкладку \"Настройки конкурса\" или нажми \"№ работ из голосов\".", "Нет работ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _rmStore.Sync(contest, votes);
            var output = _builder.BuildAndOpen(txtTemplate.Text.Trim(), txtOutput.Text.Trim(), contest, votes);
            RefreshResults(log: false);
            Log("Excel сформирован и открыт: " + output);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка генерации Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка: " + ex);
        }
    }

    private void LoadGridFromStore()
    {
        var contest = CurrentContest;
        if (contest is null || grid is null)
        {
            if (grid is not null)
                grid.DataSource = new List<GridRow>();
            RefreshVoteStatus(log: false);
            RefreshResults(log: false);
            RefreshRuleChanges(log: false);
            return;
        }

        var rows = _store.LoadVotes(contest.Id)
            .OrderBy(x => x.VoterName)
            .ThenBy(x => x.WorkNo)
            .Select(x => new GridRow(x))
            .ToList();

        grid.DataSource = rows;
        RefreshVoteStatus(log: false);
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        RefreshContestList();
    }

    private void ClearContestVotes()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        var answer = MessageBox.Show(
            this,
            $"Очистить все сохранённые голоса конкурса \"{contest}\"?",
            "Подтверждение",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return;

        _store.ClearVotes(contest.Id);
        _rmStore.Sync(contest, new List<VoteEntry>());
        LoadGridFromStore();
        RefreshVoteStatus(log: false);
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        Log("Голоса конкурса очищены.");
    }

    private void OpenRhymeMachineDatabase()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        try
        {
            SaveContestSettings(log: false);
            string path = _rmStore.Sync(contest, _store.LoadVotes(contest.Id));
            if (File.Exists(path))
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
                {
                    UseShellExecute = true
                };
                process.Start();
            }
            Log("База проекта открыта в проводнике: " + path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка открытия базы", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка открытия базы: " + ex.Message);
        }
    }

    private void AddWorkRow()
    {
        _worksBinding.Add(new ContestWork { Number = GetNextWorkNumber() });
    }

    private int GetNextWorkNumber()
    {
        int next = 1;
        foreach (ContestWork work in _worksBinding)
        {
            if (work.Number >= next)
                next = work.Number + 1;
        }

        return next;
    }

    private void DeleteSelectedWorkRows()
    {
        if (gridWorks is null)
            return;

        viewWorks.CloseEditor();
        viewWorks.UpdateCurrentRow();
        int[] selectedHandles = viewWorks.GetSelectedRows();
        if (selectedHandles.Length == 0 && viewWorks.FocusedRowHandle >= 0)
            selectedHandles = new[] { viewWorks.FocusedRowHandle };

        var indexes = selectedHandles
            .Where(x => x >= 0)
            .Select(x => viewWorks.GetDataSourceRowIndex(x))
            .Where(x => x >= 0 && x < _worksBinding.Count)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        foreach (int index in indexes)
            _worksBinding.RemoveAt(index);
    }

    private void AddWorkNumbersFromVotes()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        var existing = new HashSet<int>(_worksBinding.Where(x => x.Number > 0).Select(x => x.Number));
        var votes = _store.LoadVotes(contest.Id);
        var numbers = votes.Select(x => x.WorkNo).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
        int added = 0;

        foreach (int number in numbers)
        {
            if (existing.Add(number))
            {
                _worksBinding.Add(new ContestWork { Number = number, Author = (chkHostKnowsAuthors?.Checked ?? true) ? string.Empty : WorkTextImporter.UnknownAuthor });
                added++;
            }
        }

        Log(added == 0 ? "Новых номеров работ из голосов не найдено." : $"Добавлено номеров работ из голосов: {added}.");
    }


    private bool ConfirmSpellCheck(WorkSpellCheckReport report, string caption)
    {
        if (!report.HasIssues)
            return true;

        using var form = new Form
        {
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            Width = 820,
            Height = 560,
            MinimumSize = new Size(680, 430)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = report.HasErrors
                ? "Перед сохранением найдены ошибки/подозрительные места. Можно вернуться и исправить или принять работу всё равно."
                : "Перед сохранением найдены подозрительные места. Это не блокировка, но лучше проверить."
        }, 0, 0);

        var text = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = report.BuildText()
        };
        root.Controls.Add(text, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnAccept = new Button { Text = "Принять всё равно", Width = 160, Height = 34, DialogResult = DialogResult.OK };
        var btnBack = new Button { Text = "Вернуться исправить", Width = 170, Height = 34, DialogResult = DialogResult.Cancel };
        var btnCopy = new Button { Text = "Скопировать отчёт", Width = 155, Height = 34 };
        btnCopy.Click += (_, _) => Clipboard.SetText(report.BuildText());
        buttons.Controls.Add(btnAccept);
        buttons.Controls.Add(btnBack);
        buttons.Controls.Add(btnCopy);
        root.Controls.Add(buttons, 0, 2);
        form.AcceptButton = btnAccept;
        form.CancelButton = btnBack;

        return form.ShowDialog(this) == DialogResult.OK;
    }

    private void ReceiveSingleWorkDialog()
    {
        using var form = new Form
        {
            Text = "Принять одну работу от автора",
            StartPosition = FormStartPosition.CenterParent,
            Width = 900,
            Height = 720,
            MinimumSize = new Size(760, 560)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Как в старом Delphi-приёме: автор назначается отдельно, тема выбирается/извлекается отдельно, а полный текст сохраняется как содержимое работы. Вставь текст, укажи автора; первая строка станет названием, тема может быть в скобках: \"ПОЭТ ВО СНЕ И НАЯВУ (Тема)\". Номер можно оставить автоматическим."
        }, 0, 0);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var nudNumber = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999,
            Value = Math.Clamp(GetNextWorkNumber(), 0, 999),
            Dock = DockStyle.Fill
        };
        var txtAuthor = new TextBox { Dock = DockStyle.Fill };
        top.Controls.Add(new Label { Text = "№:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        top.Controls.Add(nudNumber, 1, 0);
        top.Controls.Add(new Label { Text = "Автор:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
        top.Controls.Add(txtAuthor, 3, 0);
        root.Controls.Add(top, 0, 1);

        var txtWork = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = true,
            Text = SafeGetClipboardText()
        };
        root.Controls.Add(txtWork, 0, 2);

        var chkOverwrite = new CheckBox
        {
            Text = "Если такой № уже есть - заменить название, автора, тему и текст работы",
            Checked = true,
            Dock = DockStyle.Fill
        };
        root.Controls.Add(chkOverwrite, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        SingleWorkSubmission? acceptedSubmission = null;
        bool acceptedOverwrite = true;

        var btnOk = new Button { Text = "Принять", Width = 120, Height = 34 };
        btnOk.Click += (_, _) =>
        {
            int number = (int)nudNumber.Value;
            if (number <= 0)
                number = GetNextWorkNumber();

            SingleWorkSubmission submission = _singleWorkImporter.Parse(txtWork.Text, txtAuthor.Text, number);
            if (string.IsNullOrWhiteSpace(submission.Author))
            {
                MessageBox.Show(form, "Укажи автора работы. В этом режиме ведущий назначает автора вручную.", "Принять работу", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(submission.Title))
            {
                MessageBox.Show(form, "Не нашёл название работы. Первая содержательная строка должна быть заголовком.", "Принять работу", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            WorkSpellCheckReport spellReport = _spellChecker.CheckSubmission(submission);
            if (!ConfirmSpellCheck(spellReport, "Проверка работы перед сохранением"))
                return;

            acceptedSubmission = submission;
            acceptedOverwrite = chkOverwrite.Checked;
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 4);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK || acceptedSubmission is null)
            return;

        int actualNumber = ReceiveSingleWork(acceptedSubmission, acceptedOverwrite);
        SaveContestSettings(log: false);
        Log($"Принята работа №{actualNumber:00}: {acceptedSubmission.Title} - {acceptedSubmission.Author}" + (string.IsNullOrWhiteSpace(acceptedSubmission.Topic) ? "." : $"; тема: {acceptedSubmission.Topic}."));
    }

    private int ReceiveSingleWork(SingleWorkSubmission submission, bool overwriteExisting)
    {
        ContestWork imported = _singleWorkImporter.ToContestWork(submission);
        ContestWork? existing = _worksBinding.FirstOrDefault(x => x.Number == imported.Number);
        if (existing is null)
        {
            _worksBinding.Add(imported);
            SortWorksBinding();
            return imported.Number;
        }

        if (!overwriteExisting)
        {
            int newNumber = GetNextWorkNumber();
            imported.Number = newNumber;
            _worksBinding.Add(imported);
            SortWorksBinding();
            return imported.Number;
        }

        existing.Title = imported.Title;
        existing.Author = imported.Author;
        existing.Topic = imported.Topic;
        existing.Content = imported.Content;
        SortWorksBinding();
        return existing.Number;
    }

    private void ImportWorksFromTextDialog()
    {
        using var form = new Form
        {
            Text = "Импорт работ / авторов из текста",
            StartPosition = FormStartPosition.CenterParent,
            Width = 900,
            Height = 650,
            MinimumSize = new Size(720, 520)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Импорт работает в двух режимах. Если учёт ведёт ведущий - авторы берутся из строки. Если счёт ведёт сторонний человек до раскрытия - автор всегда будет \"Неизвестный автор\". После закрытия можно применить список авторства к уже созданным работам."
        }, 0, 0);

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            Text = string.IsNullOrWhiteSpace(txtVotes?.Text) ? SafeGetClipboardText() : txtVotes.Text
        };
        root.Controls.Add(textBox, 0, 1);

        bool hostKnowsAuthors = chkHostKnowsAuthors?.Checked ?? CurrentContest?.HostKnowsAuthors ?? true;
        var modePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var rbHost = new RadioButton
        {
            Text = "Я ведущий: авторы известны, брать автора из строки",
            AutoSize = true,
            Checked = hostKnowsAuthors
        };
        var rbHidden = new RadioButton
        {
            Text = "Счётчик до раскрытия: все авторы = \"Неизвестный автор\"",
            AutoSize = true,
            Checked = !hostKnowsAuthors
        };
        var rbApplyAuthors = new RadioButton
        {
            Text = "После раскрытия: применить авторство к существующим работам",
            AutoSize = true
        };
        modePanel.Controls.Add(rbHost);
        modePanel.Controls.Add(rbHidden);
        modePanel.Controls.Add(rbApplyAuthors);
        root.Controls.Add(modePanel, 0, 2);

        var replaceCheck = new CheckBox
        {
            Dock = DockStyle.Fill,
            Text = "Заменить существующий список работ полностью",
            Checked = false
        };
        root.Controls.Add(replaceCheck, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnOk = new Button { Text = "Импортировать", Width = 140, Height = 34, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 4);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        WorkTextImportMode mode = rbApplyAuthors.Checked
            ? WorkTextImportMode.ApplyAuthorsAfterClose
            : rbHidden.Checked
                ? WorkTextImportMode.WorksBeforeClose
                : WorkTextImportMode.WorksWithKnownAuthors;

        WorkTextImportResult result = _workImporter.Parse(textBox.Text, mode);
        if (result.Count == 0)
        {
            MessageBox.Show(this, "Работы в тексте не найдены. Поддерживаемые строки: 01 - Название; 01 - Название - Автор; 01 - Автор для режима раскрытия.", "Импорт работ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        WorkSpellCheckReport spellReport = _spellChecker.CheckWorks(result.Works);
        if (!ConfirmSpellCheck(spellReport, "Проверка импортируемых работ перед сохранением"))
            return;

        ApplyImportedWorks(result.Works, replaceCheck.Checked);
        SaveContestSettings(log: false);
        string modeText = mode == WorkTextImportMode.ApplyAuthorsAfterClose
            ? "раскрытие авторства"
            : mode == WorkTextImportMode.WorksBeforeClose
                ? "до раскрытия, авторы скрыты"
                : "ведущий, авторы известны";
        Log($"Импорт работ: найдено {result.Count}, дублей номеров {result.DuplicateNumbers}, режим: {modeText}, {(replaceCheck.Checked ? "замена" : "дополнение/обновление")}.");
        foreach (string warning in result.Warnings)
            Log("⚠ " + warning);
    }

    private void ImportWorksFromPrivateMessagesDialog()
    {
        if (CurrentContest is null)
        {
            MessageBox.Show(this, "Сначала выбери или создай конкурс.", "Импорт работ из лички", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new Form
        {
            Text = "Импорт работ из лички",
            StartPosition = FormStartPosition.CenterParent,
            Width = 1100,
            Height = 760,
            MinimumSize = new Size(860, 600)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        form.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Вставь сообщения авторов. Форматы: \"Автор: Иван Иванов\" + \"Название: Осенний дождь\" + текст; или простым блоком: автор, название, текст. Несколько работ можно разделять строкой ---."
        }, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520
        };
        root.Controls.Add(split, 0, 1);

        var txtMessages = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = true,
            Text = SafeGetClipboardText()
        };
        split.Panel1.Controls.Add(txtMessages);

        var previewGrid = new System.Windows.Forms.DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "№", DataPropertyName = nameof(PrivateMessageWorkPreviewRow.Number), FillWeight = 30 });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Автор", DataPropertyName = nameof(PrivateMessageWorkPreviewRow.Author), FillWeight = 90 });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Название", DataPropertyName = nameof(PrivateMessageWorkPreviewRow.Title), FillWeight = 120 });
        previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Статус", DataPropertyName = nameof(PrivateMessageWorkPreviewRow.Status), FillWeight = 120 });
        split.Panel2.Controls.Add(previewGrid);

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var nudFirstNumber = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 999,
            Value = Math.Clamp(GetNextWorkNumber(), 1, 999),
            Dock = DockStyle.Fill
        };
        var replaceCheck = new CheckBox
        {
            Text = "Заменять работы с теми же номерами",
            Checked = false,
            Dock = DockStyle.Fill
        };
        settings.Controls.Add(new Label { Text = "Начать с №:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        settings.Controls.Add(nudFirstNumber, 1, 0);
        settings.Controls.Add(replaceCheck, 2, 0);
        root.Controls.Add(settings, 0, 2);

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(74, 85, 104)
        };
        root.Controls.Add(statusLabel, 0, 3);

        PrivateMessageWorkImportResult? previewResult = null;
        List<ContestWork> BuildWorksForImport()
        {
            if (previewResult is null)
                return new List<ContestWork>();

            var works = previewResult.Works.Select(x => x.Clone()).ToList();
            if (replaceCheck.Checked)
                return works;

            var usedNumbers = new HashSet<int>(_worksBinding.Where(x => x.Number > 0).Select(x => x.Number));
            int nextNumber = Math.Max((int)nudFirstNumber.Value, GetNextWorkNumber());
            foreach (ContestWork work in works)
            {
                if (work.Number <= 0 || usedNumbers.Contains(work.Number))
                {
                    while (usedNumbers.Contains(nextNumber))
                        nextNumber++;
                    work.Number = nextNumber;
                }

                usedNumbers.Add(work.Number);
            }

            return works;
        }

        void RefreshPreview()
        {
            previewResult = _privateMessageWorkImporter.Parse(txtMessages.Text, (int)nudFirstNumber.Value);
            var existingNumbers = new HashSet<int>(_worksBinding.Select(x => x.Number));
            var rows = previewResult.Works
                .Select(x => new PrivateMessageWorkPreviewRow
                {
                    Number = x.Number,
                    Author = x.Author,
                    Title = x.Title,
                    Status = existingNumbers.Contains(x.Number)
                        ? (replaceCheck.Checked ? "заменит существующую" : "получит свободный номер")
                        : "готова к импорту"
                })
                .ToList();

            previewGrid.DataSource = rows;
            statusLabel.Text = $"Найдено работ: {previewResult.Count}. Пропущено блоков: {previewResult.RejectedBlocks}.";
            if (previewResult.Warnings.Count > 0)
                statusLabel.Text += " Есть предупреждения - они попадут в журнал.";
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnImport = new Button { Text = "Импортировать", Width = 140, Height = 34 };
        var btnPreview = new Button { Text = "Проверить", Width = 110, Height = 34 };
        var btnPaste = new Button { Text = "Из буфера", Width = 110, Height = 34 };
        var btnClear = new Button { Text = "Очистить", Width = 100, Height = 34 };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };

        btnPaste.Click += (_, _) =>
        {
            txtMessages.Text = SafeGetClipboardText();
            RefreshPreview();
        };
        btnClear.Click += (_, _) =>
        {
            txtMessages.Clear();
            RefreshPreview();
        };
        btnPreview.Click += (_, _) => RefreshPreview();
        btnImport.Click += (_, _) =>
        {
            RefreshPreview();
            if (previewResult is null || previewResult.Count == 0)
            {
                MessageBox.Show(form, "Работы не найдены. Проверь формат: автор, название и текст работы.", "Импорт работ из лички", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ContestWork> worksForImport = BuildWorksForImport();
            WorkSpellCheckReport spellReport = _spellChecker.CheckWorks(worksForImport);
            if (!ConfirmSpellCheck(spellReport, "Проверка работ из лички перед сохранением"))
                return;

            ApplyImportedWorks(worksForImport, replaceCheck.Checked);
            SaveContestSettings(log: false);
            SetCurrentContestStage(ContestStage.WorkReception);
            Log($"Импорт работ из лички: добавлено/обновлено {worksForImport.Count}, стартовый номер {(int)nudFirstNumber.Value}.");
            foreach (string warning in previewResult.Warnings)
                Log("Предупреждение импорта из лички: " + warning);
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        buttons.Controls.Add(btnImport);
        buttons.Controls.Add(btnPreview);
        buttons.Controls.Add(btnPaste);
        buttons.Controls.Add(btnClear);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 4);
        form.CancelButton = btnCancel;

        RefreshPreview();
        form.ShowDialog(this);
    }

    private sealed class PrivateMessageWorkPreviewRow
    {
        public int Number { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private void ApplyImportedWorks(IEnumerable<ContestWork> importedWorks, bool replaceExisting)
    {
        if (replaceExisting)
            _worksBinding.Clear();

        var byNumber = new Dictionary<int, ContestWork>();
        foreach (ContestWork work in _worksBinding.Where(x => x.Number > 0))
        {
            if (!byNumber.ContainsKey(work.Number))
                byNumber.Add(work.Number, work);
        }

        int added = 0;
        int updated = 0;
        foreach (ContestWork imported in importedWorks.OrderBy(x => x.Number))
        {
            if (imported.Number <= 0)
                continue;

            if (byNumber.TryGetValue(imported.Number, out ContestWork? existing))
            {
                if (!string.IsNullOrWhiteSpace(imported.Title))
                    existing.Title = imported.Title.Trim();
                if (!string.IsNullOrWhiteSpace(imported.Author))
                {
                    string importedAuthor = imported.Author.Trim();
                    bool importedIsUnknown = importedAuthor.Equals(WorkTextImporter.UnknownAuthor, StringComparison.OrdinalIgnoreCase);
                    bool existingIsEmptyOrUnknown = string.IsNullOrWhiteSpace(existing.Author)
                        || existing.Author.Trim().Equals(WorkTextImporter.UnknownAuthor, StringComparison.OrdinalIgnoreCase);

                    // "Неизвестный автор" не должен затирать уже раскрытого автора при повторном импорте.
                    if (!importedIsUnknown || existingIsEmptyOrUnknown)
                        existing.Author = importedAuthor;
                }
                if (!string.IsNullOrWhiteSpace(imported.Topic))
                    existing.Topic = imported.Topic.Trim();
                updated++;
            }
            else
            {
                var clone = imported.Clone();
                _worksBinding.Add(clone);
                byNumber.Add(clone.Number, clone);
                added++;
            }
        }

        SortWorksBinding();
        Log($"Работы обновлены: добавлено {added}, обновлено {updated}.");
    }

    private void SortWorksBinding()
    {
        var ordered = _worksBinding
            .Where(x => x.Number > 0)
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Title)
            .Select(x => x.Clone())
            .ToList();

        _worksBinding.Clear();
        foreach (ContestWork work in ordered)
            _worksBinding.Add(work);
    }

    private static string SafeGetClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void AddVotersFromStoredVotes()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        var existing = new HashSet<string>(
            _votersBinding.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        var votes = _store.LoadVotes(contest.Id)
            .GroupBy(x => x.VoterKey)
            .Select(g => g.Last())
            .OrderBy(x => x.VoterName)
            .ToList();

        int added = 0;
        foreach (VoteEntry vote in votes)
        {
            var key = NameNormalizer.Normalize(vote.VoterName);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (existing.Add(key))
            {
                _votersBinding.Add(new VoterSetting { Name = vote.VoterName });
                added++;
            }
        }

        Log(added == 0 ? "Новых голосующих из базы не найдено." : $"Добавлено голосующих из базы: {added}.");
    }



    private void AddAuthorsToVoters()
    {
        var existing = new HashSet<string>(
            _votersBinding.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (ContestWork work in _worksBinding.OrderBy(x => x.Number))
        {
            string name = (work.Author ?? string.Empty).Trim();
            string key = NameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(key) || !existing.Add(key))
                continue;

            _votersBinding.Add(new VoterSetting { Name = name, MustVote = true });
            added++;
        }

        Log(added == 0 ? "Новых авторов для списка судей не найдено." : $"Добавлено авторов в список судей: {added}.");
    }

    private void RefreshVoteStatus(bool log = true)
    {
        var contest = CurrentContest;
        if (contest is null || gridStatus is null)
        {
            if (gridStatus is not null)
                gridStatus.DataSource = new BindingList<VoterStatusRow>();
            return;
        }

        var votes = _store.LoadVotes(contest.Id);
        ContestAuditReport report = _audit.BuildReport(contest, votes);
        _statusBinding = new BindingList<VoterStatusRow>(report.Rows);
        gridStatus.DataSource = _statusBinding;
        PaintStatusGrid();

        if (log)
        {
            Log($"Контроль: обязательных судей {report.RequiredVoters}, полностью проголосовали {report.CompletedVoters}, должников {report.Debtors}, неизвестных голосующих {report.UnknownVoters}.");
            string debtors = _audit.BuildDebtorsText(report);
            if (!debtors.StartsWith("Должников", StringComparison.OrdinalIgnoreCase))
                Log(debtors.Replace(Environment.NewLine, "; "));
        }
    }

    private void PaintStatusGrid()
    {
        viewStatus?.RefreshData();
    }

    private void PaintStatusRow(RowStyleEventArgs e)
    {
        if (e.RowHandle < 0 || viewStatus.GetRow(e.RowHandle) is not VoterStatusRow status)
            return;

        if (status.IsUnknownVoter)
            e.Appearance.BackColor = Color.FromArgb(255, 245, 180);
        else if (status.IsDebtor)
            e.Appearance.BackColor = Color.FromArgb(255, 220, 220);
        else if (status.AcceptedVotes > 0)
            e.Appearance.BackColor = Color.FromArgb(225, 245, 225);
        else
            e.Appearance.BackColor = Color.White;
    }

    private void CopyDebtorsToClipboard()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        ContestAuditReport report = _audit.BuildReport(contest, _store.LoadVotes(contest.Id));
        string text = _audit.BuildDebtorsText(report);
        Clipboard.SetText(text);
        Log("Список должников скопирован в буфер обмена.");
    }

    private void AddUnknownVotersToSettings()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        ContestAuditReport report = _audit.BuildReport(contest, _store.LoadVotes(contest.Id));
        var existing = new HashSet<string>(
            _votersBinding.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (VoterStatusRow row in report.Rows.Where(x => x.IsUnknownVoter))
        {
            string key = NameNormalizer.Normalize(row.VoterName);
            if (string.IsNullOrWhiteSpace(key) || !existing.Add(key))
                continue;

            _votersBinding.Add(new VoterSetting { Name = row.VoterName, MustVote = true });
            added++;
        }

        if (added > 0)
            SaveContestSettings(log: false);

        Log(added == 0 ? "Неизвестных голосующих для добавления нет." : $"Добавлено неизвестных голосующих в настройки: {added}.");
        RefreshVoteStatus(log: false);
        RefreshPeopleDirectory(log: false);
    }

    private void RefreshPeopleDirectory(bool log = false)
    {
        Contest? contest = CurrentContest;
        if (gridPeople is null)
            return;

        if (contest is null)
        {
            _peopleBinding = new BindingList<PersonDirectoryRow>();
            gridPeople.DataSource = _peopleBinding;
            return;
        }

        List<VoteEntry> votes = _store.LoadVotes(contest.Id);
        ContestAuditReport auditReport = _audit.BuildReport(contest, votes);
        var rows = BuildPeopleDirectoryRows(contest, auditReport);
        _peopleBinding = new BindingList<PersonDirectoryRow>(rows);
        gridPeople.DataSource = _peopleBinding;

        if (log)
            Log($"Участники обновлены: {rows.Count}; авторов {rows.Count(x => x.IsAuthor)}, судей {rows.Count(x => x.IsVoter)}, неизвестных голосующих {rows.Count(x => x.IsUnknownVoter)}.");
    }

    private List<PersonDirectoryRow> BuildPeopleDirectoryRows(Contest contest, ContestAuditReport auditReport)
    {
        var map = new Dictionary<string, PersonDirectoryRow>(StringComparer.OrdinalIgnoreCase);

        PersonDirectoryRow GetOrAdd(string name)
        {
            string clean = (name ?? string.Empty).Trim();
            string key = NameNormalizer.Normalize(clean);
            if (string.IsNullOrWhiteSpace(key))
                key = clean.ToUpperInvariant();

            if (!map.TryGetValue(key, out PersonDirectoryRow? row))
            {
                row = new PersonDirectoryRow(clean);
                map[key] = row;
            }

            return row;
        }

        if (!string.IsNullOrWhiteSpace(contest.HostName))
            GetOrAdd(contest.HostName).MarkHost();

        if (!string.IsNullOrWhiteSpace(contest.NextHostName))
            GetOrAdd(contest.NextHostName).MarkNextHost();

        foreach (ContestWork work in contest.Works.OrderBy(x => x.Number))
        {
            string author = (work.Author ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(author) || author.Equals(WorkTextImporter.UnknownAuthor, StringComparison.OrdinalIgnoreCase))
                continue;

            GetOrAdd(author).AddWork(work.Number, work.Title);
        }

        foreach (VoterSetting voter in contest.Voters.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            GetOrAdd(voter.Name).MarkVoter(voter.MustVote);

        foreach (VoterStatusRow status in auditReport.Rows)
            GetOrAdd(status.VoterName).ApplyStatus(status);

        return map.Values
            .OrderByDescending(x => x.IsHost)
            .ThenByDescending(x => x.IsNextHost)
            .ThenByDescending(x => x.IsAuthor)
            .ThenByDescending(x => x.IsRequiredVoter)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private List<PersonDirectoryRow> GetSelectedPeople()
    {
        if (viewPeople is null)
            return new List<PersonDirectoryRow>();

        int[] handles = viewPeople.GetSelectedRows();
        if (handles.Length == 0 && viewPeople.FocusedRowHandle >= 0)
            handles = new[] { viewPeople.FocusedRowHandle };

        return handles
            .Select(handle => viewPeople.GetRow(handle) as PersonDirectoryRow)
            .Where(row => row is not null && !string.IsNullOrWhiteSpace(row.Name))
            .Cast<PersonDirectoryRow>()
            .GroupBy(row => NameNormalizer.Normalize(row.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void AssignSelectedPersonAsHost(bool nextHost)
    {
        Contest? contest = CurrentContest;
        if (contest is null)
            return;

        PersonDirectoryRow? person = GetSelectedPeople().FirstOrDefault();
        if (person is null)
        {
            MessageBox.Show(this, "Выбери человека в списке участников.", "Участники", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (nextHost)
            contest.NextHostName = person.Name;
        else
            contest.HostName = person.Name;

        contest.UpdatedAt = DateTime.Now;
        _store.SaveContests(_contests);
        RefreshContestList();
        RefreshContestCard(contest);
        UpdateWorkflowPanel(contest);
        RefreshPeopleDirectory(log: false);
        Log(nextHost ? $"Следующий ведущий назначен: {person.Name}." : $"Ведущий назначен: {person.Name}.");
    }

    private void AddSelectedPeopleToVoters()
    {
        Contest? contest = CurrentContest;
        if (contest is null)
            return;

        List<PersonDirectoryRow> selected = GetSelectedPeople();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Выбери одного или нескольких людей.", "Участники", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var existing = new HashSet<string>(
            _votersBinding.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (PersonDirectoryRow person in selected)
        {
            string key = NameNormalizer.Normalize(person.Name);
            if (string.IsNullOrWhiteSpace(key) || !existing.Add(key))
                continue;

            _votersBinding.Add(new VoterSetting { Name = person.Name, MustVote = true });
            added++;
        }

        if (added > 0)
            SaveContestSettings(log: false);

        RefreshVoteStatus(log: false);
        RefreshPeopleDirectory(log: false);
        Log(added == 0 ? "Выбранные люди уже есть в списке судей." : $"Добавлено в обязательные судьи: {added}.");
    }


    private ContestResultsReport? RefreshResults(bool log = true)
    {
        var contest = CurrentContest;
        if (contest is null || gridResults is null || txtFinalReport is null)
        {
            if (gridResults is not null)
                gridResults.DataSource = new BindingList<ContestRatingRow>();
            if (txtFinalReport is not null)
                txtFinalReport.Text = string.Empty;
            return null;
        }

        var votes = _store.LoadVotes(contest.Id);
        ContestResultsReport report = _results.BuildReport(contest, votes);
        _ratingBinding = new BindingList<ContestRatingRow>(report.Rows);
        gridResults.DataSource = _ratingBinding;
        txtFinalReport.Text = _results.BuildFinalText(contest, report);
        PaintResultsGrid();

        if (log)
            Log($"Итоги: работ {report.WorkCount}, судей {report.VoterCount}, принято голосов {report.AcceptedVoteCount}, самоголосов в 0 {report.SelfVoteCount}.");

        return report;
    }

    private void PaintResultsGrid()
    {
        viewResults?.RefreshData();
    }

    private void PaintResultRow(RowStyleEventArgs e)
    {
        if (e.RowHandle < 0 || viewResults.GetRow(e.RowHandle) is not ContestRatingRow result)
            return;

        if (result.PlaceNo == 1)
            e.Appearance.BackColor = Color.FromArgb(255, 245, 190);
        else if (result.PlaceNo == 2)
            e.Appearance.BackColor = Color.FromArgb(235, 240, 250);
        else if (result.PlaceNo == 3)
            e.Appearance.BackColor = Color.FromArgb(245, 226, 204);
        else
            e.Appearance.BackColor = Color.White;
    }

    private void CopyFinalReportToClipboard()
    {
        ContestResultsReport? report = RefreshResults(log: false);
        if (report is null || txtFinalReport is null)
            return;

        Clipboard.SetText(txtFinalReport.Text);
        Log("Итоговый протокол скопирован в буфер обмена.");
    }

    private void CopyWinnersToClipboard()
    {
        ContestResultsReport? report = RefreshResults(log: false);
        if (report is null)
            return;

        Clipboard.SetText(_results.BuildWinnersText(report));
        Log("Список победителей скопирован в буфер обмена.");
    }

    private void ExportCurrentContestReportPackage()
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            ContestReportExportResult export = _reportExporter.ExportContestPackage(
                Path.Combine(_store.RootFolder, "Reports"),
                contest,
                _store.LoadVotes(contest.Id));
            Cursor = Cursors.Default;

            Log("Пакет отчётов сохранён: " + export.Folder);
            MessageBox.Show(
                this,
                "Пакет отчётов сохранён:" + Environment.NewLine + export.Folder,
                "Отчёты конкурса",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, ex.Message, "Ошибка сохранения отчётов", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка сохранения отчётов: " + ex.Message);
        }
    }

    private void ExportCurrentContestHtml(bool openPrintVersion)
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            ContestReportExportResult export = _reportExporter.ExportContestPackage(
                Path.Combine(_store.RootFolder, "Reports"),
                contest,
                _store.LoadVotes(contest.Id));
            Cursor = Cursors.Default;

            string htmlPath = openPrintVersion && !string.IsNullOrWhiteSpace(export.PrintHtmlFile)
                ? export.PrintHtmlFile
                : export.HtmlFile;

            if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
            {
                MessageBox.Show(this, "HTML-файл протокола не был создан.", "HTML-протокол", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });

            Log((openPrintVersion ? "Печатная HTML-версия открыта: " : "HTML-протокол открыт: ") + htmlPath);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, ex.Message, "Ошибка HTML-протокола", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Ошибка HTML-протокола: " + ex.Message);
        }
    }

    private void OpenReportsFolder()
    {
        string folder = Path.Combine(_store.RootFolder, "Reports");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void EnsureContestSettingsFromImport(Contest contest, ImportResult result)
    {
        var voterKeys = new HashSet<string>(
            contest.Voters.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (ParsedVoteBlock block in result.Blocks)
        {
            string key = NameNormalizer.Normalize(block.VoterName);
            if (!string.IsNullOrWhiteSpace(key) && voterKeys.Add(key))
                contest.Voters.Add(new VoterSetting { Name = block.VoterName });
        }

        var workNumbers = new HashSet<int>(contest.Works.Where(x => x.Number > 0).Select(x => x.Number));
        foreach (int number in result.Blocks.SelectMany(x => x.Votes).Select(x => x.WorkNo).Distinct().OrderBy(x => x))
        {
            if (workNumbers.Add(number))
                contest.Works.Add(new ContestWork { Number = number, Author = contest.HostKnowsAuthors ? string.Empty : WorkTextImporter.UnknownAuthor });
        }

        contest.Works = contest.Works.OrderBy(x => x.Number).ThenBy(x => x.Title).ToList();
        contest.UpdatedAt = DateTime.Now;
    }

    private void SaveContestSettings(bool log = true)
    {
        var contest = CurrentContest;
        if (contest is null)
            return;

        viewWorks?.CloseEditor();
        viewWorks?.UpdateCurrentRow();
        viewVoters?.CloseEditor();
        viewVoters?.UpdateCurrentRow();

        contest.Number = txtContestNo.Text.Trim();
        contest.Name = txtContestName.Text.Trim();
        contest.VoteLimit = nudVoteLimit is null ? contest.VoteLimit : (int)nudVoteLimit.Value;
        contest.BaseVote = nudBaseVote is null ? contest.BaseVote : (int)nudBaseVote.Value;
        contest.MaxVote = nudMaxVote is null ? contest.MaxVote : (int)nudMaxVote.Value;
        contest.LimitMaxVote = nudLimitMaxVote is null ? contest.LimitMaxVote : (int)nudLimitMaxVote.Value;
        contest.AllowZeroVotes = chkAllowZeroVotes?.Checked ?? contest.AllowZeroVotes;
        contest.TreatSelfVoteAsZero = chkSelfVoteZero?.Checked ?? contest.TreatSelfVoteAsZero;
        contest.LimitMaxVoteByTopic = chkLimitMaxVoteByTopic?.Checked ?? contest.LimitMaxVoteByTopic;
        contest.OneMaxVotePerTopic = chkOneMaxVotePerTopic?.Checked ?? contest.OneMaxVotePerTopic;
        contest.DowngradeExtraMaxVoteToBase = chkDowngradeExtraMaxVote?.Checked ?? contest.DowngradeExtraMaxVoteToBase;
        contest.HostKnowsAuthors = chkHostKnowsAuthors?.Checked ?? contest.HostKnowsAuthors;
        if (contest.MaxVote < contest.BaseVote)
            contest.BaseVote = contest.MaxVote;
        contest.Works = NormalizeWorksFromGrid();
        contest.Voters = NormalizeVotersFromGrid();
        contest.UpdatedAt = DateTime.Now;

        _store.SaveContests(_contests);
        SaveSettings();
        _rmStore.Sync(contest, _store.LoadVotes(contest.Id));
        BindContestSettings(contest);
        RefreshVoteStatus(log: false);
        RefreshResults(log: false);
        RefreshRuleChanges(log: false);
        RefreshContestList();
        RefreshPeopleDirectory(log: false);

        if (log)
            Log($"Настройки конкурса сохранены: работ {contest.Works.Count}, голосующих {contest.Voters.Count}.");
    }

    private List<ContestWork> NormalizeWorksFromGrid()
    {
        var result = new List<ContestWork>();
        var used = new HashSet<int>();

        foreach (ContestWork work in _worksBinding)
        {
            if (work.Number <= 0 || !used.Add(work.Number))
                continue;

            result.Add(new ContestWork
            {
                Number = work.Number,
                Title = (work.Title ?? string.Empty).Trim(),
                Author = (work.Author ?? string.Empty).Trim(),
                Topic = (work.Topic ?? string.Empty).Trim(),
                Content = (work.Content ?? string.Empty).Trim()
            });
        }

        return result.OrderBy(x => x.Number).ThenBy(x => x.Title).ToList();
    }

    private List<VoterSetting> NormalizeVotersFromGrid()
    {
        var result = new List<VoterSetting>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (VoterSetting voter in _votersBinding)
        {
            var name = (voter.Name ?? string.Empty).Trim();
            var key = NameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(key) || !used.Add(key))
                continue;

            result.Add(new VoterSetting { Name = name, MustVote = voter.MustVote });
        }

        return result;
    }


    private void RefreshRuleChanges(bool log)
    {
        if (gridRuleChanges is null || txtRuleChangeSummary is null)
            return;

        Contest? contest = CurrentContest;
        if (contest is null)
        {
            gridRuleChanges.DataSource = new List<RuleChangeRow>();
            txtRuleChangeSummary.Text = "Конкурс не выбран.";
            return;
        }

        List<RuleChangeRow> rows = BuildRuleChangeRows(contest);
        gridRuleChanges.DataSource = rows;

        int selfVotes = rows.Count(x => x.IsSelfVote);
        int maxLimit = rows.Count(x => x.IsMaxLimitCorrection);
        int plusScores = rows.Count(x => x.IsPlusScore);
        txtRuleChangeSummary.Text =
            $"№{contest.Number} - {contest.Name}{Environment.NewLine}" +
            $"Всего правок правил: {rows.Count}; лимит максимальной оценки: {maxLimit}; самоголосов в 0: {selfVotes}; 3+ как 3.5: {plusScores}.{Environment.NewLine}" +
            "Оригинальная оценка не теряется: в таблице есть два явных значения - Проголосовал и Принято.";

        if (log)
            Log($"Правки правил обновлены: {rows.Count}.");
    }

    private List<RuleChangeRow> BuildRuleChangeRows(Contest contest)
    {
        var worksByNo = contest.Works
            .Where(x => x.Number > 0)
            .GroupBy(x => x.Number)
            .ToDictionary(x => x.Key, x => x.Last());

        return _store.LoadVotes(contest.Id)
            .Where(VoteHasRuleChange)
            .OrderBy(x => x.VoterName)
            .ThenBy(x => x.WorkNo)
            .Select(x => new RuleChangeRow(x, worksByNo))
            .ToList();
    }

    private static bool VoteHasRuleChange(VoteEntry vote)
    {
        if (vote.WasChangedByRules || !string.IsNullOrWhiteSpace(vote.RuleNote))
            return true;

        string votedText = string.IsNullOrWhiteSpace(vote.VotedScoreText) ? vote.OriginalScoreText : vote.VotedScoreText;
        string acceptedText = string.IsNullOrWhiteSpace(vote.AcceptedScoreText) ? vote.ScoreText : vote.AcceptedScoreText;
        if (!string.IsNullOrWhiteSpace(votedText) && !string.Equals(votedText, acceptedText, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((vote.ScoreText ?? string.Empty).Contains('+') || (vote.OriginalScoreText ?? string.Empty).Contains('+') || (vote.VotedScoreText ?? string.Empty).Contains('+') || (vote.ScoreText ?? string.Empty).Contains('-') || (vote.OriginalScoreText ?? string.Empty).Contains('-') || (vote.VotedScoreText ?? string.Empty).Contains('-'))
            return true;

        return vote.OriginalScore != 0m && vote.OriginalScore != vote.Score;
    }

    private void PaintRuleChangeRow(RowStyleEventArgs e)
    {
        if (e.RowHandle < 0 || viewRuleChanges is null)
            return;

        if (viewRuleChanges.GetRow(e.RowHandle) is not RuleChangeRow row)
            return;

        if (row.IsSelfVote)
            e.Appearance.BackColor = Color.FromArgb(255, 226, 226);
        else if (row.IsMaxLimitCorrection)
            e.Appearance.BackColor = Color.FromArgb(255, 244, 204);
        else if (row.IsPlusScore)
            e.Appearance.BackColor = Color.FromArgb(226, 242, 255);
        else
            e.Appearance.BackColor = Color.FromArgb(235, 247, 235);
    }

    private void CopyRuleChangesToClipboard()
    {
        Contest? contest = CurrentContest;
        if (contest is null)
            return;

        List<RuleChangeRow> rows = BuildRuleChangeRows(contest);
        if (rows.Count == 0)
        {
            Clipboard.SetText("Правок правил нет.");
            Log("Правок правил нет.");
            return;
        }

        Clipboard.SetText(BuildRuleChangesText(contest, rows));
        Log($"Правки правил скопированы: {rows.Count}.");
    }

    private void ExportRuleChangesCsv(bool openFolder)
    {
        Contest? contest = CurrentContest;
        if (contest is null)
            return;

        List<RuleChangeRow> rows = BuildRuleChangeRows(contest);
        string folder = Path.Combine(LocalDatabase.DatabaseFolder, "Reports", $"{DateTime.Now:yyyyMMdd_HHmmss}_rule_changes_{SanitizeFilePart(contest.Number)}");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "rule_changes.csv");
        File.WriteAllText(file, BuildRuleChangesCsv(rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Log("CSV правок правил сохранён: " + file);

        if (openFolder)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private static string BuildRuleChangesText(Contest contest, IReadOnlyList<RuleChangeRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Правки правил: №{contest.Number} - {contest.Name}");
        sb.AppendLine(new string('-', 72));
        foreach (RuleChangeRow row in rows)
            sb.AppendLine($"{row.VoterName}: №{row.WorkNoText} {row.OriginalScoreText} -> {row.AcceptedScoreText}; {row.RuleNote}");
        return sb.ToString();
    }

    private static string BuildRuleChangesCsv(IReadOnlyList<RuleChangeRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VoterName;WorkNo;WorkTitle;Topic;VotedScore;AcceptedScore;Delta;RuleNote;Comment");
        foreach (RuleChangeRow row in rows)
        {
            sb.Append(EscapeCsv(row.VoterName)).Append(';');
            sb.Append(EscapeCsv(row.WorkNoText)).Append(';');
            sb.Append(EscapeCsv(row.WorkTitle)).Append(';');
            sb.Append(EscapeCsv(row.Topic)).Append(';');
            sb.Append(EscapeCsv(row.OriginalScoreText)).Append(';');
            sb.Append(EscapeCsv(row.AcceptedScoreText)).Append(';');
            sb.Append(EscapeCsv(row.DeltaText)).Append(';');
            sb.Append(EscapeCsv(row.RuleNote)).Append(';');
            sb.AppendLine(EscapeCsv(row.Comment));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        string text = value ?? string.Empty;
        if (text.Contains(';') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    private static string SanitizeFilePart(string? value)
    {
        string source = value ?? string.Empty;
        var chars = source.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
        string result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "contest" : result;
    }

    private static string SanitizeIdentifier(string? value)
    {
        string source = value ?? string.Empty;
        var chars = source
            .Where(char.IsLetterOrDigit)
            .ToArray();
        string result = new string(chars);
        return string.IsNullOrWhiteSpace(result) ? "Command" : result;
    }

    private static string FormatScore(decimal value)
        => value == decimal.Truncate(value) ? ((int)value).ToString() : value.ToString("0.##");


    private static string BuildImportSummary(string prefix, ImportResult result)
    {
        var voterNames = result.Blocks
            .Select(x => x.VoterName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var workCount = result.Blocks
            .SelectMany(x => x.Votes)
            .Select(x => x.WorkNo)
            .Distinct()
            .Count();

        var entries = result.Blocks.SelectMany(x => x.Votes).ToList();
        int selfZero = entries.Count(x => x.RuleNote.Contains("самоголос", StringComparison.OrdinalIgnoreCase));
        int changed = entries.Count(x => x.WasChangedByRules);
        string voters = voterNames.Count == 0 ? "список пуст" : string.Join(", ", voterNames);
        string tail = changed > 0 ? $", исправлено правилами {changed}" : string.Empty;
        if (selfZero > 0)
            tail += $", самоголосов в 0: {selfZero}";

        return $"{prefix}: проголосовало {voterNames.Count} судей ({voters}), за {workCount} работ, принято {result.VoteCount} голосов{tail}.";
    }

    private void Log(string message)
    {
        if (txtLog is null)
            return;

        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private sealed record TopicImportLine(int SourceLine, ContestTopic? Topic);

    private sealed class TopicImportPreview
    {
        public TopicImportPreview(List<TopicImportPreviewRow> rows, List<ContestTopic> acceptedTopics)
        {
            Rows = rows;
            AcceptedTopics = acceptedTopics;
        }

        public List<TopicImportPreviewRow> Rows { get; }
        public List<ContestTopic> AcceptedTopics { get; }
        public int FoundCount => Rows.Count;
        public int DuplicateCount => Rows.Count(x => !x.Accepted);
    }

    private sealed class TopicImportPreviewRow
    {
        public TopicImportPreviewRow(int sourceLine, int number, string title, string status, bool accepted)
        {
            SourceLine = sourceLine;
            Number = number;
            Title = title;
            Status = status;
            Accepted = accepted;
        }

        public int SourceLine { get; }
        public int Number { get; }
        public string Title { get; }
        public string Status { get; }
        public bool Accepted { get; }
    }

    private sealed class ContestComboItem
    {
        public ContestComboItem(Contest contest)
        {
            Id = contest.Id;
            string number = string.IsNullOrWhiteSpace(contest.Number) ? "без №" : $"№{contest.Number}";
            string status = contest.IsActive ? "активный" : "архив";
            DisplayText = $"{number} - {contest.Name} ({status})";
        }

        public string Id { get; }
        public string DisplayText { get; }

        public override string ToString() => DisplayText;
    }

    private sealed class ContestListRow
    {
        public ContestListRow(Contest contest, int voteCount)
        {
            Id = contest.Id;
            Number = contest.Number;
            Name = contest.Name;
            ActiveText = contest.IsActive ? "активный" : "архив";
            StageText = $"{contest.Stage} - {GetContestStageTitle(GetContestStage(contest))}";
            TopicCount = contest.Topics?.Count ?? 0;
            WorkCount = contest.Works.Count;
            VoterCount = contest.Voters.Count;
            VoteCount = voteCount;
            AuthorMode = contest.HostKnowsAuthors ? "ведущий" : "счётчик";
            UpdatedText = contest.UpdatedAt.ToString("dd.MM.yyyy HH:mm");
        }

        public string Id { get; }
        public string Number { get; }
        public string Name { get; }
        public string ActiveText { get; }
        public string StageText { get; }
        public int TopicCount { get; }
        public int WorkCount { get; }
        public int VoterCount { get; }
        public int VoteCount { get; }
        public string AuthorMode { get; }
        public string UpdatedText { get; }
    }

    private sealed class ContestWorkPreviewRow
    {
        public ContestWorkPreviewRow(ContestRatingRow row)
        {
            PlaceText = row.PlaceText;
            WorkNoText = row.WorkNoText;
            Title = string.IsNullOrWhiteSpace(row.Title) ? "без названия" : row.Title;
            Author = string.IsNullOrWhiteSpace(row.Author) ? "автор не указан" : row.Author;
            Topic = row.Topic;
            Rate = row.Rate;
            AcceptedVotes = row.AcceptedVotes;
        }

        public string PlaceText { get; }
        public string WorkNoText { get; }
        public string Title { get; }
        public string Author { get; }
        public string Topic { get; }
        public decimal Rate { get; }
        public int AcceptedVotes { get; }
    }

    private sealed class ContestVoterPreviewRow
    {
        public ContestVoterPreviewRow(VoterStatusRow row)
        {
            VoterName = row.VoterName;
            Status = row.Status;
            AcceptedVotes = row.AcceptedVotes;
            MissingWorks = row.MissingWorks;
            Note = row.Note;
        }

        public string VoterName { get; }
        public string Status { get; }
        public int AcceptedVotes { get; }
        public string MissingWorks { get; }
        public string Note { get; }
    }

    private sealed class PersonDirectoryRow
    {
        private readonly SortedSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _works = new();

        public PersonDirectoryRow(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string Roles => string.Join(", ", _roles);
        public string Works => string.Join("; ", _works);
        public int AcceptedVotes { get; private set; }
        public string MissingWorks { get; private set; } = string.Empty;
        public string Note { get; private set; } = string.Empty;
        public bool IsHost { get; private set; }
        public bool IsNextHost { get; private set; }
        public bool IsAuthor { get; private set; }
        public bool IsVoter { get; private set; }
        public bool IsRequiredVoter { get; private set; }
        public bool IsUnknownVoter { get; private set; }

        public void MarkHost()
        {
            IsHost = true;
            _roles.Add("ведущий");
        }

        public void MarkNextHost()
        {
            IsNextHost = true;
            _roles.Add("следующий ведущий");
        }

        public void AddWork(int number, string title)
        {
            IsAuthor = true;
            _roles.Add("автор");
            string caption = number > 0 ? number.ToString("000") : "?";
            if (!string.IsNullOrWhiteSpace(title))
                caption += " " + title.Trim();
            _works.Add(caption);
        }

        public void MarkVoter(bool mustVote)
        {
            IsVoter = true;
            IsRequiredVoter |= mustVote;
            _roles.Add(mustVote ? "обязательный судья" : "судья");
        }

        public void ApplyStatus(VoterStatusRow status)
        {
            AcceptedVotes = status.AcceptedVotes;
            MissingWorks = status.MissingWorks;
            Note = status.Note;
            IsUnknownVoter = status.IsUnknownVoter;
            if (status.IsUnknownVoter)
                _roles.Add("голосует не из списка");
            else if (status.RequiredToVote)
                MarkVoter(mustVote: true);
            else if (status.AcceptedVotes > 0)
                MarkVoter(mustVote: false);
        }
    }

    private sealed class FirebirdImportPreviewItem
    {
        public FirebirdImportPreviewItem(Contest contest, int voteCount, bool existsById, bool existsByNumberAndName)
        {
            Contest = contest;
            VoteCount = voteCount;
            ExistsById = existsById;
            ExistsByNumberAndName = existsByNumberAndName;
        }

        public Contest Contest { get; }
        public int VoteCount { get; }
        public bool ExistsById { get; }
        public bool ExistsByNumberAndName { get; }

        public string ToPreviewLine(bool mergeByNumberAndName)
        {
            string state = ExistsById
                ? "обновит существующий по ID"
                : ExistsByNumberAndName && mergeByNumberAndName
                    ? "обновит существующий по №/названию"
                    : ExistsByNumberAndName
                        ? "есть похожий, но будет создан новый"
                        : "новый";

            return $"№{Contest.Number} - {Contest.Name} [{state}; работ {Contest.Works.Count}; судей {Contest.Voters.Count}; голосов {VoteCount}]";
        }

        public override string ToString()
        {
            string state = ExistsById ? "обновление" : ExistsByNumberAndName ? "совпадение №/название" : "новый";
            return $"№{Contest.Number} - {Contest.Name} | {state} | работ {Contest.Works.Count} | судей {Contest.Voters.Count} | голосов {VoteCount}";
        }
    }


    private sealed class RuleChangeRow
    {
        public RuleChangeRow(VoteEntry entry, IReadOnlyDictionary<int, ContestWork> worksByNo)
        {
            VoterName = entry.VoterName;
            WorkNo = entry.WorkNo;
            WorkNoText = entry.WorkNo.ToString("000");
            if (worksByNo.TryGetValue(entry.WorkNo, out ContestWork? work))
            {
                WorkTitle = string.IsNullOrWhiteSpace(work.Title) ? "без названия" : work.Title;
                Topic = string.IsNullOrWhiteSpace(work.Topic) ? "Общая тема" : work.Topic;
            }
            else
            {
                WorkTitle = "работа не найдена в настройках";
                Topic = "неизвестная тема";
            }

            decimal original = entry.OriginalScore != 0m || !string.IsNullOrWhiteSpace(entry.OriginalScoreText)
                ? entry.OriginalScore
                : entry.Score;
            OriginalScoreText = string.IsNullOrWhiteSpace(entry.VotedScoreText)
                ? (string.IsNullOrWhiteSpace(entry.OriginalScoreText) ? FormatScore(original) : entry.OriginalScoreText)
                : entry.VotedScoreText;
            AcceptedScore = entry.AcceptedScore != 0m || !string.IsNullOrWhiteSpace(entry.AcceptedScoreText) ? entry.AcceptedScore : entry.Score;
            AcceptedScoreText = string.IsNullOrWhiteSpace(entry.AcceptedScoreText) ? FormatScore(AcceptedScore) : entry.AcceptedScoreText;
            Delta = AcceptedScore - original;
            DeltaText = Delta == 0m ? "0" : (Delta > 0 ? "+" : "") + FormatScore(Delta);
            RuleNote = entry.RuleNote;
            Comment = entry.Comment;
            IsSelfVote = RuleNote.Contains("самоголос", StringComparison.OrdinalIgnoreCase);
            IsMaxLimitCorrection = RuleNote.Contains("лимит", StringComparison.OrdinalIgnoreCase) || RuleNote.Contains("максим", StringComparison.OrdinalIgnoreCase);
            IsPlusScore = OriginalScoreText.Contains('+') || OriginalScoreText.Contains('-') || OriginalScoreText.Contains("3+") || OriginalScoreText.Contains("3-") || AcceptedScore == 3.5m || AcceptedScore == 2.5m;
        }

        public string VoterName { get; }
        public int WorkNo { get; }
        public string WorkNoText { get; }
        public string WorkTitle { get; }
        public string Topic { get; }
        public string OriginalScoreText { get; }
        public decimal AcceptedScore { get; }
        public string AcceptedScoreText { get; }
        public decimal Delta { get; }
        public string DeltaText { get; }
        public string RuleNote { get; }
        public string Comment { get; }
        public bool IsSelfVote { get; }
        public bool IsMaxLimitCorrection { get; }
        public bool IsPlusScore { get; }
    }


    private sealed class GridRow
    {
        public GridRow(VoteEntry entry)
        {
            VoterName = entry.VoterName;
            WorkNo = entry.WorkNo;
            Score = entry.Score;
            ScoreText = entry.ScoreText;
            OriginalScoreText = string.IsNullOrWhiteSpace(entry.OriginalScoreText) ? entry.ScoreText : entry.OriginalScoreText;
            VotedScoreText = string.IsNullOrWhiteSpace(entry.VotedScoreText) ? OriginalScoreText : entry.VotedScoreText;
            AcceptedScoreText = string.IsNullOrWhiteSpace(entry.AcceptedScoreText) ? entry.ScoreText : entry.AcceptedScoreText;
            RuleNote = entry.RuleNote;
            Comment = entry.Comment;
            UpdatedAt = entry.UpdatedAt;
        }

        public string VoterName { get; }
        public int WorkNo { get; }
        public decimal Score { get; }
        public string ScoreText { get; }
        public string OriginalScoreText { get; }
        public string VotedScoreText { get; }
        public string AcceptedScoreText { get; }
        public string RuleNote { get; }
        public string Comment { get; }
        public DateTime UpdatedAt { get; }
    }
}
