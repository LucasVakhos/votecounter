# STAGE40.2 - Бренд-файлы в папке Img

Что сделано:

- `votecounter.ico`, `votecounter-logo.png`, `votecounter-icon.png` и исходник бренда сохранены в `Img`;
- `ApplicationIcon` переключён на `Img\votecounter.ico`;
- `Img\**\*.*` копируется в output при сборке;
- загрузчик картинок в интерфейсе сначала ищет файлы в `Img`;
- старый путь `Resources/Images` оставлен как fallback, чтобы не ломать совместимость.

Проверка:

- сборка в отдельный output `.img_verify_out`;
- результат: 0 ошибок, 0 предупреждений;
- папка `Img` скопирована в output.
