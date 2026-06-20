# Release Checklist

Use this checklist before making the repository public or publishing a release.

## Repository setup

- [ ] Repository is the clean open-source repository, not the old private development repository
- [ ] Repository is private while staging
- [ ] Old `.git` history was not copied
- [ ] MIT License exists
- [ ] README exists
- [ ] Security policy exists
- [ ] Contribution guide exists
- [ ] Changelog exists
- [ ] `.gitignore` exists

## Clean source copy

- [ ] Copy only required source files
- [ ] Do not copy `.git`
- [ ] Do not copy `.vs`
- [ ] Do not copy `bin`
- [ ] Do not copy `obj`
- [ ] Do not copy `artifacts`
- [ ] Do not copy `publish`
- [ ] Do not copy installer output unless intentionally added as a release asset
- [ ] Do not copy `.codex-security-scans`
- [ ] Do not copy `graphify-out`
- [ ] Do not copy personal notes unless intentionally public

## Secret and privacy scan

Run from the clean repository folder:

```powershell
rg -n --hidden -S "password|passwd|secret|token|api[_-]?key|client_secret|private_key|BEGIN .*PRIVATE KEY|GITHUB_TOKEN|OPENAI|ANTHROPIC|GEMINI|C:\\Users\\|buchr|AppData|Desktop|Documents|Downloads|\.pfx|\.p12|\.pem|\.env" .
```

Run Gitleaks:

```powershell
gitleaks detect --source . --verbose --redact
```

Required result:

- [ ] No real secrets found
- [ ] No certificates/private keys found
- [ ] No private local paths found in public files
- [ ] No personal files found

## Build verification

Run:

```powershell
dotnet restore
dotnet build
```

Required result:

- [ ] Restore succeeds
- [ ] Build succeeds
- [ ] App launches
- [ ] Basic screenshot workflow works
- [ ] Help/about information works
- [ ] Installer builds, if included

## Release asset verification

- [ ] Installer file is the intended final build
- [ ] Installer launches correctly
- [ ] App installs correctly
- [ ] App uninstalls correctly
- [ ] No debug-only files are packaged
- [ ] No private files are packaged

Generate checksum:

```powershell
Get-FileHash .\ScreenShotToolSetup.exe -Algorithm SHA256
```

Create `SHA256SUMS.txt`:

```text
<sha256-hash>  ScreenShotToolSetup.exe
```

## GitHub protection before public release

- [ ] Protect `main`
- [ ] Block force pushes
- [ ] Block branch deletion
- [ ] Require pull request before merge, if desired
- [ ] Confirm no unknown collaborators have write access

## Make public

Only after all checks pass:

- [ ] Change repository visibility from private to public
- [ ] Create GitHub Release
- [ ] Upload installer
- [ ] Upload `SHA256SUMS.txt`
- [ ] Confirm release download works
- [ ] Confirm checksum matches after download
