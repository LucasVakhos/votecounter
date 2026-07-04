# STAGE15.2 - ZIP REPACK FIX

Исправлен ZIP-чекпойнт, который Windows мог открыть как поврежденный архив.

Что сделано:

- архив пересобран стандартным Python `zipfile` с обычным `ZIP_DEFLATED`;
- убраны слишком длинные служебные имена старых sample-файлов в `Samples`;
- sample-файлы переименованы в короткие ASCII-имена:
  - `old_firebird_protocol_sample_1.xlsx`
  - `old_firebird_protocol_sample_2.xlsx`
- проектный код и исправление `**/*.resx` сохранены.

Проверка архива выполнена через `ZipFile.testzip()`.
