# アーキテクチャ

## 目的

セーブ解析、進捗計算、WPF表示、将来のOBS出力を分離し、バイナリ解析をGUIなしでテストできる構造にする。

## プロジェクト

| プロジェクト | 責務 | 依存先 |
|---|---|---|
| `ERBossTrackerJP.Core` | 言語非依存モデル、進捗スナップショット、出力契約 | なし |
| `ERBossTrackerJP.Save` | BND4、スロット、イベントフラグの読み取り | Core |
| `ERBossTrackerJP` | WPF View、ViewModel、Windows固有サービス、構成ルート | Core、Save |
| `ERBossTrackerJP.Tests` | CoreとSaveの単体テスト | Core、Save |

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
```

`SaveLoadService`はメモリスナップショットとキャラクター解析を統合し、公開結果にはパス、更新日時、ファイルサイズ、キャラクター一覧だけを含める。生のセーブデータは`LoadedSaveFile`内部に保持し、同じスナップショットからイベントフラグを読むサービス処理だけが利用する。

`BossProgressService`は選択スロットから取得したイベントフラグ、検証済みのボス定義、組み込みBSTマップを結合する。ボスを安定した表示順で判定し、地域別集計を含む不変の`TrackerSnapshot`を一度に生成する。判定途中の解析エラーでは部分的なスナップショットを公開しない。

`TrackerDisplayService`は`TrackerSnapshot`を変更せず、選択言語に応じたボス名・地域名・場所名を持つ表示モデルへ投影する。撃破状態、地域、本編／DLC、現在の表示言語におけるボス名検索を組み合わせ、言語を切り替えても安定ID、フラグID、撃破状態、集計、元の表示順を維持する。

## 設計規則

- Viewのコードビハインドには表示固有処理以外を置かない。
- ViewModelはバイナリオフセットやイベントフラグを扱わない。
- SaveプロジェクトはWPF、ファイルダイアログ、設定保存へ依存しない。
- バイナリ解析APIは`ReadOnlyMemory<byte>`または`ReadOnlySpan<byte>`を入力とし、ファイルI/Oと分離する。
- 解析失敗を`false`や未撃破へ変換せず、`SaveParseException`と`SaveParseErrorCode`で追跡する。
- `TrackerSnapshot`は構築時にコレクションをコピーし、購読者へ変更可能な一覧を渡さない。
- 将来のOBS出力は`ITrackerOutput`を実装し、WPFコントロールを読み取らない。

初期画面は起動時に既定場所を探索し、候補があれば最新のセーブからキャラクター一覧を読み込む。見つからない場合はフォルダー参照を案内する。再読み込みに失敗してもViewModelは直前の正常なキャラクター一覧を消去せず、日本語の状態メッセージだけを更新する。

## ファイル読み込み境界

実ファイルを開くサービスはWPFプロジェクト側のWindows固有サービスとして実装する。セーブファイルは次の共有条件で短時間だけ開き、全内容をメモリ上の`byte[]`へ読み込んだら直ちに閉じる。ディスク上の一時ファイルは作成せず、Saveプロジェクトへ変更されないメモリスナップショットを渡す。

```csharp
FileAccess.Read
FileShare.ReadWrite | FileShare.Delete
```

読み込み前後でファイルサイズと最終更新日時を比較し、読み込み中に変化した場合や一時的な共有違反では短時間リトライする。最終的な解析失敗時に前回の正常な表示を破棄するかどうかは、後続の監視サービスが判断する。

`SaveFileLocator`は既定の`%APPDATA%\EldenRing`直下にあるアカウント別フォルダーから`ER0000.sl2`を列挙する。既定場所で見つからない場合、GUIから渡されたフォルダーについて、セーブファイルを直接含むフォルダー、アカウント別フォルダーを含むルート、または`EldenRing`フォルダーの親、の3形式を探索できるようにする。初期版の対象外である`.co2`やコピー名は候補に含めない。

Saveプロジェクトには書き込みAPI、イベントフラグ変更API、チェックサム更新APIを追加しない。
