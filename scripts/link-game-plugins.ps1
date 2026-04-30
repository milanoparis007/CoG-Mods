param(
    [string]$PluginDir = 'C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\BepInEx\plugins'
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$links = @(
    @{ Name = 'AutoLevelup.dll';           Target = "$repoRoot\AutoLevelup\bin\Release\AutoLevelup.dll" }
    @{ Name = 'BossBuildings.dll';         Target = "$repoRoot\BossBuildings\bin\Release\BossBuildings.dll" }
    @{ Name = 'BossDeath.dll';             Target = "$repoRoot\BossDeath\bin\Release\BossDeath.dll" }
    @{ Name = 'CopKilling.dll';            Target = "$repoRoot\CopKilling\bin\Release\CopKilling.dll" }
    @{ Name = 'GameOptimizer.dll';         Target = "$repoRoot\GameOptimizer\bin\Release\GameOptimizer.dll" }
    @{ Name = 'GameplayTweaks.dll';        Target = "$repoRoot\GameplayTweaks\bin\Release\GameplayTweaks.dll" }
    @{ Name = 'OrgChartMod.dll';           Target = "$repoRoot\OrgChartMod\bin\Release\OrgChartMod.dll" }
    @{ Name = 'ProhibitionLauncher.dll';   Target = "$repoRoot\ClassLibrary1\bin\Release\ProhibitionLauncher.dll" }
    @{ Name = 'AICrewLevelup.dll';         Target = "$repoRoot\AICrewLevelup\bin\Release\AICrewLevelup.dll" }
    @{ Name = 'EthnicityPlacementFix.dll'; Target = "$repoRoot\EthnicityPlacementFix\bin\Release\EthnicityPlacementFix.dll" }
)

foreach ($link in $links) {
    $dest = Join-Path $PluginDir $link.Name
    if (-not (Test-Path $link.Target)) {
        Write-Host "Skipping missing target: $($link.Target)"
        continue
    }

    if (Test-Path $dest) {
        Remove-Item $dest -Force
    }

    New-Item -ItemType SymbolicLink -Path $dest -Target $link.Target | Out-Null
    Get-Item $dest | Select-Object FullName, LinkType, Target
}
