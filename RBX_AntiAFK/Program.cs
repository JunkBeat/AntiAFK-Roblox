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
using RBX_AntiAFK.Core;

namespace RBX_AntiAFK;

class Program
{
    private static readonly KeyPresser keyPresser = new();
    private static NotifyIcon trayIcon = new();
    private static CancellationTokenSource? cts;
    private static SynchronizationContext? _uiContext;
    private static Task? _afkTask;
    private static readonly object settingsLock = new();

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

    // Settings
    private static readonly Settings settings = new();
    private static bool allowNotifications = true;
    private static int interactionDelay = 0;

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

        InitializeEvents();
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
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 100,
                    SelectedItem = ActionTypeEnum.CameraShift
                })
            }
        };

        foreach (ActionTypeEnum enumValue in Enum.GetValues(typeof(ActionTypeEnum)))
        {
            actionTypeComboBox.Items.Add(enumValue);
        }

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
            new ToolStripMenuItem("Test Anti-AFK move", null, (s, e) => TestMove()),
            new ToolStripMenuItem("About", null, (s, e) => ShowAbout()),
            new ToolStripMenuItem("Exit", null, async (s, e) => await ExitAsync())
        ]);

        return menu;
    }

    private static void InitializeEvents()
    {
        enableMaximizationCheckBox.CheckedChanged += OnSettingsChanged;
        maximizationDelayNumericUpDown.ValueChanged += OnSettingsChanged;
        hideWindowContentsCheckBox.CheckedChanged += OnSettingsChanged;
        actionTypeComboBox.SelectedIndexChanged += OnSettingsChanged;
    }

    private static void OnSettingsChanged(object? sender, EventArgs e)
    {
        lock (settingsLock)
        {
            settings.EnableWindowMaximization = enableMaximizationCheckBox.Checked;
            settings.WindowMaximizationDelaySeconds = (int)maximizationDelayNumericUpDown.Value;
            settings.HideWindowContentsOnMaximizing = hideWindowContentsCheckBox.Checked;
            settings.ActionType = (ActionTypeEnum?)actionTypeComboBox?.SelectedItem ?? ActionTypeEnum.Jump;
        }
    }

    private static void LoadSettings()
    {
        lock (settingsLock)
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

            keyPresser.KeypressDelay = settings.DelayBetweenKeyPressMilliseconds;
            keyPresser.InteractionDelay = interactionDelay = settings.DelayBeforeWindowInteractionMilliseconds;
        }
    }

    private static void SaveSettings()
    {
        lock (settingsLock)
        {
            settings.Save();
        }
    }

    private static Settings GetSettingsCopy()
    {
        lock (settingsLock)
        {
            return settings.Clone();
        }
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

        foreach (var win in WinManager.GetVisibleRobloxWindows())
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

            Task.Run(() =>
            {
                foreach (var win in WinManager.GetVisibleRobloxWindows())
                {
                    win.SetTop();
                }
            });
        }
    }

    private static void CloseScreensaver()
    {
        allowNotifications = true;

        if (screensaverForm != null)
        {
            if (screensaverForm.InvokeRequired == false)
            {
                screensaverForm.Invoke(new Action(() =>
                {
                    screensaverForm.Close();
                    screensaverForm.Dispose();
                }));
            }
            else
            {
                screensaverForm.Close();
                screensaverForm.Dispose();
            }
        }
    }

    private static void StartAfk()
    {
        var windows = WinManager.GetAllRobloxWindows();
        if (windows.Count == 0)
        {
            MessageBox.Show("Roblox window not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        cts = new CancellationTokenSource();

        _uiContext?.Post(_ =>
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
                _afkTask?.Wait();
            }
            catch (AggregateException) { }
            cts.Dispose();
            cts = null;
        }

        await RepairWindowsAsync();

        _uiContext?.Post(__ =>
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

    private static async Task ExitAsync()
    {
        try
        {
            await StopAfkAsync();
            SaveSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during exit: {ex}");
            MessageBox.Show($"Error during exit: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }

    private static void TestMove()
    {
        Task.Run(() =>
        {
            try
            {
                var windows = WinManager.GetVisibleRobloxWindows();

                if (windows.Count != 0)
                {
                    var firstWindow = windows.First();

                    for (int i = 0; i < 3; i++)
                    {
                        if (firstWindow.IsMinimized) firstWindow.Restore();
                        firstWindow.Activate();
                        Thread.Sleep(interactionDelay);
                        keyPresser.PressSpace();
                        Thread.Sleep(interactionDelay);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestMove: {ex}");
                _uiContext?.Post(_ => MessageBox.Show($"Error in TestMove: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error), null);
            }
        });
    }

    private static void ShowRoblox()
    {
        Task.Run(() =>
        {
            try
            {
                var windows = WinManager.GetHiddenRobloxWindows();

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
                    _uiContext?.Post(_ => MessageBox.Show("Hidden Roblox window not found.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning), null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowRoblox: {ex}");
                _uiContext?.Post(_ => MessageBox.Show($"Error in ShowRoblox: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error), null);
            }
        });
    }

    private static void HideRoblox()
    {
        Task.Run(() =>
        {
            try
            {
                bool foundMinimized = false;

                foreach (var win in WinManager.GetVisibleRobloxWindows())
                {
                    if (win.IsMinimized)
                    {
                        foundMinimized = true;
                        win.Restore();
                        win.Hide();
                    }
                }

                if (!foundMinimized)
                {
                    _uiContext?.Post(_ => MessageBox.Show("Minimized Roblox window not found.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning), null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HideRoblox: {ex}");
                _uiContext?.Post(_ => MessageBox.Show($"Error in HideRoblox: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error), null);
            }
        });
    }

    private static async Task RepairWindowsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                foreach (var win in WinManager.GetAllRobloxWindows())
                {
                    if (!win.IsVisible)
                    {
                        win.Minimize();
                        win.Show();
                    }

                    win.SetTransparency(255);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RepairWindowsAsync: {ex}");
                _uiContext?.Post(_ => MessageBox.Show($"Error in RepairWindowsAsync: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error), null);
            }

        });
    }

    private static async Task AfkLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ProcessLoopIterationAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static async Task ProcessLoopIterationAsync(CancellationToken token)
    {
        var userWin = WinManager.GetActiveWindow();
        var windows = WinManager.GetAllRobloxWindows();

        if (windows.Count == 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return;
        }

        await ProcessWindowsAsync(windows, userWin, token);

        await Task.Delay(TimeSpan.FromMinutes(15), token);
    }

    private static async Task ProcessWindowsAsync(List<WindowInfo> windows, WindowInfo? userWin, CancellationToken token)
    {
        var s = GetSettingsCopy();
        ActionTypeEnum selectedAction = s.ActionType;
        bool enableMaximization = s.EnableWindowMaximization;
        int maximizationDelaySec = s.WindowMaximizationDelaySeconds;
        bool shouldHideWindow = s.HideWindowContentsOnMaximizing;

        if (enableMaximization && allowNotifications)
        {
            _uiContext!.Post(_ => ShowToast("Roblox is opening soon", "Anti-AFK RBX", 2), null);
            await Task.Delay(TimeSpan.FromSeconds(3), token);
        }

        foreach (var robloxWin in windows.Where(w => w.IsValidWindow))
        {
            bool wasMinimized = robloxWin.IsMinimized;

            if (enableMaximization)
            {
                if (shouldHideWindow) robloxWin.SetTransparency(0);
                if (wasMinimized) robloxWin.Restore();
                robloxWin.Activate();
                await Task.Delay(TimeSpan.FromSeconds(maximizationDelaySec), token);
                if (wasMinimized) robloxWin.Minimize();
            }

            await PerformActionsOnWindowAsync(robloxWin, selectedAction, token);

            if (userWin?.IsValidWindow == true) userWin.Activate();
            robloxWin.SetTransparency(255);
        }
    }

    private static async Task PerformActionsOnWindowAsync(WindowInfo robloxWin, ActionTypeEnum selectedAction, CancellationToken token)
    {
        for (int i = 0; i < 3; i++)
        {
            robloxWin.Activate();
            await Task.Delay(interactionDelay, token);

            switch (selectedAction)
            {
                case ActionTypeEnum.Jump:
                    keyPresser.PressSpace();
                    break;
                case ActionTypeEnum.CameraShift:
                    keyPresser.MoveCamera();
                    break;
                default:
                    Console.WriteLine($"Unknown action: {selectedAction}");
                    keyPresser.PressSpace();
                    break;
            }

            await Task.Delay(interactionDelay, token);
        }
    }
}
