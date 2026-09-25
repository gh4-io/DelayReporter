# Report layout

One sheet, `Departure delays`: a title, a summary block, then one row per flight. This is the
grouped layout, the default since the redesign that followed 0.3.1. It is modelled on the printed
movement delay reports stations already file: black on white, no boxes, tall rows, every cell
centred vertically, and a thin rule after every fifth flight.

The layout it replaced is still available as Settings > Report > **Workbook layout** > **Classic**,
and is recorded in [report layout: classic](report-layout-classic.md), together with how to roll
back. Both layouts write the same flights, in the same order, with the same columns and summary.
Beyond the look, one cell differs: on a flight whose codes do not add up, the grouped layout's
Delay cell shows both figures, `0:32 ≠ 0:25`, as the preview and the email do, where the classic
layout shows the clock figure alone in red.

## The sheet

```
 1                        Departure Delay Report                         (merged A:N, centred)
 2           CVG departures   ·   Period: 22.09.2026 15:00 - 23.09.2026 07:00 UTC
 3   Minimum delay 15 min (included codes)  ·  Source ...  ·  Generated ... UTC     (small, grey)
 4
 5  Summary                                   Delay codes by time          (bold, ruled beneath)
 6  CVG departures          324               93B  Aircraft rotation   21:15   12 events
 7  No coded delay            9               41A  Aircraft defects    12:01    9 events
    ...                                       ...
    [Detail, only when Settings > Show additional debug data is on]


18  Date  MVT Nr  Reg  From  To  STD  ATD  Delay  OPR  Aircraft  Code  Reason  Dur  Notes
    ─────────────────────────────────────────────────────────────────────────────────────
19+ one tall row per flight; a thin grey rule under every fifth
```

The summary is the classic layout's, figure for figure: labels in grey, values in bold, and no
filled bands. Two clear rows separate it from the table, whose header row moves with the summary's
length. Only the header row repeats when printed. The pane is frozen below it, and it carries an
autofilter, whose buttons show on screen but never print.

## Columns

| # | Column | Width | Alignment |
|---|---|---|---|
| A | Date | 11 | centred |
| B | MVT Nr | 10 | left |
| C | Reg | 10 | left |
| D | From | 6 | centred |
| E | To | 6 | centred |
| F | STD | 7 | centred |
| G | ATD | 8 | centred |
| H | Delay | 11 | centred; bold red, both figures, when the codes do not reconcile |
| I | OPR | sized to the report, see below | left, wrapped |
| J | Aircraft | sized to the report, see below | left, wrapped |
| K | Code | 7 | stacked, centred |
| L | Reason | 38 | stacked, wrapped by the writer at 36 characters |
| M | Dur | 7 | stacked, right aligned so the minutes line up |
| N | Notes | whatever is left of 199, at least 30 | empty, to be written or typed in |

Reason is narrower than in the classic layout: most reasons fit in 36 characters, and the width
goes to Notes, which is where the printed page gets written on.

## Sized columns

OPR and Aircraft take their width from the flights in the report, so they follow the settings that
change them (carrier names on or off, the Min, Std or Full aircraft label) and the data itself.
Each gets the narrowest width at which every value fits on two lines, since a minimum-height row
already has room for two, plus two characters of padding because Excel wraps by pixels. The width
is never narrower than the heading and never more than 22 characters; a value longer than that
takes a third line and the row grows to fit.

The table's total width stays at 199 character units whatever the options, so the page always
prints at the same scale, and every unit OPR and Aircraft do not need goes to Notes. On the sample,
full aircraft labels give Aircraft 16 and Notes 46; the Min label gives Aircraft 10 and Notes 52.

## Vertical alignment

Every cell in a flight's row is centred vertically. The Code, Reason and Dur cells stack one line
per code, padded to equal line counts exactly as in the classic layout, so centring all three keeps
each code level with its reason and duration. The single-value cells then sit level with the middle
of the stack, which is how the printed reports read: the flight's details beside the centre of its
delay block.

