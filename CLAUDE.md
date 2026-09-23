# Working in this repository

Delay Reporter parses movement sheets and writes a printable departure-delay workbook. It is the
third tool in the family started by ICS Scrubber and OFT Scrubber, and follows their conventions.

## Dynamic Model Routing Instructions

Analyze incoming user tasks and categorize their complexity before proceeding. Optimize for speed, capability, and token cost by adhering strictly to this routing logic:

1. USE HAIKU FOR:
- Single-file quick edits, typos, and formatting fixes.
- Adding simple comments or docstrings.
- Generating file structure summaries or fast file lookups.
- Low-complexity, localized utility functions.

2. USE SONNET FOR:
- General feature development and standard day-to-day coding.
- Writing unit tests and standard bug fixes.
- Refactoring well-defined, single or dual-file components.
- General explanations and technical writing.

3. USE OPUS FOR:
- Complex system architecture and high-level design decisions.
- Tracing subtle, multi-file bugs or deep performance bottlenecks.
- Database migrations, security audits, and framework upgrades.
- Tasks requiring heavy sustained reasoning across extensive contexts.

If working in an environment supporting subagent routing (such as [Claude Code Subagent Routing](https://medium.com/@roanmonteiro/claude-code-subagent-model-routing-stop-paying-for-opus-on-haiku-work-ee76dc32cb88)), automatically assign subagents the minimum viable model tier matching the criteria above.


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
  Email/         the compact email and the .eml draft writer
src/DelayReporter/           WPF shell: window, views, controls, themes
  Themes/        Fluent.xaml and Shell.xaml match the siblings byte for byte; Report.xaml is ours
tools/                       PowerShell checks, in place of a test framework
docs/                        architecture, decisions, format notes, release process
```

`Core` never references WPF. The UI binds to `ReportOptions` and displays the `ReportModel` that
will be written, so the preview and the saved workbook cannot disagree.

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
  report says (columns, summary, stacking) belongs in both, or in neither.
- **Cell text is wrapped by us, not by Excel.** The Code, Reason and Duration columns must stay
  aligned line for line, so a wrapped reason gets matching blank lines in the other two. Row height
  is then computed from the line count. Letting Excel wrap breaks the alignment.
- **Delay codes are zero-padded in the sheet but not in the code list.** `09` and `9` are the same
  code. All lookups go through `MappingTable.Normalize`.
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
  data as `inlineStr`. The reader handles both; do not assume either.

## Verification

There is no test framework, matching the sibling projects. `tools/test-core.ps1` loads the built
executable and drives `Core` directly against `samples/demo-movement-sheet.csv`, which is
constructed to exercise the awkward cases: three-code pairing, a midnight rollover, a code/duration
count mismatch, a coded/actual mismatch, an unmapped code, an unmapped aircraft type, supplementary
prompts, ground runs, tows, an arrival, another station's departure and the totals footer.

`tools/check-repo.ps1` checks hygiene: no binaries, no real operator codes in the sample, version
numbers in step across the project file, manifest, README, changelog and release notes.

Run both before a release, plus a real build and a look at the generated workbook in Excel.

## Style

Match the surrounding code and the sibling repositories: explanatory comments where a decision is
not obvious from the code, none where it is. Prose in documentation, not bullet fragments. British
spelling in prose; American spelling stays where it is already in code identifiers.
