# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.1.0
