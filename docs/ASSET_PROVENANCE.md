# アプリ資産の来歴

この文書は、ER Boss Tracker JPで配布する視覚資産の作成経緯と加工内容を記録する。

## アプリアイコン「進捗カタツムリ」

| 項目 | 内容 |
|---|---|
| 採用日 | 2026-09-14 |
| 作成方法 | OpenAI Codexの組み込み画像生成機能（imagegen）で新規生成 |
| 入力画像 | なし |
| 第三者のロゴ・ゲーム画像 | 使用していない |
| 原画 | `src/ERBossTrackerJP/Assets/AppIconSource.png`（1254 x 1254、透過PNG） |
| アプリ内画像 | `src/ERBossTrackerJP/Assets/ERBossTrackerJP.png`（1024 x 1024、透過PNG） |
| Windowsアイコン | `src/ERBossTrackerJP/Assets/ERBossTrackerJP.ico`（16、20、24、32、40、48、64、128、256 px） |
| 派生処理 | `tools/New-AppIcon.ps1`による高品質縮小とマルチサイズICO化のみ |

採用した生成プロンプトは次のとおり。

```text
Use case: logo-brand
Asset type: Windows desktop application icon concept, pop-cute option D
Primary request: invent an original progress-snail explorer mascot: a compact cheerful snail with a five-segment candy-colored shell that reads as a completion meter, one segment still cream; one eyestalk bends into a tiny locator-pin shape and the other ends in a square dot; body has an unusual soft zigzag underside
Style/medium: crisp flat vector-like sticker icon, charming and clever utility mascot, thick deep-navy outline, simple asymmetric shapes, highly legible at 16–32 pixels
Composition/framing: snail centered inside a pale-sky-blue rounded-square badge; transparent outside the badge; generous padding
Color palette: mint body, coral/tangerine/turquoise/lemon shell segments, sky-blue badge, cream gap, deep navy outline
Constraints: no text, no letters, no numbers, no gradients, no mockup, no watermark, original design only
Avoid: realistic snail, famous cartoon snails, medieval fantasy, dark gothic style, frogs, skulls, weapons, shields, rune circles, recognizable characters, official logos
```

ファイルの同一性確認用SHA-256は、生成後に次の値で固定した。

| ファイル | SHA-256 |
|---|---|
| `AppIconSource.png` | `79CDCC0D7C48D6D7905FCD93D8158802F2D30677A94CB1E4FDAECDA3062FE0D9` |
| `ERBossTrackerJP.png` | `DEF35CD838D4634208CD9B8897D9142B89ED77A0F79AC7B6DDC9A03DC790846E` |
| `ERBossTrackerJP.ico` | `722E428A1C9CC0BECBC5E07C324485AA57AD4EC1C694279ACCE37D5CAD250A62` |

アイコンを変更した場合は、原画、生成プロンプト、入力画像の有無、派生処理およびSHA-256をこの文書で更新する。
