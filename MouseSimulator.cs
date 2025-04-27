using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseRecorder
{
    public static class MouseSimulator
    {
        // ------------- 鼠标模拟 -------------
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);

        private enum MouseEventFlags : uint
        {
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            Wheel = 0x0800,
            Absolute = 0x8000
        }

        public static void MoveTo(int x, int y)
        {
            Cursor.Position = new Point(x, y);
        }

        public static void LeftDown() => mouse_event((uint)MouseEventFlags.LeftDown, 0, 0, 0, UIntPtr.Zero);
        public static void LeftUp() => mouse_event((uint)MouseEventFlags.LeftUp, 0, 0, 0, UIntPtr.Zero);
        public static void RightDown() => mouse_event((uint)MouseEventFlags.RightDown, 0, 0, 0, UIntPtr.Zero);
        public static void RightUp() => mouse_event((uint)MouseEventFlags.RightUp, 0, 0, 0, UIntPtr.Zero);
        public static void MiddleDown() => mouse_event((uint)MouseEventFlags.MiddleDown, 0, 0, 0, UIntPtr.Zero);
        public static void MiddleUp() => mouse_event((uint)MouseEventFlags.MiddleUp, 0, 0, 0, UIntPtr.Zero);
        public static void MouseWheel(int delta) => mouse_event((uint)MouseEventFlags.Wheel, 0, 0, (uint)delta, UIntPtr.Zero);

        // ------------- 键盘模拟 -------------
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void KeyDown(Keys key) => keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        public static void KeyUp(Keys key) => keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}