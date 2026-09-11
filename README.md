# ER Boss Tracker JP

PC版ELDEN RINGのセーブファイルを読み取り、キャラクターごとのボス撃破状況を表示するWindows向けアプリです。

現在はセーブ解析、207件のボスデータ、撃破判定・集計、WPF画面への進捗表示、自動監視、ダーク／ライトテーマ、主要なユーザー設定の保存と復元、診断ログまで実装済みです。

## 方針

- Windows 10 / 11、x64
- C# / .NET 10 / WPF
- WPFはMVVMで構成し、セーブ解析から分離する
- `ER0000.sl2`は読み取り専用で扱う
- Seamless Co-opおよび`.co2`は対象外
- OBS出力と死亡カウントは初期版の対象外
- 実在するユーザーのセーブファイルをリポジトリへ含めない

## 動作条件

- Windows 10 version 1809以降またはWindows 11
- x64環境
- PC Steam版ELDEN RINGの標準セーブ`ER0000.sl2`

自己完結版には.NETランタイムが含まれるため、利用者が.NET SDKやVisual Studioをインストールする必要はありません。

## 使い方

1. `ERBossTrackerJP-v0.1.0-win-x64.zip`を任意のフォルダーへ完全に展開します。
2. `ERBossTrackerJP.exe`を起動します。
3. 標準のセーブ場所が見つかれば、自動的に最新の`ER0000.sl2`を読み込みます。
4. 見つからない場合は［フォルダー参照］から`ER0000.sl2`のあるフォルダー、アカウントフォルダーを含む`EldenRing`フォルダー、またはその親フォルダーを指定します。
5. 追跡するキャラクターを選択します。ゲームのセーブ更新は自動的に画面へ反映されます。

画面はダークモードで起動します。右上の［ダークモード］をオフにするとライトモードへ即時に切り替わり、選択は次回起動時にも復元されます。

本アプリはコード署名されていないポータブル版です。Windowsが発行元に関する警告を表示する場合があります。入手元と、同梱の`SHA256SUMS.txt`またはZIP横の`.sha256.txt`を確認してから実行してください。

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

## ソースからのビルド

.NET 10 SDKが必要です。

```powershell
dotnet restore ERBossTrackerJP.sln
dotnet build ERBossTrackerJP.sln -c Release
dotnet test ERBossTrackerJP.sln -c Release
```

## 配布物の作成

次のスクリプトはクリーンなGit作業ツリーを確認し、Releaseテストを実行してから、win-x64自己完結・単一EXEの配布物をGit管理外の`..\outputs\releases`へ生成します。配布物の`SOURCE_COMMIT.txt`には発行元コミットが記録されます。

```powershell
.\tools\Publish-Release.ps1
```

版番号を変更する場合は、例えば`.\tools\Publish-Release.ps1 -Version 0.1.1`と指定します。生成物にはREADME、[リリースノート](RELEASE_NOTES.md)、LICENSE、第三者通知、ファイルごとのSHA-256一覧が含まれます。

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
- ダークモード（既定）／ライトモードの即時切り替え
- `FileSystemWatcher`と定期確認を併用したセーブ更新の自動反映
- 連続する変更通知のデバウンスと手動での自動監視停止
- 最後に読み込んだセーブ、キャラクタースロット、表示言語、自動監視設定、画面テーマの復元

## ユーザー設定

設定は次のユーザー領域へJSONで保存します。

```text
%LOCALAPPDATA%\ERBossTrackerJP\settings.json
```

保存するのはセーブファイルのパス、キャラクタースロット番号、表示言語（`ja` / `en`）、自動監視の有効状態、画面テーマ（`dark` / `light`）だけです。セーブ内容、イベントフラグ、キャラクター名は保存しません。

保存済みセーブが現在も検出できる場合だけ、そのセーブとキャラクタースロットを復元します。見つからない場合は既定場所の検索へ戻り、別のセーブへ以前のスロット番号を誤適用しません。設定ファイルが破損している場合や未対応の形式の場合も、既定値で起動を続けます。

## 診断ログ

起動、セーブ読み込み、自動監視、設定保存などの診断情報は次のUTF-8ログへ保存します。

```text
%LOCALAPPDATA%\ERBossTrackerJP\Logs\ERBossTrackerJP.log
```

ログが2 MiBを超える前にローテーションし、`ERBossTrackerJP.log.1`から`.3`までの3世代を保持します。現行ログと合わせた使用量の目安は最大約8 MiBです。ログを作成できない場合でもアプリとセーブ監視は継続します。

セーブのバイト列、イベントフラグ、キャラクター名は記録しません。セーブファイルのパス、OS・ランタイム情報、例外の詳細は含まれるため、第三者へ渡す前に内容を確認してください。

## 既知の制限

- Seamless Co-opの`.co2`には対応していません。
- セーブデータの変更や、撃破状態の手動編集は行えません。
- OBS向け出力と死亡カウントは初期版の対象外です。
- インストーラーとコード署名はありません。

## ライセンス

このプロジェクトはMIT Licenseで公開します。参考資料と第三者成果物については[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
