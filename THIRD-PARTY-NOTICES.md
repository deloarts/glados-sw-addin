# Third-party notices

The GLADOS SOLIDWORKS Add-in ships, embeds or builds against the following third-party
components. Their license texts are in [`licenses/`](licenses/).

## Embedded in `GladosSwAddin.dll`

| Component | Version | Copyright | License |
| --- | --- | --- | --- |
| [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) (merged by ILRepack) | 13.0.3 | Copyright (c) 2007 James Newton-King | MIT - [`licenses/MIT-Newtonsoft.Json.txt`](licenses/MIT-Newtonsoft.Json.txt) |
| [Audiowide](https://fonts.google.com/specimen/Audiowide) font | - | Copyright (c) 2012 Brian J. Bonislawsky DBA Astigmatic (AOETI), Reserved Font Name "Audiowide" | SIL Open Font License 1.1 - [`licenses/OFL-Audiowide.txt`](licenses/OFL-Audiowide.txt) |
| [Play](https://fonts.google.com/specimen/Play) font | - | Copyright (c) 2011 Jonas Hecksher, Playtypes, e-types AS, Reserved Font Names "Play", "Playtype", "Playtype Sans" | SIL Open Font License 1.1 - [`licenses/OFL-Play.txt`](licenses/OFL-Play.txt) |
| [Material Icons](https://github.com/google/material-design-icons) font | - | Copyright 2018 Google, Inc. | Apache License 2.0 - [`licenses/Apache-2.0-MaterialIcons.txt`](licenses/Apache-2.0-MaterialIcons.txt) |

## Build-time only (not redistributed in the installer)

| Component | License |
| --- | --- |
| SOLIDWORKS API interop assemblies, restored from the [`Visiativ.SOLIDWORKS.Interop`](https://www.nuget.org/packages/Visiativ.SOLIDWORKS.Interop) NuGet package (or a local SOLIDWORKS installation's `api\redist`) | Proprietary, Dassault Systèmes SolidWorks Corporation; not part of this repository. Only the interop type definitions the add-in uses are embedded (`EmbedInteropTypes`); no interop assembly is shipped. |
| [ILRepack](https://github.com/gluck/il-repack), [WiX Toolset](https://wixtoolset.org/), [Microsoft.CodeAnalysis.NetAnalyzers](https://github.com/dotnet/roslyn-analyzers), [MSTest](https://github.com/microsoft/testfx) | Their respective open-source licenses (Apache 2.0 / MS-RL / MIT); used to build and test, not distributed. |
