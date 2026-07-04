# STAGE14 - DATABASE STORAGE

База хранится в папке проекта.

## Файл базы

```text
VoteCounter/Database/VoteCounter.db
```

## Технология

SQLite через `Microsoft.Data.Sqlite`.

## Основные таблицы

- `SchemaInfo`
- `AppSettings`
- `Contests`
- `ContestWorks`
- `VoterSettings`
- `Votes`

## Поведение

- `LocalStore` больше не пишет `contests.json`, `settings.json` и `votes_*.json`.
- Старый публичный API `LocalStore` сохранён, поэтому `MainForm` почти не пришлось ломать.
- Начиная со STAGE15, JSON-миграция убрана: старые конкурсы импортируются из Firebird `*.fdb`.
- Кнопка `Открыть базу проекта` выделяет `VoteCounter.db` в проводнике.
