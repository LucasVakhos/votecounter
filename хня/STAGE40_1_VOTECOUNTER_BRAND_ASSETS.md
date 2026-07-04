# STAGE40.1 - Иконка и логотип VoteCounter

Что добавлено:

- фирменная иконка приложения на основе выбранного изображения пользователя:
  - `Img/votecounter.ico`;
  - `Img/votecounter-icon.png`;
- горизонтальный логотип:
  - `Img/votecounter-logo.png`;
- исходное выбранное изображение сохранено как:
  - `Img/votecounter-brand-source.png`;
- `.ico` подключён как `ApplicationIcon`;
- иконка устанавливается у главного окна при запуске;
- в левой accordion-панели добавлен компактный знак и название `VoteCounter`;
- палитра выдержана в стиле OK:
  - графит;
  - белый;
  - оранжевый `#FF7200`;
  - синий акцент.

Проверка:

- `dotnet build --no-restore -p:RestorePackagesPath="C:\Users\User\.nuget\packages" -p:NuGetAudit=false`
- результат: 0 ошибок, 0 предупреждений.
