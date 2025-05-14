using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;

namespace AppSwitcher
{
    public class AppSwitcherContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint GW_HWNDNEXT = 2;
        private const uint GW_HWNDPREV = 3;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private const int HOTKEY_ID = 9000;

        private NotifyIcon trayIcon;
        
        private IntPtr currentWindow = IntPtr.Zero;
        private IntPtr previousWindow = IntPtr.Zero;

        private uint modifiers = MOD_ALT;
        private uint key = (uint)Keys.Q;

        private MessageWindow messageWindow;

        public AppSwitcherContext()
        {
            messageWindow = new MessageWindow();
            messageWindow.HotkeyPressed += OnHotkeyPressed;

            LoadSettings();

            RegisterHotKey();

            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application, // Default icon, should be replaced with custom icon
                ContextMenuStrip = CreateContextMenu(),
                Visible = true,
                Text = "App Switcher"
            };

            StartWindowTracking();
        }

        private void LoadSettings()
        {
            try
            {
                string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty;
                string iniPath = Path.Combine(exeDir, "settings.ini");

                if (!File.Exists(iniPath))
                {
                    CreateDefaultIniFile(iniPath);
                }

                Dictionary<string, string> settings = ReadIniFile(iniPath);

                if (settings.TryGetValue("Modifiers", out string modifiersStr))
                {
                    modifiers = 0;
                    if (modifiersStr.Contains("ALT")) modifiers |= MOD_ALT;
                    if (modifiersStr.Contains("CTRL")) modifiers |= MOD_CONTROL;
                    if (modifiersStr.Contains("SHIFT")) modifiers |= MOD_SHIFT;
                    if (modifiersStr.Contains("WIN")) modifiers |= MOD_WIN;
                }

                if (settings.TryGetValue("Key", out string keyStr) && 
                    Enum.TryParse(keyStr, out Keys parsedKey))
                {
                    key = (uint)parsedKey;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                modifiers = MOD_ALT;
                key = (uint)Keys.Q;
            }
        }

        private void CreateDefaultIniFile(string path)
        {
            string defaultContent = 
                "; AppSwitcher Settings\r\n" +
                "; Modifiers: ALT, CTRL, SHIFT, WIN (comma separated)\r\n" +
                "Modifiers=ALT\r\n" +
                "; Key: Any key from the Keys enum (e.g., Q, W, F1, etc.)\r\n" +
                "Key=Q\r\n";

            File.WriteAllText(path, defaultContent);
        }

        private Dictionary<string, string> ReadIniFile(string path)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmedLine = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue;
                
                int separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex > 0)
                {
                    string key = trimmedLine.Substring(0, separatorIndex).Trim();
                    string value = trimmedLine.Substring(separatorIndex + 1).Trim();
                    settings[key] = value;
                }
            }
            
            return settings;
        }

        private void RegisterHotKey()
        {
            uint finalModifiers = modifiers | MOD_NOREPEAT;
            
            if (!RegisterHotKey(messageWindow.Handle, HOTKEY_ID, finalModifiers, key))
            {
                MessageBox.Show("Failed to register hotkey. The application may not work correctly.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UnregisterHotKey()
        {
            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            SwitchToPreviousWindow();
        }

        private void StartWindowTracking()
        {
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 100; // Check every 100ms
            timer.Tick += (sender, e) => TrackWindowChanges();
            timer.Start();
        }

        private void TrackWindowChanges()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            
            if (foregroundWindow != currentWindow && foregroundWindow != IntPtr.Zero)
            {
                previousWindow = currentWindow;
                currentWindow = foregroundWindow;
            }
        }

        private void SwitchToPreviousWindow()
        {
            if (previousWindow != IntPtr.Zero)
            {
                SetForegroundWindow(previousWindow);
                
                IntPtr temp = currentWindow;
                currentWindow = previousWindow;
                previousWindow = temp;
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            
            menu.Items.Add("Exit", null, (sender, e) => Exit());
            
            return menu;
        }

        private void OpenSettings()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty;
            string iniPath = Path.Combine(exeDir, "settings.ini");
            
            System.Diagnostics.Process.Start("notepad.exe", iniPath);
        }

        private void ReloadSettings()
        {
            UnregisterHotKey();
            
            LoadSettings();
            
            RegisterHotKey();
            
            trayIcon.ShowBalloonTip(
                2000, 
                "Settings Reloaded", 
                "Hotkey settings have been updated.", 
                ToolTipIcon.Info);
        }

        private void Exit()
        {
            UnregisterHotKey();
            trayIcon.Visible = false;
            
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotKey();
                if (trayIcon != null) trayIcon.Dispose();
                if (messageWindow != null) messageWindow.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }

    public class MessageWindow : Form
    {
        private const int WM_HOTKEY = 0x0312;
        
        public event EventHandler? HotkeyPressed;
        
        public MessageWindow()
        {
            this.Text = "AppSwitcherMessageWindow";
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(0, 0);
            this.Opacity = 0;
        }
        
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
            
            base.WndProc(ref m);
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }
    }
}
