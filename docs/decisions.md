# Design decisions

## First repository release: v0.1.0

The first publishable release starts at 0.1.0 with a single initial implementation. Real movement
sheets and the delay code source document are retained outside this repository; only synthetic
fixtures and the transcribed code table are included.

## .NET Framework 4.8, and a hand-written XLSX writer

The tool is passed around a station and run from wherever it lands. That argues for one small
executable that needs no installer, no runtime and no administrator rights — the promise both
sibling projects make. .NET Framework 4.8 is the only managed runtime that ships with Windows 10
1903+ and Windows 11, so targeting it keeps the download around a megabyte and the prerequisites at
none.

Writing XLSX then has to be done by hand, because the mature Excel libraries are not available on
that footing. ClosedXML pulls a chain of assemblies and its current releases target .NET Standard
2.0 and later, so using it on net48 means pinning an unmaintained line; merging assemblies with
ILRepack adds a fragile build step; EPPlus's licence excludes commercial use. Against roughly 1,500
lines of writer, the deciding factor was that the report has exactly one layout. The writer is a
bounded, single-purpose component, not an open-ended Excel library, and it is verified against a
reference workbook.

The reader uses System.IO.Packaging, which resolves relationships properly and was proven against
real files. The writer does not: it emits a plain zip with explicit content types and
relationships, so the output is exactly the bytes intended and can be diffed against a reference.
Using the simpler mechanism on each side is deliberate, not an oversight.

## One flight is one row

An earlier layout drew each flight as a bordered block with its codes listed beneath. It printed
well and read badly as a spreadsheet: sorting, filtering and freezing all operate on rows, and a
block spanning several rows defeats them.

So a flight is a single row, and its codes stack as lines within that row's Code, Reason and
Duration cells. The report behaves like a spreadsheet and still prints as a document.

The cost is that Excel cannot be left to wrap the text: if it reflowed the Reason cell, the codes
and durations beside it would no longer line up. The writer therefore wraps the reason itself and
pads the other two cells with blank lines, then computes the row height from the line count. That
is the reason `TextWrap` exists and why row heights are explicit.

## Positional pairing of codes and durations

The delay cell packs N codes followed by N durations. Pairing them positionally is an assumption,
so it was tested rather than trusted: across the development sample, every flight's coded durations
summed exactly to its STD-to-ATD delay, including flights crossing midnight and one whose codes
totalled 11:08. The report still shows both figures and flags disagreement, because the next file
may not be so well behaved.

## Exclusion drops events, not flights

An excluded delay code removes that event; the flight keeps its remaining codes. A flight whose
codes are all excluded leaves the report entirely. The alternative — dropping any flight touched by
an excluded code — would hide delays the station does own behind one it does not.

Excluded events are counted on the summary. An exclusion that quietly shrinks the report is a
reporting error waiting to happen; a counted one is a decision.

## Supplementary information is a prompt, not a flag

66 of the 173 published codes require the station to record supplementary information, and the
source document says what. Rather than marking those flights and leaving the reader to look the
requirement up, the report pre-fills the Notes cell with the requirement itself. The workbook
becomes a checklist of what is still owed rather than a record of what happened.

## Nothing unmapped is fatal

A delay code or aircraft type absent from the mapping files prints as written, is labelled
`(unmapped)`, and is listed on the summary. A new code appearing in a future export must not stop a
report being produced, and must not vanish from it either.

## Codes are normalised, labels are not corrected

The movement sheet zero-pads codes (`09`, `04`); the published list does not (`9`, `4`). Lookups
normalise both sides. The labels themselves are transcribed verbatim from *Global Network Delay
Codes v8.1*, including its own inconsistencies — one row's Network Domain reads `Air` where every
comparable row reads `Air & Road`, and several labels carry the source's spelling. Correcting them
in the seed would make the shipped file disagree with the document it claims to reproduce, and the
user's own copy is the right place for local corrections.

## Movement types are classified, not listed

Ground runs and tows carry no departure, no load and no delay codes. Rather than hard-coding
`T/GR` and `T/XL`, `ReportOptions.IsFlightType` tests the type's `/`-separated segments. An
unfamiliar type in a future export is therefore included by default rather than silently dropped —
the safer direction to fail in.

## The station is a setting, checked against the file

Which station a report covers is a decision, so it is a setting rather than inferred. But a wrong
setting produces an empty report, which looks like a broken tool. The reader therefore detects the
station that dominates the file and the report warns when the two disagree, naming both.

## Every row is counted, not just every reported one

`IsStationDeparture` used to reject arrivals, other-station rows and same-station ground runs/tows
with a bare `continue` — nothing on the summary recorded that they existed. On the synthetic sample,
15 rows read became 11 departures with 4 unaccounted for. A single `ExcludedByFilters` counter also
did quadruple duty for date, operator, registration and delay-code exclusions, so a thin report
could not say which filter was responsible.

