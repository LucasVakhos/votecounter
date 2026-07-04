# Stage 41.2 - Designer fix и схема базы

Исправлена ошибка открытия `MainForm` в Visual Studio Designer на строке 145.

Причина:
- в `InitializeComponent` был lambda-обработчик `mainTabs.SelectedPageChanged += (_, _) => ...`;
- CodeDom-дизайнер WinForms может падать на таких выражениях даже при чистой сборке.

Что изменено:
- обработчик `mainTabs.SelectedPageChanged` вынесен в метод `MainTabsSelectedPageChanged`;
- обработчик `frame.HandleCreated` вынесен в метод `SideFrameHandleCreated`;
- сборка проверена: 0 ошибок, 0 предупреждений.

Для просмотра структуры живой SQLite-базы создан отчёт:

`Database/VoteCounter_schema_report.txt`

Основной файл схемы проекта:

`Schema/vote_counter_schema.sql`
