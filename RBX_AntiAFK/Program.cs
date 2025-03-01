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
using Newtonsoft.Json;
using RBX_AntiAFK.Input;
using RBX_AntiAFK.SystemInterop;

namespace RBX_AntiAFK;

class Program
{
    private static NotifyIcon trayIcon = new();
    private static CancellationTokenSource? cts;
    private static SynchronizationContext? _uiContext;
    private static Task? _afkTask;

    // Screensaver
    private static Form? screensaverForm;
    private static Point lastMousePosition;
    private const int movementThreshold = 15; // in pixels

    // GUI elements
    private static ToolStripMenuItem startAntiAfkMenuItem = new();
    private static ToolStripMenuItem stopAntiAfkMenuItem = new();
    private static ComboBox actionTypeComboBox = new();
    private static CheckBox enableMaximizationCheckBox = new();
    private static NumericUpDown maximizationDelayNumericUpDown = new();
    private static CheckBox hideWindowContentsCheckBox = new();
    //private static NumericUpDown windowDelayNumericUpDown = new();
    //private static NumericUpDown keypressDelayNumericUpDown = new();

    // Settings
    private static readonly Settings settings = new();
    private static bool allowNotifications = true;
    private static int interactionDelay = 0; // ms
    private static int keypressDelay = 0; // ms

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

        _uiContext = SynchronizationContext.Current;

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

        //var windowDelayRowPanel = new FlowLayoutPanel
        //{
        //    AutoSize = true,
        //    FlowDirection = FlowDirection.LeftToRight,
        //    WrapContents = false,
        //    Dock = DockStyle.Top,
        //    Controls =
        //    {
        //        new Label
        //        {
        //            Text = "Window delay:",
        //            TextAlign = ContentAlignment.MiddleLeft,
        //            AutoSize = true,
        //            Dock = DockStyle.Left
        //        },
        //        (windowDelayNumericUpDown = new NumericUpDown
        //        {
        //            Width = 45,
        //            Minimum = 15,
        //            Maximum = 1000,
        //            Value = 30
        //        }),
        //        new Label
        //        {
        //            Text = "ms",
        //            TextAlign = ContentAlignment.MiddleLeft,
        //            AutoSize = true,
        //            Dock = DockStyle.Left
        //        }
        //    }
        //};

        //var keypressDelayRowPanel = new FlowLayoutPanel
        //{
        //    AutoSize = true,
        //    FlowDirection = FlowDirection.LeftToRight,
        //    WrapContents = false,
        //    Dock = DockStyle.Top,
        //    Controls =
        //    {
        //        new Label
        //        {
        //            Text = "Keypress delay:",
        //            TextAlign = ContentAlignment.MiddleLeft,
        //            AutoSize = true,
        //            Dock = DockStyle.Left
        //        },
        //        (keypressDelayNumericUpDown = new NumericUpDown
        //        {
        //            Width = 45,
        //            Minimum = 15,
        //            Maximum = 1000,
        //            Value = 45
        //        }),
        //        new Label
        //        {
        //            Text = "ms",
        //            TextAlign = ContentAlignment.MiddleLeft,
        //            AutoSize = true,
        //            Dock = DockStyle.Left
        //        }
        //    }
        //};

