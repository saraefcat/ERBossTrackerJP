[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\..\reference\kamikouryaku-localization.json'),

    [Parameter()]
    [ValidateRange(100, 5000)]
    [int] $DelayMilliseconds = 250
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$baseUri = [Uri] 'https://kamikouryaku.net/eldenring/'
$lists = @(
    [pscustomobject]@{
        Content = 'baseGame'
        Url = 'https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5'
    },
    [pscustomobject]@{
        Content = 'shadowOfTheErdtree'
        Url = 'https://kamikouryaku.net/eldenring/?%E3%83%9C%E3%82%B9%E6%94%BB%E7%95%A5%28DLC%29'
    }
)

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AutomaticDecompression =
    [System.Net.DecompressionMethods]::GZip -bor
    [System.Net.DecompressionMethods]::Deflate -bor
    [System.Net.DecompressionMethods]::Brotli
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(45)
$client.DefaultRequestHeaders.UserAgent.ParseAdd(
    'ERBossTrackerJP-localization-audit/1.0 (+https://github.com/)')

function Get-PageText {
    param([Parameter(Mandatory)][Uri] $Uri)

    $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
    try {
        $response.EnsureSuccessStatusCode() | Out-Null
        return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    }
    finally {
        $response.Dispose()
    }
}

function ConvertFrom-HtmlFragment {
    param([AllowEmptyString()][string] $Html)

    $withoutTags = [regex]::Replace($Html, '<[^>]+>', '')
    return [System.Net.WebUtility]::HtmlDecode($withoutTags).Trim()
}

try {
    $occurrences = [System.Collections.Generic.List[object]]::new()
    $rowPattern = [regex]::new(
        '<tr><th[^>]*\browspan="3"[^>]*>(?<bossCell>.*?)</th>\s*' +
        '<td[^>]*\browspan="3"[^>]*>(?<areaCell>.*?)</td>',
        [System.Text.RegularExpressions.RegexOptions]::Singleline -bor
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $linkPattern = [regex]::new(
        '<a\s+[^>]*href="(?<href>[^"]+)"[^>]*>(?<text>.*?)</a>',
        [System.Text.RegularExpressions.RegexOptions]::Singleline -bor
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($list in $lists) {
        Write-Host "Reading $($list.Url)"
        $html = Get-PageText -Uri ([Uri] $list.Url)

        foreach ($rowMatch in $rowPattern.Matches($html)) {
            $bossLink = $linkPattern.Match($rowMatch.Groups['bossCell'].Value)
            if (-not $bossLink.Success) {
                continue
            }

            $sourceUrl = [Uri]::new($baseUri, $bossLink.Groups['href'].Value).AbsoluteUri
            $areaNames = @(
                foreach ($areaLink in $linkPattern.Matches($rowMatch.Groups['areaCell'].Value)) {
                    ConvertFrom-HtmlFragment $areaLink.Groups['text'].Value
                }
            )

            $occurrences.Add([pscustomobject]@{
                content = $list.Content
                listNameJa = ConvertFrom-HtmlFragment $bossLink.Groups['text'].Value
                areaNamesJa = $areaNames
                sourceUrl = $sourceUrl
            })
        }
    }

    $pages = [System.Collections.Generic.List[object]]::new()
    $uniqueUrls = @($occurrences | Select-Object -ExpandProperty sourceUrl -Unique)
    $descriptionPattern = [regex]::new(
        '<meta\s+name="description"\s+content="(?<content>[^"]*)"',
        [System.Text.RegularExpressions.RegexOptions]::Singleline -bor
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $namePattern = [regex]::new('(?<ja>[^()]+?)\((?<en>[^()]+)\)')
    $locationPattern = [regex]::new(
        '--場所-(?<locations>.*?)-弱点属性-',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    for ($index = 0; $index -lt $uniqueUrls.Count; $index++) {
        $url = [Uri] $uniqueUrls[$index]
        $progressParameters = @{
            Activity = 'Reading individual boss pages'
            Status = "$($index + 1) / $($uniqueUrls.Count)"
            PercentComplete = (($index + 1) * 100) / $uniqueUrls.Count
        }
        Write-Progress @progressParameters

        $html = Get-PageText -Uri $url
        $descriptionMatch = $descriptionPattern.Match($html)
        $description = if ($descriptionMatch.Success) {
            [System.Net.WebUtility]::HtmlDecode($descriptionMatch.Groups['content'].Value)
        }
        else {
            ''
        }
        $nameSection = $description.Split('--', 2)[0]
        $names = @(
            foreach ($nameMatch in $namePattern.Matches($nameSection)) {
                [pscustomobject]@{
                    nameJa = $nameMatch.Groups['ja'].Value.Trim()
                    nameEn = $nameMatch.Groups['en'].Value.Trim()
                }
            }
        )
        $locationMatch = $locationPattern.Match($description)

        $pageOccurrences = @($occurrences | Where-Object sourceUrl -EQ $url.AbsoluteUri)
        $pages.Add([pscustomobject]@{
            content = @($pageOccurrences | Select-Object -ExpandProperty content -Unique)
            listNamesJa = @($pageOccurrences | Select-Object -ExpandProperty listNameJa -Unique)
            areaNamesJa = @($pageOccurrences | Select-Object -ExpandProperty areaNamesJa -Unique)
            names = $names
            locationsTextJa = if ($locationMatch.Success) {
                $locationMatch.Groups['locations'].Value.Trim()
            }
            else {
                ''
            }
            sourceUrl = $url.AbsoluteUri
        })

        if ($index + 1 -lt $uniqueUrls.Count) {
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
    Write-Progress -Activity 'Reading individual boss pages' -Completed

    $document = [ordered]@{
        schemaVersion = 1
        retrievedAt = [DateTimeOffset]::Now.ToString('O')
        sourcePages = @($lists | Select-Object -ExpandProperty Url)
        occurrences = @($occurrences)
        bossPages = @($pages)
    }

    $resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8NoBOM

    $missingEnglish = @($pages | Where-Object { $_.names.Count -eq 0 })
    Write-Host "Occurrences: $($occurrences.Count)"
    Write-Host "Unique boss pages: $($pages.Count)"
    Write-Host "Pages without an English name: $($missingEnglish.Count)"
    Write-Host "Wrote: $resolvedOutputPath"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
