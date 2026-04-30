param(
	[string]$RepoRoot = "C:\Users\User\source\repos\ClassLibrary1",
	[string]$PlayerLogPath = "$env:USERPROFILE\AppData\LocalLow\SomaSim\City of Gangsters\Player.log",
	[int]$TopFiles = 12,
	[int]$TopSignals = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$Title)
{
	Write-Output ""
	Write-Output "== $Title =="
}

function Invoke-Capture([scriptblock]$Block)
{
	try
	{
		return & $Block
	}
	catch
	{
		return @("[unavailable] $($_.Exception.Message)")
	}
}

$resolvedRoot = (Resolve-Path $RepoRoot).Path
$projectArgs = @(
	"GameplayTweaks",
	"GameOptimizer",
	"CopKilling",
	"BossBuildings",
	"BossDeath",
	"OrgChartMod",
	"AutoLevelup",
	"ClassLibrary1"
)

Push-Location $resolvedRoot
try
{
	Write-Section "Repo"
	Write-Output $resolvedRoot

	Write-Section "Recent commits"
	Invoke-Capture { git log --oneline -n 8 } | Write-Output

	Write-Section "HEAD changed files"
	Invoke-Capture { git show --stat --name-only --oneline HEAD } | Write-Output

	Write-Section "Method-heavy files"
	$files = @(Invoke-Capture { rg --files @projectArgs -g '*.cs' })
	$methodPattern = '^[ \t]*(public|private|protected|internal)\s+(static\s+)?([A-Za-z0-9_<>,\[\]\.?]+\s+)+[A-Za-z0-9_]+\s*\('
	$rows = foreach ($file in $files)
	{
		if (-not (Test-Path $file))
		{
			continue
		}

		$content = Get-Content -Path $file
		[PSCustomObject]@{
			File = $file
			Lines = ($content | Measure-Object -Line).Lines
			MethodLike = @($content | Select-String -Pattern $methodPattern).Count
			HarmonyRefs = @($content | Select-String -Pattern '\[Harmony|harmony\.Patch').Count
			VerifyLogs = @($content | Select-String -Pattern 'VerificationLog|Debug\.Log').Count
		}
	}

	$rows |
		Sort-Object @(
			@{ Expression = "MethodLike"; Descending = $true }
			@{ Expression = "Lines"; Descending = $true }
		) |
		Select-Object -First $TopFiles |
		Format-Table -AutoSize |
		Write-Output

	Write-Section "TODO and disabled signals"
	$signalOutput = @(Invoke-Capture {
		rg -n "TODO|FIXME|WIP|disabled by default|disabled:|fallback-only|skipped=|not implemented" GameplayTweaks GameOptimizer CopKilling docs CLAUDE.md session_summary.md -S -g '!**/bin/**' -g '!**/obj/**'
	})
	if ($signalOutput.Count -eq 0)
	{
		Write-Output "[none found]"
	}
	else
	{
		$signalOutput | Select-Object -First $TopSignals | Write-Output
	}

	Write-Section "Doc follow-up signals"
	$docOutput = @(Invoke-Capture {
		rg -n "Follow-Up Plan|Goal:|Order of work|Quick win|Pending|Plan for|Files to touch" docs session_summary.md -S
	})
	if ($docOutput.Count -eq 0)
	{
		Write-Output "[none found]"
	}
	else
	{
		$docOutput | Select-Object -First $TopSignals | Write-Output
	}

	Write-Section "Player log signals"
	Write-Output "Using log: $PlayerLogPath"
	if (-not (Test-Path $PlayerLogPath))
	{
		Write-Output "Player.log not found"
	}
	else
	{
		$logOutput = @(Invoke-Capture {
			rg -n "\[Warning|\[Error|AccessTools.TypeByName|failed|disabled|skipped=no-eligible-gangs|Microsoft\.CSharp|Outpost disappeared|BossRoleSetter|Multi-crew vehicle|CopWarSystem initialized|gangOpsMode|activeSubsystems" $PlayerLogPath -S
		})

		if ($logOutput.Count -eq 0)
		{
			Write-Output "[no matching log signals]"
		}
		else
		{
			$logOutput | Select-Object -First $TopSignals | Write-Output
		}
	}
}
finally
{
	Pop-Location
}
