# アーキテクチャ

## 目的

セーブ解析、進捗計算、WPF表示、将来のOBS出力を分離し、バイナリ解析をGUIなしでテストできる構造にする。

## プロジェクト

| プロジェクト | 責務 | 依存先 |
|---|---|---|
| `ERBossTrackerJP.Core` | 言語非依存モデル、進捗スナップショット、出力契約 | なし |
| `ERBossTrackerJP.Save` | BND4、スロット、イベントフラグの読み取り | Core |
| `ERBossTrackerJP` | WPF View、ViewModel、Windows固有サービス、構成ルート | Core、Save |
| `ERBossTrackerJP.Tests` | Core、Save、WPF側サービスとViewModelの単体テスト | Core、Save、ERBossTrackerJP |

依存方向は次のとおりとする。

```text
View -> ViewModel -> Core
          |
          +-------> Save -> Core

TrackerSnapshot -> WPF ViewModel
                -> 将来のOBS出力アダプター
```

セーブ読み込みのアプリケーション境界は次のとおりとする。

```text
SaveFileLocator -> SaveLoadService -> SaveFileSnapshotReader
                                  -> EldenRingSaveReader

SaveLoadService -> TrackerSnapshotService -> BossProgressService
                                      |----> EmbeddedBossDefinitions

FileSystemWatcher ----> SaveFileMonitor ----> MainWindowViewModel
2秒ごとの定期確認 ---------^                      |
                                                   +--> 既存の再読み込み経路

settings.json <----> JsonUserSettingsService <----> MainWindowViewModel

Trace出力 ------> RollingFileTraceListener ------> Logs/ERBossTrackerJP.log
                  2 MiBごとにローテーション ------> .log.1～.log.3
```

`SaveLoadService`はメモリスナップショットとキャラクター解析を統合し、公開結果にはパス、更新日時、ファイルサイズ、キャラクター一覧だけを含める。生のセーブデータは`LoadedSaveFile`内部に保持し、同じスナップショットからイベントフラグを読むサービス処理だけが利用する。

`BossProgressService`は選択スロットから取得したイベントフラグ、検証済みのボス定義、組み込みBSTマップを結合する。ボスを安定した表示順で判定し、地域別集計を含む不変の`TrackerSnapshot`を一度に生成する。判定途中の解析エラーでは部分的なスナップショットを公開しない。

WPFプロジェクトの`TrackerSnapshotService`は、`LoadedSaveFile`から選択スロットのイベントフラグを取得し、組み込みボス定義と`BossProgressService`へ渡すアプリケーション境界である。ViewModelはセーブ内部のバイト列やBSTマップを扱わず、完成した`TrackerSnapshot`だけを受け取る。

`TrackerDisplayService`は`TrackerSnapshot`を変更せず、選択言語に応じたボス名・地域名・場所名を持つ表示モデルへ投影する。撃破状態、地域、本編／DLC、現在の表示言語におけるボス名検索を組み合わせ、言語を切り替えても安定ID、フラグID、撃破状態、集計、元の表示順を維持する。

`SaveFileMonitor`は監視対象ファイルだけを`FileSystemWatcher`で監視し、通知漏れに備えて2秒ごとにファイルサイズと最終更新日時も確認する。異なる更新を検出すると750ミリ秒のデバウンスを開始し、その間の追加通知では待機時間を延長する。ViewModelは通知をUIスレッドへ戻し、手動再読み込みと同じ経路を呼び出す。読み込み中に次の更新を検出した場合は1回にまとめて後続読み込みを実行する。

`JsonUserSettingsService`は`%LOCALAPPDATA%\ERBossTrackerJP\settings.json`を境界とし、最後に正常に読み込んだセーブパス、選択キャラクタースロット、表示言語、自動監視設定だけを一時ファイル経由で保存する。設定の破損、未対応スキーマ、読み書き失敗は起動や画面操作を停止させず、既定値または直前のメモリ上の設定を利用する。保存済みパスが現在も同じセーブ候補として検出できた場合だけスロットを復元し、既定検索へフォールバックした別セーブには適用しない。

`DiagnosticLogService`はアプリ起動時に`RollingFileTraceListener`を登録し、既存の`Trace`出力を`%LOCALAPPDATA%\ERBossTrackerJP\Logs\ERBossTrackerJP.log`へUTF-8で保存する。現行ログが2 MiBを超える前に`.1`から`.3`までローテーションし、各書き込みを即時フラッシュする。ログフォルダー作成、書き込み、ローテーションの失敗はアプリへ伝播させない。起動時にはアプリ・ランタイム・OSバージョンを、終了時には終了コードを記録し、UI、AppDomain、未監視Taskの未処理例外も終了前に記録する。

## 設計規則

- Viewのコードビハインドには表示固有処理以外を置かない。
- ViewModelはバイナリオフセットやイベントフラグを扱わない。
- SaveプロジェクトはWPF、ファイルダイアログ、設定保存へ依存しない。
- バイナリ解析APIは`ReadOnlyMemory<byte>`または`ReadOnlySpan<byte>`を入力とし、ファイルI/Oと分離する。
- 解析失敗を`false`や未撃破へ変換せず、`SaveParseException`と`SaveParseErrorCode`で追跡する。
- `TrackerSnapshot`は構築時にコレクションをコピーし、購読者へ変更可能な一覧を渡さない。
- 将来のOBS出力は`ITrackerOutput`を実装し、WPFコントロールを読み取らない。

初期画面は起動時に保存済みパスを先に確認し、有効なら同じセーブとキャラクタースロットを復元する。無効または未保存なら既定場所を探索し、候補があれば最新のセーブからキャラクター一覧と選択キャラクターの進捗を読み込む。見つからない場合はフォルダー参照を案内する。キャラクター選択、地域選択、撃破状態、本編／DLC、名前検索、表示言語の変更はViewModelから`TrackerDisplayService`へ渡し、同じスナップショットを再解析せず表示だけを更新する。再読み込みに失敗してもViewModelは直前の正常なキャラクター一覧と進捗を消去せず、日本語の状態メッセージだけを更新する。

## ファイル読み込み境界

実ファイルを開くサービスはWPFプロジェクト側のWindows固有サービスとして実装する。セーブファイルは次の共有条件で短時間だけ開き、全内容をメモリ上の`byte[]`へ読み込んだら直ちに閉じる。ディスク上の一時ファイルは作成せず、Saveプロジェクトへ変更されないメモリスナップショットを渡す。

```csharp
FileAccess.Read
FileShare.ReadWrite | FileShare.Delete
```

読み込み前後でファイルサイズと最終更新日時を比較し、読み込み中に変化した場合や一時的な共有違反では短時間リトライする。

自動監視による再読み込みも`SaveFileSnapshotReader`の共有読み取りとリトライを利用する。読み込みまたは同一キャラクタースロットの進捗解析に失敗した場合は、直前の正常な`TrackerSnapshot`と最終正常更新日時を保持し、状態メッセージだけを更新する。ウィンドウ終了時、監視無効化時、セーブ候補変更時には監視資源を解放する。

`SaveFileLocator`は既定の`%APPDATA%\EldenRing`直下にあるアカウント別フォルダーから`ER0000.sl2`を列挙する。既定場所で見つからない場合、GUIから渡されたフォルダーについて、セーブファイルを直接含むフォルダー、アカウント別フォルダーを含むルート、または`EldenRing`フォルダーの親、の3形式を探索できるようにする。初期版の対象外である`.co2`やコピー名は候補に含めない。

Saveプロジェクトには書き込みAPI、イベントフラグ変更API、チェックサム更新APIを追加しない。
