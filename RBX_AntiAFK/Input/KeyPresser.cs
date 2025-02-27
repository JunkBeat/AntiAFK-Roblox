using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

    public static void PressKey(byte keyCode, int delay = 15)
    {
        keybd_event(keyCode, (byte)MapVirtualKey(keyCode, 0), 0, 0);
        if (delay > 0) Thread.Sleep(delay);
        keybd_event(keyCode, (byte)MapVirtualKey(keyCode, 0), KEYEVENTF_KEYUP, 0);
    }

    public static void PressSpace()
    {
        PressKey(VK_SPACE);
    }

    public static void MoveCamera()
    {
        PressKey(73); // Zoom in
        Thread.Sleep(15);
        PressKey(79); // Zoom out
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
