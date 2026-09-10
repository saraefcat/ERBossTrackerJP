# ER Boss Tracker JP

PC版ELDEN RINGのセーブファイルを読み取り、キャラクターごとのボス撃破状況を表示するWindows向けアプリです。

現在はセーブ解析、207件のボスデータ、撃破判定と集計まで実装済みです。WPF画面への進捗表示と自動監視はまだ完成していません。

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
├─ ERBossTrackerJP.Core/     ドメインモデル、日英ボスデータ、出力契約
└─ ERBossTrackerJP.Save/     セーブ解析
tests/
└─ ERBossTrackerJP.Tests/    CoreとSaveの自動テスト
tools/                       日本語データの保守用生成スクリプト
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

アプリはセーブファイルを変更しません。ファイル読み込み処理は`FileAccess.Read`と`FileShare.ReadWrite | FileShare.Delete`を使用し、ディスク上へ一時コピーを作らずメモリスナップショットを解析します。

ボスデータはアプリへ埋め込まれており、実行時に攻略サイトへ接続しません。日本語表記の参照元と生成方法は[参考リポジトリ調査記録](docs/REFERENCE_AUDIT.md)に記載しています。

## ライセンス

このプロジェクトはMIT Licenseで公開します。参考資料と第三者成果物については[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
