using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_AntiAFK.Input;

public class KeyPresser
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_SPACE = 0x20;
    private const byte VK_MENU = 0x12;

    public static int interactionDelay = 30;
    public static int keypressDelay = 45;

    public static void PressKey(byte keyCode, int delay = 45)
    {
        keybd_event(keyCode, (byte)MapVirtualKey(keyCode, 0), 0, 0);
        if (delay > 0) Thread.Sleep(delay);
        keybd_event(keyCode, (byte)MapVirtualKey(keyCode, 0), KEYEVENTF_KEYUP, 0);
    }

    public static void PressKey(Keys key, int delay = 45)
    {
        PressKey((byte)key, delay);
    }

    public static void PressSpace()
    {
        PressKey(Keys.Space, keypressDelay);
    }

    public static void MoveCamera()
    {
        PressKey(Keys.I, keypressDelay); // Zoom in
        Thread.Sleep(interactionDelay);
        PressKey(Keys.O, keypressDelay); // Zoom out
    }

    private static void OldPressSpace()
    {
        keybd_event(VK_MENU, 0, 0, 0);
        Thread.Sleep(15);
        keybd_event(VK_SPACE, 0, 0, 0);
        Thread.Sleep(15);
        keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
        Thread.Sleep(15);
        keybd_event(VK_MENU, 0, 0, 0);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
    }
}
