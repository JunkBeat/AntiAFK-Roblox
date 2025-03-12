using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_AntiAFK.Core;

public class KeyPresser
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint KEYEVENTF_KEYUP = 0x0002;

    private int _interactionDelay = 30;
    private int _keypressDelay = 45;

    public int InteractionDelay
    {
        get => _interactionDelay;
        set => _interactionDelay = value > 0 ? value : 30;
    }

    public int KeypressDelay
    {
        get => _keypressDelay;
        set => _keypressDelay = value > 0 ? value : 45;
    }

    public void PressKey(Keys key)
    {
        byte bKey = (byte)key;
        keybd_event(bKey, (byte)MapVirtualKey(bKey, 0), 0, 0);
        Thread.Sleep(KeypressDelay);
        keybd_event(bKey, (byte)MapVirtualKey(bKey, 0), KEYEVENTF_KEYUP, 0);
    }

    public void PressSpace()
    {
        PressKey(Keys.Space);
    }

    public void MoveCamera()
    {
        PressKey(Keys.I); // Zoom in
        Thread.Sleep(InteractionDelay);
        PressKey(Keys.O); // Zoom out
    }
}