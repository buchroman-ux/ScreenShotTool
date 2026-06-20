# Contributing

Thank you for helping improve ScreenShotTool.

## Development Setup

Requirements:

- Windows
- .NET 8 SDK

Build from the repository root:

```powershell
dotnet build .\ScreenShotTool\ScreenShotTool.csproj
dotnet build .\ScreenShotTool.Installer\ScreenShotTool.Installer.csproj
```

Build the installer:

```powershell
.\ScreenShotTool\Installer\build-installer.ps1
```

## Pull Requests

- Keep changes focused.
- Update README or help text when behavior changes.
- Run a build before opening a pull request.
- Do not commit generated `bin`, `obj`, `artifacts`, payload zip, or setup exe files.

Installer executables should be published as GitHub Release assets, not committed into source history.
