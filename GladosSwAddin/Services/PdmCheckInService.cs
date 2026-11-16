﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GladosSwAddin.Services
{
    /// <summary>How a PdmCheckInService.CheckIn call ended.</summary>
    public enum PdmCheckInOutcome
    {
        CheckedIn,
        NotInVault,
        NotAddedToVault,
        NotCheckedOut,
        ClientUnavailable,
        Failed,
    }

    /// <summary>
    /// Checks a single file in to SOLIDWORKS PDM through the PDM client installed on this
    /// machine — the one PDM action the add-in takes, for a 3D Interconnect source file that
    /// lost its only reference (see ImportedComponentsService), which PDM would otherwise no
    /// longer bring along when the assembly is checked in. Writes no data-card variable.
    /// The client's own interop assembly (EPDM.Interop.epdm.dll, beside the COM server its
    /// ProgID is registered to) is loaded at runtime and called through its typed interfaces
    /// by reflection, rather than referenced at build time — it ships only with the PDM
    /// client, so a reference would tie the build (and CI) to a PDM installation — and rather
    /// than through IDispatch, which the client doesn't support for every method (UnlockFile
    /// fails late-bound with DISP_E_MEMBERNOTFOUND). On a machine without the client this
    /// simply reports ClientUnavailable. Login reuses the vault session already open in File
    /// Explorer (LoginAuto), so the check-in runs as the user, under their license. Never
    /// throws: a PDM refusal is logged and reported as Failed.
    /// </summary>
    public static class PdmCheckInService
    {
        private const string VaultProgId = "ConisioLib.EdmVault";
        private const string InteropFileName = "EPDM.Interop.epdm.dll";
        private const string VaultInterfaceName = "EPDM.Interop.epdm.IEdmVault5";
        private const string FileInterfaceName = "EPDM.Interop.epdm.IEdmFile5";

        private static readonly Lazy<Assembly> Interop = new Lazy<Assembly>(LoadInterop);

        /// <summary>Checks <paramref name="path"/> in with <paramref name="comment"/> as the
        /// version comment — if it is in a vault and checked out; a file outside every vault
        /// or already checked in is left alone, and one lying in a vault folder without having
        /// been added is reported as NotAddedToVault, not added. <paramref name="parentWindow"/>
        /// parents the PDM login dialog, shown only when no one is logged in to the vault yet.</summary>
        public static PdmCheckInOutcome CheckIn(string path, string comment, int parentWindow)
        {
            var vaultType = Type.GetTypeFromProgID(VaultProgId);
            var interop = vaultType == null ? null : Interop.Value;
            if (interop == null)
            {
                Log.Info("No SOLIDWORKS PDM client available; " + path + " was not checked in");
                return PdmCheckInOutcome.ClientUnavailable;
            }

            object vault = null;
            try
            {
                var vaultInterface = interop.GetType(VaultInterfaceName, true);
                var fileInterface = interop.GetType(FileInterfaceName, true);
                vault = Activator.CreateInstance(vaultType);

                string vaultName;
                try
                {
                    vaultName = (string)Call(vaultInterface, "GetVaultNameFromPath", vault, path);
                }
                catch (COMException)
                {
                    // E_EDM_KEY_NOT_FOUND: the path isn't in any vault.
                    return PdmCheckInOutcome.NotInVault;
                }

                if (!(bool)Get(vaultInterface, "IsLoggedIn", vault))
                    Call(vaultInterface, "LoginAuto", vault, vaultName, parentWindow);

                // The second argument is GetFileFromPath's out parameter (the parent folder).
                var file = Call(vaultInterface, "GetFileFromPath", vault, path, null);
                if (file == null)
                    return PdmCheckInOutcome.NotAddedToVault;

                Call(fileInterface, "Refresh", file);
                if (!(bool)Get(fileInterface, "IsLocked", file))
                    return PdmCheckInOutcome.NotCheckedOut;

                // 0 = EdmUnlock_Simple; no reference callback.
                Call(fileInterface, "UnlockFile", file, parentWindow, comment, 0, null);
                Log.Info("Checked in " + path);
                return PdmCheckInOutcome.CheckedIn;
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Checking in " + path + " failed", ex);
                return PdmCheckInOutcome.Failed;
            }
            finally
            {
                if (vault != null && Marshal.IsComObject(vault))
                    Marshal.FinalReleaseComObject(vault);
            }
        }

        /// <summary>Calls <paramref name="method"/> of the COM interface
        /// <paramref name="interfaceType"/> on <paramref name="target"/>, rethrowing what the
        /// call itself threw rather than reflection's TargetInvocationException wrapper.</summary>
        private static object Call(Type interfaceType, string method, object target, params object[] arguments)
        {
            try
            {
                return interfaceType.GetMethod(method).Invoke(target, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static object Get(Type interfaceType, string property, object target)
        {
            try
            {
                return interfaceType.GetProperty(property).GetValue(target);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        /// <summary>The PDM client's interop assembly, found next to the COM server the vault
        /// ProgID is registered to; null when the client isn't installed or the file is
        /// missing.</summary>
        private static Assembly LoadInterop()
        {
            try
            {
                var vaultType = Type.GetTypeFromProgID(VaultProgId);
                if (vaultType == null)
                    return null;

                using (var key = Registry.ClassesRoot.OpenSubKey(
                    @"CLSID\" + vaultType.GUID.ToString("B") + @"\InprocServer32"))
                {
                    var server = key?.GetValue(null) as string;
                    if (string.IsNullOrWhiteSpace(server))
                        return null;

                    var interopPath = Path.Combine(Path.GetDirectoryName(server), InteropFileName);
                    if (!File.Exists(interopPath))
                    {
                        Log.Warning("SOLIDWORKS PDM interop not found at " + interopPath);
                        return null;
                    }

                    return Assembly.LoadFrom(interopPath);
                }
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("SOLIDWORKS PDM interop could not be loaded", ex);
                return null;
            }
        }
    }
}
