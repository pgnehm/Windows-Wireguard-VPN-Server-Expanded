using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WireGuardServerForWindows.Models
{
    /// <summary>
    /// Protects configuration secrets with Windows DPAPI for the current user.
    /// WireGuard runtime files are still generated as plaintext because the
    /// WireGuard service must be able to read them; the editable data store is
    /// encrypted at rest.
    /// </summary>
    public static class DpapiSecretProtector
    {
        private const string Prefix = "dpapi:";

        public static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
            return Prefix + Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(value)));
        }

        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
            byte[] protectedBytes = Convert.FromBase64String(value.Substring(Prefix.Length));
            return Encoding.UTF8.GetString(Unprotect(protectedBytes));
        }

        private static byte[] Protect(byte[] value)
        {
            return InvokeCrypt(ProtectedDataOperation.Protect, value);
        }

        private static byte[] Unprotect(byte[] value)
        {
            return InvokeCrypt(ProtectedDataOperation.Unprotect, value);
        }

        private static byte[] InvokeCrypt(ProtectedDataOperation operation, byte[] value)
        {
            var input = new DataBlob(value);
            try
            {
                bool success = operation == ProtectedDataOperation.Protect
                    ? CryptProtectData(ref input.Blob, null, IntPtr.Zero, null, IntPtr.Zero, 0, out NativeDataBlob output)
                    : CryptUnprotectData(ref input.Blob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
                if (!success) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    byte[] result = new byte[output.cbData];
                    Marshal.Copy(output.pbData, result, 0, result.Length);
                    return result;
                }
                finally
                {
                    if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
                }
            }
            finally
            {
                input.Dispose();
            }
        }

        private enum ProtectedDataOperation { Protect, Unprotect }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeDataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        private sealed class DataBlob : IDisposable
        {
            public DataBlob(byte[] data)
            {
                Blob = new NativeDataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
                Marshal.Copy(data, 0, Blob.pbData, data.Length);
            }

            public NativeDataBlob Blob;

            public void Dispose()
            {
                if (Blob.pbData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Blob.pbData);
                    Blob.pbData = IntPtr.Zero;
                }
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref NativeDataBlob pDataIn,
            string szDataDescr,
            IntPtr pOptionalEntropy,
            string pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            out NativeDataBlob pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref NativeDataBlob pDataIn,
            IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            out NativeDataBlob pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
