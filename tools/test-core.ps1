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

    # ---- order, search and hand decisions ----------------------------------
    Write-Output ''
    Write-Output '== order, search and hand decisions =='
    function Row($m, $flight) { ($m.FlightsIncludingHidden | Where-Object { $_.FlightNumber -eq $flight }).SourceRowNumber }
    function Options {
        $o = New-Object DelayReporter.Core.Report.ReportOptions
        $o.Station = 'CVG'
        $o.MinimumDelayMinutes = 15
        $o.SelectDefaultMovementTypes($sheet)
        $o
    }

    $sorted = Options
    $sorted.SortColumn = [DelayReporter.Core.Report.ReportSortColumn]::ActualDelay
    $sorted.SortDescending = $true
    $byDelay = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $sorted)
    AssertEqual 'ZZ104' $byDelay.Flights[0].FlightNumber 'sorted by delay, descending, the 11:08 flight leads'
    AssertEqual $model.ReportedFlights $byDelay.ReportedFlights 'sorting changes the order, never the count'

    $hide = Options
    [void]$hide.HiddenRows.Add((Row $model 'ZZ101'))
    $hidden = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $hide)
    AssertEqual ($model.ReportedFlights - 1) $hidden.ReportedFlights 'a hidden flight leaves the report'
    AssertEqual 1 $hidden.ExcludedHidden 'and is counted as hidden by hand'
    Assert ($hidden.HiddenFlights[0].IsHidden) 'it is kept, marked hidden, for the preview'
    AssertEqual $model.ReportedFlights $hidden.FlightsIncludingHidden.Count 'the preview can still list it'
    $hiddenMetric = [DelayReporter.Core.Report.ReportSummary]::Metrics($hidden) | Where-Object { $_.Key -eq 'Hidden by hand' }
    AssertEqual '1 flight' $hiddenMetric.Value 'the summary says so on its face'

    $select = Options
    [void]$select.SelectedRows.Add((Row $model 'ZZ101'))
    $selected = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $select)
    AssertEqual 1 $selected.ReportedFlights 'report selected keeps only the selection'
    AssertEqual ($model.ReportedFlights - 1) $selected.ExcludedNotSelected 'and counts the rest as not selected'

    $search = Options
    $search.SearchText = '93A'
    $found = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $search)
    AssertEqual 2 $found.ReportedFlights 'searching 93A finds the two flights carrying it'
    AssertEqual ($model.ReportedFlights - 2) $found.ExcludedBySearch 'the others are counted as not matching'
    $search.SearchText = 'rotation xxd'
    AssertEqual 'ZZ104' ([DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $search)).Flights[0].FlightNumber `
        'every word must match, across the reason and the route'
    # Only the coded/actual mismatch's outstanding item says this.
    $search.SearchText = 'does not match'
    AssertEqual 'ZZ108' ([DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $search)).Flights[0].FlightNumber `
        'what is outstanding is searched too'

    $mx = Options
    $mx.MxFilter = [DelayReporter.Core.Report.MxFilter]::MxOnly
    $mxOnly = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $mx)
    AssertEqual $model.FlightsWithMxDelay $mxOnly.ReportedFlights 'MX only keeps exactly the MX flights'
    Assert (($mxOnly.Flights | Where-Object { -not $_.IsMxDelay }).Count -eq 0) 'and nothing else'
    AssertEqual ($model.ReportedFlights - $model.FlightsWithMxDelay) $mxOnly.ExcludedByMx 'the rest are counted as left out by the MX filter'
    [void]$mx.MxOverrides.Add((Row $model 'ZZ101'), $true)
    AssertEqual ($model.FlightsWithMxDelay + 1) ([DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $mx)).ReportedFlights `
        'a flight ticked MX by hand counts as MX'
    $mx.MxFilter = [DelayReporter.Core.Report.MxFilter]::NotMx
    $mx.MxOverrides.Clear()
    AssertEqual ($model.ReportedFlights - $model.FlightsWithMxDelay) ([DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $mx)).ReportedFlights `
        'not MX keeps the rest'
    Assert (-not ($mxOnly.Warnings -match 'category MX')) 'the seeded list marks MX codes, so no warning'

    # A delay-codes.csv from before the category column marks nothing MX.
    $saved = @{}
    foreach ($e in $store.DelayCodes.Entries) { $saved[$e.Code] = $e.Category; $e.Category = '' }
    $bare = Options
    $bare.MxFilter = [DelayReporter.Core.Report.MxFilter]::MxOnly
    $noCategory = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $bare)
    Assert (@($noCategory.Warnings | Where-Object { $_ -match 'category MX' }).Count -eq 1) 'an MX filter over a list with no MX codes says why it is empty'
    foreach ($e in $store.DelayCodes.Entries) { $e.Category = $saved[$e.Code] }

    $mxSpec = [DelayReporter.Core.Report.ReportWriter]::BuildSheet($mxOnly)
    AssertEqual ($mxOnly.ReportedFlights + $mxSpec.HeaderRow) $mxSpec.LastRow 'an MX-only workbook holds one row per MX flight'

    $dates = Options
    $dates.DateFormat = 'yyyy-MM-dd'
    AssertEqual '2026-09-14' ([DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $dates)).Flights[0].DateText `
        'the Date column can be reformatted'
    AssertEqual '14.09.2026' $model.Flights[0].DateText 'and prints as in the file by default'

    $mismatch = $model.Flights | Where-Object { $_.Reconciles -eq $false } | Select-Object -First 1
    # The not-equal sign by code point: Windows PowerShell reads this file as ANSI.
    AssertEqual ('0:32 ' + [char]0x2260 + ' 0:25') $mismatch.DelayDisplay 'a coded/actual mismatch shows both figures'
    AssertEqual '0:42' $model.Flights[0].DelayDisplay 'an agreeing flight shows one figure'

    # ---- the email draft ---------------------------------------------------
    Write-Output ''
    Write-Output '== the email draft =='
    $attachment = New-Object DelayReporter.Core.Email.EmailAttachment('report.xlsx',
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        [DelayReporter.Core.Report.ReportWriter]::ToBytes($hidden))
    $draft = [DelayReporter.Core.Email.DelayEmail]::Compose($hidden, 'ops@example.com; not an address', '', $attachment)
    Assert ($draft.Subject -match '^CVG departure delays') "the subject names the station: $($draft.Subject)"
    Assert ($draft.Html -match 'ZZ104') 'the table lists the reported flights'
    Assert (-not ($draft.Html -match 'ZZ101')) 'but not the hidden one'
    Assert ($draft.Html -match '1 flight hidden by hand is not listed') 'and says a flight was hidden'
    Assert ($draft.PlainText -match 'Outstanding: record ULD ID') 'the plain text carries what is outstanding'
    $full = [DelayReporter.Core.Email.DelayEmail]::Compose($model, '', '', $null)
    Assert ($model.FlightsWithMxDelay -gt 0 -and -not ($full.Html -match '>MX</span>') -and -not ($full.PlainText -match '\(MX\)')) `
        'MX flights are not tagged beside their flight number'
    $notes = $null
    $eml = [Text.Encoding]::ASCII.GetString([DelayReporter.Core.Email.EmlDraftWriter]::ToBytes($draft, [ref]$notes))
    Assert ($eml.StartsWith('X-Unsent: 1')) 'the draft opens unsent in the mail app'
    Assert ($eml -match 'To: <ops@example.com>') 'a plain address is kept'
    AssertEqual 1 $notes.Count 'an address that is not one is left out with a note'
    Assert ($eml -match 'multipart/mixed' -and $eml -match 'filename="report.xlsx"') 'the workbook is attached'

    # ---- the workbook ------------------------------------------------------
    Write-Output ''
    Write-Output '== the workbook =='
    AssertEqual 'Grouped' $model.Options.Layout.ToString() 'the grouped layout is the default'
    $spec = [DelayReporter.Core.Report.ReportWriter]::BuildSheet($model)
    AssertEqual 14 $spec.Columns.Count 'fourteen columns'
    Assert ($spec.HeaderRow -gt 1) "the table header sits below the summary (row $($spec.HeaderRow))"
    AssertEqual ($model.ReportedFlights + $spec.HeaderRow) $spec.LastRow 'one row per flight'
    $firstFlight = $spec.HeaderRow + 1
    Assert ($spec.RowHeights[$firstFlight] -ge 36) 'grouped rows are tall enough to write on'
    AssertEqual 1 $spec.ConditionalRules.Count 'a single rule groups the flights'
    AssertEqual "MOD(SUBTOTAL(103,`$B`$$($firstFlight):`$B$($firstFlight)),5)=0" $spec.ConditionalRules[0].Formula `
        'every fifth visible flight closes a group'
    AssertEqual "A$($firstFlight):N$($spec.LastRow)" $spec.ConditionalRules[0].Range 'over the flights only'

    $notesText = @(foreach ($r in $firstFlight..$spec.LastRow) { $spec.Rows[$r][13].Text }) -join ''
    AssertEqual '' $notesText 'the Notes cells are left empty to write in'
    AssertEqual $model.FlightsRequiringSupplementary $spec.InputPrompts.Count 'each flight owing information gets a hint instead'
    Assert (@($spec.InputPrompts | Where-Object { $_.Text -match 'record ULD ID' }).Count -ge 1) 'and the hint says what to record'

    function WidthOf($s, $name) { ($s.Columns | Where-Object { $_.Header -eq $name }).Width }
    function TotalWidth($s) { ($s.Columns | Measure-Object -Property Width -Sum).Sum }
    $narrow = $options.Clone()
    $narrow.AircraftFormat = [DelayReporter.Core.Report.AircraftLabelFormat]::Family
    $narrowSpec = [DelayReporter.Core.Report.ReportWriter]::BuildSheet(
        [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $narrow))
    Assert ((WidthOf $narrowSpec 'Aircraft') -lt (WidthOf $spec 'Aircraft')) `
        "the Aircraft column narrows with the Min label ($(WidthOf $narrowSpec 'Aircraft') < $(WidthOf $spec 'Aircraft'))"
    Assert ((WidthOf $narrowSpec 'Notes') -gt (WidthOf $spec 'Notes')) 'and Notes takes what it gives up'
    AssertEqual (TotalWidth $spec) (TotalWidth $narrowSpec) 'so the table prints at the same scale'
    Assert ((WidthOf $spec 'Aircraft') -le 16) "a full aircraft label wraps to two lines rather than widening ($(WidthOf $spec 'Aircraft'))"
    Assert ((WidthOf $spec 'OPR') -le 6) "unnamed operators keep OPR narrow ($(WidthOf $spec 'OPR'))"

    $classicOptions = $options.Clone()
    $classicOptions.Layout = [DelayReporter.Core.Report.ReportLayout]::Classic
    $classic = [DelayReporter.Core.Report.ReportBuilder]::Build($sheet, $store, $classicOptions)
    $classicSpec = [DelayReporter.Core.Report.ReportWriter]::BuildSheet($classic)
    AssertEqual 14 $classicSpec.Columns.Count 'the classic layout keeps its fourteen columns'
    AssertEqual ($classic.ReportedFlights + $classicSpec.HeaderRow) $classicSpec.LastRow 'and one row per flight'
    AssertEqual 0 $classicSpec.ConditionalRules.Count 'and no group rules'
    AssertEqual 'DEPARTURE DELAY REPORT' $classicSpec.Rows[1][0].Text 'and its own title'
    AssertEqual 'Header' $classicSpec.Rows[$classicSpec.HeaderRow][0].Style.ToString() 'and its banded header'

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
        Assert ($null -ne $xml.worksheet.conditionalFormatting) 'and its group rule'

        # Schema order: mergeCells, then conditionalFormatting, then printOptions.
        $order = @($xml.worksheet.ChildNodes | ForEach-Object { $_.LocalName })
        Assert ($order.IndexOf('mergeCells') -lt $order.IndexOf('conditionalFormatting') -and
                $order.IndexOf('conditionalFormatting') -lt $order.IndexOf('dataValidations') -and
                $order.IndexOf('dataValidations') -lt $order.IndexOf('printOptions')) `
            'merges, conditional formatting, hints and print setup come in schema order'
        $hint = @($xml.worksheet.dataValidations.dataValidation)[0]
        AssertEqual '1' $hint.showInputMessage 'a hint shows when its Notes cell is selected'
        Assert ($null -eq $hint.type) 'and restricts nothing typed there'

        # CellStyle and DifferentialStyle index the style table; a count out of step with
        # either enum means the report is silently restyled.
        $styleEntry = $zip.GetEntry('xl/styles.xml')
        $styleReader = New-Object IO.StreamReader($styleEntry.Open())
        $styles = [xml]$styleReader.ReadToEnd()
        $styleReader.Dispose()
        $cellStyles = [Enum]::GetValues([DelayReporter.Core.Spreadsheet.CellStyle]).Count
        AssertEqual $cellStyles $styles.styleSheet.cellXfs.xf.Count 'one cellXfs entry per CellStyle'
        AssertEqual ([string]$cellStyles) $styles.styleSheet.cellXfs.count 'and the declared count agrees'
        $dxfStyles = [Enum]::GetValues([DelayReporter.Core.Spreadsheet.DifferentialStyle]).Count
        AssertEqual $dxfStyles @($styles.styleSheet.dxfs.dxf).Count 'one dxf per DifferentialStyle'
        AssertEqual ([string]$styles.styleSheet.fonts.font.Count) $styles.styleSheet.fonts.count 'the font count agrees'
        AssertEqual ([string]$styles.styleSheet.borders.border.Count) $styles.styleSheet.borders.count 'the border count agrees'
        Assert ($null -ne $xml.worksheet.pageSetup) 'and a page setup'
        AssertEqual 'landscape' $xml.worksheet.pageSetup.orientation 'printed landscape'
        AssertEqual '1' $xml.worksheet.pageSetup.fitToWidth 'fitted to one page wide'
    }
    finally { $zip.Dispose() }

    # ---- reading an XLSX ---------------------------------------------------
    Write-Output ''
    Write-Output '== reading an XLSX =='

    # An exporter that streams its output cannot seek back to fill in an entry's size, so
    # it writes the size in a trailing Zip64 data descriptor and marks the local header as
    # needing version 4.5. Real movement sheets arrive written this way; Excel opens them
    # and System.IO.Packaging does not, which is why the reader works over a plain zip.
    Add-Type -TypeDefinition @'
using System;
using System.IO;
public sealed class ForwardOnlyStream : Stream
{
    private readonly Stream _inner;
    private long _written;
    public ForwardOnlyStream(Stream inner) { _inner = inner; }
    public override bool CanRead  { get { return false; } }
    public override bool CanSeek  { get { return false; } }
    public override bool CanWrite { get { return true; } }
    public override long Length { get { return _written; } }
    public override long Position { get { return _written; } set { throw new NotSupportedException(); } }
    public override void Flush() { _inner.Flush(); }
    public override int Read(byte[] b, int o, int c) { throw new NotSupportedException(); }
    public override long Seek(long o, SeekOrigin r) { throw new NotSupportedException(); }
    public override void SetLength(long v) { throw new NotSupportedException(); }
    public override void Write(byte[] b, int o, int c) { _inner.Write(b, o, c); _written += c; }
}
'@
    Add-Type -AssemblyName System.IO.Compression

    $mainNs = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    $relNs = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
    $pkgNs = 'http://schemas.openxmlformats.org/package/2006/relationships'

    # sharedStrings carries no indentation, the way Excel itself writes the part, and the
    # worksheet is reached through a relative relationship target.
    $parts = [ordered]@{
        '[Content_Types].xml' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><Types xmlns=`"http://schemas.openxmlformats.org/package/2006/content-types`"><Default Extension=`"rels`" ContentType=`"application/vnd.openxmlformats-package.relationships+xml`"/><Default Extension=`"xml`" ContentType=`"application/xml`"/></Types>"
        '_rels/.rels' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><Relationships xmlns=`"$pkgNs`"><Relationship Id=`"rId1`" Type=`"$relNs/officeDocument`" Target=`"xl/workbook.xml`"/></Relationships>"
        'xl/workbook.xml' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><workbook xmlns=`"$mainNs`" xmlns:r=`"$relNs`"><sheets><sheet name=`"Movements`" sheetId=`"1`" r:id=`"rId1`"/></sheets></workbook>"
        'xl/_rels/workbook.xml.rels' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><Relationships xmlns=`"$pkgNs`"><Relationship Id=`"rId1`" Type=`"$relNs/worksheet`" Target=`"worksheets/sheet1.xml`"/><Relationship Id=`"rId2`" Type=`"$relNs/sharedStrings`" Target=`"sharedStrings.xml`"/></Relationships>"
        'xl/sharedStrings.xml' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><sst xmlns=`"$mainNs`" count=`"2`" uniqueCount=`"2`"><si><t>ALPHA</t></si><si><t>BETA</t></si></sst>"
        'xl/worksheets/sheet1.xml' =
            "<?xml version=`"1.0`" encoding=`"UTF-8`"?><worksheet xmlns=`"$mainNs`"><sheetData><row r=`"1`"><c r=`"A1`" t=`"inlineStr`"><is><t>INLINE</t></is></c><c r=`"B1`" t=`"s`"><v>0</v></c><c r=`"C1`" t=`"s`"><v>1</v></c></row></sheetData></worksheet>"
    }

    $xlsxPath = Join-Path $temp 'streamed.xlsx'
    $file = [IO.File]::Create($xlsxPath)
    try {
        $forward = New-Object ForwardOnlyStream($file)
        $archive = New-Object IO.Compression.ZipArchive($forward, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($name in $parts.Keys) {
                $entryStream = $archive.CreateEntry($name).Open()
                $bytes = [Text.Encoding]::UTF8.GetBytes($parts[$name])
                $entryStream.Write($bytes, 0, $bytes.Length)
                $entryStream.Dispose()
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $file.Dispose() }

    # ZipArchive streams the entries but still stamps them version 2.0, so the local
    # headers are raised to 4.5 by hand to match what the real exporter writes.
    $raw = [IO.File]::ReadAllBytes($xlsxPath)
    $eocd = -1
    for ($i = $raw.Length - 22; $i -ge 0; $i--) {
        if ($raw[$i] -eq 0x50 -and $raw[$i + 1] -eq 0x4B -and
            $raw[$i + 2] -eq 0x05 -and $raw[$i + 3] -eq 0x06) { $eocd = $i; break }
    }
    $entryCount = [BitConverter]::ToUInt16($raw, $eocd + 10)
    $directory = [BitConverter]::ToUInt32($raw, $eocd + 16)
    for ($e = 0; $e -lt $entryCount; $e++) {
        $nameLength = [BitConverter]::ToUInt16($raw, $directory + 28)
        $extraLength = [BitConverter]::ToUInt16($raw, $directory + 30)
        $commentLength = [BitConverter]::ToUInt16($raw, $directory + 32)
        $localHeader = [BitConverter]::ToUInt32($raw, $directory + 42)
        [Array]::Copy([BitConverter]::GetBytes([uint16]45), 0, $raw, $localHeader + 4, 2)
        $directory += 46 + $nameLength + $extraLength + $commentLength
    }
    [IO.File]::WriteAllBytes($xlsxPath, $raw)

    # Bit 3 of the general purpose flag is what says "the size follows the data".
    $flags = [BitConverter]::ToUInt16([IO.File]::ReadAllBytes($xlsxPath), 6)
    Assert (($flags -band 0x8) -ne 0) 'the fixture is written with data descriptors'
    Assert ([BitConverter]::ToUInt16([IO.File]::ReadAllBytes($xlsxPath), 4) -eq 45) 'and marked as needing Zip64'

    $grid = [DelayReporter.Core.Spreadsheet.XlsxReader]::ReadFile($xlsxPath)
    AssertEqual 'INLINE' ($grid.Cell(0, 0)) 'an inline string is read'
    AssertEqual 'ALPHA' ($grid.Cell(0, 1)) 'a shared string is read from an unindented part'
    AssertEqual 'BETA' ($grid.Cell(0, 2)) 'and so is the one after it'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output ''
if ($script:Failures -eq 0) { Write-Output 'ALL CHECKS PASSED' }
else { throw "$($script:Failures) check(s) failed." }
