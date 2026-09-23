# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-09-23

### Added

- Resolves the `OPR` column against a new editable `operators.csv`, seeded with a handful of well
  known cargo carriers under their ICAO-style codes, following the exact pattern already used for
  delay codes and aircraft types. An operator absent from the file is never fatal and is listed on
  the summary, the same as an unmapped code or aircraft type.
- Station, movement type, operator, delay code and tail number are now dropdowns instead of free
  text: Station is an editable single-select filled with the stations seen in the file, and the
  other four are a new multi-select dropdown built from the values actually present, showing the
  mapped label beside operator and delay codes once one exists.
- **Preview in Excel**: opens the current report in Excel from a temporary file immediately, with no
  save dialog and no prompt, for a quick look before committing to "Generate report…".

### Fixed

- Rows rejected by the station-departure test — arrivals, other stations, and ground runs or tows
  at the report's own station — were dropped with nothing recorded on the summary. A single
  `Excluded by filters` counter also covered four unrelated exclusion reasons (date, operator, tail
  number, delay code) at once. Every drop point now has its own counter, so the summary's rows-read
  figure reconciles exactly against everything reported and everything excluded.

## [0.1.0] - 2026-09-22

First release.

### Added

- Reads movement sheets as `.xlsx` or `.csv`. The header row is located by column name, so title
  rows above the table and reordered or additional columns do not break the read. Both shared and
  inline strings are handled.
- Parses the `Dep delay` column into individual coded delays: N codes followed by N durations,
  paired in order. A count mismatch, interleaved values or a missing duration produce a warning and
  keep the flight rather than failing.
- Measures the actual delay between STD and ATD, carrying a departure past midnight forward instead
  of reporting a negative delay, and flags flights where the coded durations do not reconcile with
  the clock.
- Resolves delay codes and aircraft types through CSV files in `%APPDATA%\Delay Reporter\mappings\`,
  seeded on first run and never overwritten afterwards. `delay-codes.csv` ships all 173 codes from
  *Global Network Delay Codes v8.1*; codes are matched with zero-padding normalised, so the sheet's
  `09` finds the list's `9`.
- An `exclude` column on each mapping file drops that code's events from the report while the
  flight keeps its remaining codes. Flights left with no reportable code leave the report, and the
  excluded count is shown on the summary.
- Pre-fills each flight's Notes cell with the supplementary information its codes oblige, from the
  `si_required` and `si_remark` columns.
- Filters by station, movement type, date range, operator, delay code and tail number, with a
  minimum-delay threshold measured against either the included codes or the actual delay.
- Detects the station the file is actually about and warns when it disagrees with the setting.
- Writes a single-sheet workbook: a summary block that repeats when printed, then one row per
  flight with its delay codes stacked and aligned inside that row, a blank Notes column, an
  autofilter, a frozen header and landscape fit-to-width print setup.
- Windows shell with drag and drop, a live preview of exactly what will be written, and an About
  dialog showing the build.

### Known limits

- Departure delays only. The `Arr delay` column is not parsed; the model has room for it.
- Aircraft type names are seeded from standard IATA equipment codes for the types present in the
  development sample and should be checked against the station's own fleet naming.
- No codes are excluded by default, so the first report shows everything until the mapper is edited.

[0.2.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.2.0
[0.1.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.1.0
