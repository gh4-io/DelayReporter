# Repository rules for coding agents

This file is the persistent, agent-neutral instruction entry point for this repository. Any coding
agent should read it before inspecting, changing, testing or reviewing anything here. Tool-specific
files (such as `CLAUDE.md`) point back to it and only add what applies to that one tool; nothing
here depends on which agent is reading it. It deliberately stays short and links to the
authoritative documentation rather than repeating it.

## What this is

Delay Reporter is a Windows desktop tool (WPF on .NET Framework 4.8) that parses airport movement
sheets and writes a printable departure-delay workbook, a compact email draft and an on-screen
preview. It is the third tool in the family started by ICS Scrubber and OFT Scrubber, and follows
their conventions. [README.md](../README.md) describes what a user sees; read it for behaviour.

## The promises this project makes

These are the constraints everything else is judged against. Breaking one is a release decision,
not an implementation detail.

1. **One executable, no runtime, no admin.** `dotnet build` produces a single
   `DelayReporter.exe` that runs on any Windows 10 1903+ or Windows 11 machine with nothing
   installed. No runtime NuGet packages, no companion DLL, no installer, no registry writes.
   Everything it stores lives under `%APPDATA%\Delay Reporter\`.
2. **The user's mapping files always win.** Seed files are written once if missing and never
   overwritten. A code or aircraft type that is absent from them is reported as unmapped, never
   treated as an error and never silently dropped.
3. **Nothing disappears silently.** Every row that leaves the report is counted somewhere on the
   summary: excluded by type, by filter, by threshold, by the mapper's exclusion column. A thin
   report must always be explainable.
4. **No real operational data in Git.** `samples/` is synthetic. Real movement sheets are used for
   development only.

## Layout

```
src/DelayReporter/Core/      no WPF reference, ever
  Spreadsheet/   XLSX and CSV reading, the hand written XLSX writer, the fixed style table
  Movement/      the movement sheet format: header discovery, clock arithmetic, delay parsing
  Mapping/       the editable CSV tables and where they live
  Report/        filtering, search, sort, tallies and the workbook layout
  Email/         the compact email, the .eml draft writer, and the Outlook on the web link
src/DelayReporter/           WPF shell: window, views, controls, themes
  Themes/        Fluent.xaml and Shell.xaml match the siblings byte for byte; Report.xaml is ours
