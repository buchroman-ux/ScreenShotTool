# ScreenShotTool

Native Windows screenshot capture and annotation utility built with C#/.NET 8 and WPF.

## Download

Prebuilt installer downloads should be published on the GitHub Releases page as `ScreenShotToolSetup.exe`.

The setup executable is intentionally not committed to source history. This keeps the repository lightweight while still letting users download the installer from a release.

## Build

```powershell
dotnet build .\ScreenShotTool\ScreenShotTool.csproj
dotnet publish .\ScreenShotTool\ScreenShotTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

The published app folder is created under:

```text
ScreenShotTool\bin\Release\net8.0-windows\win-x64\publish\
```

## Installer

Build the installer with:

```powershell
.\ScreenShotTool\Installer\build-installer.ps1
```

The installer output is created under:

```text
ScreenShotTool\artifacts\installer\ScreenShotToolSetup.exe
```

The installer uses a per-user install location by default, creates a Start Menu shortcut, and registers the app in Windows Installed apps so it can be uninstalled normally. The wizard lets the user choose the install folder, whether to start with Windows, whether to create a Desktop shortcut, and whether to launch after setup. For unattended install without launch:

```powershell
.\ScreenShotTool\artifacts\installer\ScreenShotToolSetup.exe /silent /no-launch
```

For unattended install with startup and a Desktop shortcut:

```powershell
.\ScreenShotTool\artifacts\installer\ScreenShotToolSetup.exe /silent /startup /desktop
```

For unattended uninstall:

```powershell
%LOCALAPPDATA%\Programs\ScreenShotTool\Uninstall.exe /uninstall /silent
```

An optional Inno Setup script is also included:

```text
ScreenShotTool\Installer\ScreenShotTool.iss
```

If Inno Setup's `ISCC.exe` is installed, `build-installer.ps1` also compiles that standard installer.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## License

ScreenShotTool is released under the [MIT License](LICENSE).

## Shortcuts and tools

- `Ctrl + Shift + S`: default screenshot shortcut. It can be changed in Settings.
- The capture screen freezes the desktop immediately, so short-lived popups can still be selected.
- `Ctrl + Shift + S`: save final PNG, copy it to the clipboard, and close the editor when the editor is open.
- `Esc`: cancel capture or close the editor.
- `Ctrl + Z`: undo the last annotation action.
- `Ctrl + Y`: redo the last undone annotation action.
- `Ctrl + C`: copy selected text while editing text; otherwise copy selected annotation objects.
- `Ctrl + V`: paste text into an active text annotation; otherwise paste copied annotation objects.
- Pencil draws freehand strokes. Newly selected pencil strokes keep their original color and thickness when future pencil color or thickness is changed.

`Ctrl + C` does not copy the final screenshot. Final screenshot save/copy is done with `Ctrl + Shift + S` while the editor is open.

## Screenshots

By default, screenshots are saved to:

```text
%USERPROFILE%\Pictures\Screenshots
```

The folder can be changed in Settings from the tray menu.
