<!--
AI AGENT ENTRYPOINT

This branch holds only the GitHub Actions workflows. The application and its
instructions are on the main branch; continue by reading, on main:

.agents/rules.md

Treat that file as the repository's agent-neutral instruction entry point.
-->

# Delay Reporter: workflows

This branch holds only the GitHub Actions workflows that build, test and release Delay Reporter.
**The application, its documentation and its releases are on the
[`main`](https://github.com/gh4-io/DelayReporter/tree/main) branch**; start with its
[README](https://github.com/gh4-io/DelayReporter/blob/main/README.md). Download the application
from [Releases](https://github.com/gh4-io/DelayReporter/releases/latest).

Keeping the workflows apart leaves `main` holding the application alone. GitHub only runs
scheduled workflows, and only offers **Run workflow**, from the default branch, so this branch is
the repository's default branch.

| Workflow | Runs | What it does |
|---|---|---|
| **CI** (`.github/workflows/ci.yml`) | By hand: Actions > CI > Run workflow, with the branch, tag or commit to test (`main` by default). | Builds `DelayReporter.exe` on Windows, runs `tools/test-core.ps1`, `tools/check-repo.ps1` and the version field check, and keeps the tested executable as the run's `DelayReporter` artifact for 30 days. |
| **Release** (`.github/workflows/release.yml`) | Nightly at 03:17 UTC, and by hand: Actions > Release > Run workflow. | Reads `<Version>` from `main`. When that version has no release yet, runs CI on that exact commit and publishes the tested executable as release `v<version>` with its SHA-256 hash and `docs/releases/v<version>.md` as the notes. Otherwise it stops after the check. |

A push to `main` starts nothing by itself: a workflow only runs on a push to a branch whose own
commits hold the workflow file. To release, bump the version on `main` as
[docs/releasing.md](https://github.com/gh4-io/DelayReporter/blob/main/docs/releasing.md)
describes, then run Release, or leave it for the nightly run.

GitHub pauses scheduled workflows in a repository with no activity for 60 days; Actions shows a
banner to re-enable them, and Run workflow always works.

<!--
AI AGENT CONTINUE

If you intend to change these workflows, read .agents/rules.md on main first:
the application's build and check steps they run are defined there.
-->
