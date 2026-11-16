# Security policy

## Reporting a vulnerability

Please **do not** open a public issue for security problems. Report them privately through
GitHub: **Security → Report a vulnerability** on this repository. You will get an
acknowledgement within a few working days, and a fix or mitigation plan once the report is
confirmed.

Useful details: the add-in version (listed in *Apps & features*, and logged each time
SOLIDWORKS loads the add-in), the SOLIDWORKS version, and steps to reproduce. Never
include a real personal access token - revoke any token that may have been exposed.

## Supported versions

Only the latest release receives security fixes.

## Scope notes

- The add-in stores the GLADOS personal access token in the Windows Credential Manager (per
  user) and sends it only to the configured server. A non-HTTPS server URL triggers a warning,
  because the token would then travel unencrypted.
- The diagnostics log (`%LOCALAPPDATA%\GladosSwAddin\logs`) never contains the token.
- Vulnerabilities in the GLADOS server itself belong to that project, not this one.
