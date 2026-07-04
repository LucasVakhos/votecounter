# STAGE34 - Проголосовал / Принято

- Оценка `3+` принимается как `3.5`.
- Оценка `3-` принимается как `2.5`.
- Оценка `4+` принимается как `4`, а при исчерпанном лимите максимальной оценки - как `3+` (`3.5`).
- Исходный текст оценки сохраняется в полях `VotedScoreText` / `VotedScore` и показывается как `Проголосовал`.
- Расчётная оценка сохраняется в `AcceptedScoreText` / `AcceptedScore` и показывается как `Принято`.
- Старые поля `OriginalScoreText` / `OriginalScore` и `ScoreText` / `Score` оставлены для совместимости.
- В таблицу `Votes` автоматически добавляются колонки `VotedScore`, `VotedScoreText`, `AcceptedScore`, `AcceptedScoreText`.
- Рабочая база остаётся в `VoteCounter/Database/VoteCounter.db` и в ZIP не включается.
