using System;
using System.Runtime.InteropServices;
using System.Text;

namespace POS_UI.Services
{
    public static class CredentialManagerService
    {
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;

        // Keys we use to store secrets
        public const string LaravelClientIdKey = "POS_UI/LaravelClientId";
        public const string LaravelClientSecretKey = "POS_UI/LaravelClientSecret";

        public static bool TryGetSecret(string target, out string value)
        {
            value = string.Empty;
            bool success = CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr credPtr);
            if (!success || credPtr == IntPtr.Zero)
            {
                return false;
            }
            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                {
                    return false;
                }
                byte[] blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, cred.CredentialBlobSize);
                value = Encoding.Unicode.GetString(blob).TrimEnd('\0');
                return !string.IsNullOrWhiteSpace(value);
            }
            finally
            {
                CredFree(credPtr);
            }
        }

        public static void SetSecret(string target, string secret)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target name is required", nameof(target));
            if (secret == null) secret = string.Empty;

            byte[] secretBytes = Encoding.Unicode.GetBytes(secret);

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                AttributeCount = 0,
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = Marshal.AllocCoTaskMem(secretBytes.Length),
                UserName = Environment.UserName,
            };

            try
            {
                Marshal.Copy(secretBytes, 0, credential.CredentialBlob, secretBytes.Length);
                if (!CredWrite(ref credential, 0))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"CredWrite failed with error {error}");
                }
            }
            finally
            {
                if (credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.CredentialBlob);
                }
            }
        }

        public static void DeleteSecret(string target)
        {
            // Best-effort; ignore failure
            CredDelete(target, CRED_TYPE_GENERIC, 0);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
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

        [DllImport("advapi32", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32", EntryPoint = "CredFree", SetLastError = true)]
        private static extern void CredFree([In] IntPtr cred);

        [DllImport("advapi32", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, int type, int flags);
    }
}


