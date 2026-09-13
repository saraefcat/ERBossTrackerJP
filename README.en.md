# ER Boss Tracker JP

[日本語](README.md) | English

The [Japanese README](README.md) is the canonical version. This English file is maintained as a reference translation.

ER Boss Tracker JP is a Windows application that reads PC ELDEN RING save files and displays boss completion progress for each character.

The current version includes save parsing, data for 207 bosses, defeat detection and summaries, cumulative death counts, a WPF progress interface, automatic save monitoring, OBS text output, dark and light themes, user-setting persistence, and diagnostic logs.

## Project principles

- Windows 10 / 11, x64
- C# / .NET 10 / WPF
- MVVM-based WPF interface, separated from save parsing
- Read-only handling of `ER0000.sl2`
- Seamless Co-op and `.co2` files are not supported
- Cumulative deaths are read from `ER0000.sl2`; the game process memory is not accessed
- Real user save files are never included in the repository

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- x64 environment
- Standard PC Steam ELDEN RING save file named `ER0000.sl2`

The self-contained release includes the .NET runtime. End users do not need to install the .NET SDK or Visual Studio.

## Usage

1. Fully extract `ERBossTrackerJP-v0.1.0-win-x64.zip` to a folder of your choice.
2. Launch `ERBossTrackerJP.exe`.
3. If the standard save location is found, the application automatically loads the most recently updated `ER0000.sl2`.
4. If no save is found, use ［フォルダーを選択］ (Select Folder) to choose the folder containing `ER0000.sl2`, the `EldenRing` folder containing account folders, or one of its parent folders.
5. Select the character to track. Game save updates are automatically reflected in the application.
6. To display data in OBS, enable ［OBSテキスト出力を有効にする］ (Enable OBS Text Output) and select an output file in an OBS Text (GDI+) source.

The application interface is primarily Japanese. The language option changes boss, region, and location names between Japanese and English; it does not translate every interface label.

The application starts in dark mode. Disable ［ダークモード］ (Dark Mode) on the Settings tab to switch immediately to the light theme. The selected theme is restored the next time the application starts.

This is an unsigned portable application. Windows may display an unknown-publisher warning. Verify the download source and the included `SHA256SUMS.txt`, or the `.sha256.txt` file next to the ZIP archive, before running the application.

## Repository layout

```text
ERBossTrackerJP.sln
src/
├─ ERBossTrackerJP/          WPF application and composition root
├─ ERBossTrackerJP.Core/     Domain models, Japanese/English boss data, output contracts
└─ ERBossTrackerJP.Save/     Save parser
tests/
└─ ERBossTrackerJP.Tests/    Automated Core, Save, service, ViewModel, and WPF tests
tools/                       Maintenance and release scripts
docs/
├─ ARCHITECTURE.md
├─ OBS_OUTPUT.md
├─ REFERENCE_AUDIT.md
└─ SAVE_PARSER_DESIGN.md
```

## Building from source

The .NET 10 SDK is required.

```powershell
dotnet restore ERBossTrackerJP.sln
dotnet build ERBossTrackerJP.sln -c Release
dotnet test ERBossTrackerJP.sln -c Release
```

## Creating a release

The following script first requires a clean Git working tree and runs the Release test suite. It then creates a self-contained, single-file win-x64 package under the Git-ignored `..\outputs\releases` directory. The source commit is recorded in `SOURCE_COMMIT.txt`.

```powershell
.\tools\Publish-Release.ps1
```

To override the version, run a command such as `.\tools\Publish-Release.ps1 -Version 0.1.1`. The package contains the Japanese and English README files, [release notes](RELEASE_NOTES.md), the [OBS output guide](docs/OBS_OUTPUT.md), the license, third-party notices, and per-file SHA-256 checksums. Supporting documentation and release notes are currently maintained in Japanese.

## Save-file safety

The application never modifies the save file. It opens the file using `FileAccess.Read` and `FileShare.ReadWrite | FileShare.Delete`, reads it into an in-memory snapshot, and closes it without creating a temporary copy on disk.

Boss data is embedded in the application, so no strategy website is contacted at runtime. Japanese name sources and the data-generation process are documented in the [reference audit](docs/REFERENCE_AUDIT.md), which is currently available in Japanese.

## Current interface features

