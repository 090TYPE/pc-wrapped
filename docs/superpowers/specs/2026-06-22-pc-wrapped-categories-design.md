# PC Wrapped — Categories (expanded rules + manual assignment) — Design Spec

**Дата:** 2026-06-22
**Статус:** Утверждён (готов к плану)
**Контекст:** Кольцо категорий сейчас почти всегда «Прочее» — встроенный словарь `DefaultRules` крошечный, а ручного назначения нет. Первый из серии под-проектов улучшений (далее: локализация, экран настроек, полиш-пачка, дистрибуция — отдельными циклами).

## 1. Объём
- Сильно расширить встроенный словарь категорий.
- Ручное назначение категории приложению через право-клик по плитке, с сохранением.
- Переопределения побеждают встроенные правила; донат/дашборд обновляются сразу.
- Вне объёма (YAGNI): свои пользовательские категории (набор фиксирован: Работа/Игры/Соцсети/Браузер/Прочее), массовый редактор-список, drag&drop.

## 2. Хранение переопределений
Новая таблица `category_overrides(process TEXT PRIMARY KEY, category TEXT NOT NULL)`, создаётся в `InitializeAsync` через `CREATE TABLE IF NOT EXISTS` (не разрушает существующие БД). Методы `IStatsRepository`:
- `Task UpsertCategoryOverrideAsync(string process, Category category)` — ON CONFLICT обновляет.
- `Task<IReadOnlyDictionary<string, Category>> GetCategoryOverridesAsync()` — process → Category (case-insensitive ключи).
Категория хранится строкой `Category.ToString()`; при чтении парсится `Enum.Parse`, неизвестное значение игнорируется (пропускается).

## 3. Категоризация (мёрдж)
`Categorizer` остаётся как есть (принимает готовый `IReadOnlyDictionary<string,Category>`). Добавляется чистый хелпер слияния:
- `CategoryRules.Merge(IReadOnlyDictionary<string,Category> defaults, IReadOnlyDictionary<string,Category> overrides) → IReadOnlyDictionary<string,Category>` — ключи нормализуются как в `Categorizer` (trim, без `.exe`, OrdinalIgnoreCase); при совпадении ручное переопределение побеждает дефолт.
`MainViewModel.BuildStatsAsync` строит `Categorizer` из `Merge(DefaultRules.Map, overrides)`, где overrides берутся из репозитория.

## 4. Расширенный `DefaultRules`
Существенно дополнить `DefaultRules.Map` распространёнными приложениями (имена процессов, без `.exe`):
- **Браузеры:** chrome, msedge, firefox, opera, opera_gx, brave, browser (yandex), vivaldi, arc.
- **Работа:** code, devenv, rider64, idea64, pycharm64, webstorm64, clion64, goland64, sublime_text, notepad++, excel, winword, powerpnt, onenote, outlook, notion, obsidian, figma, photoshop, illustrator, blender, windowsterminal, powershell, cmd, wt.
- **Игры:** steam, steamwebhelper, epicgameslauncher, battle.net, riotclient, leagueclient, dota2, csgo, cs2, valorant, minecraft, javaw, gog galaxy.
- **Соцсети/чат:** discord, telegram, slack, teams, ms-teams, whatsapp, zoom, skype, viber, signal.
- **Медиа:** spotify, vlc, music (yandex), wmplayer, mpc-hc, foobar2000.
(Список — отправная точка; легко дополнять. Конкретные пары process→Category фиксируются в плане.)

## 5. UI — ручное назначение
Каждая плитка приложения получает `ContextMenu` (правый клик) с пятью пунктами категорий. Текущая категория приложения отмечена галочкой. Выбор пункта → `MainViewModel.AssignCategoryAsync(process, category)` → `UpsertCategoryOverrideAsync` → `RefreshAsync()` (пересчёт статистики и графиков; донат сразу меняется). `AppRowVm` несёт `ProcessName` (уже есть как `Name`) для передачи в команду.

## 6. MainViewModel
- При построении статистики грузит overrides (`GetCategoryOverridesAsync`) и строит мёрдж-categorizer.
- `Task AssignCategoryAsync(string process, Category category)` — upsert override (пересчёт инициирует View через `RefreshAsync`).

## 7. Тестирование
- **Core/Storage:** `category_overrides` upsert/get round-trip; upsert обновляет существующее; неизвестная строка категории при чтении пропускается без падения.
- **Core:** `CategoryRules.Merge` — переопределение побеждает дефолт; нормализация имён (`Code.exe` == `code`); пустые входы.
- **Core:** расширенный `DefaultRules` содержит ключевые приложения (chrome→Browser, code→Work, discord→Social, steam→Games, spotify→Other [медиа → Прочее, см. §9]).
- UI (контекст-меню, refresh) — платформенно, ручная проверка.

## 8. Затрагиваемые файлы (ориентир)
- Изменить: `src/PcWrapped.Core/Storage/IStatsRepository.cs` + `SqliteStatsRepository.cs` (таблица + методы), `src/PcWrapped.Core/Categorization/DefaultRules.cs` (расширение), `src/PcWrapped/ViewModels/MainViewModel.cs` (мёрдж + AssignCategoryAsync), `src/PcWrapped/Views/MainWindow.axaml(.cs)` (контекст-меню на плитке).
- Создать: `src/PcWrapped.Core/Categorization/CategoryRules.cs` (Merge).
- Тесты: расширить `SqliteStatsRepositoryTests`, `CategorizerTests`; создать `CategoryRulesTests`.

## 9. Замечание по медиа-категории
Набор категорий фиксирован (Работа/Игры/Соцсети/Браузер/Прочее) — отдельной «Медиа» нет. Медиаплееры (spotify/vlc/…) по умолчанию → **Прочее**; пользователь может переназначить вручную. (Зафиксировано, чтобы не плодить категории.)
