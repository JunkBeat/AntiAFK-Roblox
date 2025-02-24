using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.Versioning;
using System.Reflection;

namespace RBX_AntiAFK
{
    public class KeyPresser
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_SPACE = 0x20;
        private const byte VK_MENU = 0x12;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;

        public static void PressLeftArrow()
        {
            keybd_event(VK_LEFT, (byte)MapVirtualKey(VK_LEFT, 0), 0, 0);
            Thread.Sleep(15);
            keybd_event(VK_LEFT, (byte)MapVirtualKey(VK_LEFT, 0), KEYEVENTF_KEYUP, 0);
        }

        public static void PressRightArrow()
        {
            keybd_event(VK_RIGHT, (byte)MapVirtualKey(VK_RIGHT, 0), 0, 0);
            Thread.Sleep(15);
            keybd_event(VK_RIGHT, (byte)MapVirtualKey(VK_RIGHT, 0), KEYEVENTF_KEYUP, 0);
        }

        public static void PressSpace()
        {
            keybd_event(VK_SPACE, (byte)MapVirtualKey(VK_SPACE, 0), 0, 0);
            Thread.Sleep(15);
            keybd_event(VK_SPACE, (byte)MapVirtualKey(VK_SPACE, 0), KEYEVENTF_KEYUP, 0);
        }
        public static void MoveCamera()
        {
            PressRightArrow();
            Thread.Sleep(15);
            PressLeftArrow();
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

    class Program
    {
        private static NotifyIcon trayIcon = new();
        private static CancellationTokenSource cts = new();

        // Screensaver
        private static Form? screensaverForm;
        private static Point lastMousePosition;
        private static int movementThreshold = 15; // in pixels

        // GUI elements
        private static ToolStripMenuItem startAntiAfkMenuItem = new();
        private static ToolStripMenuItem stopAntiAfkMenuItem = new();
        private static ComboBox actionTypeComboBox = new();
        private static CheckBox enableDelayCheckBox = new();
        private static NumericUpDown delaySecondsNumericUpDown = new();
        private static CheckBox hideWindowContentsCheckBox = new();

        private static Settings settings = new();
        private static bool allowNotifications = true;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            trayIcon = new NotifyIcon
            {
                Text = "Anti-AFK RBX",
                Icon = Properties.Resources.DefaultIcon,
                Visible = true,
                ContextMenuStrip = CreateTrayMenu()
            };

            LoadSettings();
            Application.Run();
        }

        private static ContextMenuStrip CreateTrayMenu()
        {
            var actionRowPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.Top,
                Controls =
                {
                    new Label { 
                        Text = "Action:", 
                        TextAlign = ContentAlignment.MiddleLeft, 
                        AutoSize = true, 
                        Dock = DockStyle.Left 
                    },
                    (actionTypeComboBox = new ComboBox
                    {
                        Items = { "Jump", "Camera Shift" },
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 100,
                        SelectedIndex = 0
                    })
                }
            };

            var delayRowPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.Top,
                Controls =
                {
                    (enableDelayCheckBox = new CheckBox
                    {
                        Text = "Open for",
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true,
                        Dock = DockStyle.Left
                    }),
                    (delaySecondsNumericUpDown = new NumericUpDown
                    {
                        Width = 45,
                        Minimum = 1,
                        Maximum = 60,
                        Value = 3,
                        Enabled = false,
                    }),
                    new Label
                    {
                        Text = "sec",
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true,
                        Dock = DockStyle.Left
                    }
                }
            };

            enableDelayCheckBox.CheckedChanged += ToggleInput;

            hideWindowContentsCheckBox = new CheckBox
            {
                Text = "Hide window contents",
                AutoSize = true,
                Margin = new Padding(5, 0, 0, 0),
                Dock = DockStyle.Top
            };

