# ERBossTrackerJP.DataGenerator

FromDataToolkitで生成したELDEN RING v1.17の公式日英テキストと、レビュー済みmappingから、runtime schema v1の`bosses.json`候補および比較レポートを生成する保守用ツールです。

アプリのruntimeからは参照されません。公式テキストJSONも製品や配布物へ組み込みません。

## 入力

- 現行`src/ERBossTrackerJP.Core/Data/bosses.json`
- `Data/boss-localization-map.json`
- FromDataToolkit出力
  - `npc-names.json`
  - `place-names.json`
  - `localization.manifest.json`

公式テキストはゲームデータから生成したローカル入力として扱い、Gitへ追加しません。

## 実行例

リポジトリルートから次のように実行します。

```powershell
dotnet run --project tools/ERBossTrackerJP.DataGenerator -- `
  --source src/ERBossTrackerJP.Core/Data/bosses.json `
  --mapping tools/ERBossTrackerJP.DataGenerator/Data/boss-localization-map.json `
  --official-text-directory ..\reference\official-localization-implementation-ready `
  --output artifacts/official-localization/bosses.generated.json `
  --report artifacts/official-localization/comparison-report.json
```

PR1では、生成JSONまたは比較レポートによってproductionの`bosses.json`を上書きするパスを拒否します。

## 検証方針

- 207件のsourceとmappingがflagIdで完全一致すること
- manifest、NpcName、PlaceName、mappingがschema v1かつELDEN RING 1.17で一致すること
- `exact`、`same-display-ambiguous`、`composite-reviewed`の構造とtextIdを厳密に検証すること
- `same-display-ambiguous`の全候補が同じJA/EN表示を持つこと
- compositeをcomponent順に解決し、日本語`＆`、英語` & `で連結すること
- 文字列類似度、fallback、推測によるtextId選択を行わないこと
- `nameJa`、`nameEn`、`locationJa`、`locationEn`以外の差分をunexpectedとして生成失敗にすること
- 生成JSONを既存runtime schema v1ローダーで再検証してからファイルへ書き込むこと

比較レポートには全207件の旧値、新値、resolution、候補textId、変更フィールドと集計を出力します。production切替はレポートを人間が確認した後の別作業です。
