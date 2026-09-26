# ER Boss Tracker JP 0.1.2

ELDEN RING公式ゲームテキストを基準としたローカライズへ移行したメンテナンスリリースです。

> [!IMPORTANT]
> 本プロジェクトは非公式のコミュニティツールであり、FromSoftwareおよびBandai Namco Entertainmentとは関係ありません。

## 0.1.1からの変更点

- ボス名・場所名の日英表記を、ELDEN RING v1.17の公式NpcName / PlaceNameを基準とするデータへ移行
- 207件すべてのボスについてレビュー済みmappingを導入
- 複数体・武器違い・複数形態のボス名を公式表記に合わせて更新
- 場所名を公式PlaceNameへ統一し、Minor Erdtreeの独自地域補足を削除
- `Blackgaol Knight`を公式NpcNameの`Knight of the Solitary Gaol`へ更新
- `Cleanrot Knight`など、日本語側で省略されていた公式表記を修正
- 旧神攻略wiki／手動辞書によるローカライズ生成経路を廃止
- FromDataToolkit公式テキスト＋レビュー済みmappingから再生成できるDataGeneratorを追加
- validation / comparison reportにより、未解決・fallback・unexpected diffを検出可能にした
- DataGeneratorの生成JSONをLFへ固定し、Windows / Linux / macOS間で決定的な出力にした

## ローカライズ検証

- boss count: 207
- reviewed mapping: 207
- unresolved: 0
- fallback: 0
- warning: 0
- unexpected diff: 0
- productionとDataGenerator再生成結果はバイト単位で一致

## 互換性

- runtime schema v1を維持
- セーブ解析方式の変更なし
- 設定スキーマの変更なし
- FromDataToolkitやゲームデータへのruntime依存なし

## 主な機能

- 標準場所または指定フォルダーから`ER0000.sl2`を読み取り
- 最大10個のキャラクタースロットから追跡対象を選択
- 本編165件、DLC42件、合計207件のボス撃破状況を表示
- 全体・地域別の撃破数、未撃破数、進捗率を表示
- セーブ内の累計死亡数と、キャラクター別の周回基準値を差し引いた表示死亡数を表示
- 撃破状態、地域、本編／DLC、ボス名による絞り込み
- ボス名、地域名、場所名の日本語／English切り替え
- ダークモード（既定）とライトモードの切り替え
- OBSのテキストソース向けUTF-8ファイル出力
- セーブ更新の自動監視と、一時的な読み込み失敗後の自動再試行
- サイズ制限とローテーション付き診断ログ

## 動作条件

- Windows 10 version 1809以降またはWindows 11
- x64環境
- PC Steam版ELDEN RINGの標準セーブ`ER0000.sl2`
- .NETランタイムの別途インストールは不要

## 既知の制限

- Seamless Co-opの`.co2`には対応していません。
- セーブデータを変更する機能や、手動で撃破状態を編集する機能はありません。
- OBS WebSocket連携とブラウザソース画面は含まれません。テキストファイル出力を利用できます。
- 累計死亡数はセーブ更新時に反映され、ゲームプロセスのメモリは監視しません。
- インストーラーとコード署名はありません。ZIPを展開して実行するポータブル版です。
