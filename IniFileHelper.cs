using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace AtFoucsVoice
{
    public class IniFileHelper
    {
        private string _filePath;

        public IniFileHelper(string filePath)
        {
            _filePath = filePath;
            Debug.WriteLine($"INI File Path: {_filePath}");
        }

        public void WriteValue(string section, string key, string value)
        {
            bool result = WritePrivateProfileString(section, key, value, _filePath);
            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to write INI file at path: {_filePath}. Error code: {error}. Message: {GetErrorMessage(error)}");
            }
        }

        public string ReadValue(string section, string key, string defaultValue = "false")
        {
            StringBuilder result = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, result, 255, _filePath);
            return result.ToString();
        }

        // 获取详细的错误信息
        private string GetErrorMessage(int errorCode)
        {
            var buffer = new StringBuilder(256);
            FormatMessage(
                FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM | FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                errorCode,
                0,
                buffer,
                buffer.Capacity,
                IntPtr.Zero);
            return buffer.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int FormatMessage(
            FormatMessageFlags dwFlags,
            IntPtr lpSource,
            int dwMessageId,
            int dwLanguageId,
            StringBuilder lpBuffer,
            int nSize,
            IntPtr Arguments
        );

        [Flags]
        private enum FormatMessageFlags : int
        {
            FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200
        }
    }
}