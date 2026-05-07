using System;
using System.Runtime.InteropServices;

namespace POS_UI.Services
{
    public static class RawPrinterHelper
    {
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true)]
        static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool StartDocPrinter(IntPtr hPrinter, int level, IntPtr pDocInfo);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", SetLastError = true)]
        static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            IntPtr printerHandle;

            if (!OpenPrinter(printerName.Normalize(), out printerHandle, IntPtr.Zero))
                return false;

            var docInfo = new DOCINFOA
            {
                pDocName = "Raw Document",
                pDataType = "RAW"
            };

            IntPtr pDocInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DOCINFOA)));
            Marshal.StructureToPtr(docInfo, pDocInfo, false);

            bool success = false;

            if (StartDocPrinter(printerHandle, 1, pDocInfo) && StartPagePrinter(printerHandle))
            {
                IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
                success = WritePrinter(printerHandle, pUnmanagedBytes, bytes.Length, out _);
                EndPagePrinter(printerHandle);
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }

            EndDocPrinter(printerHandle);
            ClosePrinter(printerHandle);
            Marshal.FreeHGlobal(pDocInfo);

            return success;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }
    }
}


