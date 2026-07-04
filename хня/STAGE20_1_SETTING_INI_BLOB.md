# STAGE20.1 - Setting.Ini BLOB

Layout больше не пишется как рабочий `VoteCounter.layouts.ini` рядом с exe.

## Что изменено

- Рабочая база остаётся на прежнем месте:

```text
VoteCounter/Database/VoteCounter.db
```

- Добавлена таблица:

```sql
CREATE TABLE IF NOT EXISTS Setting(
    Id INTEGER NOT NULL PRIMARY KEY CHECK(Id = 1),
    Ini BLOB NULL,
    UpdatedAt TEXT NOT NULL
);
```

- Полный текст layout ini сохраняется в `Setting.Ini` как UTF-8 BLOB.
- При запуске layout читается из `Setting.Ini`.
- Старый внешний `VoteCounter.layouts.ini`, если найден, один раз подхватывается и переносится в базу.
- Кнопка `Сбросить layout` очищает `Setting.Ini` и удаляет старый внешний ini, если он остался.

## Что не менялось

- JSON не возвращался.
- Импорт `*.fdb` не менялся.
- DevExpress GridControl/GridView layout остаётся в том же ini-формате, но хранится внутри базы.