        var maximizeRowPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Dock = DockStyle.Top,
            Controls =
            {
                (enableMaximizationCheckBox = new CheckBox
                {
                    Text = "Open for",
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true,
                    Dock = DockStyle.Left
                }),
                (maximizationDelayNumericUpDown = new NumericUpDown
                {
                    Width = 45,
                    Minimum = 1,
                    Maximum = 60,
                    Value = 3
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

        hideWindowContentsCheckBox = new CheckBox
        {
            Text = "Hide window contents",
            AutoSize = true,
            Dock = DockStyle.Top
        };

        var mainPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            Controls = { actionRowPanel, maximizeRowPanel, hideWindowContentsCheckBox }
        };

        actionRowPanel.Margin = new Padding(2, 0, 0, 0);
        maximizeRowPanel.Margin = new Padding(2, 0, 0, 0);
        hideWindowContentsCheckBox.Margin = new Padding(5, 0, 0, 0);


        var menu = new ContextMenuStrip();

        startAntiAfkMenuItem = new ToolStripMenuItem("▶️ Start Anti-AFK", null, (s, e) => StartAfk()) { Enabled = true };
        stopAntiAfkMenuItem = new ToolStripMenuItem("■ Stop Anti-AFK", null, async (s, e) => await StopAfkAsync()) { Enabled = false };

        menu.Items.AddRange([
            startAntiAfkMenuItem,
            stopAntiAfkMenuItem,
            new ToolStripSeparator(),
            new ToolStripControlHost(mainPanel),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Show Roblox", null, (s, e) => ShowRoblox()),
            new ToolStripMenuItem("Hide Roblox", null, (s, e) => HideRoblox()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Open Screensaver [Night Farm]", null, (s, e) => OpenScreensaver()),
            new ToolStripMenuItem("Test Anti-AFK move", null, async (s, e) => await TestMoveAsync()),
            new ToolStripMenuItem("About", null, (s, e) => ShowAbout()),
            new ToolStripMenuItem("Exit", null, (s, e) => Exit())
        ]);

        return menu;
    }

    private static void LoadSettings()
    {
        settings.Load();
        enableMaximizationCheckBox.Checked = settings.EnableWindowMaximization;
        maximizationDelayNumericUpDown.Value = settings.WindowMaximizationDelaySeconds;
        hideWindowContentsCheckBox.Checked = settings.HideWindowContentsOnMaximizing;

        int index = actionTypeComboBox.Items.IndexOf(settings.ActionType);

        if (index != -1)
        {
            actionTypeComboBox.SelectedIndex = index;
        }

        interactionDelay = settings.DelayBeforeWindowInteractionMilliseconds;
        keypressDelay = settings.DelayBetweenKeyPressMilliseconds;
    }

    private static void SaveSettings()
    {
        settings.EnableWindowMaximization = enableMaximizationCheckBox.Checked;
        settings.WindowMaximizationDelaySeconds = maximizationDelayNumericUpDown.Value;
        settings.HideWindowContentsOnMaximizing = hideWindowContentsCheckBox.Checked;
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
        if (windows.Count == 0)
        {
            MessageBox.Show("Roblox window not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        cts = new CancellationTokenSource();

        _uiContext!.Post(_ =>
        {
            startAntiAfkMenuItem.Enabled = false;
            stopAntiAfkMenuItem.Enabled = true;
            trayIcon.Icon = Properties.Resources.RunningIcon;
        }, null);

        _afkTask = Task.Run(() => AfkLoopAsync(cts.Token));
    }

    private static async Task StopAfkAsync()
    {
        if (cts != null)
        {
            cts.Cancel();

            try
            {
                if (_afkTask != null)
                {
                    await _afkTask; // Wait for the task to complete
                }
            }
            catch (OperationCanceledException)
            {
                // Handle the cancellation (but this is already done in AfkLoop)
            }
            finally
            {
                cts.Dispose();
                cts = null;
                _afkTask = null;
            }
        }

        RepairWindows();

        _uiContext!.Post(_ =>
        {
            startAntiAfkMenuItem.Enabled = true;
            stopAntiAfkMenuItem.Enabled = false;
            trayIcon.Icon = Properties.Resources.DefaultIcon;
        }, null);
    }

    public static string GetAssemblyVersion<T>()
    {
        var assembly = typeof(T).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    private static void ShowAbout()
    {
        var aboutForm = new Form
        {
            Text = "About",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(400, 160),
            BackColor = Color.White
        };

        Panel contentPanel = new() { Size = new(360, 140), Location = new(20, 10) };
        aboutForm.Controls.Add(contentPanel);

        contentPanel.Controls.Add(new PictureBox
        {
            Image = Properties.Resources.ProjectImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new(100, 100),
            Location = new(20, 10)
        });

        Panel textPanel = new() { Size = new(270, 120), Location = new(150, 10) };
        contentPanel.Controls.Add(textPanel);

        void AddLabel(string text, Font font, int yOffset)
            => textPanel.Controls.Add(new Label
            {
                Text = text,
                Font = font,
                AutoSize = true,
                Location = new(0, yOffset)
            });

        AddLabel("AntiAFK-Roblox", new Font("Arial", 18, FontStyle.Bold), 0);
        AddLabel("by JunkBeat", new Font("Arial", 12), 27);
        AddLabel($"Version: {GetAssemblyVersion<Program>()}", new Font("Arial", 12), 60);

        Label githubLink = new()
        {
            Text = "GitHub",
            Font = new Font("Arial", 12, FontStyle.Bold),
            ForeColor = Color.Blue,
            AutoSize = true,
            Location = new(0, 90),
            Cursor = Cursors.Hand
        };
        githubLink.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/JunkBeat/AntiAFK-Roblox",
            UseShellExecute = true
        });

        textPanel.Controls.Add(githubLink);
        aboutForm.ShowDialog();
    }

    private static void RepairWindows()
    {
        foreach (var win in WinManager.GetAllWindows())
        {
            if (!win.IsVisible)
            {
                win.Minimize();
                win.Show();
            }

            win.SetTransparency(255);
        }
    }

    private static void Exit()
    {
        cts?.Cancel();
        RepairWindows();
        SaveSettings();
        trayIcon.Visible = false;
        Application.Exit();
    }

    private static async Task TestMoveAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            foreach (var win in WinManager.GetVisibleWindows())
            {
                if (win.IsMinimized) win.Restore();
                win.Activate();
                await Task.Delay(interactionDelay);
                KeyPresser.PressSpace(keypressDelay);
                await Task.Delay(interactionDelay);
            }
        }
    }

    private static async Task AfkLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var userWin = WinManager.GetActiveWindow();
            var windows = WinManager.GetAllWindows();

            if (!windows.Any())
            {
                await SleepAsync(TimeSpan.FromSeconds(30), token);
                continue;
            }

            // Get UI settings within the UI context.  Must be synchronous.
            string selectedAction = "";
            bool enableMaximization = false;
            int maximizationDelaySec = 3;
            bool shouldHideWindow = false;

            _uiContext!.Send(_ =>
            {
                selectedAction = actionTypeComboBox.SelectedItem?.ToString() ?? "";
                enableMaximization = enableMaximizationCheckBox.Checked;
                maximizationDelaySec = (int)maximizationDelayNumericUpDown.Value;
                shouldHideWindow = hideWindowContentsCheckBox.Checked;
            }, null);

            if (enableMaximization && allowNotifications)
            {
                // UI interaction, so use _uiContext.Send (synchronous)
                _uiContext.Send(_ => ShowToast("Roblox is opening soon", "Anti-AFK RBX", 2), null);
                await SleepAsync(3000, token);
            }

            foreach (var robloxWin in windows.Where(w => w.IsValidWindow))
            {
                bool wasMinimized = robloxWin.IsMinimized;

                if (enableMaximization)
                {
                    if (shouldHideWindow) robloxWin.SetTransparency(0);
                    if (wasMinimized) robloxWin.Restore();
                    robloxWin.Activate();
                    await SleepAsync(TimeSpan.FromSeconds(maximizationDelaySec), token);
                    if (wasMinimized) robloxWin.Minimize();
                }

                // Perform three times for greater reliability
                for (int i = 0; i < 3; i++)
                {
                    robloxWin.Activate();
                    await SleepAsync(interactionDelay, token);

                    switch (selectedAction)
                    {
                        case "Jump":
                            KeyPresser.PressSpace(keypressDelay);
                            break;
                        case "Camera Shift":
                            KeyPresser.MoveCamera(keypressDelay, interactionDelay);
                            break;
                        default:
                            Console.WriteLine($"Unknown action: {selectedAction}");
                            KeyPresser.PressSpace();
                            break;
                    }
                    await SleepAsync(interactionDelay, token);
                }

                if (userWin?.IsValidWindow == true) userWin.Activate();
                robloxWin.SetTransparency(255);
            }

            await SleepAsync(TimeSpan.FromMinutes(15), token);
        }
    }


    private static void ShowRoblox()
    {
        var windows = WinManager.GetHiddenWindows();

        if (windows.Count != 0)
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

    private static async Task SleepAsync(TimeSpan delay, CancellationToken token)
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

    private static async Task SleepAsync(int milliseconds, CancellationToken token)
    {
        await SleepAsync(TimeSpan.FromMilliseconds(milliseconds), token);
    }
}
