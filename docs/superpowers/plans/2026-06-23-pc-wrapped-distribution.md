# PC Wrapped — Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Отдавать приложение пользователям: portable single-file .exe, установщик Inno Setup, авто-релиз в GitHub по тегу, инструкция в README.

**Architecture:** `dotnet publish` делает самодостаточный single-file exe. Inno Setup `.iss` упаковывает его в установщик. GitHub Actions на тег `v*` тестирует, публикует exe, собирает установщик (choco innosetup) и прикладывает оба к Release.

**Tech Stack:** .NET 8 publish (win-x64, self-contained, single-file), Inno Setup 6, GitHub Actions.

**Commit author:** ВСЕ коммиты — обычный `git commit -m "..."` (git config user = `090_TYPE`). НИКАКИХ `Co-Authored-By` трейлеров, без `--author`, без упоминания Claude/AI. Это упаковка/конфиг — юнит-тестов нет; проверка через реальный publish и ревью скриптов.

---

## File Structure
```
src/PcWrapped/PcWrapped.csproj          # MODIFY: <Version>1.0.0</Version>
scripts/publish.ps1                      # CREATE: self-contained single-file publish -> dist/
installer/pcwrapped.iss                  # CREATE: Inno Setup script
.github/workflows/release.yml            # CREATE: build + installer + GitHub Release on tag v*
.gitignore                               # MODIFY: dist/ + installer/Output/
README.md                                # MODIFY: Download/Install section
```

---

## Task 1: Publish script + version + README download

**Files:**
- Modify: `src/PcWrapped/PcWrapped.csproj`
- Create: `scripts/publish.ps1`
- Modify: `.gitignore`
- Modify: `README.md`

- [ ] **Step 1: App version**

In `src/PcWrapped/PcWrapped.csproj`, add to the first `<PropertyGroup>`:
```xml
    <Version>1.0.0</Version>
```

- [ ] **Step 2: .gitignore**

Append to `.gitignore`:
```
dist/
installer/Output/
```

- [ ] **Step 3: publish.ps1**

Create `scripts/publish.ps1`:
```powershell
#requires -version 5
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet publish src/PcWrapped -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
    $exe = Join-Path $root "dist\PcWrapped.exe"
    if (-not (Test-Path $exe)) { throw "PcWrapped.exe not found in dist" }
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Output "Published: $exe ($mb MB)"
}
finally { Pop-Location }
```

- [ ] **Step 4: Run publish — verify exe is produced**

Run (PowerShell tool): `& C:\Users\090\pc-wrapped\scripts\publish.ps1`
Expected: ends with `Published: ...\dist\PcWrapped.exe (NN MB)` and `dist\PcWrapped.exe` exists. (First run may download the win-x64 runtime pack — that's fine.) If `taskkill` is needed because a prior instance locks files, run `taskkill //F //IM PcWrapped.exe` first.

- [ ] **Step 5: README — Download/Install section**

In `README.md`, add a section after the title/screenshot (before or after "## Возможности") :
```markdown
## Скачать / установить

Готовые сборки — на странице [Releases](https://github.com/090TYPE/pc-wrapped/releases):
- **PcWrapped.exe** — портативная версия, просто запусти (ничего ставить не нужно).
- **PCWrapped-Setup.exe** — установщик (ярлык в меню Пуск, удаление через «Программы и компоненты»).

> ⚠️ Приложение пока без цифровой подписи, поэтому при первом запуске Windows SmartScreen
> может показать «Windows защитила ваш компьютер». Нажми **Подробнее → Выполнить в любом случае**.
> Код открыт — можно собрать самому.

### Сборка портативного exe локально
```
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```
Результат: `dist\PcWrapped.exe`.
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: self-contained publish script, version, and download docs"
```

---

## Task 2: Inno Setup installer script

**Files:**
- Create: `installer/pcwrapped.iss`

This file cannot be compiled here (Inno Setup is not installed); it is verified by review and compiled in CI (Task 3) / on the user's machine.

- [ ] **Step 1: Create the .iss**

Create `installer/pcwrapped.iss`:
```iss
#define MyAppName "PC Wrapped"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "090TYPE"
#define MyAppExeName "PcWrapped.exe"

[Setup]
AppId={{7A2D5E3C-1B9F-4C6A-8E10-3F4B5C6D7E8F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PC Wrapped
DefaultGroupName=PC Wrapped
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=PCWrapped-Setup
SetupIconFile=..\src\PcWrapped\Assets\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\PcWrapped.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\PC Wrapped"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PC Wrapped"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,PC Wrapped}"; Flags: nowait postinstall skipifsilent
```
Notes: `AppId` is a fixed GUID (do not change between versions — it identifies the app for upgrades/uninstall). Uninstall is provided by Inno automatically and does NOT remove `%APPDATA%\PcWrapped` (user stats are preserved). Autostart is intentionally NOT added here — the app manages it via its own Settings.

- [ ] **Step 2: Sanity-check the script (no Inno here)**

Run: `bash -lc "grep -c '^\[' installer/pcwrapped.iss"` (or read the file) and confirm the sections `[Setup] [Languages] [Tasks] [Files] [Icons] [Run]` are present and the `Source:` path is `..\dist\PcWrapped.exe`. (Actual compile happens in CI / on the user's machine via `ISCC pcwrapped.iss`.)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: Inno Setup installer script"
```

---

## Task 3: GitHub Actions release workflow

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Step 1: Create the workflow**

Create `.github/workflows/release.yml`:
```yaml
name: release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Test
        run: dotnet test PcWrapped.slnx -c Release

      - name: Publish portable exe
        run: >
          dotnet publish src/PcWrapped -c Release -r win-x64 --self-contained true
          -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist

      - name: Install Inno Setup
        run: choco install innosetup -y

      - name: Build installer
        run: '& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\pcwrapped.iss'

      # Optional code signing — enable when you have a certificate in repo secrets:
      # - name: Sign
      #   run: |
      #     signtool sign /fd SHA256 /f cert.pfx /p "${{ secrets.CERT_PASSWORD }}" dist\PcWrapped.exe installer\Output\PCWrapped-Setup.exe

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            dist/PcWrapped.exe
            installer/Output/PCWrapped-Setup.exe
```

- [ ] **Step 2: Validate YAML**

Run: `bash -lc "python -c \"import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml')); print('yaml ok')\""` (if python is available) OR read the file and confirm indentation/structure are valid. Expected: `yaml ok` / no parse issues.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "ci: release workflow builds portable exe and installer on tag"
```

---

## Final verification
- [ ] `scripts/publish.ps1` produces `dist/PcWrapped.exe` (verified in Task 1).
- [ ] `installer/pcwrapped.iss` references `..\dist\PcWrapped.exe` and has all sections (review).
- [ ] `.github/workflows/release.yml` is valid; on a future `git tag v1.0.0 && git push --tags` it will test, publish, build the installer, and attach both to a GitHub Release.
- [ ] `dist/` and `installer/Output/` are gitignored.
- [ ] README has a Download/Install section with the SmartScreen note.
- [ ] All new commits authored by `090_TYPE`, no `Co-Authored-By` trailers.

## Spec Coverage Notes
- Portable single-file exe (publish.ps1 + version) → Task 1.
- Installer (.iss) → Task 2.
- GitHub Actions release on tag (+ commented signing step) → Task 3.
- README download + SmartScreen note → Task 1.
- .gitignore for build outputs → Task 1.
- Out of scope (auto-update, MSIX, real signing) → not included.
```
