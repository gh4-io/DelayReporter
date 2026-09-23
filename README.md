# Delay Reporter

A Windows tool that reads a movement sheet, pulls the coded departure delays out of it, resolves
every code against an editable list, and writes a professional, printable Excel workbook with room
for the station's own notes.

**Version 0.3.1.** WPF on .NET Framework 4.8. Download `DelayReporter.exe` from a published release
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

1. **Open** a movement sheet (Ctrl+O, drop it on the window, or pass the path on the command line).
   Both `.xlsx` and `.csv` are read. The header row is found by column name, so title rows above it
   and reordered columns do not matter.
2. **Check the options** on the left. The preview updates as you change them, and what the preview
   shows is what the workbook will contain.
3. **Preview in Excel** writes a temporary copy and opens it straight away, no dialog, no save — a
   quick look before you commit to a file. **Generate report…** writes the real workbook and offers
   to open it.

### Options

| Option | Effect |
|---|---|
| **Station** | An editable dropdown, filled with every station seen in the file. Only departures from this station are reported: `From` is the station and `To` is not. Arrivals, ground runs and tows are therefore excluded. A station not yet in any loaded file can still be typed. The app reports which station actually dominates the file, so a wrong setting is obvious rather than producing an empty report. |
| **Minimum delay** | Flights under this are left out. Measured against the sum of the delay codes that survive the mapper's exclusion column, or against the actual `ATD − STD` clock delay. Zero reports every flight that carries a code. |
| **Dates** | Defaults to the period named in the file. |
| **Movement types**, **Operators**, **Delay codes**, **Tail numbers** | Multi-select dropdowns, built from the values actually present in the loaded file. Operators and delay codes show the mapped label beside the code once one exists. Ticking nothing and ticking everything both mean no restriction, so the report stays stable if a later file holds a value this one did not. Movement types default to the types that are actual flights; ground runs (`T/GR`) and tows (`T/XL`) start unticked, because they carry no departure and no delay codes. |

### Reconciliation

The coded durations should add up to the real delay between STD and ATD, and in practice they do.
The report shows both and flags the flight in red when they disagree, rather than quietly trusting
either figure. A departure that slips past midnight is measured forward, so it reads as an 11:08
delay rather than a negative one.

## Mapping files

Two CSVs under `%APPDATA%\Delay Reporter\mappings\` decide how codes are named and which are
reported. They are written on first run and never overwritten afterwards, so your edits always win.
Help > **Mappings** opens the folder.

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
| `si_required`, `si_remark` | 66 codes oblige the station to record supplementary information. When one applies, the flight's Notes cell is pre-filled with what is required — `record ULD ID`, `record causing movement(s)` — turning the report into a checklist of what is still owed. |
| `category` | A cause grouping, kept apart from the label. Seeded with `MX` for every code in the 40s (maintenance); free text otherwise, and uncategorised is never an error. Drives the summary's "with MX coded delay" figure. Each flight also carries its own tick in the preview to force or exclude the MX classification for that run — Automatic, Force, Exclude, cycling back to Automatic — which is never saved and resets when you open another file. |

`aircraft-types.csv` maps the `EQP` column (`77X`, `76Y`, …) to readable names, with the same
`exclude` column.

`operators.csv` maps the `OPR` column's ICAO-style codes (`CKS`, `GTI`, …) to a carrier name, seeded
with a handful of well known cargo operators as a starting point — your own station's codes always
win once you edit the file.

A code, aircraft type or operator that is not in its file is never fatal: it prints as written, is
marked `(unmapped)`, and is listed on the summary so you know to add it.

## Settings

The **Settings** button changes how the preview and the report read, without changing what they
contain:

| Setting | Effect |
|---|---|
| **Delay codes not chosen in the filter** | Visible, Dimmed or Hidden. Applies to the preview's Codes column while the Delay codes filter is narrowed. The workbook always lists every code regardless, so its coded total keeps reconciling with the clock delay. |
| **Show the carrier name in the OPR column** | On by default. Applies to both the preview and the workbook. |
| **Aircraft** | Family (`767`), family and variant (`767-300`), or the full mapped label (`Boeing 767-300 Freighter`). Applies to the workbook's Aircraft column; an aircraft type missing from `aircraft-types.csv` always shows its raw `EQP` code regardless. |
| **Show additional debug data** | Off by default. Adds one line to the summary, on screen and in the workbook, carrying every count the default summary leaves out — rows read, every exclusion reason, mapper exclusions, reconciliation mismatches, MX overrides, and every unmapped code, aircraft and operator by name. Nothing that leaves the report is ever uncounted; this is just where the detail goes when the default summary does not need it. |

`%APPDATA%\Delay Reporter\settings.txt` holds the station, the minimum delay and its basis, and
these four preferences. It is plain text; deleting it restores the defaults.

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