tools/                       PowerShell checks, in place of a test framework
docs/                        architecture, decisions, format notes, release process
```

The GitHub Actions workflows are not on this branch. They live on the `workflows` branch (the
repository's default branch), which holds nothing else; see "Building and testing" below.

`Core` never references WPF. The UI binds to `ReportOptions` and displays the `ReportModel` that
will be written, so the preview and the saved workbook cannot disagree. The pipeline and the
mechanisms behind it are in [docs/architecture.md](../docs/architecture.md); why things are the way
they are is in [docs/decisions.md](../docs/decisions.md), which is worth searching before changing
any behaviour that looks odd.

## Things that will bite you

- **OOXML element order is fixed.** Inside `<worksheet>`, `autoFilter` comes *before* `mergeCells`,
  and `printOptions`, `pageMargins`, `pageSetup`, `headerFooter` come last in that order. Excel
  rejects the file outright when this is wrong, usually with no useful message.
- **`CellStyle` and `<cellXfs>` are one list in two places.** The enum's numeric values index the
  style table in `XlsxStyles`, as `DifferentialStyle` indexes `<dxfs>`. Change them together or the
  report silently restyles; `test-core.ps1` checks the counts. Entries 1 to 12 are the classic
  layout's; append, never insert.
- **There are two workbook layouts.** `GroupedReportWriter` is the default; `ClassicReportWriter`
  is the pre-redesign layout kept for rollback and should not be changed. A change to what the
  report says (columns, summary, stacking) belongs in both, or in neither. See
  [docs/report-layout.md](../docs/report-layout.md).
- **Cell text is wrapped by us, not by Excel.** The Code, Reason and Duration columns must stay
  aligned line for line, so a wrapped reason gets matching blank lines in the other two. Row height
  is then computed from the line count. Letting Excel wrap breaks the alignment.
- **Delay codes are zero-padded in the sheet but not in the code list.** `09` and `9` are the same
  code. All lookups go through `MappingTable.Normalize`. See [docs/delay-codes.md](../docs/delay-codes.md).
- **Times carry no date.** A departure after midnight shows an ATD earlier than its STD. Delay is
  always measured forward; never let it go negative.
- **The list is the report.** Sort, search, hidden rows and "report selected" are `ReportOptions`
  fields applied in `ReportBuilder`, never screen-only filtering, and each exclusion is counted on
  the summary. A view-only filter would let the preview and the workbook disagree.
- **Don't edit Fluent.xaml or Shell.xaml.** They are the family's shared theme; put new styles in
  `Report.xaml`. XAML comments must not contain double hyphens.
- **Change handlers fire during InitializeComponent.** A text box's initial `Text` and a checked
  radio button raise their events before later controls exist, so option handlers return until
  the window is loaded.
- **Movement sheets use inline strings.** The sample ships a `sharedStrings` part but writes its
  data as `inlineStr`. The reader handles both; do not assume either. See
  [docs/movement-sheet-notes.md](../docs/movement-sheet-notes.md).

## Persisted data and compatibility

There is no database, so there are no migrations. What persists is plain text under
`%APPDATA%\Delay Reporter\`: `settings.txt`, one file per saved preset, and the mapping CSVs. Treat
their formats as a contract with every copy already on a user's machine. A new setting needs a
default for files that lack it, a new CSV column must be optional (a copy written before 0.3.0 has
no `category` column), and a seed change never reaches a user whose copy already exists.

## Building and testing

Building needs Windows and the .NET SDK; the WPF project does not build on Linux or macOS. There is
no test framework, matching the sibling projects:

```powershell
dotnet build src/DelayReporter/DelayReporter.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
```

`tools/test-core.ps1` loads the built executable and drives `Core` directly against
`samples/demo-movement-sheet.csv`, which is constructed to exercise the awkward cases: three-code
pairing, a midnight rollover, a code/duration count mismatch, a coded/actual mismatch, an unmapped
code, an unmapped aircraft type, supplementary prompts, ground runs, tows, an arrival, another
station's departure and the totals footer. Add a check there for any change to `Core`.
`tools/check-repo.ps1` checks hygiene: no binaries, no real operator codes in the sample, and
version numbers in step across the project file, manifest, README, changelog and release notes.

An agent without Windows cannot run either script. Push the change to a branch and run the **CI**
workflow against it instead (Actions > CI > Run workflow, naming the branch, or a
`workflow_dispatch` through the GitHub API with input `ref`): it builds and runs both on a Windows
runner, and its result is the verification. A push alone runs nothing, because the workflows live
on the `workflows` branch rather than beside the code. Neither the scripts nor CI can drive the
window or open a workbook in Excel, so say plainly when a change has not been looked at in the
running app.

## Git and releases

- `main` holds the application and is the branch releases come from. `workflows` holds only the
  GitHub Actions workflows and is the default branch; never merge one into the other, and never add
  `.github/` to `main`. Work on a short-lived branch and merge it to `main` once CI is green.
- Never commit binaries, build output (`bin/`, `obj/`, `dist/`), workbooks or real movement sheets.
  Text files are stored with LF endings, except `*.csv` and `*.ps1` (CRLF), as `.gitattributes`
  sets.
- The **Release** workflow (nightly, or run by hand) publishes `main` as release `v<version>`
  whenever `main`'s `<Version>` has no release yet. Bumping the version is therefore the release
  decision: follow
  [docs/releasing.md](../docs/releasing.md) for every field that must move with it.
- Record anything a user would notice in [CHANGELOG.md](../CHANGELOG.md), and anything worth
  explaining in [docs/decisions.md](../docs/decisions.md), keeping past entries intact.

## Safety

- Help > Reset sends `%APPDATA%\Delay Reporter` to the Recycle Bin. Never widen what it removes,
  and never write outside that folder and the file the user chose to save.
- Email is only ever drafted. Nothing in the application sends mail, and nothing should.
- Keep credentials, tokens, real addresses and real operational data out of code, samples, tests
  and documentation. The operator codes `tools/check-repo.ps1` blocks in the sample are real
  carriers; the sample uses fictional `ZZ` codes instead.

## Style and documentation

Match the surrounding code and the sibling repositories: explanatory comments where a decision is
not obvious from the code, none where it is. Prose in documentation, not bullet fragments. British
spelling in prose; American spelling stays where it is already in code identifiers. When behaviour
changes, update the README section that describes it in the same change.
[CONTRIBUTING.md](../CONTRIBUTING.md) lists what the project will not accept.
