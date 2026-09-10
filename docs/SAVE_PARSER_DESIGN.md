# セーブ解析設計

## 参照元からC#への対応

| 参照元の処理 | C#側の責務 | 状態 |
|---|---|---|
| `open_save_file` | `SaveFileSnapshotReader`による共有読み取りとメモリスナップショット | 初期実装済み |
| `parse_save_bytes` | `Bnd4Reader` | 初期実装済み |
| `get_character_slots` | `CharacterSlotReader` | 初期実装済み |
| `find_event_flags_offset` | `EventFlagSectionReader` | 初期実装済み |
| `load_bst_map` | `EventFlagBlockMapReader` | 初期実装済み |
| `check_event_flag` | `EventFlagReader` | 初期実装済み |
| `load_boss_data` | ボス定義ローダーと検証サービス | 未実装 |

外部から使用する入口は`IEldenRingSaveReader`とし、`EldenRingSaveReader`がBND4解析、キャラクター一覧、選択スロットのイベントフラグ領域取得を統合する。PC版セーブとして12エントリーを要求し、解析エラーは`SaveParseException`で返す。

## BND4

| 項目 | オフセット／サイズ |
|---|---:|
| Magic | `0x00` / 4 bytes (`BND4`) |
| Header minimum | `0x40` bytes |
| File count | `0x0C` / `u32` little-endian |
| Entry headers start | `0x40` |
| Entry header size | `0x20` bytes |
| Entry size | entry `+0x08` / `u64` little-endian |
| Entry data offset | entry `+0x10` / `u32` little-endian |
| Entry name offset | entry `+0x14` / `u32` little-endian |

PC版では通常12エントリーを持つが、12という期待値を検証する前に、宣言されたエントリー数を使った計算がファイル範囲内であることを確認する。過大確保を防ぐため、汎用BND4層の対応上限を1024エントリーとする。Elden Ring固有層では後続実装で12エントリーを要求する。各エントリーは`dataStart <= offset <= fileLength`かつ`size <= fileLength - offset`の形式で検証し、`offset + size`を未検査で計算しない。名前オフセットはエントリーヘッダー表の末尾からデータ領域先頭までの名前テーブル内に限定する。

## キャラクタースロット

- `USER_DATA_000`～`USER_DATA_009`: 最大10スロット
- PC版エントリー先頭のMD5: `0x10` bytes。読み取りでは検証候補だが更新しない。
- `USER_DATA_010`のProfileSummary: エントリーデータ先頭から`0x1964`
- 有効スロット配列: 10 bytes
- Profile entry: 1件`0x24C` bytes、10件
- 名前: entry `+0x00`、UTF-16LE、最大32 bytes、NUL終端
- レベル: entry `+0x22`、`u32` little-endian

`CharacterSlotReader`は非アクティブスロットを結果から除外し、元の0始まりスロット番号を保持する。有効スロットの名前は置換フォールバックを使わずUTF-16LEとして厳密にデコードし、空名、空白名、不正なサロゲートを`InvalidCharacterName`として扱う。

## イベントフラグ直前までの順次解析

以下の位置は、PC版キャラクターエントリーの16-byte checksumを除いた`slotData`先頭を基準とする。

| 順序 | セクション | サイズ／規則 |
|---:|---|---|
| 1–4 | Header | 32 bytes。先頭`version`が0なら空スロット |
| 5 | Gaitem map | version `<= 81`: `0x13FE`件、それ以外: `0x1400`件。各8 bytesに条件付き8 bytes、type `0x80000000`ではさらに5 bytes |
| 6 | PlayerGameData | `0x1B0` bytes |
| 7 | SPEffects | `13 * 16` bytes |
| 8 | EquippedItemsEquipIndex | 88 bytes |
| 9 | ActiveWeaponSlotsAndArmStyle | 28 bytes |
| 10 | EquippedItemsItemIds | 88 bytes |
| 11 | EquippedItemsGaitemHandles | 88 bytes |
| 12 | Held inventory | `4 + 0xA80*12 + 4 + 0x180*12 + 4 + 4` bytes |
| 13 | EquippedSpells | `14*8 + 4` bytes |
| 14 | EquippedItems | `10*8 + 4 + 6*8 + 4 + 4` bytes |
| 15 | EquippedGestures | `6*4` bytes |
| 16 | AcquiredProjectiles | count `u32` + `count*8` bytes |
| 17 | EquippedArmamentsAndItems | `39*4` bytes |
| 18 | EquippedPhysics | `3*4` bytes |
| 19 | FaceData | `0x12F` bytes |
| 20 | Storage inventory | `4 + 0x780*12 + 4 + 0x80*12 + 4 + 4` bytes |
| 21 | Gestures | `64*4` bytes |
| 22 | Unlocked Regions | count `u32` + `count*4` bytes |
| 23 | RideGameData | 40 bytes |
| 24 | Control byte | 1 byte |
| 25 | BloodStain | `0x44` bytes |
| 26 | Unknown GameDataMan fields | 8 bytes |
| 27 | MenuProfileSaveLoad | 8-byte header。`+4`のsize分を後続データとして読む |
| 28 | TrophyEquipData | `0x34` bytes |
| 29 | GaitemGameData | `8 + 7000*16` bytes |
| 30 | TutorialData | 8-byte header。`+4`のsize分を後続データとして読む |
| 31 | GameMan bytes | 3 bytes |
| 32 | Total death count | 4 bytes。初期版では値を利用しない |
| 33–39 | Online／character state | 22 bytes |
| 40 | Event flags | `0x1BF99F` bytes |

可変長値は、値を`int`へ変換する前に上限、残りサイズ、乗算オーバーフローを検証する。不正値を推定値へ置換して解析を続行しない。

## イベントフラグ判定

`eventflag_bst.txt`はUTF-8の`block,offset`形式として厳密に読み取る。空行は許容するが、不正な列数、符号付き値、`uint`範囲外、ブロック重複、オフセット重複、空のマップは`InvalidEventFlagBlockMap`とする。固定した埋め込みデータは11,920件で、SHA-256は`092C3B73B7049D04087DA425544396BC93ED9B1F80EE3BB3C2E4734C8900B728`である。

```text
block      = eventId / 1000
index      = eventId % 1000
byteOffset = blockOffsets[block] * 125 + index / 8
bitIndex   = 7 - index % 8
```

ビット順はMSB-firstである。BSTに`block`がない場合は`MissingEventFlagBlock`、`byteOffset`がイベントフラグ領域外の場合は`EventFlagOutOfBounds`として扱う。

## 境界検査の共通規則

- 範囲検証は`size <= length - offset`を使用する。
- 符号なし値から`int`への変換前に`int.MaxValue`以下であることを確認する。
- countと要素サイズの積は`checked`または除算による事前検証を使用する。
- 読み取りヘルパーは範囲外を0として返さず、位置情報付きの解析エラーを返す。
- 実ファイルを使う結合テストと、合成バイト列を使う単体テストを分離する。
