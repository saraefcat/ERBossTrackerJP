# Third-Party Notices

このプロジェクトは、以下の成果物を技術資料として参照しています。取り込むコードやデータが増えた場合は、配布前にこの文書を更新します。

## ER Boss Kill Checklist

- Project: <https://github.com/BuLEEto/ER_Boss_Kill_Checklist>
- Version: `v2.1.3`
- Commit: `fa6fa33bc762a9f2bc1724e93618a07da5a83036`
- Copyright: Copyright (c) 2026 haxenabled.net
- License: MIT
- Usage: BND4構造、キャラクタースロット、可変長セクション、イベントフラグ判定および英語ボスデータの技術資料

```text
MIT License

Copyright (c) 2026 haxenabled.net

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## eventflag_bst.txt（Hapfelプロジェクト由来）

本プロジェクトの取り込み記録では、次の`er-save-manager`を取得元としています。

- Project: <https://github.com/Hapfel1/er-save-manager>
- MIT-era import commit: `8473fd30dd7f0cf6c2114274cb25c1c6980fa8ff`
- Last verified MIT commit: `07bfd75834b9ee46b1e16e8a9c3ccf6d10c9139d`
- Upstream file: `src/resources/eventflag_bst.txt`
- Upstream Git blob: `8497ded00dfd8814965715eeeb981425e92bc9d0`
- License at the commits above: MIT
- MIT-era source: <https://github.com/Hapfel1/er-save-manager/blob/07bfd75834b9ee46b1e16e8a9c3ccf6d10c9139d/src/resources/eventflag_bst.txt>
- MIT-era license: <https://github.com/Hapfel1/er-save-manager/blob/07bfd75834b9ee46b1e16e8a9c3ccf6d10c9139d/LICENSE>
- License at the current upstream: Source Available
- License-change commit: `4b507ab4818e8406395bd5505b7f0a518da96c58`
- Current license: <https://github.com/Hapfel1/er-save-manager/blob/main/LICENSE>
- Usage: セーブ形式の順次解析方式とイベントフラグ格納位置の対応表
- Redistributed data: `src/ERBossTrackerJP.Save/Data/eventflag_bst.txt`
- Redistributed SHA-256: `092C3B73B7049D04087DA425544396BC93ED9B1F80EE3BB3C2E4734C8900B728`
- Modification: 数値ペアの内容と順序は変更せず、改行をLFからCRLFへ変更

`eventflag_bst.txt`と同一のGit blobは、`er-save-manager`へ取り込まれる前に次のプロジェクトにも存在します。

- Project: <https://github.com/Hapfel1/ER_Save_File_Fixer>
- Introduction commit: `97a4b84b3f431b3796fdafa135bada572e46dff4`
- Upstream file: `src/er_save_fixer/resources/eventflag_bst.txt`
- Upstream Git blob: `8497ded00dfd8814965715eeeb981425e92bc9d0`
- License at the commit above: MIT
- MIT-era source: <https://github.com/Hapfel1/ER_Save_File_Fixer/blob/97a4b84b3f431b3796fdafa135bada572e46dff4/src/er_save_fixer/resources/eventflag_bst.txt>
- MIT-era license: <https://github.com/Hapfel1/ER_Save_File_Fixer/blob/97a4b84b3f431b3796fdafa135bada572e46dff4/LICENSE>

ERBossTrackerJP側のCRを除いてLFへ正規化すると、上記upstreamファイルとバイト単位で一致します。MIT時代の固定コミットと当時のLICENSEを再配布根拠としており、現在のSource Available版からデータを取り込んだものではありません。

`er-save-manager`のMIT表示は次のとおりです。

```text
MIT License

Copyright (c) 2026 Hapfel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

`ER_Save_File_Fixer`のMIT表示は次のとおりです。

```text
MIT License

Copyright (c) 2025 Hapfel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Microsoft .NET

- Product: Microsoft .NET 10
- Usage: win-x64自己完結版へ.NETランタイムとWPFランタイムを同梱
- Distribution terms: 発行に使用した.NET SDK付属の`LICENSE.txt`
- Third-party notices: 発行に使用した.NET SDK付属の`ThirdPartyNotices.txt`
- Redistributed notices: 配布物の`DOTNET_LICENSE.txt`および`DOTNET_THIRD_PARTY_NOTICES.txt`

発行スクリプトは、実際に使用する`dotnet`実行ファイルと同じディレクトリから上記2ファイルを取得し、改変せず配布物へコピーする。これにより、自己完結版へ含まれるランタイムに対応した配布条件と第三者通知を同梱する。

## Additional format research

次の成果物はセーブ形式の相互確認資料です。現時点でこれらのソースコードは取り込んでいません。コードまたはデータを取り込む場合は、その時点で各リビジョンとライセンスを改めて確認します。

- ER-Save-Lib by ClayAmore: <https://github.com/ClayAmore/ER-Save-Lib>
- EldenRingSaveTemplate by ClayAmore: <https://github.com/ClayAmore/EldenRingSaveTemplate>
- The Grand Archives Elden Ring Cheat Table: <https://github.com/The-Grand-Archives/Elden-Ring-CT-TGA>
- Souls Modding Community: <https://soulsmodding.com>
- DSDeaths community-maintained fork: <https://github.com/saraefcat/DSDeaths>（2026-09-12に周回オフセットの利用者向け仕様だけを確認。ソースコード、メモリ参照処理、UIは取り込んでいない）

## 神攻略wiki（エルデンリング攻略wiki）

- Site: <https://kamikouryaku.net/eldenring/>
- Base-game boss list: <https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5>
- DLC boss list: <https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5%28DLC%29>
- Accessed: 2026-09-10
- Usage: ボス名、地域名、場所名の日本語表記と、個別ボスページに併記された英語名の照合
- Not redistributed: 攻略本文、攻略表の数値、画像、コメント

同サイトの転載方針に従い、出典ページへのリンクを本通知に記載しています。ゲーム名、会社名、製品名およびゲーム内固有名詞に関する権利は、それぞれの権利者に帰属します。

Skald、SDL3、Vulkan関連コードおよびフォントは本プロジェクトへ取り込んでいません。WPFはWindowsに含まれる.NETデスクトップフレームワークを使用します。

## アプリアイコン

アプリアイコン「進捗カタツムリ」は、第三者のロゴやゲーム画像を入力せず、OpenAI Codexの組み込み画像生成機能で新規生成したものです。生成プロンプト、原画、派生処理およびファイルハッシュは[アプリ資産の来歴](docs/ASSET_PROVENANCE.md)に記録しています。