Both are counting bugs, not filtering bugs: nothing about which rows survive changed. Every drop
point now increments its own counter (`ExcludedNotStationDeparture`, `ExcludedByDate`,
`ExcludedByOperator`, `ExcludedByRegistration`, `ExcludedNoCodedDelay`, `ExcludedByDelayCode`), so
`RowsRead` reconciles exactly against the printed summary at every level. This is the same
principle as "Exclusion drops events, not flights" above, applied to the filters that ran before a
flight was ever built.

## Operator codes are ICAO, not IATA

The `OPR` column carries three-letter codes (the sample's fictional `ZZA`–`ZZD` follow the same
shape `tools/check-repo.ps1` blocks for real carriers: `ABX`, `CKS`, `CJT`, `DHK`, `GTI`), not the
two-letter IATA designators. `operators.csv` is seeded accordingly, the same way `aircraft-types.csv`
is seeded with real type names against a fictional flight number — a handful of well known cargo
carriers, not a guess at any particular station's own internal codes. Unmapped operators behave
exactly like unmapped delay codes and aircraft types: printed as written, never fatal, listed on the
summary.

## One shared filter control, not five bespoke ones

Station, movement type, operator, delay code and tail number all narrow the same list of rows, and
four of them can hold more than one value at once. Rather than a free-text box parsed on every
keystroke for some and a checkbox `WrapPanel` for others, `Controls/MultiSelectDropdown` is one
control used four times, each instance fed the distinct values actually present in the loaded file
(`MovementSheet.Stations`/`Registrations`/`DelayCodes`, already-existing `MovementTypes`/`Operators`)
rather than free text a user has to get right by hand. It keeps the existing "nothing ticked and
everything ticked both mean no filter" convention exactly, so `ReportOptions` and `ReportBuilder` did
not need to change shape at all — only how the UI fills them. Station stays single-select and stays
an editable `ComboBox`, because it names one home station the whole report is measured from, not a
set of values to include.

## View settings live in ReportOptions, not beside it

Operator-label substitution, the aircraft label format and the debug summary line all change what
the preview shows and what the workbook prints, identically. CLAUDE.md's rule that "the preview and
the saved workbook cannot disagree" means these could not be a WPF-only rendering trick: they are
ordinary `ReportOptions` fields, resolved once in `ReportBuilder` and read from the same
`ReportModel` by both `MainWindow` and `ReportWriter`. Delay-code dimming/hiding for codes the
Delay codes filter did not choose is the one exception — it is deliberately screen-only, because
letting it touch the exported workbook would mean the coded-duration total could stop reconciling
with the clock delay, and an audited report silently losing lines is exactly what this project
exists to prevent. That setting is a plain WPF field, not a `ReportOptions` field, because its whole
point is to let the preview and the workbook differ.

## MX is a code category, not a hard-coded list

The report's "with MX coded delay" figure is driven by a `category` column on `delay-codes.csv`,
seeded `MX` for the codes in the 40s, rather than a fixed set of code numbers baked into the
application. This follows the same shape as `exclude` and `si_required`: a plain column the station
can extend to its own codes, never fatal when blank, and never silently overwritten once edited.
A per-flight tick can force or exclude a flight from that count for the file currently open — session
only, never saved — because the seeded classification is a starting guess, not a claim about any
one flight, and the user always has the last word on a report they are about to sign.

## The summary got long enough to need two tiers

Closing the "nothing disappears silently" gap (see above) grew the summary to nineteen line items,
which stopped being a summary and started reading as a debug log. The fix keeps every one of those
counts — dropping any of them would be the same silent gap in a different shape — but splits them
into seven that describe the report as displayed (departures, coded or not, MX, threshold, reported,
events) and everything else folded into one optional detail line, off by default. `ReportSummary` is
the one place both the workbook and the on-screen preview build this text from, so the two cannot
say different things about the same run.

## Operator codes were checked against a real file, not guessed

`operators.csv` started as eight carriers I was confident about. Checked against a real CVG
movement sheet, five of its eleven distinct codes were missing. A quick web search misidentified
one of them — it conflated AeroLogic with EAT Leipzig, two different DHL-network German cargo
carriers whose ICAO and IATA codes are easy to cross — so each match was verified independently
before being added, and the one code (`WIN`) that no source confirmed was left unmapped rather than
guessed. A wrong carrier name in an operational report is worse than an honest "unmapped" tag, so
the bar for adding a row here is confirmation, not plausibility.

## No continuous integration

Neither sibling has CI, the build is Windows-only, and a workflow that cannot build a WPF
application or open the resulting workbook would add ceremony without value. Verification is
`tools/test-core.ps1`, `tools/check-repo.ps1`, and looking at the report.
