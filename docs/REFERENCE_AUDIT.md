# 参考リポジトリ調査記録

調査日: 2026-09-10

## 固定した参照元

| 項目 | 値 |
|---|---|
| Repository | `BuLEEto/ER_Boss_Kill_Checklist` |
| Tag | `v2.1.3` |
| Commit | `fa6fa33bc762a9f2bc1724e93618a07da5a83036` |
| License | MIT |
| Upstream copyright | Copyright (c) 2026 haxenabled.net |

参照用cloneは製品リポジトリ外の`../reference/ER_Boss_Kill_Checklist`に置き、製品のGit履歴へ含めない。

## 確認したファイル

- `save_parser.odin`: BND4、ProfileSummary、可変長セクション、イベントフラグ判定
- `boss_data.odin`: 地域とボスデータの読込・集計
- `bosses.json`: 32地域、207ボス、調査時点で`flag_id`重複なし
- `eventflag_bst.txt`: イベントフラグのブロック番号とブロックオフセット
- `SAVE_FORMAT_REFERENCE.md`: PC版BND4と内部ファイルの技術資料
- `THIRD_PARTY.md`: 由来と第三者クレジット

## 移植時に変更する点

参照元をそのまま逐語移植せず、次の安全性を追加する。

- `file_count`を使った配列確保前に上限とヘッダー範囲を検証する。
- BND4の各エントリーについて、データオフセット、サイズ、名前オフセットを検証する。
- すべての加算と乗算をオーバーフロー検査付きで行う。
- 可変長セクションの不正サイズを推定既定値へ置換せず、解析エラーにする。
- BSTにブロックがない場合とフラグ位置が領域外の場合を未撃破にしない。
- 実ファイルI/Oとバイト列解析を分離する。

## ローカライズデータの参照元

### 現在

ERBossTrackerJPのボス名・場所名の日英ローカライズは、FromDataToolkitでELDEN RING v1.17の公式ゲームテキストから出力した`NpcName`と`PlaceName`を基準とする。日本語と英語は同一のnumeric `textId`で対応付け、文字列類似度や推測によるfallbackは使用しない。

現在の保守経路は次のとおり。

```text
FromDataToolkit
  ↓
npc-names.json / place-names.json / localization.manifest.json
  ↓
reviewed boss-localization-map.json（207件）
  ↓
ERBossTrackerJP.DataGenerator
  ↓
bosses.generated.json / comparison report
  ↓
人手レビュー後にproduction bosses.jsonへ反映
```

公式入力JSONはGit管理せず、製品やruntimeにも含めない。runtimeはFromDataToolkitやゲームデータへ依存せず、レビュー済みmappingから生成した`bosses.json`を埋め込む。production runtime schemaはv1を維持する。

### 初版で使用した旧経路

2026-09-10の初版生成時は、神攻略wikiを日本語固有名詞の照合元として使用した。

- 本編ボス: <https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5>
- DLCボス: <https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5%28DLC%29>
- 地域・場所: <https://kamikouryaku.net/eldenring/>

当時は`Export-KamikouryakuLocalization.ps1`と`New-BossDefinitions.ps1`を保守スクリプトとして使用した。公式ゲームテキストへの移行完了後、この生成経路と両スクリプトは廃止した。攻略本文、攻略表の数値、画像、コメントは製品データへ取り込んでいない。

当時の照合規則は次のとおりだった。

- `bosses.json`の英語名を起点にし、一覧の表示順を識別子として使わない。
- 多形態ボス、同名ボス、複数地点に出現するボスは、英語名、場所、`flag_id`を合わせて確認する。
- 神攻略wikiの分類と参考元の32地域分類が一致しない場合、`regionEn`の分類は維持し、対応する日本語表示名だけを設定する。
- 表記が一致しない項目は推測で埋めず、要確認として残す。
- 取得時点と参照URLを記録し、配布物の第三者通知から辿れるようにする。

この記述は初版データの来歴を残すための監査記録であり、現在のローカライズ生成手順ではない。

## 初版日本語ボスデータの生成記録（旧経路）

2026-09-10に旧経路で次の手順を実施し、初版の本番データを生成した。以下は当時の監査記録であり、現在は実行しない。

1. `Export-KamikouryakuLocalization.ps1`で、本編とDLCの一覧および各個別ページから固有名詞だけをGit管理外の`kamikouryaku-localization.json`へ抽出した。
2. 参考元の英語ボス名154種類のうち124種類を、個別ページに併記された英語名と直接対応付けた。
3. 残る30種類は、複数体、武器違い、二形態、英語側の語順差などを一覧表示と個別ページで確認し、`New-BossDefinitions.ps1`の手動辞書へ明示的な対応を記録した。
4. 地域32件と英語場所名159件を神攻略wikiの表記へ対応付けた。参考元で場所が空欄だった32ボスは、空文字を保存せず所属地域名を場所表示のフォールバックにした。
5. `src/ERBossTrackerJP.Core/Data/bosses.json`を生成し、埋め込みリソースとして読み込むようにした。

生成後の検査結果:

- ボス: 207件（本編165件、DLC42件）
- 地域: 32件
- 参考元の`region_name`、`boss`、`flag_id`との相違: 0件
- `flagId`重複: 0件
- 独自ID重複: 0件
- 日本語名、地域名、場所名の空欄: 0件

旧経路で未解決だった、場所が空欄の32ボスに地域名より詳細な場所を表示するかという課題は、公式`PlaceName`への移行によって解消した。

## 未解決事項

- 対応するセーブバージョンの許容範囲
- BND4内部ファイル名を必須検証する場合の文字コードと終端規則

これらは推測で確定せず、該当フェーズの実装前に調査する。

## eventflag_bst.txtの来歴再監査

再監査日: 2026-09-14

`src/ERBossTrackerJP.Save/Data/eventflag_bst.txt`について、参照リポジトリの履歴とライセンスを再確認した。

| 項目 | 値 |
|---|---|
| ERBossTrackerJP側SHA-256 | `092C3B73B7049D04087DA425544396BC93ED9B1F80EE3BB3C2E4734C8900B728` |
| `er-save-manager`への導入コミット | `8473fd30dd7f0cf6c2114274cb25c1c6980fa8ff` |
| `er-save-manager`で最後に確認したMITコミット | `07bfd75834b9ee46b1e16e8a9c3ccf6d10c9139d` |
| `er-save-manager`のライセンス変更コミット | `4b507ab4818e8406395bd5505b7f0a518da96c58` |
| さらに上流の`ER_Save_File_Fixer`導入コミット | `97a4b84b3f431b3796fdafa135bada572e46dff4` |
| 各upstreamの対象Git blob | `8497ded00dfd8814965715eeeb981425e92bc9d0` |

ローカルファイルはCRLF、upstreamはLFである。ローカルのCRを除いてLFへ正規化すると、11,920組の数値、順序、末尾状態を含めてupstreamとバイト単位で一致する。ローカル側の実質的な変更は改行形式だけである。

上記`er-save-manager`の2コミットと`ER_Save_File_Fixer`の導入コミットでは、対象ファイルとMIT LICENSEが同時に存在する。`er-save-manager`の現在のライセンスはSource Availableへ変更済みのため、現在のmainではなく、上記の固定MITコミットを再配布根拠として記録する。

`er-save-manager`のREADMEにはTGA Event Flag Manager tables等への謝辞があるが、11,920組を最初に生成した主体、元ファイル、生成手順、個別ライセンスまでは一次資料から特定できなかった。このさらに上流の由来は確認不能として扱い、第三者通知では断定しない。
