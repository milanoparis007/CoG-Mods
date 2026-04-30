param(
    [string]$Path = "C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\StreamingAssets\UI\ConvoGangs.sim"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "ConvoGangs.sim not found at '$Path'."
}

$targetPrefixes = @(
    'gang-robbery-',
    'gang-trade-drugs-',
    'gang-trade2-drugs-',
    'gang-trade-liquor-',
    'gang-trade2-liquor-',
    'gang-trade3-liquor-',
    'gang-trade-loan-'
)

$targetStates = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$targetStates.Add('gang-money-in') | Out-Null
$targetStates.Add('gang-trade2-cigs-low') | Out-Null

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$raw = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path, $utf8NoBom)

$firstLowbIndex = $raw.IndexOf("gang-trade-liquor-lowb {", [System.StringComparison]::Ordinal)
if ($firstLowbIndex -ge 0) {
    $raw = $raw.Remove($firstLowbIndex, "gang-trade-liquor-lowb".Length).Insert($firstLowbIndex, "gang-trade-liquor-lowb2")
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.AddRange([string[]]($raw -split "`r?`n"))

$result = New-Object System.Collections.Generic.List[string]
$currentState = $null
$insideTargetState = $false
$insideGrantButton = $false
$insideGoodbyeButton = $false

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $trimmed = $line.Trim()

    if ($trimmed -match '^([A-Za-z0-9\-]+)\s*\{$') {
        $currentState = $matches[1]
        $insideTargetState = $targetStates.Contains($currentState)
        if (-not $insideTargetState) {
            foreach ($prefix in $targetPrefixes) {
                if ($currentState.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $insideTargetState = $true
                    break
                }
            }
        }
        $insideGrantButton = $false
        $insideGoodbyeButton = $false
    }

    $result.Add($line) | Out-Null

    if (-not $insideTargetState) {
        continue
    }

    if ($trimmed -like '{*;; goodbye*') {
        $insideGoodbyeButton = $true
    }

    if ($trimmed -match '^\s*grants\s*\[') {
        $insideGrantButton = $true
    }

    if ($insideGrantButton -and $trimmed.EndsWith('}]')) {
        $nextLine = if ($i + 1 -lt $lines.Count) { $lines[$i + 1].Trim() } else { '' }
        if ($nextLine -notmatch '^next\s*\{') {
            $result.Add("		next 	{ goto gang-toplevel }") | Out-Null
        }
        $insideGrantButton = $false
        continue
    }

    if ($insideGoodbyeButton -and $trimmed -match '^onClick\s+Goodbye$') {
        $nextLine = if ($i + 1 -lt $lines.Count) { $lines[$i + 1].Trim() } else { '' }
        if ($nextLine -notmatch '^next\s*\{') {
            $result.Add("		next 	{ goto gang-toplevel }") | Out-Null
        }
        continue
    }

    if ($insideGoodbyeButton -and $trimmed -eq '}') {
        $insideGoodbyeButton = $false
    }
}

$newRaw = [string]::Join("`n", $result)

if ($newRaw -eq $raw) {
    Write-Host "No changes needed."
    exit 0
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = "$Path.codex-backup-$timestamp"
Copy-Item -LiteralPath $Path -Destination $backupPath -Force
[System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $Path).Path, $newRaw, $utf8NoBom)

Write-Host "Patched: $Path"
Write-Host "Backup:  $backupPath"
