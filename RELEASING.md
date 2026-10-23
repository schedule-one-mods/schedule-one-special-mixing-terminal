# Release procedure

1. Update only `Directory.Build.props` for the version, plus packaging/CHANGELOG.md. MelonInfo and the assembly version are generated from that value.
2. Record only observed compatibility in packaging/compatibility.json. Native assignment checks do not substitute for full production or Mod Manager acceptance.
3. Run package.ps1 against local game references. It validates the compiled identity, rejects development-tool references/types, and creates a ZIP with an explicit production file list and SHA-256 sidecar.
4. Review and commit source. Keep devtools/, playtest-devtools.ps1, artifacts/, local saves and generated game references untracked. Never upload game assemblies.
5. Run `publish.ps1` to build, atomically push source/tag, upload a draft and publish the verified ZIP/checksum as Latest. See PUBLISHING.md for commit handling, retries and the optional version-aware post-commit hook. Never replace a published asset in place.
6. Download the public ZIP and check its size, hash, manifest, compatibility and exact file list against the packaged bytes.
7. Registry schema v2 registers special-mixing-terminal / SpecialMixingTerminal once. Keep packaging/compatibility.json in the release tag and mark the intended stable GitHub Release Latest. The manager discovers each tagged release and its asset digest directly; subsequent versions need no registry PR or discovery Actions job. Registry PRs are only for identity, repository, display metadata or discovery policy changes.
8. The user completes installation and production acceptance through Mod Manager. Update compatibility claims only after those checks.

The production source repository and release exclude the private local test editor and launcher. Existing local development files remain on the developer's machine.

## Automated publishing

Use [PUBLISHING.md](PUBLISHING.md) for the bundled local publisher and optional version-aware post-commit hook. The publisher uses the existing Git credential helper, validates exact package contents, preserves published versions and never deploys or launches the game.
