# STAGE18.1 - DevExpress ComboBox ambiguity fix

Исправлена ошибка компиляции после подключения DevExpress:

```text
"ComboBox" является неоднозначной ссылкой между "System.Windows.Forms.ComboBox" и "DevExpress.XtraEditors.ComboBox".
```

Что изменено:

- поле `cboContests` явно объявлено как `System.Windows.Forms.ComboBox`;
- создание выпадающего списка конкурса тоже переведено на явный `System.Windows.Forms.ComboBox`;
- `ComboBoxStyle.DropDownList` указан как `System.Windows.Forms.ComboBoxStyle.DropDownList`;
- DevExpress UI-слой сохранён;
- SQLite, импорт Firebird, spellcheck и отчёты не тронуты.

Версия: `1.17.1-stage18-devexpress-combobox-fix`.
