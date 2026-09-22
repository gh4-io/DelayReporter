# Delay Reporter

A Windows tool that reads a movement sheet, pulls the coded departure delays out of it, resolves
every code against an editable list, and writes a professional, printable Excel workbook with room
for the station's own notes.

**Version 0.1.0.** WPF on .NET Framework 4.8. Download `DelayReporter.exe` from a published release
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
3. **Generate report…** writes the workbook and offers to open it.

### Options

| Option | Effect |
|---|---|
| **Station** | Only departures from this station are reported: `From` is the station and `To` is not. Arrivals, ground runs and tows are therefore excluded. The app reports which station actually dominates the file, so a wrong setting is obvious rather than producing an empty report. |
| **Minimum delay** | Flights under this are left out. Measured against the sum of the delay codes that survive the mapper's exclusion column, or against the actual `ATD − STD` clock delay. Zero reports every flight that carries a code. |
| **Dates** | Defaults to the period named in the file. |
| **Movement types** | Built from the types actually present in the file. Flights are selected by default; ground runs (`T/GR`) and tows (`T/XL`) are not, because they carry no departure and no delay codes. |
| **Operators** | Built from the file. Selecting all is the same as no filter. |
| **Delay codes**, **Tail numbers** | Comma separated. Empty means no restriction. |

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
code,label,exclude,si_required,si_remark
93A,Aircraft rotation,,Yes,record delayed inbound movement
2,Non standard load,,Yes,record ULD ID
81,Atfm due to atc en route demand / capacity,Y,No,
```

| Column | Meaning |
|---|---|
| `code` | As published. The sheet's zero-padded `09` matches the list's `9` automatically. |
| `label` | What the report prints. |
| `exclude` | `Y` drops that code's events from the report. The flight keeps its other codes; a flight whose codes are *all* excluded leaves the report. Excluded events are counted on the summary, so the number stays auditable. |
| `si_required`, `si_remark` | 66 codes oblige the station to record supplementary information. When one applies, the flight's Notes cell is pre-filled with what is required — `record ULD ID`, `record causing movement(s)` — turning the report into a checklist of what is still owed. |

`aircraft-types.csv` maps the `EQP` column (`77X`, `76Y`, …) to readable names, with the same
`exclude` column.

A code or aircraft type that is not in its file is never fatal: it prints as written, is marked
`(unmapped)`, and is listed on the summary so you know to add it.

## Settings

`%APPDATA%\Delay Reporter\settings.txt` holds the station, the minimum delay and its basis.
It is plain text; deleting it restores the defaults.

The executable is unsigned, so Windows may show a security prompt for a downloaded copy.

## Build and test

Windows with the .NET SDK installed:

```powershell
dotnet build src/DelayReporter/DelayReporter.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
```

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
