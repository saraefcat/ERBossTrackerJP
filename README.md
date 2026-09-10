# ER Boss Tracker JP

PC版ELDEN RINGのセーブファイルを読み取り、キャラクターごとのボス撃破状況を表示するWindows向けアプリです。

現在は設計およびセーブ解析PoCの準備段階です。実用できるトラッカーはまだ完成していません。

## 方針

- Windows 10 / 11、x64
- C# / .NET 10 / WPF
- WPFはMVVMで構成し、セーブ解析から分離する
- `ER0000.sl2`は読み取り専用で扱う
- Seamless Co-opおよび`.co2`は対象外
- OBS出力と死亡カウントは初期版の対象外
- 実在するユーザーのセーブファイルをリポジトリへ含めない

## リポジトリ構成

```text
ERBossTrackerJP.sln
src/
├─ ERBossTrackerJP/          WPFアプリと構成ルート
├─ ERBossTrackerJP.Core/     ドメインモデルと出力契約
└─ ERBossTrackerJP.Save/     セーブ解析
tests/
└─ ERBossTrackerJP.Tests/    CoreとSaveの自動テスト
docs/
├─ ARCHITECTURE.md
├─ REFERENCE_AUDIT.md
└─ SAVE_PARSER_DESIGN.md
```

## ビルド

.NET 10 SDKが必要です。

```powershell
dotnet restore ERBossTrackerJP.sln
dotnet build ERBossTrackerJP.sln -c Release
dotnet test ERBossTrackerJP.sln -c Release
```

## 安全性

アプリはセーブファイルを変更しません。実装するファイル読み込み処理は`FileAccess.Read`と`FileShare.ReadWrite | FileShare.Delete`を使用し、一時コピーを解析する設計とします。

## ライセンス

このプロジェクトはMIT Licenseで公開します。参考資料と第三者成果物については[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
