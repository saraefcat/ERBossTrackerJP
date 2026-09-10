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

## 設計規則

- Viewのコードビハインドには表示固有処理以外を置かない。
- ViewModelはバイナリオフセットやイベントフラグを扱わない。
- SaveプロジェクトはWPF、ファイルダイアログ、設定保存へ依存しない。
- バイナリ解析APIは`ReadOnlyMemory<byte>`または`ReadOnlySpan<byte>`を入力とし、ファイルI/Oと分離する。
- 解析失敗を`false`や未撃破へ変換せず、`SaveParseException`と`SaveParseErrorCode`で追跡する。
- `TrackerSnapshot`は構築時にコレクションをコピーし、購読者へ変更可能な一覧を渡さない。
- 将来のOBS出力は`ITrackerOutput`を実装し、WPFコントロールを読み取らない。

## ファイル読み込み境界

実ファイルを開くサービスはWPFプロジェクト側のWindows固有サービスとして実装する。セーブファイルは次の共有条件で短時間だけ開き、一時ファイルへコピーしてからSaveプロジェクトへバイト列を渡す。

```csharp
FileAccess.Read
FileShare.ReadWrite | FileShare.Delete
```

Saveプロジェクトには書き込みAPI、イベントフラグ変更API、チェックサム更新APIを追加しない。
