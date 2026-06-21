# PC Wrapped — UI Redesign + App Icons — Design Spec

**Дата:** 2026-06-21
**Статус:** Утверждён (готов к плану реализации)
**Контекст:** v1 приложения готов (см. `2026-06-21-pc-wrapped-design.md`), но главное окно выглядит как отладочная форма. Этот документ — редизайн главного окна + настоящие иконки приложений.

## 1. Объём

Полный редизайн главного окна (тёмная тема, направление «A — превью в центре») с большим списком топ-приложений и **настоящими иконками** приложений. Логика трекинга/хранения/приватности не ломается — только расширяется. Вне объёма (YAGNI): дисковый кэш иконок, hi-res иконки 256px, круговая диаграмма/тепловая карта на карточке, анимации.

## 2. Иконки приложений (пайплайн)

- **Захват пути к .exe:** `Win32ForegroundWindowSource` уже резолвит процесс по pid. Добавить в `ForegroundInfo` (PcWrapped.Core.Tracking) поле `string? ExecutablePath`, заполняемое из `Process.GetProcessById(pid).MainModule.FileName` в try/catch (у защищённых/системных процессов доступ может бросать — тогда null).
- **Хранение пути:** новая таблица `app_paths(process TEXT PRIMARY KEY, path TEXT NOT NULL)`. Создаётся в `InitializeAsync` через `CREATE TABLE IF NOT EXISTS` — без разрушающей миграции; существующие БД доберут таблицу. Методы репозитория: `Task UpsertAppPathAsync(string process, string path)` и `Task<IReadOnlyDictionary<string,string>> GetAppPathsAsync()`.
- **Запись пути:** `ActivityTracker.TickAsync` — когда `ExecutablePath` не null/empty, апсертит путь через `UpsertAppPathAsync` (по `ProcessName`).
- **Извлечение иконки:** `AppIconProvider` в app-проекте (`src/PcWrapped/Native/`, Windows-only): по пути .exe извлекает иконку (`Icon.ExtractAssociatedIcon` / `SHGetFileInfo` → `Bitmap` → PNG в `MemoryStream` → Avalonia `Bitmap`). Кэш в памяти `Dictionary<string,Bitmap?>` по пути (одна попытка на путь). Зависимость `System.Drawing.Common` (Windows; CA1416-предупреждения допустимы). Если иконка не извлекается — возвращает null, UI рисует фолбэк (цветной квадрат с первой буквой имени).

## 3. Главное окно (вёрстка и стиль)

- **Тема:** `RequestedThemeVariant="Dark"` + кастомные `Styles`/ресурсы в `App.axaml` (фон окна, плитки приложений, pill-кнопки, прогресс-бары, свотчи). FluentTheme сохраняется как база.
- **Раскладка** (`Grid` из трёх колонок):
  - **Левый рейл** (`Border`): бренд «PC Wrapped»; вкладки периода **Сегодня / Неделя / Год** (выбор подсвечивается); ряд из 3 кликабельных свотчей тем; формат **1:1 / 9:16** (две pill-кнопки-переключатель); большая кнопка **«Поделиться»** (экспорт PNG через `StorageProvider.SaveFilePickerAsync`).
  - **Центр:** шапка — мелкий подзаголовок периода, крупное общее активное время, серия дней; ниже заголовок «Топ приложений» и **сетка** топ-приложений в `ScrollViewer` → `ItemsControl` (2 колонки, `UniformGrid`/`WrapPanel`). Каждая плитка: иконка (≈30px), имя, время, полоса использования (доля от максимума).
  - **Правая панель:** «Превью» — живое изображение текущей карточки (`Image`), пересобирается при смене темы/периода/формата.
- **ViewModel:** `MainViewModel` получает `SelectedTheme`, `SelectedSize` (есть), добавляются `SelectedPeriod` (Today/Week/Year) и `ObservableCollection<AppRowVm>` (Name, TimeText, Fraction 0..1, ExecutablePath). Иконку по `ExecutablePath` резолвит View/слой app через `AppIconProvider` (Core остаётся без UI-зависимостей).
- **Периоды:** через `Aggregator.BuildPeriodStats` с диапазоном: Today = `today..today`; Week = `today.AddDays(-6)..today`; Year = `today.AddDays(-364)..today`. `topAppLimit` поднять до 12.
- **Превью:** рендер текущей карточки в `RenderTargetBitmap` (через `CardRenderer.BuildCard` + measure/arrange/render) и присвоение `Image.Source`. Масштабируется под панель.

## 4. Карточка для шеринга

`CardRenderer.BuildCard` принимает **опциональный** резолвер иконок (например, `IReadOnlyDictionary<string, IImage>? appIcons = null`). Для топ-приложений рисуем мини-иконку рядом с именем, если она есть; при null/отсутствии — поведение как сейчас. Headless-тест рендера (`PcWrapped.App.Tests`) продолжает вызывать `BuildCard`/`RenderToPng` без иконок и остаётся зелёным.

## 5. Тестирование

- **Core (юнит/интеграция):**
  - `app_paths` upsert + get на in-memory SQLite (upsert обновляет путь; get возвращает словарь).
  - `ActivityTracker` апсертит путь, когда `ForegroundInfo.ExecutablePath` задан; не апсертит при null.
  - Существующие тесты (28) остаются зелёными; headless-рендер — зелёный с иконками = null.
- **Платформенное (ручная проверка):** извлечение иконок и UI — сборка + запуск без исключений; иконки реальных приложений отображаются; превью обновляется; экспорт PNG (1:1 и 9:16) работает.

## 6. Затрагиваемые файлы (ориентир)

- Изменить: `Core/Tracking/IForegroundWindowSource.cs` (ForegroundInfo += ExecutablePath), `Core/Tracking/ActivityTracker.cs` (upsert path), `Core/Storage/IStatsRepository.cs` + `SqliteStatsRepository.cs` (app_paths), `App.axaml` (dark + styles), `Views/MainWindow.axaml(.cs)` (новая вёрстка), `ViewModels/MainViewModel.cs` (период/AppRow), `Native/Win32ForegroundWindowSource.cs` (заполнять путь), `Rendering/CardRenderer.cs` (опц. иконки).
- Создать: `Native/AppIconProvider.cs`, `ViewModels/AppRowVm.cs`; тесты на app_paths и tracker-path.
- Тесты: расширить `SqliteStatsRepositoryTests`, `ActivityTrackerTests`.