- Save selection from the standard location or a user-selected folder
- Character slot, name, and level display and selection
- Cumulative save-file death count and a displayed death count adjusted by a per-character New Game cycle baseline
- Defeated, remaining, total, and completion-percentage summaries
- Synchronized regional progress and boss list
- Filtering by defeat state, region, base game/DLC, and boss name
- Immediate Japanese/English switching for boss, region, and location names
- Immediate dark-theme (default) and light-theme switching
- Automatic UTF-8 text and JSON output for OBS Studio
- Automatic save-update monitoring using both `FileSystemWatcher` and periodic checks
- Debouncing of consecutive change notifications and a manual monitoring toggle
- Restoration of the last save, character slot, display language, monitoring setting, theme, OBS settings, and main-window placement

## User settings

Settings are stored as JSON in the following user directory:

```text
%LOCALAPPDATA%\ERBossTrackerJP\settings.json
```

The application stores the save-file path, character slot number, display language (`ja` / `en`), automatic-monitoring state, theme (`dark` / `light`), OBS output state, OBS output directory, `progress.txt` format, per-character death-count baseline and its enabled state, and the main window's normal position, size, and maximized state. Death-count baselines are associated with the combination of save-file path and character slot.

The minimized state is not stored, so the application will not reopen minimized. If the monitor containing the saved window position is no longer available, the window is moved back into the visible desktop area.

Boss-name search text, defeat-state, base-game/DLC and region filters, and the region selected on the Progress tab are session-only and reset when the application restarts. Save contents, event flags, and character names are not stored in settings. If settings cannot be written, a warning appears at the bottom of the Settings tab; the in-memory changes remain active, and saving is retried on the next setting change.

## Cumulative death count

The death count stored by ELDEN RING is cumulative across New Game cycles. The unadjusted cumulative value is shown in the death summary card on the Progress tab.

When the New Game cycle offset is enabled on either the Settings or OBS Output tab, the displayed death count used by the interface and OBS is calculated as `max(0, cumulative save death count - cycle-start baseline)`. Use ［現在値を基準にする］ (Set Current Value as Baseline) to start the displayed count from `0`. The baseline can also be entered directly. Turning the adjustment off preserves the stored baseline. If the baseline exceeds the cumulative count, the displayed value is clamped to `0` and a warning is shown.

The incorrect additive death-offset value from settings schema 1 is discarded instead of being misinterpreted as a baseline. Other save, display, monitoring, and OBS settings are preserved during migration.

## OBS text output

When OBS text output is enabled, the application writes `progress.txt`, `deaths.txt`, `defeated.txt`, `remaining.txt`, `total.txt`, `percentage.txt`, `latest_boss.txt`, and `snapshot.json` to `%LOCALAPPDATA%\ERBossTrackerJP\OBS` by default. `deaths.txt` contains the adjusted displayed death count. The output directory can be changed in the application, and the OBS Output tab provides separate copy actions for the recommended `progress.txt` and `deaths.txt` paths.

Bosses already defeated during the first load are not treated as new defeats. Only a transition from undefeated to defeated for the same save and character is written to `latest_boss.txt`. See the [OBS text output guide](docs/OBS_OUTPUT.md) for setup and detailed behavior. The guide is currently maintained in Japanese.

The saved character slot is restored only when the same saved file can still be detected. If it cannot be found, the application falls back to the default search and does not apply the previous slot number to an unrelated save. A corrupt or unsupported settings file also falls back to defaults without preventing startup.

## Diagnostic logs

Startup, save loading, automatic monitoring, and settings-persistence diagnostics are written as UTF-8 text to:

```text
%LOCALAPPDATA%\ERBossTrackerJP\Logs\ERBossTrackerJP.log
```

The current log is rotated before it exceeds 2 MiB, with three previous generations retained as `ERBossTrackerJP.log.1` through `.3`. The approximate maximum disk usage is 8 MiB. The application and save monitoring continue if the log cannot be created.

Save bytes, event flags, and character names are not logged. Save-file paths, operating-system and runtime information, and exception details may be present, so review the log before sharing it with a third party.

## Known limitations

- Seamless Co-op `.co2` files are not supported.
- The application cannot modify save data or manually edit boss completion state.
- OBS WebSocket integration and a browser-source overlay are not included. Use the generated text files with OBS text sources.
- The death count is updated when the save file changes. This is not a real-time counter that reads game-process memory.
- No installer or code signing is currently provided.

## License

This project is released under the MIT License. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for reference sources and third-party works.
