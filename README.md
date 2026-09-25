<!--
AI AGENT ENTRYPOINT

If you are reading this README as part of repository discovery,
continue by reading:

.agents/rules.md

Treat that file as the repository's agent-neutral instruction entry point.
-->

# Delay Reporter

A Windows tool that reads a movement sheet, pulls the coded departure delays out of it, resolves
every code against an editable list, and writes a professional, printable Excel workbook with room
for the station's own notes.

**Version 0.4.1.** WPF on .NET Framework 4.8. Download `DelayReporter.exe` from a published release
or build it from source. Copy the executable anywhere and run it: there is no installer, no runtime
to install, no companion DLL, and it never needs administrator rights. Build output and release
binaries are not stored in Git.

Part of the same family as [ICS Scrubber](https://github.com/gh4-io/ICS-Scrubber) and
[OFT Scrubber](https://github.com/gh4-io/OFT-Scrubber).

## What it does

A movement sheet is wide and mostly irrelevant to a delay review: hundreds of rows covering
flights, ground runs and tows, across dozens of columns. The part that matters is one packed cell
per flight:

```
93A/09/28A/00:17/00:15/00:10
```

That is three delay codes followed by their three durations. Delay Reporter unpacks it, names each
code, and lays the result out so a duty manager can read, annotate and sign it:

| Date | MVT Nr | Reg | From | To | STD | ATD | Delay | OPR | Aircraft | Code | Reason | Dur | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 13.09.2026 | 3S395 | DAALK | CVG | LEJ | 18:50 | 19:32 A | 0:42 | 3S | Boeing 777 Freighter | 93A | Aircraft rotation | 0:17 | |
| | | | | | | | | | | 09 | Ground time less than declared minimum | 0:15 | |
| | | | | | | | | | | 28A | Incorrect build up of ULD's | 0:10 | |

One flight is one row, so sorting, filtering and freezing all behave the way a spreadsheet should.
The codes stack as lines inside that single row's Code, Reason and Duration cells, and stay aligned
line for line even when a reason wraps.

## Using it

The window follows its siblings: a **File** menu, then **Home**, **Presets**, **View** and **Help**
ribbon tabs, a collapsible options pane on the left, a search bar over the flight list, and a
status bar whose standing text is the report's summary, worded exactly as the workbook words it.
Hover over the summary for the file, its period and every count behind it; a message after an
action shows there for a few seconds, then the summary returns.

1. **Open** a movement sheet (Ctrl+O, drop it anywhere on the window, or pass the path on the
   command line). Both `.xlsx` and `.csv` are read. The header row is found by column name, so
   title rows above it and reordered columns do not matter.
2. **Narrow it down** with the options on the left, the search bar, and the rows themselves. The
   list updates as you go, and **only what the list shows is reported**: every output below writes
   exactly those flights, in that order.
3. **Save report** (Ctrl+S) writes the workbook and offers to open it. **Preview in Excel** writes a
   temporary copy and opens it straight away, with no dialog and no save. **Send email** opens a
   compact draft in your mail app, or in Outlook on the web.
4. **Close** (Ctrl+W) puts the window back as it starts: no file, and no filter, search, hidden row,
   MX tick or sort order left over. The station and minimum delay stay, as saved preferences.

### The flight list

Every heading carries two small tags against its right edge, always there and quiet until they
apply. The **sort** tag turns into an accent arrow on the sorted column: click a heading to sort by
it, and again to reverse; the workbook and email follow the same order. The **filter** tag, on
Date, Reg, Delay, MX, OPR and Codes, lights up when a filter narrows that column (a date range
narrower than the file's period, the tail numbers, operators or codes chosen, the minimum delay, or
the MX filter). Hover over it to see the filter, and click it to jump to it in the options pane.

**Delay** is the clock delay, `ATD − STD`. The coded durations should add up to it and nearly
always do, so they no longer take a column of their own: when they disagree, the cell shows both,
`0:32 ≠ 0:25`, in red. Right-click the **Date** heading to choose how dates print (as in the file,
`14.09.2026`, `14/09/2026`, `09/14/2026`, `2026-09-14` or `14 Sep 2026`); the workbook and email
follow it too.

Rows can be selected with their tick boxes, with Ctrl and Shift, or all at once from the box in the
heading (Ctrl+A). The Home tab's **Rows** group and the right-click menu then offer:

| Action | Effect |
|---|---|
| **Select all**, **Select none** | Ctrl+A and Esc do the same. |
| **Hide** | Leaves the selected flights out of the report (Del). A hidden flight is counted on the summary as *hidden by hand*, and a bar above the list says how many are hidden. |
| **Unhide all** | Brings every hidden flight back. View > **Hidden rows** shows hidden flights faded in the list, so single ones can be brought back with **Unhide selected**. |
| **Report selected** | Saves, previews or emails only the selected flights. The rest are counted on that report's summary as *not selected*, so a partial report always says it is partial. |

Hidden rows, like the MX ticks, belong to the open file: they are never saved and reset when you
open another.

### Search

The search bar across the top of the flights (Ctrl+E or Ctrl+F) looks through every field of a
flight: date, flight number, tail, route, times, delays, operator code and name, aircraft type and
label, every delay code with its reason and category, the raw delay cell, and whatever is
outstanding. Every word typed must appear somewhere, as in Outlook, and while it narrows the list
the bar shows how many flights match, such as `2 of 8`. Enter moves into the results; Esc clears
the search.

Because only what is listed is reported, a search narrows the report like any other filter. The
flights it leaves out are counted on the summary as *Not matching "…"*, in the workbook and the
email alike, so a report produced mid-search cannot quietly lose flights. Opening another file
clears the search.

### Email

**Send email** writes the report as a draft `.eml` and opens it in your default mail app (Outlook,
for most), the way OFT Scrubber does. Nothing is sent: you review the draft there and press Send.

The draft is a compact table of the reported flights (date, operator, tail, flight, origin,
destination, STD, ATD, delay, each code with its duration and reason), drawn in a monochrome "Ink
Minimal" style: a bold rule under the header, no zebra striping, and each flight's codes given
space of their own rather than a tight list. A line above the table says how many flights were left
out by a search, hidden or not selected, and the full summary line closes the message. Recipients
and whether to attach the workbook are set under Settings > Email; the workbook is attached by
default.

The Outstanding column is left out of the table for now; the plain-text draft still lists what
each flight owes.

To send from **Outlook on the web** instead, choose it under Settings > Email > **Send with**: one
option for a work or school account (`outlook.office.com`), one for a personal Outlook.com account.
Send email then opens a new message in your browser with the recipients and subject filled in.
Outlook's compose link cannot take a formatted body or an attachment from another app, so the table
is put on the clipboard: click in the message body and press **Ctrl+V**. When the workbook is to be
attached, it is saved and shown selected in its folder, ready to drag into the message. Nothing is
sent until you press Send.

### Presets

The Presets tab applies a named set of filters in one click. Five are built in:

| Preset | Filters |
|---|---|
| **Standard** | How every file opens: the station's own carriers ticked under Operators, 15 minutes of included codes, no other filter. |
| **All** | 15 minutes of included codes, every operator, no other filter. |
| **MX only** | Standard, narrowed to MX delays. |
| **Every coded delay** | No minimum, no other filter. |
| **Over an hour** | 60 minutes by the clock, no other filter. |

None of them changes the station. **Save current as** stores the
station, minimum delay, basis, MX filter and the four lists under a name of your own; the dates never are,
because they come from each file. **Manage** renames and deletes them. A value a preset asks for
that the open file does not hold is named in the status bar rather than skipped silently, and a
list left with nothing to tick says that it is no longer narrowing anything.

### Options

| Option | Effect |
|---|---|
| **Station** | An editable dropdown, filled with every station seen in the file. Only departures from this station are reported: `From` is the station and `To` is not. Arrivals, ground runs and tows are therefore excluded. A station not yet in any loaded file can still be typed. The app reports which station actually dominates the file, so a wrong setting is obvious rather than producing an empty report. |
| **Minimum delay** | Flights under this are left out. The steppers (or the arrow keys) move in fives. **Coded** measures against the sum of the delay codes that survive the mapper's exclusion column, **Actual** against the `ATD − STD` clock delay. Zero reports every flight that carries a code. |
| **MX delays** | **All**, **MX only** or **Not MX**. A flight counts as MX when one of its codes maps to `MX` in the `category` column, or when its MX box is ticked by hand. Flights the filter leaves out are counted in the summary's detail, and the workbook's subtitle and the email both say the report is MX only. If `delay-codes.csv` marks no code as MX (a copy written before 0.3.0 has no `category` column), the report says so rather than coming out empty without explanation. |
| **Dates** | Defaults to the period named in the file. |
| **Movement types**, **Operators**, **Delay codes**, **Tail numbers** | Multi-select dropdowns, built from the values actually present in the loaded file. Operators and delay codes show the mapped label beside the code once one exists. Ticking nothing and ticking everything both mean no restriction, so the report stays stable if a later file holds a value this one did not. Movement types and tail numbers start with everything ticked (no restriction); Operators starts as the **Standard** preset has it, with the station's own seven carriers ticked (`3S`, `CJT`, `CKS`, `CSB`, `DHK`, `KII`, `SIA`), falling back to no restriction when a file holds none of them; delay codes start unticked. Only a value the file holds can be listed, so a carrier the file lacks is named under the list instead (*Also wanted, but not in this file: SIA*), which is why five of seven carriers can show as "5 of 11 selected". |

Only the station and the minimum delay with its basis are remembered between sessions, in
`settings.txt`. The filters (dates, MX, the four lists), the search, hidden rows, MX ticks and the
sort order all belong to the open file: every file opens with the filters **Standard** applies, at
the saved minimum delay, and a saved preset is how a different set of filters is kept.

### Reconciliation

The coded durations should add up to the real delay between STD and ATD, and in practice they do.
The report shows both and flags the flight in red when they disagree, rather than quietly trusting
either figure. A departure that slips past midnight is measured forward, so it reads as an 11:08
delay rather than a negative one.

## Mapping files

Two CSVs under `%APPDATA%\Delay Reporter\mappings\` decide how codes are named and which are
reported. They are written on first run and never overwritten afterwards, so your edits always win.
Help > **Data folder**, or Settings > Data, opens `%APPDATA%\Delay Reporter`, which holds them.
Help > **Reset** returns them to the seed, with everything else, as [Settings](#settings) describes.

`delay-codes.csv` — seeded with all 173 codes from *Global Network Delay Codes v8.1*:

```csv
code,label,exclude,si_required,si_remark,category
93A,Aircraft rotation,,Yes,record delayed inbound movement,
2,Non standard load,,Yes,record ULD ID,
81,Atfm due to atc en route demand / capacity,Y,No,,
41,Aircraft / truck defects,,No,,MX
```

| Column | Meaning |
|---|---|
| `code` | As published. The sheet's zero-padded `09` matches the list's `9` automatically. |
| `label` | What the report prints. |
| `exclude` | `Y` drops that code's events from the report. The flight keeps its other codes; a flight whose codes are *all* excluded leaves the report. Excluded events are counted on the summary, so the number stays auditable. |
| `si_required`, `si_remark` | 66 codes oblige the station to record supplementary information. When one applies, what is required — `record ULD ID`, `record causing movement(s)` — is attached to the flight's Notes cell as a hint Excel shows while the cell is selected. The cell itself stays empty to write in, and the hint never prints. The summary counts the flights that still owe it, and the email lists it under Outstanding. (The Classic layout pre-fills the cell with it instead.) |
| `category` | A cause grouping, kept apart from the label. Seeded with `MX` for every code in the 40s (maintenance); free text otherwise, and uncategorised is never an error. Drives the summary's "with MX coded delay" figure. Each flight also carries its own tick in the preview, ticked when any of its codes maps to `MX` and clear otherwise. Clicking it corrects the flight by hand for that run; clicking back to what the mapping would already say drops the correction. Never saved, and resets when you open another file. |

`aircraft-types.csv` maps the `EQP` column (`77X`, `76Y`, …) to readable names, with the same
`exclude` column.

`operators.csv` maps the `OPR` column's ICAO-style codes (`CKS`, `GTI`, …) to a carrier name, seeded
with a handful of well known cargo operators as a starting point — your own station's codes always
win once you edit the file.

A code, aircraft type or operator that is not in its file is never fatal: it prints as written, is
marked `(unmapped)`, and is listed on the summary so you know to add it.

## Settings

**Settings** (the gear at the top right, or the File menu) is laid out like Outlook's: pages
down the left, on-and-off choices as sliding switches. Nothing applies until **Save**.

| Page | Setting | Effect |
|---|---|---|
| Report | **Workbook layout** | **Grouped**, the default: black on white, tall rows centred vertically, flights in groups of five under a thin rule, and an empty Notes column to write in. OPR and Aircraft are sized to what they hold, so a shorter aircraft label or raw operator codes give Notes more room. **Classic**: the boxed, blue-banded layout used up to 0.3.1, kept unchanged for comparison. Both write the same flights, columns and summary. See [report layout](docs/report-layout.md). |
| Report | **Show the carrier name in the OPR column** | On by default. Applies to the preview, the workbook and the email. |
| Report | **Date format** | As in the file, or one of five patterns; `14 Sep 2026` by default. Applies to the preview, the workbook and the email; also on the Date heading's right-click menu. The date pickers follow it. |
| Report | **Aircraft** | **Min** (`767`), **Std** (`767-300`), or **Full**, the mapped label as written (`Boeing 767-300 Freighter`). Applies to the workbook's Aircraft column; an aircraft type missing from `aircraft-types.csv` always shows its raw `EQP` code regardless. |
| Preview | **Delay codes not chosen in the filter** | Visible, Dimmed or Hidden. Applies to the preview's Codes column while the Delay codes filter is narrowed; also on View > **Codes not chosen**. The workbook always lists every code regardless, so its coded total keeps reconciling with the clock delay. |
| Email | **Send with** | **Default mail app**, the default, opens an `.eml` draft with the workbook attached. **Outlook on the web** (work or school account) or **Outlook.com** (personal account) open a new message in the browser, with the table on the clipboard to paste and the workbook shown in its folder to drag in. See [Email](#email). |
| Email | **To**, **Cc** | Filled in on every draft. Separate addresses with semicolons; anything that is not a plain address is left out with a note in the status bar. |
| Email | **Attach the report workbook** | On by default. |
| Summary | **Show additional debug data** | Off by default. Adds one line to the workbook's summary carrying every count the default summary leaves out — rows read, every exclusion reason, mapper exclusions, reconciliation mismatches, MX overrides, and every unmapped code, aircraft and operator by name. On screen the same detail is always in the status bar's tooltip. Nothing that leaves the report is ever uncounted; this is just where the detail goes when the default summary does not need it. |
| Data | **Data folder** | Where everything lives, `%APPDATA%\Delay Reporter`, with a button to open it. |

`%APPDATA%\Delay Reporter\settings.txt` holds the station, the minimum delay and its basis, these
preferences, and whether the options pane and summary are showing. Saved presets are one plain
text file each under `%APPDATA%\Delay Reporter\presets\`. Deleting either restores the defaults.

Help > **Reset** puts everything back as first installed in one step. After asking first, it moves
the whole `%APPDATA%\Delay Reporter` folder to the Recycle Bin (settings, saved presets and the
mapping CSVs, including your edits to them) and restarts Delay Reporter, which writes fresh copies.
Anything reset by mistake can be restored from the Recycle Bin. Reports you have saved are not
touched.

The executable is unsigned, so Windows may show a security prompt for a downloaded copy.

## Build and test

Windows with the [.NET SDK](https://dotnet.microsoft.com/download) installed (the `net48` target
itself needs nothing beyond what Windows already ships — only building from source needs the SDK).

### Rebuilding the executable

```powershell
dotnet build src/DelayReporter/DelayReporter.csproj -c Release
```

The rebuilt `DelayReporter.exe` lands at `src/DelayReporter/bin/Release/net48/DelayReporter.exe`.
Copy it wherever you like; nothing else in that folder needs to travel with it. A `-c Debug` build
(or omitting `-c` entirely) lands the same way under `bin/Debug/net48/`.

### Verifying a change

```powershell
dotnet build src/DelayReporter/DelayReporter.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
```

`test-core.ps1` loads the build just produced and drives the parsing, mapping and report code
directly against `samples/demo-movement-sheet.csv`. `check-repo.ps1` checks repository hygiene:
no binaries, no real operator codes in the sample, versions in step across the project file,
manifest, README, changelog and release notes. See [releasing](docs/releasing.md) for the full
checklist a tagged release goes through, including the dist copy and hash verification step.

### GitHub Actions

The same checks run on GitHub. The workflows live on their own
[`workflows`](https://github.com/gh4-io/DelayReporter/tree/workflows) branch, the repository's
default branch, so `main` holds the application alone and a push to it starts nothing by itself.
**CI** is run by hand from the Actions tab against any branch, tag or commit: it builds the
executable on Windows, runs both scripts and the version field check, and keeps the tested
`DelayReporter.exe` as a downloadable artifact of the run. **Release** runs nightly and by hand. It
reads `<Version>` from `main`; when that version has no release yet, it runs CI on that commit and
publishes the tested executable as the release `v<version>`, with `docs/releases/v<version>.md` as
its notes and a SHA-256 hash beside it. Neither can drive the window or open a workbook in Excel,
so looking at both stays part of every release.

The only package reference is the pinned build-time reference assemblies package. The runtime
target stays `net48` and the executable uses the .NET Framework supplied by Windows.

`samples/demo-movement-sheet.csv` is synthetic: fictional flight numbers, tails and operators. No
real operational data is kept in this repository.

Every build embeds a UTC timestamp shown in About, for example `0.1.0+build.20260922.184500Z`.
For a repeatable build identifier, supply `-p:BuildTimestamp=20260922.184500Z`.

See [CONTRIBUTING.md](CONTRIBUTING.md), [architecture](docs/architecture.md),
[design decisions](docs/decisions.md), [movement sheet notes](docs/movement-sheet-notes.md),
[delay code notes](docs/delay-codes.md), [report layout](docs/report-layout.md) and
[release instructions](docs/releasing.md).

<!--
AI AGENT CONTINUE

If you are reading this README as part of repository discovery
and intend to inspect, modify, debug, test, review, or continue development
of this repository, continue by reading:

.agents/rules.md

Treat that file as the repository's agent-neutral instruction entry point.
-->
