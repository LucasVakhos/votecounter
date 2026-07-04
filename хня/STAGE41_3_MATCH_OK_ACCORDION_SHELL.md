# Stage 41.3 - перенос оболочки как в OK AccordionShellForm

Перенос сделан по реальному образцу из `E:\CS\26\OK\VoteCounter\VoteCounter.zip`.

Что перенесено из `AccordionShellForm`:
- `MainForm` теперь наследуется от `DevExpress.XtraBars.Ribbon.RibbonForm`;
- верхняя панель назначается в свойство формы `Ribbon`;
- боковая навигация `AccordionControl` назначается в `NavigationControl`;
- включён режим `RibbonFormNavigationControlLayoutMode.StretchToFormTitle`;
- боковая навигация пристыкована напрямую слева, без промежуточного `SplitContainer`;
- добавлена страница `Appearance`;
- добавлены группы `Appearance` и `Accent Colors`;
- добавлены skin/palette/accent-команды из образца;
- рабочая страница `rpCurrent` сохранена для команд текущего конкурса.

Designer:
- в `InitializeComponent` не осталось lambda-обработчиков;
- обработчики вынесены в обычные методы;
- сборка проверена: 0 ошибок, 0 предупреждений.
