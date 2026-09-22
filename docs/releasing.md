# Releasing

The release number comes from `<Version>` in
[`src/DelayReporter/DelayReporter.csproj`](../src/DelayReporter/DelayReporter.csproj). Three
components: `major.minor.patch`. The first repository release is `0.1.0`.

An ordinary build updates only the UTC build suffix; it does not change the release number. About
shows `<release>+build.<UTC timestamp>` read from the compiled executable, so the running build can
be identified even after the source has moved on.

## When to increment

Fixes only: increment the patch, `0.1.0` to `0.1.1`. The next feature release: increment the minor
and reset the patch, `0.1.1` to `0.2.0`. A major change is an explicit decision. Rebuilding the
same release needs no version change.

## Checklist

1. Update `<Version>` in the project file.
2. Update `<assemblyIdentity version="...">` in
   [`src/DelayReporter/app.manifest`](../src/DelayReporter/app.manifest) to the same number with a
   fourth component of zero: project `0.1.0` means manifest `0.1.0.0`.
3. Update the version in [`README.md`](../README.md), add the release to
   [`CHANGELOG.md`](../CHANGELOG.md), and write `docs/releases/v<version>.md`. Keep past entries
   and decisions intact.
4. Record anything worth explaining in [`decisions.md`](decisions.md).
5. Build from the repository root, and stop if it fails:

   ```powershell
   dotnet build src/DelayReporter/DelayReporter.csproj -c Release
   if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
   ```

6. Run the checks:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-core.ps1
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-repo.ps1
   ```

7. Run the application against a real movement sheet and against
   `samples/demo-movement-sheet.csv`. Open the generated workbook in Excel and check, at minimum:
   the summary figures, that codes and durations line up within each flight row, that no row is
   clipped, that print preview fits one page wide with the summary repeated on page two, and that
   Help > About shows the new release with its build suffix.
8. Copy the verified executable to the ignored local `dist/DelayReporter.exe` and confirm the hash
   matches:

   ```powershell
   New-Item -ItemType Directory -Path dist -Force | Out-Null
   Copy-Item -LiteralPath src/DelayReporter/bin/Release/net48/DelayReporter.exe -Destination dist/DelayReporter.exe -Force
   $built = (Get-FileHash -LiteralPath src/DelayReporter/bin/Release/net48/DelayReporter.exe).Hash
   $dist = (Get-FileHash -LiteralPath dist/DelayReporter.exe).Hash
   if ($built -ne $dist) { throw 'Published executable does not match the verified build.' }
   ```

9. Run `git diff --check`, review the diff, and commit. Keep `dist/` and all binaries out of Git;
   attach the executable to the hosted release separately.

## Verifying the version fields

Run from the repository root after building. It reads the expected release from the project, so no
version number needs maintaining inside the check.

```powershell
$project = [xml](Get-Content -LiteralPath src/DelayReporter/DelayReporter.csproj -Raw)
$release = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$expected = "$release.0"
$manifest = [xml](Get-Content -LiteralPath src/DelayReporter/app.manifest -Raw)
$exe = (Resolve-Path -LiteralPath src/DelayReporter/bin/Release/net48/DelayReporter.exe).Path
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
$assembly = [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString()
$pattern = '^' + [regex]::Escape($release) + '\+build\.\d{8}\.\d{6}Z$'

if ($manifest.assembly.assemblyIdentity.version -ne $expected) { throw 'Manifest version does not match the project.' }
if ($assembly -ne $expected) { throw 'Assembly version does not match the project.' }
if ($info.FileVersion -ne $expected) { throw 'File version does not match the project.' }
if ($info.ProductVersion -notmatch $pattern) { throw 'Product version is missing the release or build timestamp.' }
Write-Output "Verified version: $($info.ProductVersion)"
```

The SDK generates the assembly and file versions from `<Version>`. Do not hand-edit anything under
`obj/`, and do not add a second hardcoded version to the About dialog.

For a repeatable build identifier, pass `-p:BuildTimestamp=20260922.184500Z` in UTC
`yyyyMMdd.HHmmssZ`. Omit it for ordinary builds so the timestamp advances.
