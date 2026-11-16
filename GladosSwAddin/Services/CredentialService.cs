using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Keeps the personal access token in the Windows Credential Manager as a generic
    /// credential (target "GladosSwAddin.Api", user name "PAT", persisted per user on this
    /// computer, blob = the token as UTF-16LE). Talks to advapi32's Cred* functions directly
    /// instead of through the unmaintained CredentialManagement package; the stored format is
    /// byte-for-byte what that package wrote, so tokens saved by earlier versions keep loading.
    /// Like that package, nothing here throws for a Credential Manager failure - a failed read
    /// is "no token", a failed write or delete is logged - because callers run on UI events
    /// inside SOLIDWORKS with no handler of their own. The token itself is never logged.
    /// </summary>
    public class CredentialService
    {
        private const string CredentialTarget = "GladosSwAddin.Api";
        private const string CredentialUserName = "PAT";

        private const int CredTypeGeneric = 1;
        private const int CredPersistLocalMachine = 2;
        private const int ErrorNotFound = 1168;

        /// <summary>CRED_MAX_CREDENTIAL_BLOB_SIZE: Windows rejects anything larger.</summary>
        private const int MaxBlobBytes = 5 * 512;

        public void SavePat(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                DeletePat();
                return;
            }

            var blob = Encoding.Unicode.GetBytes(token);
            if (blob.Length > MaxBlobBytes)
            {
                Log.Error("Personal access token not saved: longer than Credential Manager allows");
                return;
            }

            var blobPointer = Marshal.AllocHGlobal(blob.Length);
            try
            {
                Marshal.Copy(blob, 0, blobPointer, blob.Length);

                var credential = new NativeCredential
                {
                    Type = CredTypeGeneric,
                    TargetName = CredentialTarget,
                    UserName = CredentialUserName,
                    CredentialBlobSize = blob.Length,
                    CredentialBlob = blobPointer,
                    Persist = CredPersistLocalMachine,
                };

                if (!CredWrite(ref credential, 0))
                {
                    Log.Error("Personal access token not saved",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            finally
            {
                // Zero the unmanaged copy of the token before releasing it.
                for (var i = 0; i < blob.Length; i++)
                    Marshal.WriteByte(blobPointer, i, 0);
                Marshal.FreeHGlobal(blobPointer);
                Array.Clear(blob, 0, blob.Length);
            }
        }

        public string GetPat()
        {
            IntPtr credentialPointer;
            if (!CredRead(CredentialTarget, CredTypeGeneric, 0, out credentialPointer))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorNotFound)
                    Log.Warning("Personal access token could not be read", new Win32Exception(error));
                return null;
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                    return null;

                return Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
            }
            finally
            {
                CredFree(credentialPointer);
            }
        }

        public bool HasPat()
        {
            return !string.IsNullOrWhiteSpace(GetPat());
        }

        public void DeletePat()
        {
            if (!CredDelete(CredentialTarget, CredTypeGeneric, 0))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorNotFound)
                    Log.Error("Personal access token could not be deleted", new Win32Exception(error));
            }
        }

        /// <summary>CREDENTIALW. Only the fields this class sets or reads matter; the rest stay
        /// zero, which is what the Cred* API expects for a plain generic credential.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref NativeCredential credential, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", SetLastError = false)]
        private static extern void CredFree(IntPtr buffer);
    }
}
