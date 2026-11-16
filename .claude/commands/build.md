---
description: Build the add-in with MSBuild and report the real outcome
argument-hint: [rebuild]
---

Build the add-in and report honestly. Follow these steps.

## 1. Run MSBuild

With the full Visual Studio MSBuild, not `dotnet build` (target `Rebuild` instead of `Build`
when `$ARGUMENTS` contains `rebuild`). `CLAUDE.local.md`, when present, says where MSBuild and
the repo live on this machine:

```
MSBuild.exe GladosSwAddin\GladosSwAddin.csproj /t:Build /p:Configuration=Debug /nologo /v:m
```

## 2. Interpret the output

- `GladosSwAddin -> ...\bin\Debug\GladosSwAddin.dll` means the compile succeeded.
- The trailing `MSB3216` COM-registration access-denied error
  (`HKEY_CLASSES_ROOT\GladosSwAddin.SwAddin`) is EXPECTED without admin rights — it is not
  a build failure and must not be "fixed". Registration is done by the user in an elevated
  Windows shell.
- Any compiler error (`CS....`) or new warning is a real failure: fix it and build again
  until the compile is clean.

## 3. Report

State whether the compile was clean (errors/warnings, DLL produced), quoting the relevant
lines — never summarize a failed build as success. When pure logic changed, also build and
run `GladosSwAddin.Tests` (commands in CLAUDE.md) and quote the pass/fail line; everything
touching the SolidWorks API still needs the manual checks in SolidWorks.
