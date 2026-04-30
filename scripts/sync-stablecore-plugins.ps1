param(
    [string]$StagingPath = "C:\Users\User\Documents\COG Modding Stuff\Notes\New folder",
    [string]$PluginsPath = "C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins",
    [switch]$RemoveDenylistedFromPlugins
)

$ErrorActionPreference = "Stop"

$allowList = @(
    "ProhibitionLauncher.dll",
    "GameplayTweaks.dll",
    "CopKilling.dll",
    "GameOptimizer.dll",
    "BossBuildings.dll",
    "BossDeath.dll",
    "OrgChartMod.dll",
    "AutoLevelup.dll",
    "10K Button & Input.dll",
    "UIEnhancer.dll",
    "Tickerenhancer.dll",
    "OutpostTickerMod.dll",
    "GangWars.dll",
    "TerritoryExpansionPatch.dll",
    "MafiaHierarchy.dll",
    "CoGCustomAssets.dll",
    "CustomPortraits.dll",
    "Traiticonlimitplugin.dll",
    "Cog Ultimate Cheat.dll",
    "DirtyCashEconomy.dll",
    "DirtyCashVolumeFix.dll"
)

$denyList = @(
    "ElectionVoteCheat.dll",
    "Boss Manager.dll",
    "CityOfGangstersOptimizer.dll"
)

if (-not (Test-Path $StagingPath)) {
    throw "Staging path not found: $StagingPath"
}
if (-not (Test-Path $PluginsPath)) {
    throw "Plugins path not found: $PluginsPath"
}

Write-Host "[SYNC] staging=$StagingPath"
Write-Host "[SYNC] plugins=$PluginsPath"

$copied = @()
$missing = @()
$skipped = @()

foreach ($dll in $allowList) {
    if ($denyList -contains $dll) {
        $skipped += "$dll (denylisted)"
        continue
    }

    $src = Join-Path $StagingPath $dll
    $dst = Join-Path $PluginsPath $dll
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Force
        $copied += $dll
        Write-Host "[SYNC] copied $dll"
    }
    else {
        $missing += $dll
        Write-Host "[SYNC] missing $dll"
    }
}

if ($RemoveDenylistedFromPlugins) {
    foreach ($dll in $denyList) {
        $dst = Join-Path $PluginsPath $dll
        if (Test-Path $dst) {
            Remove-Item $dst -Force
            Write-Host "[SYNC] removed denylisted $dll"
        }
    }
}

Write-Host "[SYNC] summary copied=$($copied.Count) missing=$($missing.Count) skipped=$($skipped.Count)"
if ($missing.Count -gt 0) {
    Write-Host "[SYNC] missing-list: $($missing -join ', ')"
}

Write-Host "[SYNC] note: game only loads from BepInEx\\plugins. Staging folder is not runtime-loaded."
Write-Host "[SYNC] note: GangWars/Territory DLLs are copied, but GameplayTweaks runs native pact-first Gang Ops (Pacts + Independent) by default; GangWars adapter takeover paths are fallback-only and disabled unless explicitly enabled."
