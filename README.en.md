# ER Boss Tracker JP

[日本語](README.md) | English

> [!NOTE]
> The [Japanese README](README.md) is the canonical version. This English file is maintained as a reference translation.

ER Boss Tracker JP is a Windows application for viewing per-character boss completion and death counts from PC Steam ELDEN RING save files. It automatically detects save updates and can provide text files for use in OBS Studio streams.

> [!IMPORTANT]
> This is an unofficial community tool and is not affiliated with FromSoftware or Bandai Namco Entertainment.

## Key features

- Tracks 207 bosses: 165 from the base game and 42 from the DLC
- Automatically calculates defeated, remaining, total, and regional progress
- Selectable progress for each character slot
- Automatic updates while playing by monitoring the save file
- Cumulative save death count and an adjustable displayed count that can start from zero for a New Game cycle
- Filtering by boss name, region, defeat state, and base game/DLC
- Japanese/English switching for boss, region, and location names
- Dark and light themes
- OBS text output for boss progress, death count, latest defeated boss, and individual totals
- Restoration of the last save, character, settings, and window placement

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- x64 environment
- PC Steam version of ELDEN RING
- Standard `ER0000.sl2` save file

The self-contained release includes the .NET runtime. End users do not need to install the .NET SDK or Visual Studio.

## Download and launch

1. Download `ERBossTrackerJP-v0.1.1-win-x64.zip` from [GitHub Releases](../../releases/latest).
2. Fully extract the ZIP to a folder of your choice.
3. Launch `ERBossTrackerJP.exe` from the extracted folder.

> [!IMPORTANT]
> The current release has no installer or code signing. Windows may display an unknown-publisher warning. Verify the download source and the `.sha256.txt` file next to the ZIP, or `SHA256SUMS.txt` after extraction, before running the application.

## Basic usage

1. On startup, the application searches the standard save location for the most recently updated `ER0000.sl2`.
2. If no save is found, select ［フォルダーを選択］ (Select Folder) and choose one of the following:
   - The account folder containing `ER0000.sl2`
   - The `EldenRing` folder containing the account folders
   - A parent folder containing that `EldenRing` folder
3. Select the character slot to track under ［キャラクター］ (Character).
4. When the game updates the save file, boss progress and death counts are reloaded automatically.

The Progress tab shows regional completion and the boss list. Filters and search terms reset whenever the application restarts.

The application starts in dark mode. Disable ［ダークモード］ (Dark Mode) on the Settings tab to use the light theme.

The interface itself is primarily Japanese. The language setting changes proper names such as bosses, regions, and locations; it does not translate every interface label.

## Displaying data in OBS Studio

1. Open the OBS Output tab.
2. Enable ［OBSテキスト出力を有効にする］ (Enable OBS Text Output).
3. Add a Text (GDI+) source in OBS Studio.
4. Enable Read from file and select the appropriate output file.

| File | Example | Purpose |
|---|---|---|
| `progress.txt` | `155 / 207 (74.9%)` | Recommended one-line boss progress display |
| `deaths.txt` | `908` | Displayed death count after applying the cycle baseline |
| `latest_boss.txt` | `Godrick the Grafted` | Bosses newly detected as defeated since the application started |
| `defeated.txt`, etc. | Number only | Separate defeated, remaining, total, and percentage values |

The default output folder is:

```text
%LOCALAPPDATA%\ERBossTrackerJP\OBS
```

The output folder and `progress.txt` format can be changed in the application. See the [OBS text output guide](docs/OBS_OUTPUT.md) for setup and detailed file behavior. The guide is currently maintained in Japanese.

## Death counts and New Game cycle baseline

The death count stored by ELDEN RING is cumulative across New Game cycles. The death summary card on the Progress tab shows two values:

- Cumulative death count: the unadjusted value stored in the save
- Displayed death count: the cumulative value minus the baseline captured at the start of the current cycle

When the cycle offset is enabled on either the Settings or OBS Output tab, the displayed death count is calculated as:

```text
max(0, cumulative death count - cycle-start baseline)
```

Select ［現在値を基準にする］ (Set Current Value as Baseline) to start the displayed count from zero at that point. If the baseline exceeds the current cumulative count, the result is clamped to zero and a warning is shown. The baseline is stored separately for each save-file and character-slot combination.

## Save-file safety

The application opens `ER0000.sl2` as read-only and never modifies it. The file is read into memory for analysis, without creating a temporary save-file copy on disk.

