# Release procedure

1. Update the project Version, MelonInfo version and packaging/CHANGELOG.md together.
2. Record only observed compatibility in packaging/compatibility.json. Native assignment checks do not substitute for full production or Mod Manager acceptance.
3. Run package.ps1 against local game references. It validates the compiled identity, rejects development-tool references/types, and creates a ZIP with an explicit production file list and SHA-256 sidecar.
4. Review and commit source. Keep devtools/, playtest-devtools.ps1, artifacts/, local saves and generated game references untracked. Never upload game assemblies.
5. Create a public release tagged v<version> at the exact source commit. Upload SpecialMixingTerminal-<version>-IL2CPP.zip and its .sha256 file. Never replace a published asset in place.
6. Download the public ZIP and check its size, hash, manifest, compatibility and exact file list against the packaged bytes.
7. Submit a PR to schedule-one-mods/schedule-one-mod-registry for stable identity special-mixing-terminal / SpecialMixingTerminal. Bind the exact versioned URL, byte size, SHA-256 and compatibility subset. Validate the registry metadata before merging.
8. The user completes installation and production acceptance through Mod Manager. Update compatibility claims only after those checks.

The production source repository and release exclude the private local test editor and launcher. Existing local development files remain on the developer's machine.
