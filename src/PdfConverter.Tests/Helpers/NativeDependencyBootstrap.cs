using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfConverter.Tests.Helpers
{
    /// <summary>
    /// テスト実行時に PDFium ネイティブ DLL の検索パスを設定する
    /// </summary>
    internal static class NativeDependencyBootstrap
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        static NativeDependencyBootstrap()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string libDirectory = Path.Combine(baseDirectory, "lib");
            string nativeDirectory = Directory.Exists(libDirectory) ? libDirectory : baseDirectory;
            SetDllDirectory(nativeDirectory);
        }
    }
}