```
                                              93A  Aircraft rotation                   0:17
14.09.2026  ZZ101  N001ZZ  CVG  XXA  18:50 ...  09  Ground time less than declared     0:15
                                                   minimum
                                              28A  Incorrect build up of ULD's         0:10
```

## Row height

`max(lines × 13.5pt + 16pt, 36pt)`, where lines is the largest of the stacked delay lines and the
wrapped OPR and Aircraft values. The 16pt of padding and the 36pt floor, about half an inch, leave room to write on
even a one-code flight. Header row 24pt, title 28pt.

## Groups of five

A thin grey rule is drawn under every fifth flight. It is conditional formatting over the flight
rows, not a border on the cells:

```
MOD(SUBTOTAL(103, $B$19:$B19), 5) = 0
```

`SUBTOTAL(103, …)` counts the visible, non-empty flight numbers from the first flight down to the
row, so the rules stay in fives when the sheet is sorted or filtered in Excel. A border written
into the cells would travel with its row and scatter. The count runs through the whole table, so a
group can straddle a page break.

## Notes

Every Notes cell is left empty, so it is clean to write on when printed and to type into on
screen, with nothing to delete first. Where a flight's codes oblige supplementary information,
what is required (`record ULD ID`, `record causing movement(s)`) is a hint rather than cell text:
Excel shows it beside the cell, titled *Supplementary information*, while the cell is selected. It
is written as a data validation that restricts nothing and carries only an input message, so it
never prints and never gets in the way of what is typed. Excel caps the message at 255 characters;
a longer one is cut with an ellipsis.

The classic layout still pre-fills these prompts as grey italic cell text. The summary counts how
many flights owe supplementary information in both layouts, and the email lists it per flight
under Outstanding, so moving the prompt off the page loses nothing from the report.

## Print setup

Unchanged from the classic layout: Letter, landscape, fit to one page wide and as many pages long
as needed, horizontally centred, 0.3in side margins, and a footer carrying the application name,
the page number and the station. Gridlines are hidden.

## Styles

The style table is fixed. `CellStyle`'s numeric values index `<cellXfs>` in `XlsxStyles`, and
`DifferentialStyle`'s index `<dxfs>`; each pair is one list written in two places.
`tools/test-core.ps1` checks the counts agree. Entries 1 to 12 belong to the classic layout and are
unchanged; the grouped layout's are appended after them.

| Index | Style | Use |
|---|---|---|
| 13 | GroupedTitle | Row 1: bold 16pt, black, centred |
| 14 | GroupedPeriod | Row 2: 11pt, near black, centred |
| 15 | GroupedNote | Row 3: 10pt grey, centred |
| 16 | GroupedSection | Summary section headings, bold, ruled beneath |
| 17 | GroupedLabel | Summary metric names and event counts, grey |
| 18 | GroupedFigure | Summary values, bold |
| 19 | GroupedText | The code table and the detail line |
| 20 | GroupedHeading | Left-aligned column headings, bold, ruled beneath |
| 21 | GroupedHeadingCenter | Centred column headings |
| 22 | GroupedHeadingRight | The Dur heading |
| 23 | GroupedCell | Left-aligned cells |
| 24 | GroupedCellCenter | Centred cells |
| 25 | GroupedCellWrap | Reason, OPR and Aircraft |
| 26 | GroupedCellStack | Code |
| 27 | GroupedCellStackRight | Dur |
| 28 | GroupedNotes | The notes column: regular 10pt, for whatever is typed there |
| 29 | GroupedFlag | A delay that does not reconcile |

| dxf | Style | Use |
|---|---|---|
| 0 | GroupRule | Thin `#808080` rule under every fifth flight |

Calibri throughout; black text, `#404040` heading rules, `#808080` group rules, and red only for a
delay that does not reconcile. Nothing depends on colour to be read, so the report prints cleanly
in black and white.
