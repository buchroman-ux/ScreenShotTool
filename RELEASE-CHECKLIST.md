# Release Checklist

Release checklist for public ScreenShotTool releases.

## v1.0.0 Public Release

Status: completed.

### Repository

- [x] README is present.
- [x] MIT License is present.
- [x] CHANGELOG is present.
- [x] SECURITY policy is present.
- [x] CONTRIBUTING guide is present.
- [x] `.gitignore` blocks build outputs, local files, secrets, and installer binaries.
- [x] Source tree was checked for obvious private/dev files.

### Build

- [x] `dotnet restore` completed successfully.
- [x] `dotnet build -c Release` completed successfully.
- [x] Release build completed with 0 warnings and 0 errors.
- [x] Installer build completed successfully.

### Installer

- [x] Final release installer selected: `ScreenShotToolSetup.exe`.
- [x] Stale/root installer copy was not used.
- [x] Installer was manually tested on Windows.
- [x] Install flow was manually tested.
- [x] App launch was manually tested.
- [x] Screenshot/editor workflow was manually tested.
- [x] Uninstall flow was manually tested.

### Release Assets

- [x] `ScreenShotToolSetup.exe` uploaded to GitHub Release.
- [x] `SHA256SUMS.txt` uploaded to GitHub Release.
- [x] SHA256 checksum matches the selected installer.
- [x] Installer binary is not committed into source.

### Security / Privacy

- [x] Secret scan completed with no leaks found.
- [x] No obvious tokens, passwords, private keys, `.env` files, or certificates found.
- [x] No obvious private local paths or personal files found.
- [x] GitHub branch protection/ruleset enabled.
- [x] GitHub security features reviewed/enabled where available.

## Future Releases

Before publishing a future release:

- [ ] Build from clean `main`.
- [ ] Run `dotnet restore`.
- [ ] Run `dotnet build -c Release`.
- [ ] Build installer.
- [ ] Manually test install/app/uninstall.
- [ ] Generate new SHA256 checksum.
- [ ] Update `CHANGELOG.md`.
- [ ] Upload installer and `SHA256SUMS.txt`.
- [ ] Verify release notes and assets before publishing.
