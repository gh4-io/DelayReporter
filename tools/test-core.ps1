<#
.SYNOPSIS
    Exercises Delay Reporter's core against the synthetic sample.

.DESCRIPTION
    Loads the built executable and drives the parsing, mapping, report and workbook code
    directly. No user settings or mapping files are touched: the mapping tables are loaded
    from a temporary folder seeded from the repository's own copies.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
#>
param(
    [string]$Exe = 'src/DelayReporter/bin/Release/net48/DelayReporter.exe',
    [string]$Sample = 'samples/demo-movement-sheet.csv'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$script:Failures = 0

function Assert($condition, $message) {
    if ($condition) { Write-Output "PASS $message" }
    else { Write-Output "FAIL $message"; $script:Failures++ }
}

function AssertEqual($expected, $actual, $message) {
    if ($expected -eq $actual) { Write-Output "PASS $message" }
    else { Write-Output "FAIL $message (expected '$expected', got '$actual')"; $script:Failures++ }
}

$exePath = (Resolve-Path -LiteralPath $Exe).Path
[void][Reflection.Assembly]::LoadFrom($exePath)
Write-Output "Loaded $exePath"
Write-Output ''

# ---- delay column ---------------------------------------------------------
Write-Output '== delay code parser =='
$parsed = [DelayReporter.Core.Movement.DelayCodeParser]::Parse('93A/09/28A/00:17/00:15/00:10')
AssertEqual 3 $parsed.Events.Count 'three codes pair with three durations'
AssertEqual '93A' $parsed.Events[0].Code 'the first code'
AssertEqual 17 $parsed.Events[0].Minutes 'the first duration'
AssertEqual 10 $parsed.Events[2].Minutes 'the last duration'
AssertEqual 42 $parsed.TotalMinutes 'the coded total is 0:42'
Assert (-not $parsed.HasWarning) 'a well formed value produces no warning'

$mismatch = [DelayReporter.Core.Movement.DelayCodeParser]::Parse('36A/41A/00:07')
Assert $mismatch.HasWarning 'a code without a duration warns'
AssertEqual 2 $mismatch.Events.Count 'the unpaired code is still reported'
AssertEqual 0 $mismatch.Events[1].Minutes 'and carries zero minutes'

AssertEqual 0 ([DelayReporter.Core.Movement.DelayCodeParser]::Parse('').Events.Count) 'an empty cell yields no events'

# ---- clock ----------------------------------------------------------------
Write-Output ''
Write-Output '== clock arithmetic =='
$std = New-Object DelayReporter.Core.Movement.ClockTime
$atd = New-Object DelayReporter.Core.Movement.ClockTime
Assert ([DelayReporter.Core.Movement.ClockTime]::TryParse('18:50', [ref]$std)) 'STD parses'
Assert ([DelayReporter.Core.Movement.ClockTime]::TryParse('19:32 A', [ref]$atd)) 'ATD parses with its actual flag'
Assert $atd.IsActual 'the trailing A marks the time actual'
AssertEqual 42 ([DelayReporter.Core.Movement.ClockTime]::DelayMinutesBetween($std, $atd)) 'a 0:42 delay'

[void][DelayReporter.Core.Movement.ClockTime]::TryParse('20:55', [ref]$std)
[void][DelayReporter.Core.Movement.ClockTime]::TryParse('08:03 A', [ref]$atd)
AssertEqual 668 ([DelayReporter.Core.Movement.ClockTime]::DelayMinutesBetween($std, $atd)) 'a departure past midnight is 11:08, never negative'

# ---- code normalization ---------------------------------------------------
Write-Output ''
Write-Output '== code normalization =='
AssertEqual '9' ([DelayReporter.Core.Mapping.MappingTable]::Normalize('09')) "the sheet's 09 is the list's 9"
AssertEqual '4' ([DelayReporter.Core.Mapping.MappingTable]::Normalize('04')) "the sheet's 04 is the list's 4"
AssertEqual '93A' ([DelayReporter.Core.Mapping.MappingTable]::Normalize('93a')) 'letters upper case, suffix kept'

# ---- mappings, in an isolated folder --------------------------------------
Write-Output ''
Write-Output '== mappings =='
$temp = Join-Path ([IO.Path]::GetTempPath()) ("DelayReporterTest-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    Copy-Item 'src/DelayReporter/Resources/delay-codes.csv' $temp
    Copy-Item 'src/DelayReporter/Resources/aircraft-types.csv' $temp

    $store = New-Object DelayReporter.Core.Mapping.MappingStore($temp)
    $store.Load()
    AssertEqual 173 $store.DelayCodes.Count 'all 173 delay codes load'
    Assert ($store.AircraftTypes.Count -ge 13) 'the aircraft types load'
    AssertEqual 'Aircraft rotation' $store.DelayCodes.Label('93A') '93A resolves'
    AssertEqual 'Ground time less than declared minimum' $store.DelayCodes.Label('09') 'zero padded 09 resolves'
    AssertEqual 'record ULD ID' $store.DelayCodes.Find('2').SupplementaryRemark "code 2's supplementary remark"

    # ---- reading the sample ------------------------------------------------
    Write-Output ''
    Write-Output '== reading the synthetic sample =='
    $sheet = [DelayReporter.Core.Movement.MovementReader]::ReadFile((Resolve-Path -LiteralPath $Sample).Path)
    AssertEqual 4 $sheet.HeaderRowNumber 'the header row is found at row 4 by name'
    AssertEqual 'CVG' $sheet.DetectedStation 'the station is detected from the file'
    AssertEqual 15 $sheet.Rows.Count 'movement rows are read, the totals footer is not one'
    Assert ($sheet.PeriodText.Length -gt 0) "the period line is read: $($sheet.PeriodText)"

    $options = New-Object DelayReporter.Core.Report.ReportOptions
    $options.Station = 'CVG'
    $options.MinimumDelayMinutes = 15
    $options.SelectDefaultMovementTypes($sheet)
    Assert (-not $options.MovementTypes.Contains('T/GR')) 'ground runs are not selected by default'
    Assert ($options.MovementTypes.Contains('CL')) 'flights are selected by default'

    $model = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $options)

    Write-Output ''
    Write-Output '== report figures =='
    AssertEqual 11 $model.StationDepartures 'CVG departures, excluding arrivals and other stations'
    AssertEqual 10 $model.FlightsWithCodedDelay 'flights carrying a coded delay'
    AssertEqual 8 $model.ReportedFlights 'flights reported at a 15 minute threshold'
    AssertEqual 14 $model.ReportedEvents 'delay events reported'
    AssertEqual 2 $model.ExcludedByThreshold 'flights below the threshold'
    AssertEqual 1 $model.ReconciliationMismatches 'the seeded coded/actual mismatch is flagged'
    AssertEqual 5 $model.FlightsRequiringSupplementary 'flights owing supplementary information'
    AssertEqual '25:18' $model.TotalCodedDelayText 'total coded delay'
    AssertEqual '999' (($model.UnmappedCodes | ForEach-Object { $_.Code }) -join ',') 'the unmapped code is reported, not fatal'
    AssertEqual '99Z' (($model.UnmappedAircraft) -join ',') 'the unmapped aircraft code is reported, not fatal'

    $first = $model.Flights[0]
    AssertEqual 'ZZ101' $first.FlightNumber 'flights are ordered by date then scheduled time'
    AssertEqual '0:42' $first.ActualDelayText 'its clock delay'
    AssertEqual '0:42' $first.CodedDelayText 'its coded delay'
    Assert ($first.Reconciles -eq $true) 'it reconciles'

    $rollover = $model.Flights | Where-Object { $_.FlightNumber -eq 'ZZ104' }
    AssertEqual '11:08' $rollover.ActualDelayText 'a departure past midnight measures 11:08'
    Assert ($rollover.Reconciles -eq $true) 'and reconciles with its codes'

    # ---- exclusion ---------------------------------------------------------
    Write-Output ''
    Write-Output '== the mapper exclusion column =='
    $entry = $store.DelayCodes.Find('93A')
    $entry.Exclude = $true
    $excludeOptions = New-Object DelayReporter.Core.Report.ReportOptions
    $excludeOptions.Station = 'CVG'
    $excludeOptions.MinimumDelayMinutes = 0
    $excludeOptions.SelectDefaultMovementTypes($sheet)
    $excluded = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $excludeOptions)
    Assert ($excluded.ExcludedEvents -gt 0) "excluding 93A drops its events ($($excluded.ExcludedEvents))"
    $leaked = $excluded.Flights | ForEach-Object { $_.Events } | Where-Object { $_.Code -eq '93A' }
    Assert ($null -eq $leaked) 'no excluded code reaches the report'
    Assert ($excluded.Flights.Count -gt 0) 'flights keep their remaining codes'
    $entry.Exclude = $false

    # ---- the workbook ------------------------------------------------------
    Write-Output ''
    Write-Output '== the workbook =='
    $spec = [DelayReporter.Core.Report.ReportWriter]::BuildSheet($model)
    AssertEqual 14 $spec.Columns.Count 'fourteen columns'
    Assert ($spec.HeaderRow -gt 1) "the table header sits below the summary (row $($spec.HeaderRow))"
    AssertEqual ($model.ReportedFlights + $spec.HeaderRow) $spec.LastRow 'one row per flight'

    $outputPath = Join-Path $temp 'report.xlsx'
    [DelayReporter.Core.Report.ReportWriter]::Write($outputPath, $model)
    Assert (Test-Path $outputPath) 'the workbook is written'

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($outputPath)
    try {
        $names = $zip.Entries | ForEach-Object { $_.FullName }
        foreach ($part in @('[Content_Types].xml', '_rels/.rels', 'xl/workbook.xml',
                            'xl/_rels/workbook.xml.rels', 'xl/styles.xml', 'xl/worksheets/sheet1.xml')) {
            Assert ($names -contains $part) "the package contains $part"
        }

        $entry = $zip.GetEntry('xl/worksheets/sheet1.xml')
        $reader = New-Object IO.StreamReader($entry.Open())
        $xml = [xml]$reader.ReadToEnd()
        $reader.Dispose()
        Assert ($null -ne $xml.worksheet.autoFilter) 'the sheet carries an autofilter'
        Assert ($null -ne $xml.worksheet.pageSetup) 'and a page setup'
        AssertEqual 'landscape' $xml.worksheet.pageSetup.orientation 'printed landscape'
        AssertEqual '1' $xml.worksheet.pageSetup.fitToWidth 'fitted to one page wide'
    }
    finally { $zip.Dispose() }
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output ''
if ($script:Failures -eq 0) { Write-Output 'ALL CHECKS PASSED' }
else { throw "$($script:Failures) check(s) failed." }
