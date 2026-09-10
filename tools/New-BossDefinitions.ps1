[CmdletBinding()]
param(
    [Parameter()]
    [string] $SourcePath = (Join-Path $PSScriptRoot '..\..\reference\ER_Boss_Kill_Checklist\bosses.json'),

    [Parameter()]
    [string] $LocalizationPath = (Join-Path $PSScriptRoot '..\..\reference\kamikouryaku-localization.json'),

    [Parameter()]
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\src\ERBossTrackerJP.Core\Data\bosses.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-OrdinalDictionary {
    return [System.Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::Ordinal)
}

$regions = @{
    'Limgrave' = @('limgrave', 'リムグレイブ', 'baseGame')
    'Weeping Peninsula' = @('weeping_peninsula', '啜り泣きの半島', 'baseGame')
    'Stormveil Castle' = @('stormveil_castle', 'ストームヴィル城', 'baseGame')
    'Liurnia of the Lakes' = @('liurnia_of_the_lakes', '湖のリエーニエ', 'baseGame')
    'Moonlight Altar' = @('moonlight_altar', '月光の祭壇', 'baseGame')
    'Academy of Raya Lucaria' = @('academy_of_raya_lucaria', '魔術学院レアルカリア', 'baseGame')
    'Caelid' = @('caelid', 'ケイリッド', 'baseGame')
    "Greyoll's Dragonbarrow" = @('greyolls_dragonbarrow', 'グレイオールの竜塚', 'baseGame')
    'Altus Plateau' = @('altus_plateau', 'アルター高原', 'baseGame')
    'Capital Outskirts' = @('capital_outskirts', '王都外廓', 'baseGame')
    'Mt. Gelmir' = @('mt_gelmir', 'ゲルミア火山', 'baseGame')
    'Volcano Manor' = @('volcano_manor', '火山館', 'baseGame')
    'Leyndell, Royal Capital' = @('leyndell_royal_capital', '王都ローデイル', 'baseGame')
    'Forbidden Lands' = @('forbidden_lands', '禁域', 'baseGame')
    'Mountaintops of the Giants' = @('mountaintops_of_the_giants', '巨人たちの山嶺', 'baseGame')
    'Crumbling Farum Azula' = @('crumbling_farum_azula', '崩れゆくファルム・アズラ', 'baseGame')
    'Consecrated Snowfield' = @('consecrated_snowfield', '聖別雪原', 'baseGame')
    "Miquella's Haligtree" = @('miquellas_haligtree', 'ミケラの聖樹', 'baseGame')
    'Siofra River' = @('siofra_river', 'シーフラ河', 'baseGame')
    'Mohgwyn Dynasty Mausoleum' = @('mohgwyn_dynasty_mausoleum', 'モーグウィン王朝', 'baseGame')
    'Ainsel River' = @('ainsel_river', 'エインセル河', 'baseGame')
    'Deeproot Depths' = @('deeproot_depths', '深き根の底', 'baseGame')
    'Leyndell, Ashen Capital' = @('leyndell_ashen_capital', '灰都ローデイル', 'baseGame')
    'Gravesite Plain' = @('gravesite_plain', '墓地平原', 'shadowOfTheErdtree')
    'Scadu Altus' = @('scadu_altus', '影のアルター', 'shadowOfTheErdtree')
    'Ancient Ruins of Rauh' = @('ancient_ruins_of_rauh', 'ラウフの古遺跡', 'shadowOfTheErdtree')
    'Cerulean Coast' = @('cerulean_coast', '青海岸', 'shadowOfTheErdtree')
    "Charo's Hidden Grave" = @('charos_hidden_grave', 'カロの隠し墓地', 'shadowOfTheErdtree')
    'Jagged Peak' = @('jagged_peak', 'ギザ山', 'shadowOfTheErdtree')
    'Scaduview' = @('scaduview', '影を仰ぐ露台', 'shadowOfTheErdtree')
    'Abyssal Woods' = @('abyssal_woods', '奈落の森', 'shadowOfTheErdtree')
    'Enir-Ilim' = @('enir_ilim', 'エニル・イリム', 'shadowOfTheErdtree')
}

$bossOverrides = New-OrdinalDictionary
$bossOverrides['Abductor Virgin (Swinging Sickle) & Abductor Virgin (Wheel)'] = '人さらいの乙女人形'
$bossOverrides['Beastman of Farum Azula'] = 'ファルム・アズラの獣人'
$bossOverrides['Beastman of Farum Azula (Cleaver) & Beastman of Farum Azula (Throwing Knife)'] = 'ファルム・アズラの獣人'
$bossOverrides['Cleanrot Knight'] = '貴腐騎士'
$bossOverrides['Cleanrot Knight (Spear) & Cleanrot Knight (Sickle)'] = '貴腐騎士'
$bossOverrides['Crucible Knight & Crucible Knight Ordovis'] = '坩堝の騎士&坩堝の騎士、オルドビス'
$bossOverrides['Crucible Knight & Misbegotten Warrior'] = '混種の戦士＆坩堝の騎士'
$bossOverrides['Crystalian (Ringblade)'] = '結晶人'
$bossOverrides['Crystalian (Spear) & Crystalian (Ringblade)'] = '結晶人'
$bossOverrides['Crystalian (Staff) & Crystalian (Spear)'] = '結晶人'
$bossOverrides['Demi-Human Chief(x2)'] = '亜人の親分'
$bossOverrides['Dryleaf Dane'] = '落葉のダン'
$bossOverrides['Erdtree Burial Watchdog (Sword) & Erdtree Burial Watchdog (Scepter)'] = '還樹の番犬'
$bossOverrides['Fell Twin(x2)'] = '忌み双子'
$bossOverrides["Fia's Champion & Sorcerer Rogier & Lionel the Lionhearted"] = 'フィアの英雄'
$bossOverrides['God-Devouring Serpent & Rykard, Lord of Blasphemy'] = '神喰らいの大蛇(冒涜の君主、ライカード)'
$bossOverrides['Godskin Apostle & Godskin Noble & Spiritcaller Snail'] = '神肌の使徒＆神肌の貴種＆霊喚びつむり'
$bossOverrides['Kindred of Rot(x2)'] = '腐敗の眷属'
$bossOverrides['Mad Pumpkin Head (Hammer) & Mad Pumpkin Head (Flail)'] = 'かぼちゃ兜の狂兵'
$bossOverrides['Malenia, Blade of Miquella & Malenia, Goddess of Rot'] = 'ミケラの刃、マレニア（腐敗の女神、マレニア）'
$bossOverrides['Messmer the Impaler & Base Serpent Messmer'] = '串刺し公、メスメル(邪な蛇、メスメル)'
$bossOverrides['Miranda the Blighted Bloom'] = '病み花、ミランダ'
$bossOverrides["Night's Cavalry (Glaive) & Night's Cavalry (Flail)"] = '夜の騎兵'
$bossOverrides['Nox Monk & Nox Swordstress'] = 'ノクスの剣士＆ノクスの僧'
$bossOverrides['Promised Consort Radahn & Radahn, Consort of Miquella'] = '約束の王、ラダーン(ミケラの王、ラダーン)'
$bossOverrides['Putrid Crystalian (Ringblade) & Putrid Crystalian (Spear) & Putrid Crystalian (Staff)'] = '腐敗した結晶人'
$bossOverrides['Radagon of the Golden Order & Elden Beast'] = '黄金律、ラダゴン＆エルデの獣'
$bossOverrides['Roundtable Knight Vyke'] = '円卓の騎士、ヴァイク'
$bossOverrides['Tree Sentinel(x2)'] = 'ツリーガード'
$bossOverrides['Valiant Gargoyle & Valiant Gargoyle (Twinblade)'] = '英雄のガーゴイル'

$places = New-OrdinalDictionary
$places['Abandoned Cave'] = '廃棄洞窟'
$places['Academy Crystal Cave'] = '学院の結晶洞窟'
$places['Agheel Lake'] = 'アギール湖'
$places['Altus Highway Junction'] = 'アルター高原'
$places['Altus Tunnel'] = 'アルター坑道'
$places["Auriza Hero's Grave"] = 'アウレーザの英雄墓'
$places['Auriza Side Tomb'] = 'アウレーザの副墓'
$places['Bellum Church'] = 'ベイルム教会'
$places['Belurat Gaol'] = 'ベルラートの牢獄'
$places['Belurat, Tower Settlement'] = '塔の街、ベルラート'
$places['Beside the Great Bridge'] = '大橋梁の脇'
$places['Bestial Sanctum'] = '獣の神殿'
$places['Black Knife Catacombs'] = '黒き刃の地下墓'
$places['Bonny Gaol'] = 'ボニの牢獄'
$places['Caelem Ruins'] = 'キレムの廃墟'
$places['Caelid Catacombs'] = 'ケイリッドの地下墓'
$places['Caelid Highway South'] = 'ケイリッド街道南'
$places['Capital Rampart'] = '王都城壁前'
$places['Caria Manor'] = 'カーリアの城館'
$places['Castle Ensis'] = 'エンシスの城砦'
$places['Castle Morne Rampart'] = 'モーンの城壁前'
$places['Castle Sol'] = 'ソールの城砦'
$places['Cathedral of Manus Metyr'] = 'マヌス・メテルの大教会'
$places['Cathedral of the Forsaken'] = '忌み捨ての大聖堂'
$places['Cave of the Forlorn'] = '寄る辺の洞窟'
$places['Chapel of Anticipation'] = '王を待つ礼拝堂'
$places['Church of Elleh'] = 'エレの教会'
$places['Church of the Bud'] = '蕾の教会'
$places['Church of the Plague'] = '腐れ病の教会'
$places['Church of Vows'] = '結びの教会'
$places['Cliffbottom Catacombs'] = '断崖下の地下墓'
$places['Coastal Cave'] = '海岸の洞窟'
$places['Consecrated Snowfield Catacombs'] = '聖別雪原の地下墓'
$places['Converted Tower'] = '改宗された塔'
$places["Cuckoo's Evergaol"] = 'カッコウの封牢'
$places['Darklight Catacombs'] = '闇照らしの地下墓'
$places['Deathtouched Catacombs'] = '死に触れた地下墓'
$places['Divine Tower of Caelid'] = 'ケイリッドの神授塔'
$places['Divine Tower of East Altus'] = '東アルターの神授塔'
$places['Dominula, Windmill Village'] = '風車村ドミヌラ'
$places['Dragon Temple Altar'] = '竜の聖堂、祭壇'
$places["Dragon's Pit"] = '竜の穴'
$places['Dragonbarrow Cave'] = '竜塚の洞窟'
$places['Earthbore Cave'] = '穴下りの洞窟'
$places['Eastern Nameless Mausoleum'] = '東の無名霊廟'
$places['Elden Throne'] = 'エルデの王座'
$places['Erdtree Sanctuary'] = '黄金樹の大聖堂'
$places['Fingerstone Hill'] = '指岩の丘'
$places['Flame Peak'] = '火の頂'
$places['Fog Rift Catacombs'] = '霧谷の地下墓'
$places['Fog Rift Fort'] = '霧谷の砦'
$places['Foot of the Jagged Peak'] = 'ギザ山の麓'
$places['Forlorn Hound Evergaol'] = '主なき猟犬の封牢'
$places['Fort Laiedd'] = 'ライード砦'
$places['Fort of Reprimand'] = '懲罰砦'
$places['Fractured Marika'] = '灰都ローデイル'
$places['Freezing Lake'] = '氷結湖'
$places["Fringefolk Hero's Grave"] = '辺境の英雄墓'
$places['Gael Tunnel'] = 'ゲール坑道'
$places['Gaol Cave'] = '牢獄洞窟'
$places['Gate Town Bridge'] = '門前町の橋'
$places['Gate Town North'] = '門前町の北'
$places["Gelmir Hero's Grave"] = 'ゲルミアの英雄墓'
$places["Giant-Conquering Hero's Grave"] = '巨人戦争の英雄墓'
$places["Giants' Mountaintop Catacombs"] = '巨人山嶺の地下墓'
$places['Golden Lineage Evergaol'] = '黄金の一族の封牢'
$places['Grand Cloister'] = '大回廊'
$places["Greyoll's Dragonbarrow"] = 'グレイオールの竜塚'
$places['Groveside Cave'] = '林脇の洞窟'
$places['Hallowhorn Grounds'] = '角骸の霊場'
$places["Hermit Merchant's Shack"] = '世捨て商人のボロ家'
$places['Hermit Village'] = '隠者の村'
$places['Hidden Path to the Haligtree'] = '聖樹への秘路'
$places['Highroad Cave'] = '高路下の洞窟'
$places['Hinterland'] = '隠された地'
$places['Hinterland Bridge'] = '隠された地、橋'
$places["Impaler's Catacombs"] = '串刺しの地下墓'
$places['Inner Consecrated Snowfield'] = '聖別雪原、奥地'
$places["Isolated Merchant's Shack"] = '隠遁商人のボロ家'
$places['Jagged Peak Mountainside'] = 'ギザ山、中腹'
$places['Jagged Peak Summit'] = 'ギザ山、山頂'
$places['Kingsrealm Ruins'] = '王家領の廃墟'
$places['Lakeside Crystal Cave'] = '湖脇の結晶洞窟'
$places["Lamenter's Gaol"] = '嘆きの牢獄'
$places["Lenne's Rise"] = 'レンの魔術師塔'
$places['Leyndell Catacombs'] = 'ローデイルの地下墓'
$places['Limgrave Tunnels'] = 'リムグレイブ坑道'
$places["Lord Contender's Evergaol"] = '王に近付いた者の封牢'
$places['Lux Ruins'] = 'ルクスの廃墟'
$places["Malefactor's Evergaol"] = '盗人の封牢'
$places['Mausoleum Compound'] = '霊廟の群れ'
$places["Midra's Manse"] = 'ミドラーの館'
$places['Minor Erdtree'] = '小黄金樹'
$places['Minor Erdtree Catacombs'] = '小黄金樹の地下墓'
$places['Moonlight Altar'] = '月光の祭壇'
$places['Moorth Ruins'] = 'モースの廃墟'
$places['Morne Moangrave'] = 'モーンの嘆き墓'
$places['Morne Tunnel'] = 'モーンの坑道'
$places['Murkwater Catacombs'] = '曇り川の地下墓'
$places['Murkwater Cave'] = '曇り川の洞窟'
$places['Ninth Mt. Gelmir Campsite'] = 'ゲルミア火山、九合目'
$places['Northern Nameless Mausoleum'] = '北の無名霊廟'
$places['Old Altus Tunnel'] = '旧アルター坑道'
$places["Perfumer's Grotto"] = '調香師の隠し洞窟'
$places['Rampartside Path'] = '城壁沿いの小道'
$places['Rauh Base'] = 'ラウフの麓'
$places['Raya Lucaria Crystal Tunnel'] = 'レアルカリア結晶坑道'
$places['Redmane Castle'] = '赤獅子城'
$places["Ringleader's Evergaol"] = '刃の長の封牢'
$places['Rivermouth Cave'] = '川終わりの洞窟'
$places["Road's End Catacombs"] = '行き止まりの地下墓'
$places['Royal Grave Evergaol'] = '王家墓地の封牢'
$places['Ruin-Strewn Precipice'] = '古遺跡断崖'
$places["Sage's Cave"] = '賢者の洞窟'
$places["Sainted Hero's Grave"] = '貴き者たちの英雄墓'
$places['Scadutree Base'] = '影樹の麓'
$places['Scenic Isle'] = '見晴らし島'
$places['Scorpion River Catacombs'] = 'サソリ川の地下墓'
$places['Sealed Tunnel'] = '封印された坑道'
$places['Seethewater Cave'] = '煮え立ち川の洞窟'
$places['Sellia Crystal Tunnel'] = 'サリアの結晶坑道'
$places['Sellia Evergaol'] = 'サリアの封牢'
$places['Sellia Hideaway'] = 'サリアの隠し洞窟'
$places['Sellia, Town of Sorcery'] = '魔術街サリア'
$places['Shadow Keep'] = '影の城'
$places['Siofra Aqueduct'] = 'シーフラの水道橋'
$places['Siofra River Bank'] = 'シーフラ河、岸辺'
$places['Southern Aeonia Swamp Bank'] = 'エオニア沼、南岸'
$places['Southern Nameless Mausoleum'] = '南の無名霊廟'
$places['Spiritcaller Cave'] = '霊喚びの洞窟'
$places['Starfall Crater'] = 'アルター高原'
$places['Stillwater Cave'] = '溜水の洞窟'
$places['Stone Coffin Fissure'] = '石棺の大穴'
$places['Stormfoot Catacombs'] = '嵐の麓の地下墓'
$places['Stormhill Evergaol'] = '嵐丘の封牢'
$places['Stormveil Castle'] = 'ストームヴィル城'
$places['Stranded Graveyard'] = '漂着墓地'
$places['Subterranean Inquisition Chamber'] = '地の底の責問所'
$places['Summonwater Village'] = '呼び水の村'
$places['Swamp of Aeonia'] = 'エオニアの沼'
$places['Temple of Eiglay'] = 'エーグレーの聖堂'
$places['Temple Quarter'] = '聖堂区画'
$places['The Shaded Castle'] = '日陰城'
$places['Tombsward Catacombs'] = '霊廟ヶ原の地下墓'
$places['Tombsward Cave'] = '霊廟ヶ原の洞窟'
$places['Unsightly Catacombs'] = '醜き地下墓'
$places['Village of the Albinaurics'] = 'しろがね村'
$places['Volcano Cave'] = '火山の洞窟'
$places['Volcano Manor'] = '火山館'
$places['Wailing Dunes'] = '慟哭砂丘'
$places['War-Dead Catacombs'] = '英霊たちの地下墓'
$places["Warmaster's Shack"] = '戦学びのボロ家'
$places['Waypoint Ruins'] = '宿場跡'
$places['Weeping Evergaol'] = '啜り泣きの封牢'
$places['Western Nameless Mausoleum'] = '西の無名霊廟'
$places['Writheblood Ruins'] = '血の蠢く廃墟'
$places['Wyndham Catacombs'] = 'ウィンダムの地下墓'
$places['Wyndham Ruins'] = 'ウィンダムの廃墟'
$places['Yelough Anix Tunnel'] = 'イエロ・アニスの坑道'

$placeOverrides = New-OrdinalDictionary
$placeOverrides['Weeping Peninsula|Minor Erdtree'] = '小黄金樹(啜り泣きの半島)'
$placeOverrides['Liurnia of the Lakes|Converted Tower'] = '小黄金樹(リエーニエ西)'
$placeOverrides['Liurnia of the Lakes|Mausoleum Compound'] = '小黄金樹(リエーニエ東)'
$placeOverrides['Caelid|Minor Erdtree'] = '小黄金樹(ケイリッド北西)'
$placeOverrides["Greyoll's Dragonbarrow|Minor Erdtree"] = '小黄金樹(竜塚)'
$placeOverrides['Altus Plateau|Minor Erdtree'] = '小黄金樹(アルター高原)'
$placeOverrides['Mt. Gelmir|Minor Erdtree'] = '小黄金樹(ゲルミア火山)'
$placeOverrides['Mountaintops of the Giants|Minor Erdtree'] = '小黄金樹(巨人たちの山嶺)'
$placeOverrides['Consecrated Snowfield|Minor Erdtree'] = '小黄金樹(聖別雪原)'

$resolvedSourcePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SourcePath)
$resolvedLocalizationPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LocalizationPath)
$resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)

