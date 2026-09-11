# ER Boss Tracker JP

PC版ELDEN RINGのセーブファイルを読み取り、キャラクターごとのボス撃破状況を表示するWindows向けアプリです。

現在はセーブ解析、207件のボスデータ、撃破判定・集計、WPF画面への進捗表示、自動監視、主要なユーザー設定の保存と復元まで実装済みです。

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

## 現在の画面機能

- 既定場所または指定フォルダーからのセーブ選択
- キャラクタースロット、名前、レベルの表示と選択
- 撃破数、未撃破数、総数、進捗率の表示
- 地域別進捗とボス一覧の連動
- 撃破状態、地域、本編／DLC、ボス名による絞り込み
- ボス名、地域名、場所名の日本語／English即時切り替え
- `FileSystemWatcher`と定期確認を併用したセーブ更新の自動反映
- 連続する変更通知のデバウンスと手動での自動監視停止
- 最後に読み込んだセーブ、キャラクタースロット、表示言語、自動監視設定の復元

## ユーザー設定

設定は次のユーザー領域へJSONで保存します。

```text
%LOCALAPPDATA%\ERBossTrackerJP\settings.json
```

保存するのはセーブファイルのパス、キャラクタースロット番号、表示言語（`ja` / `en`）、自動監視の有効状態だけです。セーブ内容、イベントフラグ、キャラクター名は保存しません。

保存済みセーブが現在も検出できる場合だけ、そのセーブとキャラクタースロットを復元します。見つからない場合は既定場所の検索へ戻り、別のセーブへ以前のスロット番号を誤適用しません。設定ファイルが破損している場合や未対応の形式の場合も、既定値で起動を続けます。

## ライセンス

このプロジェクトはMIT Licenseで公開します。参考資料と第三者成果物については[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
