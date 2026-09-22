# Movement sheet format notes

What the parser had to cope with in real exports. Recorded here so the next person does not
rediscover it from a failing read.

## Shape

```
row 1   Movement sheet
row 2   Period: 14.09.2026 00:00 - 20.09.2026 23:59 UTC
row 3   Weight unit: Kg
row 4   Type | D | L | A | MVT Nr | Reg | From | STD | ATD | A/B | To | STA | ... | Bulletins
row 5+  the movements
last    Total Weight In: | ... | Total Weight Out: | ... | Total Weight: | ...
```

37 columns. The development sample held 387 rows covering one week at one hub.

The header is at row 4 in every sample seen, but it is located by column name rather than by
position, so an export with a different preamble still reads. The names treated as required are
`MVT Nr`, `From`, `To`, `STD`, `ATD`, `Date` and `Dep delay`.

The final row is a totals line, not a movement. It is recognised by a leading `Total` label rather
than by being last, so a file with trailing blank rows still reads. Rows with neither a flight
number nor a date are skipped for the same reason.

## Columns the report uses

| Column | Header | Notes |
|---|---|---|
| A | `Type` | Movement type; see below. |
| E | `MVT Nr` | Flight number. Ground runs put a numeric movement id here instead. |
| F | `Reg` | Tail. Tows put a position number here instead. |
| G | `From` | Departure station. |
| H | `STD` | Scheduled departure, `18:50`. |
| I | `ATD` | Actual departure, `19:32 A`. |
| K | `To` | Destination. |
| R | `OPR` | Operator code. |
| S | `EQP` | IATA equipment code, `77X`. |
| U | `Date` | `13.09.2026`, day first. |
| V | `Dep delay` | The packed delay cell. |

`Arr delay` (W) has the same structure and is not parsed in v0.1.0. `Routes`, `Seals`, `Property`
and `Bulletins` were entirely empty across the sample.

## Movement types

Seen in the development sample, by frequency:

| Type | Count | Meaning |
|---|---|---|
| `CL` | 264 | Flight |
| `T/GR` | 56 | Ground run |
| `T` | 37 | Flight |
| `CL/AH` | 14 | Flight |
| `T/XL/AH` | 7 | Tow |
| `T/XL` | 3 | Tow |
| `T/AH` | 1 | Flight |

The type is a `/`-separated set of segments, so the parser classifies rather than matches: a type
containing a `GR` or `XL` segment is not a flight. `T/GR` and `T/XL` rows have no ATD, no load
figures and no delay codes, and both start and end at the station.

## Times and dates

Times carry no date, so a departure that slips past midnight shows an ATD earlier in the clock than
its STD — `STD 20:55`, `ATD 08:03 A` is an 11:08 delay, not a negative one. Delay is always
measured forward.

Actual times carry a trailing status letter, usually `A`. Dates are `dd.MM.yyyy`; several other
orderings are accepted defensively.

## The delay cell

```
93A/09/28A/00:17/00:15/00:10      three codes, then their three durations
36A/41A/00:07/00:18               two codes
43/03:09                          one code
```

N codes followed by N durations, paired in order. Durations are the trailing `HH:MM` tokens.

Codes are zero-padded here but not in the published code list: this sheet's `09` and `04` are the
list's `9` and `4`. Every lookup normalises.

Across the development sample the coded durations summed exactly to the STD-to-ATD delay on every
flight, so the sum is a real, checkable figure rather than an approximation. The report shows both
and flags disagreement.

## Storage quirks

The sample workbook ships a `sharedStrings` part but writes its data rows as `inlineStr`. A reader
that handles only shared strings reads the file as entirely empty, with no error. Handle both.

Numeric cells arrive as raw numbers; the report treats every cell as text, which is all it needs.

## CSV exports

The same sheet saved as CSV keeps its title rows, its header row and its column names, so the same
header-by-name discovery applies and the rows reach the same parser. The reader sniffs the
delimiter and handles RFC 4180 quoting.
