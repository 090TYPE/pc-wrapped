# PC Wrapped — Distribution — Design Spec

**Дата:** 2026-06-23
**Статус:** Утверждён (готов к плану)
**Контекст:** Последний под-проект (5/5). Нужно отдавать готовое приложение пользователям: portable .exe, установщик, авто-релиз, инструкция.

## 1. Объём
- Portable самодостаточный single-file `PcWrapped.exe` (win-x64, без установки .NET).
- Установщик Inno Setup (скрипт `.iss`; компиляция у пользователя/в CI).
- GitHub Actions: на тег `v*` собрать и приложить оба артефакта к Release.
- README: раздел «Скачать/установить» + заметка про SmartScreen.
Вне объёма (YAGNI): авто-обновление (Velopack/Squirrel), MSIX/Store, реальная code-signing подпись (нет платного сертификата — оставить закомментированный шаг в CI).

## 2. Portable .exe
`scripts/publish.ps1` (PowerShell): запускает
`dotnet publish src/PcWrapped -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist`
→ `dist/PcWrapped.exe` (нативы SQLite/Skia извлекаются при запуске). Без trimming (`PublishTrimmed` не включаем — Avalonia/рефлексия). Скрипт печатает путь и размер итогового exe. `dist/` добавить в `.gitignore`.

## 3. Установщик (Inno Setup)
`installer/pcwrapped.iss`:
- Источник: `..\dist\PcWrapped.exe` (+ иконка из ресурсов exe; отдельный `app.ico` для ярлыка — `src\PcWrapped\Assets\app.ico`).
- Устанавливает в `{autopf}\PC Wrapped`, `AppId` (свой GUID), версия, издатель «090TYPE».
- Ярлык в меню Пуск; задача «Запускать при старте Windows» (опц., создаёт значение в реестре Run — но т.к. само приложение уже управляет автозапуском через настройки, эту задачу НЕ добавляем во избежание дубля; только ярлык Пуск и опц. ярлык на рабочем столе).
- Деинсталляция (стандартная Inno). НЕ удаляет данные пользователя в `%APPDATA%\PcWrapped` (оставляем статистику).
- Компилируется `ISCC pcwrapped.iss`; здесь не компилируем (Inno отсутствует) — только скрипт + инструкция в README/комментариях.

## 4. GitHub Actions релиз
`.github/workflows/release.yml`:
- Триггер: push тега `v*`.
- runs-on: `windows-latest`.
- Шаги: checkout; `actions/setup-dotnet@v4` (8.0.x); `dotnet test PcWrapped.slnx -c Release`; publish single-file в `dist`; `choco install innosetup -y`; `ISCC installer\pcwrapped.iss` (вывод Setup в `dist` или `installer/Output`); создать релиз и приложить `dist/PcWrapped.exe` + установщик (`softprops/action-gh-release@v2`, использует `GITHUB_TOKEN`).
- Закомментированный опциональный шаг подписи (`signtool`) с пометкой: включить при наличии `CERT`/`CERT_PASSWORD` secrets.

## 5. README
Добавить раздел «Скачать / установить»:
- Ссылка на `Releases` (portable `PcWrapped.exe` — просто запустить; либо установщик `PCWrapped-Setup.exe`).
- Заметка про **SmartScreen**: приложение без подписи → «Windows защитила ваш компьютер» → «Подробнее» → «Выполнить в любом случае». Объяснить, что код открыт.
- Краткая заметка для разработчиков: как собрать локально (`scripts/publish.ps1`).

## 6. Файлы (ориентир)
- Создать: `scripts/publish.ps1`, `installer/pcwrapped.iss`, `.github/workflows/release.yml`.
- Изменить: `.gitignore` (добавить `dist/` и `installer/Output/`), `README.md` (раздел загрузки).
- Версия приложения: задать `<Version>` в `src/PcWrapped/PcWrapped.csproj` (напр. `1.0.0`) — используется и в exe, и в `.iss` (можно прокинуть в CI через тег).

## 7. Проверка
- Локально прогнать `scripts/publish.ps1`, убедиться `dist/PcWrapped.exe` создаётся и запускается (старт без исключений).
- `.iss` и `release.yml` — ревью синтаксиса/корректности (реальная сборка установщика и релиз произойдут в CI при теге `v*`; локально Inno нет).
- Юнит-тестов нет (упаковка/конфиг).
