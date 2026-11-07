# Builds - Auto Versioning

## Automatic versions and releases

`.github/workflows/version-and-release.yml` runs after a pull request is merged
into `develop` or `master`. Closing a PR without merging it, or pushing directly
to a branch, does not increment the version.

| Merged into | Version update | Example | Published output |
| --- | --- | --- | --- |
| `develop` | Increment Build | `0.0.2.10` → `0.0.2.11` | Version commit only |
| `master` | Increment Revision and reset Build to 0 | `0.0.2.11` → `0.0.3.0` | Version commit, `v0.0.3.0` tag, and GitHub Release |

The version commit updates the shared `VersionPrefix`. Master releases have no
prerelease suffix. Major and Minor remain manual decisions. When a branch has
an older Revision than an existing release tag with the same Major/Minor, the
next bump starts from the newer release version. Previous version commits also
prevent a merge of stale version metadata from moving that branch backward.

On `master`, the workflow builds and tests the tagged commit, then publishes
generated release notes and `zero-v{Major}.{Minor}.{Revision}.{Build}-win-x64.zip`.
The ZIP contains the native `zcc.exe` and `samples` folder. A failed build/test
leaves the version commit and tag in place but does not create the Release;
rerun the failed workflow to retry. Reruns reuse the same PR's version and tag,
and an existing Release's ZIP is replaced rather than creating another Release.

Competing merge runs use retrying fast-forward pushes; master's tag and version
commit are pushed atomically. Runs are not grouped with Actions concurrency,
because its pending-run replacement can drop a merge event. Each version
component is limited to 65534 for compatibility with .NET assembly versions.

### Repository setup

1. Merge the workflow and its scripts into the default branch and retain them
   on both `develop` and `master`. The `pull_request_target` trigger uses trusted
   base-branch workflow code, and the job checks out the merged base branch.
2. Allow the workflow's `contents: write` permission. The default `GITHUB_TOKEN`
   is sufficient when the target branch and version tags allow workflow pushes.
3. If branch/tag rules require a specific bypass identity, configure that identity
   and store its repository-write token as the optional `VERSION_TOKEN` Actions
   secret. Both checkout/push and Release creation use this token when supplied.
   A token alone does not bypass branch protection or required checks.

The native build is part of this workflow because pushes and Releases created
with `GITHUB_TOKEN` do not trigger additional workflows automatically.

Run the automation tests locally with:

```powershell
pwsh -NoProfile -File .github/scripts/test-version.ps1
```

These tests use disposable local repositories and never push to GitHub.
The scripts require PowerShell 7.2 or later and Git. The workflow uses PowerShell
for checkout, SDK installation, version updates, testing, and release packaging;
it has no Node.js scripts or JavaScript-based action dependencies.
