---
description: Run a read-only security audit of the add-in
argument-hint: [scope, e.g. credentials | transport | server-data | local-files | installer | ci | dependencies | a path]
---

Run a READ-ONLY security audit of the add-in (`GladosSwAddin/`, `GladosSwAddin.Installer/`,
`.github/workflows/`, `scripts/` as relevant to scope). Do not modify any source file. Do not
commit anything. Use local tools only; never send source code, diffs, or file contents to any
external service.

This audits the whole repo. To review only the pending changes on the current branch, use the
built-in `/security-review` skill instead; it is the lighter tool for that job.

Threat model: the add-in runs inside SOLIDWORKS with the engineer's Windows rights, holds a
Glados personal access token (PAT), talks to one configured Glados server, and reads and
writes SOLIDWORKS documents that may come from customers or suppliers. Attackers to consider:
someone who controls or impersonates the configured server (or sits on the network path),
someone who supplies a crafted SOLIDWORKS/STEP file, another local user on a shared machine,
and anyone who can influence the build or release pipeline. Vulnerabilities in the Glados
server itself are out of scope (`SECURITY.md`), but note where the add-in relies on a
server-side check it cannot verify.

## Scope

Arguments given: `$ARGUMENTS`

- If `$ARGUMENTS` is empty, run a full audit across all dimensions.
- If `$ARGUMENTS` names a dimension slug (see list below), audit only that dimension.
- If `$ARGUMENTS` is `dependencies`, audit dependency advisories only (skip the other
  dimensions).
- Otherwise treat `$ARGUMENTS` as a path: audit all applicable dimensions but restrict
  findings to that path.

## Dimensions

- **[credentials] PAT handling**: `Services/CredentialService.cs` (advapi32 `Cred*` P/Invoke):
  correct marshalling, `CredFree` on every path, persistence type and target name, no copy of
  the PAT left in long-lived strings beyond what's needed. The PAT must never reach the log
  (`Services/Log.cs`, all `Log.*` call sites), exception messages, the lookup cache, the
  settings file (`Services/SettingsService.cs`), window titles, or the clipboard. Check that the
  settings form masks the token and that a server URL change cannot send the stored PAT to the
  new server before the user confirms it.
- **[transport] HTTP and TLS**: `Api/GladosApiClient.cs`: certificate validation is never
  disabled (`ServerCertificateValidationCallback`, `ServerCertificateCustomValidationCallback`),
  timeouts are set, the `Authorization` header is only ever sent to the configured server's
  origin (check absolute URLs built from server responses, paging links, and redirect
  behaviour of the handler), and the idempotency-key POST retry cannot resend to a different
  host. Server-URL validation in `Forms/SettingsForm.cs`.
- **[server-data] Trusting server responses**: Newtonsoft settings (`TypeNameHandling` must
  stay `None`; no `$type` honoured anywhere), unbounded response sizes or paging loops a
  malicious server could make endless (`Api/PagingHelper.cs`), server strings written into
  custom properties: could a value be interpreted by SOLIDWORKS as a linked expression
  (`$PRP:`, `"SW-..."`, `=`) and leak or rewrite other properties (README "Linked
  expressions on Save")? Server-supplied URLs opened with `Process.Start` (every call site must allow
  only `http`/`https`, never `file:`, UNC paths or other shell-executable schemes), and
  server-supplied names used as file names or paths.
- **[documents] Crafted input files**: values read from SOLIDWORKS custom properties and
  configurations are untrusted. Check parsing (routing in `Glados.Processes`, dimension specs,
  part numbers) for crashes, unbounded work, or values that flow into API query strings without
  encoding (`Api/QueryStringBuilder.cs`), file paths, or `Process.Start`.
