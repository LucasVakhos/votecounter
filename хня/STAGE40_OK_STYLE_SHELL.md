# STAGE40 - Стиль оболочки OK

Что перенесено из образца `OK.zip`:

- боковая навигация заменена на DevExpress `AccordionControl`;
- разделы собраны в группу `Views`, как в образце `AccordionShellForm`;
- рабочая область VoteCounter осталась прежней: скрытые вкладки продолжают работать как frame;
- синхронизация выбранного раздела переписана под accordion-элементы;
- F1/hint-логика адаптирована под новую навигацию;
- добавлен SVG-логотип из образца в `Resources/Images/logo.svg`;
- ресурсы `Resources/Images/**` копируются в output;
- в проект добавлены параметры DPI из образца:
  - `ApplicationHighDpiMode=PerMonitorV2`;
  - `ForceDesignerDpiUnaware=true`;
- версия проекта поднята до `1.40.0-ok-style-shell`.

Проверка:

- `dotnet build --no-restore -p:RestorePackagesPath="C:\Users\User\.nuget\packages" -p:NuGetAudit=false`
- результат: 0 ошибок, 0 предупреждений.