The game process memory is not accessed. Boss data is embedded in the application, and no strategy website is contacted while the application is running.

> [!TIP]
> As a general precaution when using any external save-related tool, back up important save data beforehand.

## Saved settings

Settings are stored as JSON at:

```text
%LOCALAPPDATA%\ERBossTrackerJP\settings.json
```

Stored settings include the last save, character slot, proper-name language, automatic monitoring, theme, OBS output settings, per-character cycle baselines, and main-window position and size.

Search terms, boss-list filters, the region selected on the Progress tab, and the minimized window state are not stored. If the monitor containing the saved window position is no longer available, the window is moved back into the visible desktop area.

The saved character slot is restored only when the same save can still be found. Otherwise, the application falls back to the standard search and does not apply the previous slot number to an unrelated save. A corrupt settings file also falls back to defaults without preventing startup.

## Troubleshooting

### The save file is not found

Use ［フォルダーを選択］ (Select Folder) to choose the folder containing `ER0000.sl2` or the parent `EldenRing` folder.

### Game progress is not updating

- Confirm that ［自動監視］ (Automatic Monitoring) is enabled.
- Wait for the game to finish writing its save.
- Select ［再読み込み］ (Reload) to try a manual update.

### OBS is not updating

- Confirm that OBS text output is enabled.
- Confirm that OBS references the correct `progress.txt` or `deaths.txt`.
- Select ［現在値を再出力］ (Write Current Values Again).

### Diagnostic log

Startup, save loading, automatic monitoring, and settings diagnostics are written as UTF-8 text to:

```text
%LOCALAPPDATA%\ERBossTrackerJP\Logs\ERBossTrackerJP.log
```

The log is rotated before it exceeds 2 MiB, with three previous generations retained. Save bytes, event flags, and character names are not logged. Save-file paths, operating-system and runtime information, and exception details may be present, so review the log before sharing it.

If the problem continues, remove personal information and report the reproduction steps and relevant log details through [GitHub Issues](../../issues). Do not publish an actual save file.

## Known limitations

- Seamless Co-op `.co2` files are not supported.
- Save data and boss completion state cannot be modified manually.
- OBS WebSocket integration and a browser-source overlay are not included. Use the generated text files with OBS text sources.
- Death counts update when the save file changes. This is not a real-time counter that reads game-process memory.
- The overall interface is Japanese; only proper names can currently be switched to English.

## For developers

### Repository layout

```text
ERBossTrackerJP.sln
src/
├─ ERBossTrackerJP/          WPF application and composition root
├─ ERBossTrackerJP.Core/     Domain models, Japanese/English boss data, output contracts
└─ ERBossTrackerJP.Save/     Save parser
tests/
└─ ERBossTrackerJP.Tests/    Automated tests
tools/                       Data maintenance and release scripts
docs/                        Design documents and OBS output guide
```

### Building from source

.NET SDK `10.0.204` is required. The repository pins the SDK in `global.json`.

```powershell
dotnet restore ERBossTrackerJP.sln
dotnet build ERBossTrackerJP.sln -c Release
dotnet test ERBossTrackerJP.sln -c Release
```

Test-only NuGet dependencies are pinned in `tests/ERBossTrackerJP.Tests/packages.lock.json`. Disable locked mode and regenerate that file only when intentionally changing dependency versions.

### Creating a release

The following script requires a clean Git working tree, runs the Release test suite, and creates a self-contained, single-file win-x64 package under `..\outputs\releases`.

```powershell
.\tools\Publish-Release.ps1
```

To override the version, run a command such as `.\tools\Publish-Release.ps1 -Version 0.1.1`. `SOURCE_COMMIT.txt` in the package records the source commit.

## Related documents

- [Release notes](RELEASE_NOTES.md) — Japanese
- [OBS text output guide](docs/OBS_OUTPUT.md) — Japanese
- [Architecture](docs/ARCHITECTURE.md) — Japanese
- [Save parser design](docs/SAVE_PARSER_DESIGN.md) — Japanese
- [Reference and data audit](docs/REFERENCE_AUDIT.md) — Japanese
- [Application asset provenance](docs/ASSET_PROVENANCE.md) — Japanese
- [Third-party notices](THIRD_PARTY_NOTICES.md)

## License and disclaimer

This project is released under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for reference sources and third-party works.

This is an unofficial community tool and is not affiliated with FromSoftware or Bandai Namco Entertainment. ELDEN RING and related names and trademarks belong to their respective owners.
