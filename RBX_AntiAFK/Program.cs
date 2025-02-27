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
using Newtonsoft.Json.Linq;

namespace RBX_AntiAFK;

public class Settings
{
    public string ActionType { get; set; } = "Jump";
    public bool EnableDelay { get; set; } = false;
    public decimal DelaySeconds { get; set; } = 3;
    public bool HideWindowContents { get; set; } = false;

    private const string SettingsFile = "settings.json";

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loadedSettings = JsonConvert.DeserializeObject<Settings>(json);

                if (loadedSettings != null)
                {
                    ActionType = loadedSettings.ActionType;
                    EnableDelay = loadedSettings.EnableDelay;
                    DelaySeconds = loadedSettings.DelaySeconds;
                    HideWindowContents = loadedSettings.HideWindowContents;
                }
                else
                {
                    Save();
                }
            }
            else
            {
                Save();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}

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
    private static CheckBox enableDelayCheckBox = new();
    private static NumericUpDown delaySecondsNumericUpDown = new();
    private static CheckBox hideWindowContentsCheckBox = new();

    // Settings
    private static readonly Settings settings = new();
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
        return version?.ToString() ?? "";
    }

    private static void ShowAbout()
    {
        using Form aboutForm = new();

        aboutForm.Text = "About AntiAFK-Roblox";
        aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        aboutForm.MaximizeBox = false;
        aboutForm.MinimizeBox = false;
        aboutForm.StartPosition = FormStartPosition.CenterScreen;
        aboutForm.ClientSize = new Size(400, 160);
        aboutForm.BackColor = Color.White;

        // 1. Container for logo and text
        Panel contentPanel = new()
        {
            Size = new Size(aboutForm.ClientSize.Width - 40, 100),
            Location = new Point(20, 10)
        };
        aboutForm.Controls.Add(contentPanel);

        // 2. Logo (PictureBox) on the left
        PictureBox logoPictureBox = new()
        {
            Image = Properties.Resources.ProjectImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(80, 80),
            Location = new Point(0, 10)
        };
        contentPanel.Controls.Add(logoPictureBox);

        // 3. Container for text to the right of the logo
        Panel textPanel = new()
        {
            Location = new Point(logoPictureBox.Right + 10, 10),
            Size = new Size(contentPanel.Width - logoPictureBox.Width - 10, 100)
        };
        contentPanel.Controls.Add(textPanel);

        // 4. Application name
        Label titleLabel = new()
        {
            Text = "AntiAFK-Roblox",
            Font = new Font("Arial", 14, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };
        textPanel.Controls.Add(titleLabel);

        // 5. Version and author information
        Label authorLabel = new()
        {
            Text = "by JunkBeat",
            Font = new Font("Arial", 10),
            AutoSize = true,
            Location = new Point(0, titleLabel.Bottom)
        };
        textPanel.Controls.Add(authorLabel);

        Label versionLabel = new()
        {
            Text = $"Version: {GetAssemblyVersion<Program>()}",
            Font = new Font("Arial", 10),
            AutoSize = true,
            Location = new Point(0, authorLabel.Bottom + 5)
        };
        textPanel.Controls.Add(versionLabel);

        // 6. Link container (GitHub)
        Panel githubPanel = new()
        {
            Size = new Size(150, 20),
            Location = new Point(versionLabel.Left, versionLabel.Bottom + 8)
        };
        textPanel.Controls.Add(githubPanel);

        // 7. GitHub (clickable link)
        Label githubLink = new()
        {
            Text = "GitHub",
            Font = new Font("Arial", 10, FontStyle.Bold),
            ForeColor = Color.Blue,
            AutoSize = true,
            Location = new Point(0, 2),
            Cursor = Cursors.Hand
        };
        githubLink.Click += (sender, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/JunkBeat/AntiAFK-Roblox",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open GitHub: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        githubPanel.Controls.Add(githubLink);

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
                await Task.Delay(30);
                KeyPresser.PressSpace();
                await Task.Delay(30);
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
            bool enableDelay = false;
            int delaySeconds = 3;
            bool shouldHide = false;

            _uiContext!.Send(_ =>
            {
                selectedAction = actionTypeComboBox.SelectedItem?.ToString() ?? "";
                enableDelay = enableDelayCheckBox.Checked;
                shouldHide = hideWindowContentsCheckBox.Checked;
                delaySeconds = (int)delaySecondsNumericUpDown.Value;
            }, null);

            if (enableDelay && allowNotifications)
            {
                // UI interaction, so use _uiContext.Send (synchronous)
                _uiContext.Send(_ => ShowToast("Roblox is opening soon", "Anti-AFK RBX", 2), null);
                await SleepAsync(3000, token);
            }

            foreach (var robloxWin in windows.Where(w => w.IsValidWindow))
            {
                bool wasMinimized = robloxWin.IsMinimized;

                if (enableDelay)
                {
                    if (shouldHide) robloxWin.SetTransparency(0);
                    if (wasMinimized) robloxWin.Restore();
                    robloxWin.Activate();
                    await SleepAsync(TimeSpan.FromSeconds(delaySeconds), token);
                    if (wasMinimized) robloxWin.Minimize();
                }

                // Perform three times for greater reliability
                for (int i = 0; i < 3; i++)
                {
                    robloxWin.Activate();
                    await SleepAsync(30, token);

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
                    await SleepAsync(30, token);
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
