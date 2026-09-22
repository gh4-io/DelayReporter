# Report layout

One sheet, `Departure delays`: a summary block, then one row per flight.

## The sheet

```
 1  DEPARTURE DELAY REPORT                                          (merged A:N)
 2  Station CVG · Period ... · Source ... · Generated ... · Minimum delay 15 min (included codes)
 3
 4  SUMMARY                          (A:H)      DELAY CODES BY TIME           (K:N)
 5  CVG departures          324               93B  Aircraft rotation   21:15   12 events
 6  With coded delay        138               41A  Aircraft defects    12:01    9 events
 7  Flights reported         95               ...
 8  Delay events            158
 9  Total coded delay    134:29
10  Excluded by mapper  0 events
11  Below threshold    43 flights
12  Flights needing SI       44
13  Coded/actual mismatches   0
14  Unmapped codes            0
15
16  Date | MVT Nr | Reg | From | To | STD | ATD | Delay | OPR | Aircraft | Code | Reason | Dur | Notes
17+ one row per flight
```

Rows 1 to 16 are the print titles, so the summary and the column header repeat on every printed
page. The pane is frozen below the header, and the header carries an autofilter.

## Columns

| # | Column | Width | Style |
|---|---|---|---|
| A | Date | 10 | centred |
| B | MVT Nr | 10 | left |
| C | Reg | 10 | left |
| D | From | 6 | centred |
| E | To | 6 | centred |
| F | STD | 7 | centred |
| G | ATD | 7 | centred |
| H | Delay | 8 | centred; bold red when the codes do not reconcile |
| I | OPR | 6 | centred |
| J | Aircraft | 20 | wrapped |
| K | Code | 7 | stacked, centred |
| L | Reason | 46 | stacked, wrapped by the writer |
| M | Dur | 7 | stacked, centred |
| N | Notes | 30 | wrapped, grey italic |

## Stacked delay codes

A flight's codes stack as lines inside K, L and M of its single row:

```
 K       L                                          M
 58B     System computer failure                    0:36
 22A     Late arrival of freight at aircraft/truck   0:15
         for loading
 38A     ULD                                        0:10
```

The writer wraps the reason text itself and inserts a blank line into K and M for each
continuation line, so a code always sits beside its own reason and duration. Excel is not allowed
to wrap these cells: if it reflowed L, the three columns would drift apart.

Row height is `max(lines) × 13.5pt + 3`, with a 16pt floor.

## Notes

Left blank for the station to fill in, except where a flight's codes oblige supplementary
information. Those prompts are pre-filled in grey italic:

```
record delayed inbound movement; record root cause
```

The summary counts how many flights still owe supplementary information.

## Print setup

Letter, landscape, fit to one page wide and as many pages long as needed, horizontally centred,
0.3in side margins. The footer carries the application name, the page number and the station.

Gridlines are hidden; the table carries its own light borders.

## Styles

The style table is fixed, and `CellStyle`'s numeric values index `<cellXfs>` in `XlsxStyles`.
The two are one list written in two places; changing either alone silently restyles the report.

| Index | Style | Use |
|---|---|---|
| 0 | Default | — |
| 1 | Title | Row 1 |
| 2 | Subtitle | Row 2 |
| 3 | Band | Section headers, white on dark blue |
| 4 | Label | Summary metric names |
| 5 | Value | Summary values and the code table |
| 6 | Header | The table header |
| 7 | Body | Left-aligned cells |
| 8 | BodyCenter | Centred cells |
| 9 | BodyStack | Code and Duration |
| 10 | BodyWrap | Reason and Aircraft |
| 11 | Notes | The notes column |
| 12 | BodyFlag | A delay that does not reconcile |

Accent `#1F3864`, light fill `#D9E1F2`, borders `#BFBFBF`, Calibri throughout.

## What is not here

A second sheet with one row per delay event, and a parameters sheet recording exactly which filters
produced the workbook, were both considered and left out of v0.1.0. `ReportModel` carries
everything either would need; they are layout, not new parsing.