- **[local-files] File system**: the lookup cache under `%LOCALAPPDATA%\GladosSwAddin\cache\`
  (atomic write, file name derived from the server URL must be sanitised, no path traversal,
  safe handling of a corrupt or attacker-planted cache file), the log folder, the
  `%TEMP%\GladosSwAddin\ImportedComponents\` work folders in
  `Services/ImportedComponentsService.cs` (cleanup must never delete outside its own folder;
  predictable paths another local user could pre-create), the `explorer.exe` launch in
  `SettingsForm`, and PDM check-ins in `Services/PdmCheckInService.cs` (only the intended files
  are checked in, on the user's behalf).
- **[installer] COM registration and install**: `GladosSwAddin.Installer/Package.wxs`
  (per-machine install): install directory ACLs, registry keys written under HKLM/HKCR,
  `CodeBase` paths, custom actions and what they run with elevation, upgrade/uninstall leaving
  writable leftovers. In the add-in: native library loading or DLL search-order exposure, and
  resource loading (`Helpers/FontManager.cs`) from anything other than embedded resources.
- **[ci] Build and release pipeline**: `.github/workflows/*.yml` and `scripts/`: third-party
  actions pinned to a commit SHA, minimal `permissions:`, no `pull_request_target` with a
  checkout of untrusted code, secrets not exposed to forked-PR runs, release artifacts built
  from the tagged commit, `ILRepack.targets` and other MSBuild targets not pulling anything at
  build time beyond NuGet restore, the `gitleaks` CI job still wired.
  Code signing of the DLL/MSI is a known open decision: report its current state, not as a new
  finding.
- **[secrets] Secrets in the repo**: hardcoded tokens, credentials, internal hostnames or
  personal data in source, tests, docs, workflows, `.playground` excluded. If `gitleaks` is
  installed locally, run `gitleaks detect --no-banner --redact` (history) and
  `gitleaks dir --no-banner --redact .` and quote the summary; if it isn't, say so plainly.
- **[errors] Information leakage to the user and to the log**: `GladosApiException` messages
  shown in the UI (server `detail` strings, request URLs, stack traces), and what a support
  log bundle would reveal.
- **[dependencies] Advisories**: NuGet packages in `GladosSwAddin/GladosSwAddin.csproj`,
  `GladosSwAddin.Tests/GladosSwAddin.Tests.csproj` and the WiX extension in
  `GladosSwAddin.Installer/GladosSwAddin.Installer.wixproj`. Run
  `dotnet.exe list <project> package --vulnerable --include-transitive` per project (this sends
  only package IDs and versions to nuget.org, never code) and check restore output for
  `NU1901`-`NU1904`. If SDK resolution fails because of the `global.json` pin, run the command
  from outside the repo directory with the absolute project path; if it still fails, report
  that plainly rather than guessing at results.

## Before starting

Read existing reports in `.audit/security-audit-*` (newest first) and `.audit/ACCEPTED.md` if
it exists. They track residual items and conventions or claims already accepted or rejected;
do not re-raise anything listed there as accepted/rejected.

## Method

1. Fan out parallel read-only Explore (or general-purpose) subagents, one per dimension (or a
   small group of related dimensions) in scope. Each subagent searches and reads only, with no
   edits, and locates code by shape (line numbers go stale fast in this repo).
2. Before reporting any finding, verify it: it must cite a concrete `file:line`, and you must
   re-check that file:line yourself before it goes in the report. Do not report a finding you
   have not personally verified.
3. Cross-check candidate findings against the earlier reports and `.audit/ACCEPTED.md`, and
   drop duplicates (a residual item still open is reported as "still open", not as new).

## Output

Write a report to `.audit/security-audit-<today's date>/report.md` (create the folder; use
`YYYY-MM-DD` for today). Structure:

- Scope line: dimensions covered, commit (`git rev-parse --short HEAD`), branch, and which
  tools ran (dependency check, gitleaks) or were unavailable.
- Findings ranked by severity (critical/high/medium/low).
- Each finding: **Finding** (what, and a concrete attack scenario: who can do what they
  shouldn't), **Files** (file:line references), **Approach** (how it would be fixed),
  **Risk**, **Effort**.
- Describe attack scenarios in prose. Do not write working exploit payloads into the report.
- A "Still open from previous audit" section.

Then send the user a short summary: count of findings by severity and the report path. Do not
include the full report inline unless the user asks.

Hard constraints: this is read-only. Do not edit, create, or delete any source file. Do not
run `git add`/`git commit`. Use only local tools; never transmit code to an external service.
The only files you write are the audit report files.

## Models

Use model `haiku` for the investigation subagents; verification and the report stay with the
main session. Minimize token usage. Don't assume anything.
