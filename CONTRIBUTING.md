# Contributing to ScreenShotTool

Thank you for your interest in improving ScreenShotTool.

This project is open source under the MIT License. You may fork, copy, modify, and reuse the project according to the license.

## Development Setup

Requirements:

- Windows
- .NET 8 SDK

Build from the repository root:

```powershell
dotnet restore
dotnet build -c Release
```

Build the installer:

```powershell
.\ScreenShotTool\Installer\build-installer.ps1
```

If tests exist:

```powershell
dotnet test
```

## How to Contribute

1. Fork the repository.
2. Create a new branch for your change.
3. Make a focused change.
4. Test the app.
5. Open a pull request.

## Contribution Rules

Keep pull requests focused and easy to review.

Good contributions include:

- bug fixes
- small UI polish
- documentation improvements
- installer/release improvements
- safe performance improvements
- tests

Avoid:

- unrelated large rewrites
- adding secrets, tokens, certificates, or local paths
- committing build output folders
- committing installer executables
- committing personal files
- changing the license without discussion
- replacing the app design without discussion

## Security-Sensitive Changes

Do not open a public pull request that exposes a vulnerability in detail.

Use the process in [SECURITY.md](SECURITY.md).

## Official Repository Control

Contributors can submit pull requests, but only the repository owner or approved maintainers can merge changes into the official repository.

Installer executables should be published as GitHub Release assets, not committed into source history.
