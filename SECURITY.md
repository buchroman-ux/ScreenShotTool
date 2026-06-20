# Security Policy

## Supported versions

Only the latest official release is supported for security review.

| Version | Supported |
| --- | --- |
| Latest release | Yes |
| Older releases | No |

## Reporting a vulnerability

Do not publish sensitive security details publicly before they are reviewed.

Preferred reporting options:

1. Use GitHub private vulnerability reporting if it is enabled for this repository.
2. Contact the project owner privately.
3. If no private channel is available, open a GitHub issue with only a short non-sensitive summary and ask for a private contact path.

Do not include passwords, tokens, certificates, private screenshots, exploit code, or sensitive local paths in a public issue.

## Scope

In scope:

- installer tampering concerns
- executable integrity concerns
- unsafe file handling
- privacy-related bugs
- unexpected network behavior
- permission or startup behavior concerns
- private data accidentally included in a public release

Out of scope:

- modified forks not maintained by the owner
- third-party reuploads
- unsupported old releases
- issues caused by bypassing operating-system warnings or security tools

## Download verification

Public release assets should include a SHA256 checksum file.

PowerShell verification:

```powershell
Get-FileHash .\ScreenShotTool-Setup.exe -Algorithm SHA256
```

The result should match the checksum published with the same release.

## Private-data policy

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
