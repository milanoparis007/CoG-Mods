param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string[]]$TargetProjects
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$projects = @(
    @{ Name = "ModLauncher"; Path = "ClassLibrary1/ModLauncher.csproj"; RetryCs2012 = $false },
    @{ Name = "GameplayTweaks"; Path = "GameplayTweaks/GameplayTweaks.csproj"; RetryCs2012 = $false },
    @{ Name = "CopKilling"; Path = "CopKilling/CopKilling.csproj"; RetryCs2012 = $false },
    @{ Name = "GameOptimizer"; Path = "GameOptimizer/GameOptimizer.csproj"; RetryCs2012 = $true },
    @{ Name = "BossBuildings"; Path = "BossBuildings/BossBuildings.csproj"; RetryCs2012 = $false },
    @{ Name = "BossDeath"; Path = "BossDeath/BossDeath.csproj"; RetryCs2012 = $false },
    @{ Name = "OrgChartMod"; Path = "OrgChartMod/OrgChartMod.csproj"; RetryCs2012 = $false },
    @{ Name = "AutoLevelup"; Path = "AutoLevelup/AutoLevelup.csproj"; RetryCs2012 = $false }
)

function Test-IsOutputLockFailure {
    param(
        [string]$Output
    )

    if ([string]::IsNullOrWhiteSpace($Output)) {
        return $false
    }

    return $Output.Contains("CS2012") `
        -or $Output.Contains("MSB3021") `
        -or $Output.Contains("MSB3027") `
        -or $Output.Contains("user-mapped section open") `
        -or $Output.Contains("requested operation cannot be performed on a file with a user-mapped section open")
}

