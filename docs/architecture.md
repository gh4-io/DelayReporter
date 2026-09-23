# Architecture

Delay Reporter is a single WPF executable targeting .NET Framework 4.8, built like its siblings
ICS Scrubber and OFT Scrubber: SDK-style project, no solution file, no runtime packages.

## Pipeline

```
 movement sheet (.xlsx / .csv)
        │
   Spreadsheet.XlsxReader ─┐
   Spreadsheet.CsvReader ──┴─▶ CellGrid       cells as text, header found by name
        │
   Movement.MovementReader ──▶ MovementSheet  period, rows, detected station
        │                            │
        │      Movement.DelayCodeParser ──▶ DelayEvent[]   code + duration pairs
        │
   Mapping.MappingStore (%APPDATA%) ──▶ DelayCodeMap, AircraftMap
        │
   Report.ReportBuilder(sheet, mappings, options) ──▶ ReportModel
        │                                                  │
        │                             flights, tallies, counts, warnings
        │
   Report.ReportWriter ──▶ Spreadsheet.SheetSpec ──▶ XlsxWriter ──▶ report .xlsx
```

`Core` never references WPF. The window binds to `ReportOptions` and displays the same
`ReportModel` that will be written, so the preview and the saved workbook cannot disagree — the
rule ICS Scrubber applies to its scrub pass.

## Source layout

| Folder | Responsibility |
|---|---|
| `Core/Spreadsheet` | `CellGrid` (text cells, header located by column name); `XlsxReader` (first worksheet, shared *and* inline strings, via System.IO.Packaging); `CsvReader` (RFC 4180, delimiter sniffing); `SheetSpec` (a sheet described in full before writing); `XlsxStyles` (the fixed style table); `XlsxWriter` (the package, written as a plain zip). |
| `Core/Movement` | `MovementReader` (title rows, header discovery, footer recognition, station detection); `MovementSheet`/`MovementRow`; `ClockTime` (times with a status letter, midnight-safe delay arithmetic); `DelayCodeParser` (the packed delay cell). |
| `Core/Mapping` | `MappingTable` (code to entry, with zero-padding normalisation); `MappingStore` (seed-on-first-run, never overwrite). |
| `Core/Report` | `ReportOptions` (the filters); `ReportBuilder` (narrowing and resolving); `ReportModel` (the finished report); `TextWrap`; `ReportWriter` (picks the workbook layout), `GroupedReportWriter` (the default layout) and `ClassicReportWriter` (the layout used up to 0.3.1, kept for comparison and rollback). |
| `Core/Email` | `DelayEmail` (the compact email, from the same `ReportModel`); `EmailDraft`; `EmlDraftWriter` (MIME draft marked unsent, ported from OFT Scrubber). |
| `Core` | `SettingsStore` (plain key=value preferences); `Presets` (built-in presets and the user's saved ones). |
| `MainWindow`, `Views`, `Controls` | Code-behind UI: ribbon, options pane, flight grid; Settings, About and preset dialogs; the filter dropdown, column header model and Codes cell converter. |
| `Themes` | `Fluent.xaml` and `Shell.xaml`, identical to the siblings'; `Report.xaml`, this application's own controls. |

## Key mechanisms

**Header discovery.** `CellGrid.FindHeaderRow` scans for the first row containing every required
column name, comparing case-insensitively with whitespace collapsed. Columns are then addressed by
name, never by position, so title rows above the table, extra columns and reordered exports all
read. The names required are those a departure delay report cannot do without: `MVT Nr`, `From`,
`To`, `STD`, `ATD`, `Date` and `Dep delay`.

**Delay parsing.** The cell packs N codes then N durations. `DelayCodeParser` splits on `/`,
classifies each token as a duration (`HH:MM`) or a code, and pairs them in order. Anything
irregular — a count mismatch, codes after durations, durations with no codes — produces a warning
and still yields what could be read. A code with no duration is kept at zero minutes rather than
discarded, because the code itself is information.

**Reconciliation.** `ClockTime.DelayMinutesBetween` measures forward from STD to ATD, wrapping over
midnight, so a departure that slips to the next day reads as 11:08 rather than −12:52. The coded
durations are summed independently. `ReportFlight.Reconciles` compares the two, and the workbook
flags disagreement rather than presenting either number as authoritative.

**Mapping.** `MappingTable.Normalize` upper-cases and strips leading zeros from the numeric part of
a code, so the sheet's `09` and the published list's `9` are one key while `93A` keeps its suffix.
`MappingStore` writes each CSV from an embedded copy only when the file is missing, so a user's
edits are never overwritten. An unmapped code is labelled `(unmapped)` and surfaced on the summary.

**Filtering.** `ReportBuilder` narrows in a fixed order — station, movement type, date and
identity filters, coded delay present, mapper exclusions, code filter, threshold, then the
search, hidden rows and a "report selected" restriction — recording a count at each step. The
flights are then sorted by the chosen column, so the preview, workbook and email share one order. Those counts reach the summary, so a report that came out thin can be explained
rather than guessed at.

**Writing.** `ReportWriter` hands the model to the layout its options name, which builds a
`SheetSpec`; `XlsxWriter` turns it into a package. The package
is a plain zip with its content types and relationships written out explicitly, rather than
System.IO.Packaging, so the bytes are exactly those intended and can be compared against a
reference file. Element order inside `<worksheet>` is fixed by the schema and is the single easiest
thing to get wrong; the constraints are recorded in the writer's own comments.

**Wrapping.** Excel is not allowed to wrap the delay columns. Each layout wraps the reason text
itself and pads the Code and Duration cells with matching blank lines, so every code stays beside
its own reason and duration however long the text runs. Row height follows from the resulting line
count.

## Extensibility

`ReportModel` is the seam. A PDF export, a flat one-row-per-event sheet, or arrival delays are all
additional consumers or fields, not parser changes. None of them is built: the model simply does
not stand in their way.