            var mainPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                Controls = { actionRowPanel, delayRowPanel, hideWindowContentsCheckBox }
            };

            actionRowPanel.Margin = new Padding(2, 0, 0, 0);
            delayRowPanel.Margin = new Padding(2, 0, 0, 0);


            var menu = new ContextMenuStrip();

            startAntiAfkMenuItem = new ToolStripMenuItem("▶️ Start Anti-AFK", null, (s, e) => StartAfk()) { Enabled = true };
            stopAntiAfkMenuItem = new ToolStripMenuItem("■ Stop Anti-AFK", null, (s, e) => StopAfk()) { Enabled = false };

            menu.Items.AddRange(new ToolStripItem[] {
                startAntiAfkMenuItem,
                stopAntiAfkMenuItem,
                new ToolStripSeparator(),
                new ToolStripControlHost(mainPanel),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Show Roblox", null, (s, e) => ShowRoblox()),
                new ToolStripMenuItem("Hide Roblox", null, (s, e) => HideRoblox()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Open Screensaver [Night Farm]", null, (s, e) => OpenScreensaver()),
                new ToolStripMenuItem("Test Anti-AFK move", null, (s, e) => TestMove()),
                new ToolStripMenuItem("About", null, (s, e) => ShowAbout()),
                new ToolStripMenuItem("Exit", null, (s, e) => Exit())
            });

            return menu;
        }

        private static void ToggleInput(object? sender, EventArgs e)
        {
            delaySecondsNumericUpDown.Enabled = enableDelayCheckBox.Checked;
        }

        private static void LoadSettings()
        {
            settings.Load();
            enableDelayCheckBox.Checked = settings.EnableDelay;
            delaySecondsNumericUpDown.Value = settings.DelaySeconds;
            hideWindowContentsCheckBox.Checked = settings.HideWindowContents;

            int index = actionTypeComboBox.Items.IndexOf(settings.ActionType);

            if (index != -1)
            {
                actionTypeComboBox.SelectedIndex = index;
            }
        }

        private static void SaveSettings()
        {
            settings.EnableDelay = enableDelayCheckBox.Checked;
            settings.DelaySeconds = delaySecondsNumericUpDown.Value;
            settings.HideWindowContents = hideWindowContentsCheckBox.Checked;
            settings.ActionType = actionTypeComboBox.SelectedItem?.ToString() ?? "";
            settings.Save();
        }

        private static void ShowToast(string message, string title, int duration, ToolTipIcon icon = ToolTipIcon.Info)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = message;
            trayIcon.BalloonTipIcon = icon;
            trayIcon.ShowBalloonTip(duration);
        }

        private static void OpenScreensaver()
        {
            allowNotifications = false;

            foreach (var win in WinManager.GetVisibleWindows())
            {
                win.SetNoTopMost();
            }

            screensaverForm = new Form
            {
                BackColor = Color.Black,
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true
            };

            screensaverForm.MouseMove += ScreensaverForm_MouseMove;
            screensaverForm.Show();

            lastMousePosition = Cursor.Position;
        }

        private static void ScreensaverForm_MouseMove(object? sender, MouseEventArgs e)
        {
            Point currentMousePosition = Cursor.Position;
            int deltaX = Math.Abs(currentMousePosition.X - lastMousePosition.X);
            int deltaY = Math.Abs(currentMousePosition.Y - lastMousePosition.Y);

            if (deltaX > movementThreshold || deltaY > movementThreshold)
            {
                CloseScreensaver();
                foreach (var win in WinManager.GetVisibleWindows())
                {
                    win.SetTop();
                }
            }
        }

        private static void CloseScreensaver()
        {
            allowNotifications = true;

            if (screensaverForm != null && !screensaverForm.IsDisposed)
            {
                screensaverForm.Close();
                screensaverForm.Dispose();
                screensaverForm = null;
            }
        }

        private static void StartAfk()
        {
            var windows = WinManager.GetAllWindows();
            if (!windows.Any())
            {
                MessageBox.Show("Roblox window not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            cts = new CancellationTokenSource();
            Task.Run(() => AfkLoop(cts.Token));

            UpdateUi(() =>
            {
                startAntiAfkMenuItem.Enabled = false;
                stopAntiAfkMenuItem.Enabled = true;
                trayIcon.Icon = Properties.Resources.RunningIcon;
            });
        }

        private static void StopAfk()
        {
            cts?.Cancel();

            UpdateUi(() =>
            {
                startAntiAfkMenuItem.Enabled = true;
                stopAntiAfkMenuItem.Enabled = false;
                trayIcon.Icon = Properties.Resources.DefaultIcon;
            });

            //MessageBox.Show("Anti-AFK stopped!", "Info", MessageBoxButtons.OK);
        }

        private static void ShowAbout()
        {
            var result = MessageBox.Show("Anti-AFK RBX by JunkBeat\nBeta: v1.0\nhttps://github.com/JunkBeat/AntiAFK-RBX\n\nOpen Github page?",
                                 "Info", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/JunkBeat/AntiAFK-RBX",
                    UseShellExecute = true
                });
            }
        }

        private static void Exit()
        {
            cts?.Cancel();

            foreach (var win in WinManager.GetAllWindows())
            {
                if (!win.IsVisible)
                {
                    win.Minimize();
                    win.Show();
                }

                win.SetTransparency(255);
            }

            SaveSettings();
            trayIcon.Visible = false;
            Application.Exit();
        }

        private static void TestMove()
        {
            foreach (var win in WinManager.GetVisibleWindows())
            {
                win.Restore();
                win.Activate();
                Thread.Sleep(15);
                KeyPresser.PressSpace();
            }
        }

        private static async Task AfkLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var windows = WinManager.GetAllWindows();

                if (!windows.Any())
                {
                    await Sleep(TimeSpan.FromSeconds(30), token);
                    continue;
                }

                var userWin = WinManager.GetActiveWindow();

                if (enableDelayCheckBox.Checked && allowNotifications)
                {
                    ShowToast("Roblox is opening soon", "Anti-AFK RBX", 2);
                    await Sleep(3000, token);
                }

                foreach (var robloxWin in windows)
                {
                    var wasMinimized = robloxWin.IsMinimized;

                    if (enableDelayCheckBox.Checked)
                    {
                        if (hideWindowContentsCheckBox.Checked)
                            robloxWin.SetTransparency(0);

                        robloxWin.Restore();
                        robloxWin.Activate();

                        await Sleep(TimeSpan.FromSeconds((double)delaySecondsNumericUpDown.Value), token);
                    }

                    // Perform three times for greater reliability
                    for (int i = 0; i < 3; i++)
                    {
                        robloxWin.Activate();
                        await Sleep(30, token);

                        PerformAction();
                        await Sleep(30, token);
                    
                        if (wasMinimized)
                    	    robloxWin.Minimize();

		                userWin?.Activate();
		            }

		            robloxWin.SetTransparency(255);
                }

                await Sleep(TimeSpan.FromMinutes(15), token);
            }
        }

        private static void PerformAction()
        {
            string selectedAction = actionTypeComboBox.SelectedItem?.ToString() ?? "";

            switch (selectedAction)
            {
                case "Jump":
                    KeyPresser.PressSpace();
                    break;
                case "Camera Shift":
                    KeyPresser.MoveCamera();
                    break;
                default:
                    Console.WriteLine($"Unknown action: {selectedAction}");
                    KeyPresser.PressSpace();
                    break;
            }
        }

        private static void ShowRoblox()
        {
            var windows = WinManager.GetHiddenWindows();

            if (windows.Any())
            {
                foreach (var win in windows)
                {
                    win.Minimize();
                    win.Show();
                }
            }
            else
            {
                MessageBox.Show("Hidden Roblox window not found.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void HideRoblox()
        {
            bool foundMinimized = false;

            foreach (var win in WinManager.GetVisibleWindows())
            {
                // Apply to minimized only 
                if (win.IsMinimized)
                {
                    foundMinimized = true;
                    win.Restore();
                    win.Hide();
                }
            }

            if (!foundMinimized)
            {
                MessageBox.Show("Minimized Roblox window not found.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void UpdateUi(Action action)
        {
            if (trayIcon.ContextMenuStrip!.InvokeRequired)
                trayIcon.ContextMenuStrip.BeginInvoke(action);
            else
                action();
        }

        private static async Task Sleep(TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                // Do nothing. The cancellation is handled by the caller.
            }
        }

        private static async Task Sleep(int milliseconds, CancellationToken token)
        {
            try
            {
                await Task.Delay(milliseconds, token);
            }
            catch (OperationCanceledException)
            {
                // Do nothing. The cancellation is handled by the caller.
            }
        }
    }
}