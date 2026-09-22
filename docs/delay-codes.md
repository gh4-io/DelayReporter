# Delay codes

## The seed file

`src/DelayReporter/Resources/delay-codes.csv` holds all 173 codes from *Global Network Delay Codes
v8.1*, embedded in the executable and written to `%APPDATA%\Delay Reporter\mappings\` on first run.

```csv
code,label,exclude,si_required,si_remark
0,Sort equipment failure,,No,
1,Authorised reschedule of movement,,Yes,record root cause
2,Non standard load,,Yes,record ULD ID
```

| Column | From the source document | Notes |
|---|---|---|
| `code` | Code | As published, not zero-padded. |
| `label` | Delay_Reason | Verbatim. |
| `exclude` | — | Added. Empty in the seed: nothing is excluded until you decide. |
| `si_required` | SI Required | `Yes` for 66 of the 173 codes. |
| `si_remark` | SI Remark | What must be recorded, e.g. `record ULD ID`. |

The source table also carries Definition, Network Domain, Delay_Group, Delay_Category, a
controllable/uncontrollable marker, and per-column markers for whether a code is valid on a
departure, an arrival or a diversion. Those were transcribed and deliberately left out of the
shipped file, which keeps the mapper short enough to edit by hand. They are the obvious source for
grouped summaries — by Delay_Group, or controllable versus not — if that is ever wanted.

## Matching

Codes are normalised before lookup: upper-cased, with leading zeros stripped from the numeric part
and any letter suffix kept. The movement sheet's `09` therefore finds the list's `9`, and `93a`
finds `93A`. Every one of the 48 distinct codes in the development sample resolved against the
seed.

A code that is not in the file is not an error. It prints as written, is labelled `(unmapped)`, and
is listed on the report summary so it can be added.

## Faithfulness

The seed reproduces the document, including its inconsistencies. One row's Network Domain reads
`Air` where every comparable row reads `Air & Road`; two read `Air only` rather than `Air Only`;
several labels carry the source's own spelling, such as *occupoed* and *equipent*. These are left
as published.

Correcting them in the seed would make the shipped file disagree with the document it claims to
reproduce, and would be silently overwritten for anyone who had already edited their copy. Local
corrections belong in `%APPDATA%\Delay Reporter\mappings\delay-codes.csv`, which is never
overwritten.

## Replacing the file when a new version is published

The mapper is an ordinary CSV, so a v8.2 table can be dropped in directly as long as it keeps the
`code` and `label` columns; column order does not matter and extra columns are ignored. Only `code`
is mandatory.

To change what the application ships with, replace
`src/DelayReporter/Resources/delay-codes.csv` and rebuild. `tools/check-repo.ps1` asserts the row
count, the absence of duplicates, that every row has a code and a label, and that no page furniture
has leaked into the labels — the transcription failures worth catching.

Existing users keep their own file; the new seed reaches only fresh installations. There is no
merge, by design: an automatic merge would silently overturn a station's deliberate exclusions.

## Extracting from the published PDF

The v8.1 table was transcribed from the PDF rather than retyped. Three things made that harder than
it looks, and would again:

- A row is anchored by its code cell, but wrapped text in the other columns extends both above and
  below that line, so rows are not simple top-to-bottom groups.
- The first page's table is split across several content streams, so one row's cells can straddle a
  boundary.
- A few rows are drawn inside a transformed context and land at unusable coordinates, including
  negative ones. Their values all belong to closed vocabularies, so they can be placed by value
  rather than by position.

Column assignment depends on horizontal position: `Div. Reason` and `SI Required` both contain
`Yes`, and only the x-coordinate distinguishes them. Any extraction that flattens the page to plain
text will silently confuse those two columns.
