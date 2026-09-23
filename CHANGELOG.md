# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- The workbook has a new default layout, modelled on printed movement delay reports: black on
  white with no boxes or bands, a centred title and period, tall rows with every cell centred
  vertically, flights in groups of five with a thin rule under each group, and a wider Notes column
  to write in. The rule is conditional formatting, so it stays in fives when the sheet is sorted
  or filtered in Excel. A mismatched delay shows both figures, `0:32 ≠ 0:25`, so it still reads on
  a black and white printout.

- The window is redesigned to match ICS Scrubber and OFT Scrubber, and now uses their shared
  `Fluent.xaml` and `Shell.xaml` theme unchanged: a File menu, Home, Presets, View and Help ribbon
  tabs, a collapsible options pane, and a status bar. Dragging a movement sheet over the window
  covers it with OFT Scrubber's 60% accent overlay.
- The summary moves to the status bar, still worded exactly as the workbook words it; its tooltip
  carries the file, its period and every detail count.
- Delay and Coded are one column. It shows the clock delay, and both figures in red, `0:32 ≠ 0:25`,
  only when the coded durations disagree with it.
- The minimum delay takes one row: minutes with steppers, and a Coded / Actual switch.
- Every column heading carries its sort tag and, where it can be filtered, its filter tag, both
  right-aligned and always visible, lit when they apply.
- The aircraft label choices are called Min, Std and Full.
- Mappings and settings folders are no longer on the Home tab. Help > Data folder and a new Data
  page in Settings open `%APPDATA%\Delay Reporter`, which holds both.
- **Settings** is laid out like Outlook's, with pages down the left and sliding switches, and gains
  an Email page.
- The MX tick in the preview is a plain on/off box: ticked when a code maps to `MX`, cleared
  otherwise, and a click records a correction only when it differs from the mapping. It used to
  bind to a three-state override and showed as permanently indeterminate on load.

### Added

- Settings > Report > **Workbook layout** switches between the new **Grouped** layout and the
  **Classic** one used up to 0.3.1, which is kept unchanged for comparison and rollback. The git tag
  `report-layout-classic` marks the last commit with only the classic layout.
- Clickable column headings sort the list, with an arrow on the sorted column. The workbook and
  email follow the same order.
- Filter tags on the column headings show which filters are narrowing the list; hovering shows the
  filter and clicking opens it.
- Rows can be selected, hidden and reported on their own: select all, select none, hide, unhide
  all, and report selected (save, preview or email). Flights hidden by hand or left out of a
  "report selected" run are counted on the summary, never dropped silently.
- An MX filter: all flights, MX only, or not MX. An MX-only report says so in its subtitle and
  email, and warns when `delay-codes.csv` marks no code as MX.
- **Close** (Ctrl+W) closes the movement sheet and resets the window.
- A date format for the Date column, from Settings or the Date heading's right-click menu,
  applied to the preview, workbook and email.
- A search bar over the flight list that looks through every field of every flight. It narrows the report like any
  other filter, and the flights it leaves out are counted on the summary.
- **Send email** opens a compact draft of the report in the default mail app, with an Outstanding
  column of what is still owed on each flight, optionally with the workbook attached.
- An Outstanding column in the preview, matching the email.
- Presets: three built in, plus your own saved under `%APPDATA%\Delay Reporter\presets\`.

## [0.3.1] - 2026-09-23

### Fixed

- `operators.csv`: `WIN` is Awesome Cargo, confirmed by the station. It shipped unmapped in 0.3.0
  because no independent source confirmed it at the time; guessing was rejected in favour of an
  honest "unmapped" tag, and this corrects it now that the real answer is known.

### Documentation

- README: added a short, standalone "Rebuilding the executable" section stating the exact output
  path (`src/DelayReporter/bin/Release/net48/DelayReporter.exe`), separate from the fuller
  build-and-verify flow that also runs the check scripts.

## [0.3.0] - 2026-09-23

### Added

- Delay codes carry an additional `category` column, seeded `MX` for every code in the 40s. The
  summary reports "with MX coded delay", and each reported flight carries a tick in the preview to
  force or exclude that classification for the file currently open — never saved, resets on reopen.
- The OPR column shows the resolved carrier name by default; `operators.csv` grew from 8 to 13
  entries, adding AeroLogic, EAT Leipzig, 21 Air, Kalitta Charters II and Singapore Airlines after
  checking a real CVG movement sheet's operator codes against it.
- A view option for how much of a mapped aircraft label the report shows: the model family alone
  (`767`), family and variant (`767-300`), or the full label as written in the mapping file.
- A new **Settings** dialog: the three choices above, plus how the preview's Codes column treats a
  code the Delay codes filter did not choose — visible, dimmed or left out entirely. All four
  persist across restarts; none of them change what the workbook contains except the operator and
  aircraft choices, which apply to both by design.
- A **Show additional debug data** setting appends one line to the summary, on screen and in the
  workbook, carrying every exclusion, mapping and reconciliation count the default summary leaves
  out.

### Changed

- The summary is trimmed to seven figures by default (station departures, no coded delay, with
  coded delay, with MX coded delay, below threshold, flights reported, delay events) instead of the
  nineteen it grew to in 0.2.0 — everything else moved behind the debug setting above.
- Printing repeats only the table header row on pages after the first; the title, subtitle and
  summary now belong to page one alone.

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

[0.3.1]: https://github.com/gh4-io/delayreporter/releases/tag/v0.3.1
[0.3.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.3.0
[0.2.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.2.0
[0.1.0]: https://github.com/gh4-io/delayreporter/releases/tag/v0.1.0