$source = @(Get-Content -LiteralPath $resolvedSourcePath -Raw | ConvertFrom-Json)
$localization = Get-Content -LiteralPath $resolvedLocalizationPath -Raw | ConvertFrom-Json

$localizedBossNames = [System.Collections.Generic.Dictionary[string, string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($page in $localization.bossPages) {
    foreach ($name in $page.names) {
        if (-not $localizedBossNames.TryAdd($name.nameEn, $name.nameJa) -and
            $localizedBossNames[$name.nameEn] -cne $name.nameJa) {
            throw "Conflicting Japanese names for '$($name.nameEn)'."
        }
    }
}

$bosses = [System.Collections.Generic.List[object]]::new()
$sortOrder = 0
foreach ($sourceRegion in $source) {
    if (-not $regions.ContainsKey($sourceRegion.region_name)) {
        throw "Missing region mapping for '$($sourceRegion.region_name)'."
    }

    $region = $regions[$sourceRegion.region_name]
    foreach ($sourceBoss in $sourceRegion.bosses) {
        $nameJa = if ($bossOverrides.ContainsKey($sourceBoss.boss)) {
            $bossOverrides[$sourceBoss.boss]
        }
        elseif ($localizedBossNames.ContainsKey($sourceBoss.boss)) {
            $localizedBossNames[$sourceBoss.boss]
        }
        else {
            throw "Missing boss-name mapping for '$($sourceBoss.boss)'."
        }

        $sourcePlace = [string] $sourceBoss.place
        $locationEn = if ([string]::IsNullOrWhiteSpace($sourcePlace)) {
            [string] $sourceRegion.region_name
        }
        else {
            $sourcePlace
        }
        $placeOverrideKey = "$($sourceRegion.region_name)|$sourcePlace"
        $locationJa = if ([string]::IsNullOrWhiteSpace($sourcePlace)) {
            [string] $region[1]
        }
        elseif ($placeOverrides.ContainsKey($placeOverrideKey)) {
            $placeOverrides[$placeOverrideKey]
        }
        elseif ($places.ContainsKey($sourcePlace)) {
            $places[$sourcePlace]
        }
        else {
            throw "Missing place mapping for '$sourcePlace'."
        }

        $contentPrefix = if ($region[2] -eq 'baseGame') { 'base' } else { 'dlc' }
        $bosses.Add([ordered]@{
            id = "$contentPrefix.$($region[0]).$($sourceBoss.flag_id)"
            flagId = [uint32] $sourceBoss.flag_id
            nameEn = [string] $sourceBoss.boss
            nameJa = $nameJa
            regionId = [string] $region[0]
            regionEn = [string] $sourceRegion.region_name
            regionJa = [string] $region[1]
            locationEn = $locationEn
            locationJa = $locationJa
            content = [string] $region[2]
            sortOrder = $sortOrder
        })
        $sortOrder++
    }
}

if ($bosses.Count -ne 207) {
    throw "Expected 207 bosses, found $($bosses.Count)."
}

$document = [ordered]@{
    schemaVersion = 1
    bosses = @($bosses)
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$document | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM
Write-Host "Wrote $($bosses.Count) bosses to $resolvedOutputPath"
