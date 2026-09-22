<#
.SYNOPSIS
    Repository hygiene checks: no binaries, no real operational data, versions in step.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$script:Failures = 0
function Assert($condition, $message) {
    if ($condition) { Write-Output "PASS $message" }
    else { Write-Output "FAIL $message"; $script:Failures++ }
}

# ---- no build output or binaries committed --------------------------------
$tracked = git ls-files
$binaries = $tracked | Where-Object { $_ -match '\.(exe|dll|pdb|zip|xlsx|xls|msi)$' }
Assert ($null -eq $binaries) "no binaries or workbooks are tracked$(if ($binaries) { ': ' + ($binaries -join ', ') })"

$buildDirs = $tracked | Where-Object { $_ -match '(^|/)(bin|obj|dist)/' }
Assert ($null -eq $buildDirs) 'no build output is tracked'

# ---- the icon is the one intentional binary -------------------------------
Assert (Test-Path 'src/DelayReporter/Assets/app.ico') 'the application icon is present'

# ---- no real operational data ---------------------------------------------
# The sample must be the synthetic one. Real movement sheets name real carriers and tails.
$sample = Get-Content 'samples/demo-movement-sheet.csv' -Raw
Assert ($sample -match 'ZZ10') 'the sample uses fictional flight numbers'
Assert (-not ($sample -match '\bABX\b|\bCKS\b|\bCJT\b|\bKII\b|\bCSB\b|\bDHK\b|\bGTI\b')) `
       'the sample carries no real operator codes'

# ---- versions agree --------------------------------------------------------
$project = [xml](Get-Content 'src/DelayReporter/DelayReporter.csproj' -Raw)
$release = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$manifest = [xml](Get-Content 'src/DelayReporter/app.manifest' -Raw)
$manifestVersion = $manifest.assembly.assemblyIdentity.version
Assert ($manifestVersion -eq "$release.0") "the manifest version $manifestVersion matches the project version $release"

$readme = Get-Content 'README.md' -Raw
Assert ($readme -match [regex]::Escape($release)) "the README names version $release"

$changelog = Get-Content 'CHANGELOG.md' -Raw
Assert ($changelog -match [regex]::Escape($release)) "the changelog names version $release"
Assert (Test-Path "docs/releases/v$release.md") "release notes exist for v$release"

# ---- mapping seed integrity ------------------------------------------------
$codes = Import-Csv 'src/DelayReporter/Resources/delay-codes.csv'
Assert ($codes.Count -eq 173) "the delay code seed holds 173 codes (found $($codes.Count))"
Assert (($codes | Where-Object { -not $_.code }).Count -eq 0) 'every seeded row has a code'
Assert (($codes | Where-Object { -not $_.label }).Count -eq 0) 'every seeded row has a label'
$duplicates = $codes | Group-Object code | Where-Object { $_.Count -gt 1 }
Assert ($null -eq $duplicates) 'the delay code seed has no duplicate codes'
Assert (($codes | Where-Object { $_.label -match 'Red indicates' }).Count -eq 0) `
       'no page furniture leaked into the seeded labels'

$aircraft = Import-Csv 'src/DelayReporter/Resources/aircraft-types.csv'
Assert ($aircraft.Count -ge 13) 'the aircraft seed holds the sample fleet'

# ---- line endings ----------------------------------------------------------
$crlf = git ls-files --eol | Where-Object { $_ -match 'w/crlf' -and $_ -notmatch '\.(csv|ps1)$' }
Assert ($null -eq $crlf) 'text files are stored with LF, except CSV and PowerShell'

Write-Output ''
if ($script:Failures -eq 0) { Write-Output 'REPOSITORY CHECKS PASSED' }
else { throw "$($script:Failures) check(s) failed." }
