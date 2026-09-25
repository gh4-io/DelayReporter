# Contributing

## Building

Windows with the .NET SDK installed. There is no solution file and no runtime package to restore.

```powershell
dotnet build src/DelayReporter/DelayReporter.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
```

Run all three before proposing a change, and open the generated workbook in Excel to look at it.
A workbook that opens is not the same as a workbook that prints well.

## What this project will not accept

The constraints in [.agents/rules.md](.agents/rules.md) are the point of the tool, not incidental:

- No runtime NuGet packages. The executable must stay standalone and must not require
  administrator rights or an installed runtime.
- No writes outside `%APPDATA%\Delay Reporter\` and the file the user chooses to save.
- No overwriting the user's mapping files.
- No real operational data in the repository. `samples/` stays synthetic.

A change that needs one of these relaxed is a discussion first, not a pull request.

## Conventions

- `Core` never references WPF.
- Comment the decisions that are not obvious from the code; leave the obvious uncommented.
- Keep the seeded mapping CSVs a faithful transcription of their source document. Corrections to
  the published codes belong in the user's own file, not in the seed.
- Update [CHANGELOG.md](CHANGELOG.md) with anything a user would notice.

## Releasing

See [docs/releasing.md](docs/releasing.md).
