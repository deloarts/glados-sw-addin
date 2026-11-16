using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("GLADOS SOLIDWORKS Add-in")]
[assembly: AssemblyDescription("Compares and edits GLADOS catalogue properties and checks documents before PDM release")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("DELOARTS")]
[assembly: AssemblyProduct("GLADOS SOLIDWORKS Add-in")]
[assembly: AssemblyCopyright("Copyright © Philip Delorenzo 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("7c74e4fe-ae73-48be-ac51-1efab4b5f523")]
// Lets GladosSwAddin.Tests exercise internal pure-logic helpers without widening them to public.
[assembly: InternalsVisibleTo("GladosSwAddin.Tests")]
// Single source of version truth for this project (see docs/RELEASE.md): edited here (or via
// Visual Studio's Project > Properties > Assembly Information dialog, which writes back to
// these same three attributes) and nowhere else. A release tag (vX.Y.Z) must equal
// AssemblyInformationalVersion (and AssemblyFileVersion's first three components) - release.yml's
// guard job enforces this before publishing.
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]