function Invoke-DotnetProjectBuild {
    param(
        [string]$ProjectFile,
        [string[]]$AdditionalArgs = @()
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $output = (& dotnet build $ProjectFile -c $Configuration @AdditionalArgs 2>&1 | Out-String)
    $exitCode = $LASTEXITCODE
    $sw.Stop()

    return @{
        Success = ($exitCode -eq 0)
        Output = $output
        Milliseconds = $sw.ElapsedMilliseconds
    }
}

function Ensure-ReleaseOutputJunction {
    param(
        [string]$Name,
        [string]$ProjectDir,
        [switch]$Rotate
    )

    if ($Configuration -ne "Release") {
        return $null
    }

    $binDir = Join-Path $ProjectDir "bin"
    $releaseDir = Join-Path $binDir "Release"
    $aliasRoot = Join-Path $repoRoot ".codex_build"
    $targetDir = if ($Rotate) {
        Join-Path $aliasRoot ("{0}_{1}" -f $Name, (Get-Date -Format "yyyyMMdd-HHmmss"))
    }
    else {
        Join-Path $aliasRoot $Name
    }

    New-Item -ItemType Directory -Path $aliasRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    if (Test-Path $releaseDir) {
        $releaseItem = Get-Item -LiteralPath $releaseDir -Force
        $isReparsePoint = ($releaseItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        if ($isReparsePoint) {
            $currentTarget = @($releaseItem.Target)[0]
            $resolvedCurrentTarget = if ([string]::IsNullOrWhiteSpace($currentTarget)) {
                $null
            }
            else {
                [System.IO.Path]::GetFullPath($currentTarget)
            }
            $resolvedDesiredTarget = [System.IO.Path]::GetFullPath($targetDir)
            if ($resolvedCurrentTarget -eq $resolvedDesiredTarget) {
                return $targetDir
            }

            [System.IO.Directory]::Delete($releaseDir)
        }
        else {
            $backupDir = Join-Path $binDir "Release.direct-output"
            if (Test-Path $backupDir) {
                $backupDir = Join-Path $binDir ("Release.direct-output.{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
            }
            Move-Item -LiteralPath $releaseDir -Destination $backupDir -Force
        }
    }

    if (-not (Test-Path $releaseDir)) {
        New-Item -ItemType Junction -Path $releaseDir -Target $targetDir | Out-Null
    }

    return $targetDir
}

function Invoke-ProjectBuild {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [bool]$RetryCs2012
    )

    $fullPath = Join-Path $repoRoot $ProjectPath
    $projectDir = Split-Path -Parent $fullPath
    $projectFile = Split-Path -Leaf $fullPath

    Write-Host "[PERF][Build] start project=$Name cfg=$Configuration"
    Push-Location $projectDir
    try {
        $releaseTarget = Ensure-ReleaseOutputJunction -Name $Name -ProjectDir $projectDir
        if ($releaseTarget) {
            Write-Host "[PERF][Build] release-output project=$Name target=$releaseTarget"
        }

        $buildResult = Invoke-DotnetProjectBuild -ProjectFile $projectFile
        if ($buildResult.Success) {
            Write-Host "[PERF][Build] ok project=$Name ms=$($buildResult.Milliseconds)"
            return @{ Success = $true; Output = $buildResult.Output; Retried = $false }
        }

        $output = $buildResult.Output
        if ($Configuration -eq "Release" -and (Test-IsOutputLockFailure -Output $output)) {
            $rotatedTarget = Ensure-ReleaseOutputJunction -Name $Name -ProjectDir $projectDir -Rotate
            Write-Warning "[PERF][Build] lock retry project=$Name mode=release-rotate target=$rotatedTarget"
            $retryResult = Invoke-DotnetProjectBuild -ProjectFile $projectFile
            if ($retryResult.Success) {
                Write-Host "[PERF][Build] ok project=$Name ms=$($retryResult.Milliseconds) mode=release-rotate"
                return @{ Success = $true; Output = $retryResult.Output; Retried = $true }
            }

            $output = $output + "`n--- RELEASE ROTATE RETRY ---`n" + $retryResult.Output
        }

        if ($RetryCs2012 -and $output.Contains("CS2012")) {
            Write-Warning "[PERF][Build] lock retry project=$Name reason=CS2012"
            $retryResult = Invoke-DotnetProjectBuild -ProjectFile $projectFile -AdditionalArgs @("/p:BaseIntermediateOutputPath=obj_retry\", "/p:IntermediateOutputPath=obj_retry\$Configuration\")
            if ($retryResult.Success) {
                Write-Host "[PERF][Build] ok project=$Name ms=$($retryResult.Milliseconds) mode=retry"
                return @{ Success = $true; Output = $retryResult.Output; Retried = $true }
            }
            return @{ Success = $false; Output = $output + "`n--- RETRY ---`n" + $retryResult.Output; Retried = $true }
        }

        return @{ Success = $false; Output = $output; Retried = $false }
    }
    finally {
        Pop-Location
    }
}

$selectedProjects = $projects
if ($TargetProjects -and $TargetProjects.Count -gt 0) {
    $requestedNames = @($TargetProjects | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
    $selectedProjects = @($projects | Where-Object { $requestedNames -contains $_.Name })
    $foundNames = @($selectedProjects | ForEach-Object { $_.Name })
    $missingNames = @($requestedNames | Where-Object { $_ -notin $foundNames })
    if ($missingNames.Count -gt 0) {
        throw "Unknown project name(s): $($missingNames -join ', ')"
    }
}

$failed = @()
foreach ($proj in $selectedProjects) {
    $result = Invoke-ProjectBuild -Name $proj.Name -ProjectPath $proj.Path -RetryCs2012 $proj.RetryCs2012
    if (-not $result.Success) {
        $failed += [PSCustomObject]@{
            Name = $proj.Name
            Path = $proj.Path
            Retried = $result.Retried
            Output = $result.Output
        }
    }
}

if ($failed.Count -gt 0) {
    Write-Host "[PERF][Build] failed count=$($failed.Count)"
    foreach ($f in $failed) {
        Write-Host "----- BUILD FAILURE: $($f.Name) ($($f.Path)) retried=$($f.Retried) -----"
        Write-Host $f.Output
    }
    exit 1
}

Write-Host "[PERF][Build] all projects succeeded cfg=$Configuration count=$($selectedProjects.Count)"
