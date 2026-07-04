# STAGE22.1 - RulesAutoFix compile fix

Исправлена ошибка сборки:

```text
Имя "_rulesAutoFix" не существует в текущем контексте.
```

Причина: в `ParseCurrentText()` был добавлен вызов автофикса правил перед разбором голосования, но поле сервиса не было объявлено в `MainForm`.

Что исправлено:

- добавлено поле `ContestRulesAutoFixService _rulesAutoFix`;
- база `VoteCounter.db` по-прежнему не входит в ZIP;
- расположение рабочей базы не менялось: `VoteCounter/Database/VoteCounter.db`;
- правило `3+ = 3.5` и автофиксация правил сохранены.
