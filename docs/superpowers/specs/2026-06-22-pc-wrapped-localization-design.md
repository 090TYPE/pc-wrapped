# PC Wrapped — Localization (RU/EN) — Design Spec

**Дата:** 2026-06-22
**Статус:** Утверждён (готов к плану)
**Контекст:** Интерфейс и карточка только на русском. Для массовой аудитории нужен английский. Под-проект 2/5 серии улучшений.

## 1. Объём
- Два языка: русский и английский.
- Первый запуск: язык по культуре ОС (ru-* → русский, иначе английский). Выбор сохраняется.
- Ручной переключатель RU/EN в левом рейле; смена применяется на лету.
- Локализуются все видимые строки: рейл/дашборд, период-заголовки, карточка, имена категорий, контекст-меню, онбординг, единицы (ч/м, км, дн.).
- Вне объёма (YAGNI): другие языки, плюрализация по числам (используем простые формы), локализация форматов даты/чисел сверх единиц.

## 2. Система строк
`Loc` — статический класс в app-слое (`src/PcWrapped/Localization/Loc.cs`):
- `Loc.Current` (enum `AppLanguage { Ru, En }`), `Loc.T(string key) → string`.
- Внутри: два словаря `Dictionary<string,string>` (ru/en) по строковым ключам (напр. `rail.theme`, `card.mouse`, `cat.work`).
- Отсутствующий ключ → возвращает сам ключ (заметно при пропуске).
- Без `.resx`/сателлит-сборок (проще, без лишних артефактов сборки).
- Хелперы единиц: `Loc.Hours(TimeSpan)` («37ч 12м» / «37h 12m»), `Loc.Days(int)` («5 дн.» / «5d»), километры — числовой формат + `Loc.T("unit.km")`.

## 3. Язык и хранение
- `AppSettings` (есть) получает поле `Language` (строка "ru"/"en"; парс в `AppLanguage`, неизвестное → дефолт по ОС).
- Стартовая логика (App.axaml.cs): если в settings язык задан — применить; иначе определить по `CultureInfo.CurrentUICulture` (ru → Ru, иначе En), сохранить.
- `JsonSettingsStore` сериализует новое поле без миграции (значение по умолчанию для старых файлов).

## 4. Переключатель и применение
- В рейле — две пилюли RU/EN (как пилюли формата). Клик → `Loc.Current = …`, сохранить настройки, `ApplyLanguage()`, затем `RefreshAsync()`.
- `ApplyLanguage()` (MainWindow.axaml.cs) проставляет тексты всех статических подписей из `Loc` (подписи получают `x:Name`). Динамика (период-заголовок, легенда категорий, строки приложений, превью) берёт строки из `Loc` в `RefreshAsync`/`UpdateCharts`.
- `CardRenderer` использует `Loc.T`/хелперы для всех строк карточки. `CategoryPalette.Name(Category)` возвращает локализованное имя через `Loc`. Контекст-меню категорий и онбординг — через `Loc`.

## 5. Файлы (ориентир)
- Создать: `src/PcWrapped/Localization/Loc.cs` (+ `AppLanguage`).
- Изменить: `src/PcWrapped.Core/Settings/AppSettings.cs` (поле `Language`); `src/PcWrapped/App.axaml.cs` (определение/применение языка на старте); `src/PcWrapped/Views/MainWindow.axaml(.cs)` (x:Name подписям, RU/EN пилюли, `ApplyLanguage`, строки через Loc); `src/PcWrapped/Rendering/CardRenderer.cs` (строки карточки через Loc); `src/PcWrapped/Rendering/CategoryPalette.cs` (имя категории через Loc); `src/PcWrapped/Views/OnboardingWindow.axaml(.cs)` (строки через Loc).
- Тесты: создать `tests/PcWrapped.App.Tests/LocTests.cs`; расширить `JsonSettingsStoreTests` (round-trip Language) и `CardRendererTests` (рендер на en).

## 6. Тестирование
- `Loc`: `T` отдаёт строку текущего языка; смена `Current` меняет вывод; отсутствующий ключ → ключ; `Hours/Days` дают корректные формы ru/en.
- `JsonSettingsStore`: поле `Language` round-trip; отсутствие поля в старом файле → дефолт.
- Headless `CardRenderer`: рендер карточки при `Loc.Current = En` во всех темах не падает, даёт валидный PNG.
- UI-применение (ApplyLanguage, пилюли) — платформенно, ручная проверка.

## 7. Замечание
`Loc` живёт в app-проекте (все потребители — UI/Rendering/Settings app-слоя). `PcWrapped.Core` остаётся без локализации (Category — enum; отображаемые имена — в app). Глобальное состояние `Loc.Current` приемлемо для однопользовательского десктопа; меняется только из UI-потока.
