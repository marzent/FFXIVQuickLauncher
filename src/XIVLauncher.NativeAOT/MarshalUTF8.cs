using System.Runtime.InteropServices;

namespace MarshalUTF8Extensions
{
    static class MarshalUTF8
    {
        public static nint StringToHGlobal(string s, out int length)
        {
            if (s == null)
            {
                length = 0;
                return IntPtr.Zero;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            length = bytes.Length;

            return ptr;
        }

        public static nint StringToHGlobal(string s)
        {
            int temp;
            return StringToHGlobal(s, out temp);
        }
    }
}

