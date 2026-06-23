# PC Wrapped — Settings screen — Design Spec

**Дата:** 2026-06-23
**Статус:** Утверждён (готов к плану)
**Контекст:** Нет экрана управления. Под-проект 3/5 серии улучшений (далее: полиш-пачка, дистрибуция).

## 1. Объём
Отдельное окно «Настройки», доступное из рейла (кнопка ⚙) и трея. Содержит:
- Трекинг ввода (вкл/выкл) — на лету; сохраняется в `CountInput`.
- Автозапуск с Windows (вкл/выкл) — `AutostartManager`; сохраняется в `Autostart`.
- Исключения приложений (список с «вернуть»; добавление право-кликом по плитке).
- Очистить данные (с подтверждением).
- Открыть папку данных (`%APPDATA%\PcWrapped`).
Вне объёма (YAGNI): экспорт/импорт настроек, расписание, язык (остаётся в рейле), гранулярная очистка по периодам.

## 2. Исключения
- Новая таблица `excluded_apps(process TEXT PRIMARY KEY)`; `CREATE TABLE IF NOT EXISTS` в `InitializeAsync` (без миграции).
- Методы `IStatsRepository`: `Task AddExclusionAsync(string process)`, `Task RemoveExclusionAsync(string process)`, `Task<IReadOnlySet<string>> GetExclusionsAsync()` (ключи OrdinalIgnoreCase).
- **Трекинг:** `ActivityTracker` получает проверку исключения — конструктор принимает `Func<string,bool>? isExcluded = null` (null → ничего не исключать). В `TickAsync` перед `AddSampleAsync`/`UpsertAppPathAsync`: если `isExcluded(process)` — пропустить запись активности этого окна (счётчики ввода пишутся как обычно). App передаёт предикат на основе набора, перезагружаемого при изменении.
- **Статистика:** `MainViewModel.BuildStatsAsync` грузит исключения и фильтрует сэмплы (`where !excluded`) до `BuildPeriodStats` — прошлые данные исключённых тоже скрыты из топа/категорий/часов.
- **Добавление право-кликом:** в контекст-меню плитки приложения добавляется пункт «Исключить» (Tag-маркер), который вызывает `AddExclusionAsync(process)` + refresh.

## 3. Очистка данных
- `IStatsRepository.ClearAllDataAsync()` — `DELETE FROM samples; input_counters; app_paths; category_overrides;` (исключения и настройки НЕ трогаем). В одной транзакции.
- В окне настроек кнопка «Очистить данные» показывает подтверждение (диалог да/нет) перед вызовом; после — refresh.

## 4. Связка App ↔ Settings (живые тоглы)
Живое вкл/выкл хуков требует доступа окна настроек к App-ресурсам. Вводим лёгкий сервис-контроллер:
- `Win32InputCounterSource` получает метод `Stop()` (анхук, обнуление хэндлов; повторный `Start()` снова ставит хуки — делегаты уже хранятся полями).
- `AppController` (app-слой) создаётся в `App.OnFrameworkInitializationCompleted`, хранит ссылки на `_input`, `_repo`, `JsonSettingsStore`, путь данных. Открытые методы: `bool TrackingEnabled` (+ `SetTracking(bool)` — start/stop хуков + сохранить `CountInput`), `bool AutostartEnabled` (+ `SetAutostart(bool)` — `AutostartManager.SetEnabled` + сохранить), `Task ClearDataAsync()` (repo.ClearAllDataAsync), `void OpenDataFolder()` (`Process.Start("explorer.exe", dir)`), `IStatsRepository Repo`, и набор исключений + методы добавления/удаления с обновлением предиката трекера.
- `MainWindow`/`SettingsWindow` получают `AppController` (через DataContext главного окна или параметр). Кнопка ⚙ открывает `SettingsWindow(controller)`.

## 5. UI
- `SettingsWindow` (Avalonia, тёмная тема, локализовано через `Loc`): тоглы Трекинг/Автозапуск (`CheckBox`/`ToggleSwitch`), секция Исключения (`ItemsControl` со строками process + кнопка ✕ «вернуть»), кнопки «Очистить данные» (с диалогом подтверждения) и «Открыть папку данных».
- Рейл главного окна: кнопка ⚙ (открывает настройки). Контекст-меню плитки: пункт «Исключить».
- Все строки — через `Loc` (добавить ключи `settings.*`, `menu.exclude` в ru/en).

## 6. Тестирование
- **Core/Storage:** `excluded_apps` add/remove/get round-trip; `ClearAllDataAsync` очищает samples/input_counters/app_paths/category_overrides, но НЕ excluded_apps.
- **Core:** `ActivityTracker` — при `isExcluded`→true активность окна не пишется (и путь не апсертится), счётчики ввода всё равно пишутся; при false — пишется как раньше.
- **App:** фильтрация исключённых в подготовке статистики (тест на чистой функции фильтра, если выделим helper) ИЛИ проверка, что BuildStatsAsync исключает (через in-memory repo).
- UI (окно, тоглы, диалог, право-клик, открыть папку) — платформенно, ручная проверка.

## 7. Файлы (ориентир)
- Изменить: `IStatsRepository.cs` + `SqliteStatsRepository.cs` (excluded_apps + ClearAllData), `ActivityTracker.cs` (предикат исключения), `Win32InputCounterSource.cs` (Stop), `MainViewModel.cs` (фильтр + методы), `MainWindow.axaml(.cs)` (кнопка ⚙ + пункт «Исключить»), `App.axaml.cs` (создать AppController, прокинуть), `Loc.cs` (ключи settings.*).
- Создать: `src/PcWrapped/AppController.cs`, `src/PcWrapped/Views/SettingsWindow.axaml(.cs)`.
- Тесты: расширить `SqliteStatsRepositoryTests`, `ActivityTrackerTests`; при необходимости helper-тест фильтра.
