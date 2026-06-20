# ScreenShotTool

ScreenShotTool is a Windows screenshot utility.

This repository is intended to be the clean open-source version of the project. The original private development repository is kept separate so old history, local files, and private development notes are not exposed.

> Status: private staging repository. Do not make public until the clean source copy, security scan, and build verification are complete.

## Goals

- Provide a simple Windows screenshot tool.
- Keep the official source repository clean and safe for public use.
- Allow anyone to copy, modify, fork, and reuse the project under the MIT License.
- Keep write access to the official repository controlled by the owner.

## License

This project is licensed under the MIT License. See [`LICENSE`](LICENSE).

The MIT License allows people to use, copy, modify, merge, publish, distribute, sublicense, and sell copies of the software, as long as the copyright and license notice are included.

## Repository safety model

People may:

- view the source code after the repository becomes public
- download the project
- fork the project
- copy the files
- modify their own copy
- submit pull requests

People may not:

- push directly to this official repository unless the owner gives them write access
- change the official `main` branch unless repository permissions/rules allow it
- delete or control the official repository

## Public release plan

Before making this repository public:

1. Copy only clean project files into this repository.
2. Do not copy the old `.git` folder or private commit history.
3. Run local secret scans.
4. Build and run the app from this clean repository.
5. Confirm no local paths, personal files, certificates, or tokens are included.
6. Protect the `main` branch.
7. Change repository visibility to public.
8. Create a GitHub Release with installer and checksum.

## Verify installer checksum

After a release installer is uploaded, verify it with PowerShell:

```powershell
Get-FileHash .\ScreenShotTool-Setup.exe -Algorithm SHA256
```

Compare the output with `SHA256SUMS.txt` from the same release.

## Development status

Initial clean open-source staging repository created.

Source files are not expected to be public until the clean-copy and security-review process is complete.
