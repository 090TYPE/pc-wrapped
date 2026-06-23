# PC Wrapped — Polish batch — Design Spec

**Дата:** 2026-06-23
**Статус:** Утверждён (готов к плану)
**Контекст:** Набор мелких независимых улучшений. Под-проект 4/5 (далее: дистрибуция).

## 1. Объём
- Реальный DPI мыши для расчёта км (вместо хардкода 96).
- Кнопка «Копировать» — карточка PNG в системный буфер как изображение.
- Иконка приложения и трея (`app.ico`).
- Пустой стейт дашборда («Собираю статистику…») при нулевых данных.
- Память выбранной темы и периода между запусками.
Вне объёма (YAGNI): иконки в строках карточки (уже работают), истинный физический DPI по EDID, копирование в буфер на не-Windows.

## 2. Реальный DPI мыши
`MainWindow.RefreshAsync` сейчас передаёт `mouseDpi: 96`. Заменить на `96 * RenderScaling` окна (`this.RenderScaling`, fallback 1.0 → 96). Учитывает системный масштаб (125/150%). Значение приблизительное (метрика «фановая»); `VanityMath.PixelsToKilometers` уже выполняет конверсию.

## 3. Копировать карточку в буфер (Windows)
- `ClipboardImage` (app-слой, `src/PcWrapped/Native/ClipboardImage.cs`, Windows-only): `SetPng(Stream pngStream)` — грузит PNG в `System.Drawing.Bitmap`, кладёт на буфер как `CF_BITMAP` через Win32 (`OpenClipboard`/`EmptyClipboard`/`SetClipboardData`/`CloseClipboard`; `GetHbitmap`); хэндл освобождается (`DeleteObject`). Ошибки глотаются (no-throw).
- В рейле кнопка «Копировать» рядом с «Поделиться»: рендерит текущую карточку (`CardRenderer.RenderToBitmap` с темой/размером/иконками), сохраняет в `MemoryStream` PNG, вызывает `ClipboardImage.SetPng`. Локализована (`rail.copy`).

## 4. Иконка приложения + трея
- Создать `src/PcWrapped/Assets/app.ico` — градиентный глиф (фиолетово-розовый фон + три «столбика»), сгенерировать через PowerShell + System.Drawing (256×256 → `.ico`). Файл коммитится.
- `PcWrapped.csproj`: `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` и пометить `app.ico` как ресурс Avalonia (`<AvaloniaResource Include="Assets/app.ico" />`).
- `App.axaml`: у `TrayIcon` атрибут `Icon="/Assets/app.ico"`.
- `MainWindow.axaml`: `Icon="/Assets/app.ico"` (и Onboarding/Settings опционально).

## 5. Пустой стейт + память темы/периода
- **Пустой стейт:** скрытый `TextBlock` «Собираю статистику…» (локализован `dash.empty`) в центре дашборда; в `RefreshAsync` показывать его и скрывать список/графики, когда `TotalActive == TimeSpan.Zero` и нет приложений; иначе наоборот.
- **Память темы/периода:** `AppSettings` получает поля `Theme` (id темы, дефолт "gradient") и `Period` (дефолт "Week"). `AppController` грузит начальные значения и сохраняет (`SetTheme(string)`, `SetPeriod(string)` через `settings with {...}`). На старте `MainWindow` выставляет выбранный свотч темы и вкладку периода из `Controller.Theme/Period`; при смене темы/периода — `Controller.SetTheme/SetPeriod`. (Язык уже сохраняется отдельно.)
  - Сопоставление id↔тема: `CardThemes.All` по `Id` ("gradient"/"terminal"/"minimal"); период — имя enum `StatsPeriod` (`Parse`/`ToString`).

## 6. Файлы (ориентир)
- Изменить: `AppSettings.cs` (Theme/Period), `AppController.cs` (Theme/Period get/set + save), `MainWindow.axaml(.cs)` (DPI, restore on load, save on change, empty state, кнопка «Копировать», Window.Icon), `App.axaml` (TrayIcon Icon), `PcWrapped.csproj` (ApplicationIcon + AvaloniaResource), `Loc.cs` (`rail.copy`, `dash.empty`).
- Создать: `src/PcWrapped/Native/ClipboardImage.cs`, `src/PcWrapped/Assets/app.ico`.
- Тесты: расширить `JsonSettingsStoreTests` (Theme/Period round-trip).

## 7. Тестирование
- `JsonSettingsStore`: `Theme`/`Period` round-trip; отсутствие полей в старом файле → дефолты ("gradient"/"Week").
- DPI (96*scaling), буфер обмена, иконка, пустой стейт, восстановление темы/периода — платформенные/UI, ручная проверка.
