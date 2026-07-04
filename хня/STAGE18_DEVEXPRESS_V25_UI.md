# STAGE18 - DevExpress v25 UI polish

Перевод интерфейса VoteCounter на DevExpress WinForms v25.x.

## Что изменено

- Проект подключает `DevExpress.Win.Design` версии `25.2.8` - последнюю стабильную ветку v25.x.
- Главная форма и внутренние диалоги переведены на `XtraForm`.
- Основные вкладки переведены на `XtraTabControl` / `XtraTabPage`.
- Кнопки заменены на `SimpleButton`.
- Группы заменены на `GroupControl`.
- Checkbox-элементы заменены на `CheckEdit`.
- MessageBox заменён на `XtraMessageBox`.
- Добавлена DevExpress-тема `WXI`.
- Таблицы сохранены как DataGridView для совместимости с текущей логикой редактирования, но визуально отполированы под DevExpress-стиль.

## Почему пока не все таблицы заменены на XtraGrid

Полная замена `DataGridView` на `GridControl/GridView` требует переписать выбор строк, редактирование BindingList, подсветку строк, текущую строку и работу с `SelectedRows`. В этом чекпойнте сделан безопасный переход внешнего UI на DevExpress без риска сломать импорт, базу, отчёты и проверку голосов.

Следующий безопасный шаг: `STAGE19 - XtraGrid migration`, где таблицы будут переноситься по одной.
