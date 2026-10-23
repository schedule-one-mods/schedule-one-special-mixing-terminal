# Local automated releases

## One version to edit

`Directory.Build.props` is the release version source. Use stable semantic versions:
patch for fixes, minor for features, major for intentionally incompatible changes.
`Directory.Build.targets` generates the MelonInfo constant from that value; the
assembly, package manifest, ZIP filename and GitHub tag use the same version.
Do not hardcode another version in Mod.cs or the project file.

## Publish

Requirements: PowerShell 7, Git, a compatible .NET SDK, the game's generated
MelonLoader IL2CPP references, and a GitHub login in your configured Git credential
helper with push/release access. A process-scoped GH_TOKEN or GITHUB_TOKEN also works.
No GitHub CLI, Python, paid runner, always-on service or manually copied token is required.

1. Work on `main` and bump `Directory.Build.props`.
2. Add that version's entry to `packaging/CHANGELOG.md`; update compatibility claims
   only when observed in-game. Stage the intended source files with `git add`.
3. Run:

   ```powershell
   ./publish.ps1 -CommitMessage "Release the next version"
   ```

This commits the staged changes, builds and validates a production-only ZIP and
SHA-256 sidecar, atomically pushes the exact source commit and version tag, creates
a draft GitHub Release, uploads both assets, verifies their server digests, publishes
it as Latest, and checks the public ZIP's SHA-256. A failed build never pushes a
release. The commit remains available to fix and retry. Source must be clean for
publication; newly added files must be staged intentionally. No automatic git add
is performed.

If already committed (or committed elsewhere), first pull/merge that source onto
local `main`, then run `./publish.ps1`. The script does not automatically pull or
merge source. It does not need the version bump in the immediately preceding
commit: any committed, unpublished version newer than Latest can be published.
Code pushed elsewhere cannot compile itself using this PC while the PC is offline.
Run the command later on a machine with the build references.

For a different game location or SDK:

```powershell
./publish.ps1 -GameDir 'Q:\Games\Schedule I' -Dotnet 'C:\Path\To\dotnet.exe'
```

For a build/package check without a commit, credential lookup, push, upload, tag
or release request:

```powershell
./publish.ps1 -PrepareOnly
```

This also works with uncommitted source. Candidate packages are under
`artifacts/release-candidates/`, separate from existing release ZIPs. Published
assets and tags are never replaced. A retry uses the same verified candidate for
the exact source commit and resumes an existing draft; mismatched draft assets
are preserved and reported. After publication, that version is immutable.

## Optional automatic post-commit hook

```powershell
./scripts/install-release-hook.ps1
```

This enables `.githooks/post-commit` **only in the current clone**. On `main`, a
version increase relative to the first parent runs the publisher automatically.
Other branches and commits with the same version do nothing. Installing the
central version file for the first time does not trigger a release. Decreasing
the version is rejected. Amendments must not change a published version's content;
use another version for a changed release.

You can persist machine-local build paths when installing:

```powershell
./scripts/install-release-hook.ps1 -GameDir 'Q:\Games\Schedule I' -Dotnet 'C:\Path\To\dotnet.exe'
```

The paths and PowerShell executable are stored in local Git config, not committed.
The same paths can be set with `scheduleone.gameDir` and `scheduleone.dotnet` local
Git config, or SCHEDULE_I_GAME_DIR and SCHEDULE_ONE_DOTNET environment variables.
Explicit command arguments take precedence. Existing custom hook configurations
are preserved; the installer refuses to replace them.

Disable automatic publishing with:

```powershell
./scripts/install-release-hook.ps1 -Remove
```

A pre-commit hook cannot publish the final source commit, so publishing runs after
commit creation. Failure never erases or rolls back that commit. A process guard
prevents recursive publication when publish.ps1 itself creates a commit. Concurrent
publish commands for one clone are rejected. Credentials are captured in memory
from the same Git helper used by git push, never printed, saved or passed in process
arguments. The helper is noninteractive; expired/missing logins produce an error.

## Shared tooling and Mod Manager

The same publisher, version-generation target, wrapper and hook installer are
vendored in Special Mixing Terminal, Businesses and Deal Locations. Each clone
is standalone; it never depends on a sibling checkout. `scripts/release-config.json`
contains its package identity and exact public-file allowlist. When updating shared
release tooling, synchronize those shared files across the three repositories.

The package scripts only read installed game references. They do not deploy mods,
launch/close the game, edit saves, include the private test editor, or distribute
game assemblies. New releases retain `packaging/compatibility.json` at the exact
release tag. Registry schema v2 discovers tagged GitHub Releases automatically;
a normal version bump does not require another registry PR.
