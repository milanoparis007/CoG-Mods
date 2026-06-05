param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$GameRoot = "${env:ProgramFiles(x86)}\Steam\steamapps\common\City of Gangsters"
)

$businessesSource = Join-Path $RepoRoot "Things To Have\Current After Prohibition Mod\Personal\CoG_Data\StreamingAssets\Entities\Businesses.sim"
$businessesTarget = Join-Path $GameRoot "CoG_Data\StreamingAssets\Entities\Businesses.sim"
$pluginSource = Join-Path $RepoRoot "Things To Have\Current After Prohibition Mod\Personal\BepInEx\plugins\GameplayTweaks.dll"
$pluginTarget = Join-Path $GameRoot "BepInEx\plugins\GameplayTweaks.dll"

if (!(Test-Path -LiteralPath $businessesSource)) {
    throw "Fixed Businesses.sim was not found at: $businessesSource"
}

if (!(Test-Path -LiteralPath $businessesTarget)) {
    throw "Live Businesses.sim was not found at: $businessesTarget"
}

if (!(Test-Path -LiteralPath $pluginSource)) {
    throw "Fixed GameplayTweaks.dll was not found at: $pluginSource"
}

if (!(Test-Path -LiteralPath $pluginTarget)) {
    throw "Live GameplayTweaks.dll was not found at: $pluginTarget"
}

$stamp = Get-Date -Format yyyyMMdd-HHmmss
$businessesBackup = "$businessesTarget.bak-$stamp"
$pluginBackup = "$pluginTarget.bak-$stamp"
Copy-Item -LiteralPath $businessesTarget -Destination $businessesBackup -Force
Copy-Item -LiteralPath $pluginTarget -Destination $pluginBackup -Force
Copy-Item -LiteralPath $businessesSource -Destination $businessesTarget -Force
Copy-Item -LiteralPath $pluginSource -Destination $pluginTarget -Force

Write-Host "Backed up live Businesses.sim to:"
Write-Host "  $businessesBackup"
Write-Host "Backed up live GameplayTweaks.dll to:"
Write-Host "  $pluginBackup"
Write-Host "Installed fixed Businesses.sim to:"
Write-Host "  $businessesTarget"
Write-Host "Installed fixed GameplayTweaks.dll to:"
Write-Host "  $pluginTarget"
