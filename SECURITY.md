# Security Policy

## Supported Versions

Only the latest official release and the latest code on `main` are supported for security review.

| Version | Supported |
| --- | --- |
| Latest release | Yes |
| `main` | Yes |
| Older releases | No |

## Reporting a Vulnerability

Do not publish sensitive security details publicly before they are reviewed.

Preferred reporting options:

1. Use GitHub private vulnerability reporting if it is enabled for this repository.
2. Contact the project owner privately.
3. If no private channel is available, open a GitHub issue with only a short non-sensitive summary and ask for a private contact path.

Do not include passwords, tokens, certificates, private screenshots, exploit code, or sensitive local paths in a public issue.

When reporting, include:

- A clear description of the issue
- Steps to reproduce it
- The affected version or commit
- Any relevant screenshots, logs, or proof of concept details that are safe to share privately

## Scope

In scope:

- installer tampering concerns
- executable integrity concerns
- unsafe file handling
- privacy-related bugs
- unexpected network behavior
- permission or startup behavior concerns
- private data accidentally included in a public release
- screenshot capture, clipboard, startup registration, and local file handling behavior

Out of scope:

- modified forks not maintained by the owner
- third-party reuploads
- unsupported old releases
- issues caused by bypassing operating-system warnings or security tools

## Download Verification

Public release assets should include a SHA256 checksum file.

PowerShell verification:

```powershell
Get-FileHash .\ScreenShotToolSetup.exe -Algorithm SHA256
```

The result should match the checksum published with the same release.

## Private-Data Policy

Public releases should not include:

- passwords
- API keys
- tokens
- signing certificates
- private keys
- local user folders
- personal notes
- build machine paths
- unpublished private development history
