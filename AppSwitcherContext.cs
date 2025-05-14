using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Text;

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

        public const int HOTKEY_ID = 9000;

        private NotifyIcon trayIcon;
        
        private IntPtr currentWindow = IntPtr.Zero;
        private IntPtr previousWindow = IntPtr.Zero;

        private uint modifiers = MOD_ALT;
        private uint key = (uint)Keys.Q;
        private uint alternativeKey = (uint)Keys.K;
        private bool useAlternativeKey = false;

        private MessageWindow messageWindow;
        
        private string logFilePath;

        public AppSwitcherContext()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty;
            logFilePath = Path.Combine(exeDir, "app_switcher_log.txt");
            
            File.WriteAllText(logFilePath, $"AppSwitcher started at {DateTime.Now}\r\n");
            LogMessage("Application starting...");
            
            messageWindow = new MessageWindow();
            messageWindow.HotkeyPressed += OnHotkeyPressed;
            messageWindow.SetParentContext(this);

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

        public void LogMessage(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n";
                File.AppendAllText(logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty;
                string iniPath = Path.Combine(exeDir, "settings.ini");
                LogMessage($"Loading settings from: {iniPath}");

                if (!File.Exists(iniPath))
                {
                    LogMessage("Settings file not found, creating default");
                    CreateDefaultIniFile(iniPath);
                }

                LogMessage("Reading settings from INI file");
                Dictionary<string, string> settings = ReadIniFile(iniPath);

                if (settings.TryGetValue("Modifiers", out string? modifiersStr) && modifiersStr != null)
                {
                    LogMessage($"Found Modifiers setting: '{modifiersStr}'");
                    modifiers = 0;
                    if (modifiersStr.Contains("ALT")) 
                    {
                        modifiers |= MOD_ALT;
                        LogMessage("Added ALT modifier");
                    }
                    if (modifiersStr.Contains("CTRL")) 
                    {
                        modifiers |= MOD_CONTROL;
                        LogMessage("Added CTRL modifier");
                    }
                    if (modifiersStr.Contains("SHIFT")) 
                    {
                        modifiers |= MOD_SHIFT;
                        LogMessage("Added SHIFT modifier");
                    }
                    if (modifiersStr.Contains("WIN")) 
                    {
                        modifiers |= MOD_WIN;
                        LogMessage("Added WIN modifier");
                    }
                    LogMessage($"Final modifiers value: 0x{modifiers:X4}");
                }
                else
                {
                    LogMessage("Modifiers setting not found or null, using default");
                }

                if (settings.TryGetValue("Key", out string? keyStr) && keyStr != null)
                {
                    LogMessage($"Found Key setting: '{keyStr}' (Length: {keyStr.Length})");
                    LogMessage($"Key bytes: {BitConverter.ToString(Encoding.UTF8.GetBytes(keyStr))}");
                    
                    if (keyStr == ";")
                    {
                        LogMessage("Detected semicolon key, setting to Keys.OemSemicolon");
                        key = (uint)Keys.OemSemicolon;
                    }
                    else if (keyStr == ",")
                    {
                        LogMessage("Detected comma key, setting to Keys.Oemcomma");
                        key = (uint)Keys.Oemcomma;
                    }
                    else if (keyStr == ".")
                    {
                        LogMessage("Detected period key, setting to Keys.OemPeriod");
                        key = (uint)Keys.OemPeriod;
                    }
                    else if (keyStr == "/")
                    {
                        LogMessage("Detected slash key, setting to Keys.OemQuestion");
                        key = (uint)Keys.OemQuestion;
                    }
                    else if (keyStr == "'")
                    {
                        LogMessage("Detected quote key, setting to Keys.OemQuotes");
                        key = (uint)Keys.OemQuotes;
                    }
                    else if (keyStr == "[")
                    {
                        LogMessage("Detected open bracket key, setting to Keys.OemOpenBrackets");
                        key = (uint)Keys.OemOpenBrackets;
                    }
                    else if (keyStr == "]")
                    {
                        LogMessage("Detected close bracket key, setting to Keys.OemCloseBrackets");
                        key = (uint)Keys.OemCloseBrackets;
                    }
                    else if (keyStr == "\\")
                    {
                        LogMessage("Detected backslash key, setting to Keys.OemBackslash");
                        key = (uint)Keys.OemBackslash;
                    }
                    else if (keyStr == "-")
                    {
                        LogMessage("Detected minus key, setting to Keys.OemMinus");
                        key = (uint)Keys.OemMinus;
                    }
                    else if (keyStr == "=")
                    {
                        LogMessage("Detected equals key, setting to Keys.Oemplus");
                        key = (uint)Keys.Oemplus;
                    }
                    else if (keyStr == "`")
                    {
                        LogMessage("Detected backtick key, setting to Keys.Oemtilde");
                        key = (uint)Keys.Oemtilde;
                    }
                    else if (Enum.TryParse(keyStr, out Keys parsedKey))
                    {
                        LogMessage($"Parsed key as enum value: {parsedKey} (0x{(uint)parsedKey:X4})");
                        key = (uint)parsedKey;
                    }
                    else
                    {
                        LogMessage($"WARNING: Could not parse key '{keyStr}', using default");
                    }
                }
                
                if (settings.TryGetValue("AlternativeKey", out string? altKeyStr) && altKeyStr != null)
                {
                    LogMessage($"Found AlternativeKey setting: '{altKeyStr}'");
                    
                    if (altKeyStr == ";")
                    {
                        LogMessage("Detected semicolon key for alternative, setting to Keys.OemSemicolon");
                        alternativeKey = (uint)Keys.OemSemicolon;
                    }
                    else if (Enum.TryParse(altKeyStr, out Keys parsedAltKey))
                    {
                        LogMessage($"Parsed alternative key as enum value: {parsedAltKey} (0x{(uint)parsedAltKey:X4})");
                        alternativeKey = (uint)parsedAltKey;
                    }
                    
                    useAlternativeKey = alternativeKey != key;
                    LogMessage($"Alternative key is {(useAlternativeKey ? "enabled" : "disabled")}");
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
                "; Key: Any key from the Keys enum (e.g., Q, W, F1, etc.) or symbol keys (;, ,, ., etc.)\r\n" +
                "; For testing, try using K if semicolon (;) doesn't work\r\n" +
                "Key=Q\r\n" +
                "; Alternative key to try if the primary key doesn't work\r\n" +
                "AlternativeKey=K\r\n";

            File.WriteAllText(path, defaultContent);
        }

        private Dictionary<string, string> ReadIniFile(string path)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            LogMessage($"Reading INI file: {path}");
            
            string[] lines = File.ReadAllLines(path);
            LogMessage($"INI file contains {lines.Length} lines");
            
            LogMessage("Raw INI file content:");
            for (int i = 0; i < lines.Length; i++)
            {
                LogMessage($"  Line {i+1}: '{lines[i]}'");
            }
            
            foreach (string line in lines)
            {
                LogMessage($"Processing line: '{line}'");
                string trimmedLine = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    LogMessage("  Skipping empty line");
                    continue;
                }
                
                if (trimmedLine.Length > 0 && trimmedLine[0] == ';')
                {
                    LogMessage("  Skipping comment line");
                    continue;
                }
                
                int separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex > 0)
                {
                    string keyName = trimmedLine.Substring(0, separatorIndex).Trim();
                    string rawValue = trimmedLine.Substring(separatorIndex + 1);
                    LogMessage($"  Found key-value pair: '{keyName}'='{rawValue}'");
                    
                    string finalValue;
                    if (keyName == "Key")
                    {
                        LogMessage($"  Special handling for Key setting");
                        LogMessage($"  Raw value: '{rawValue}' (Length: {rawValue.Length})");
                        LogMessage($"  Raw value bytes: {BitConverter.ToString(Encoding.UTF8.GetBytes(rawValue))}");
                        
                        int commentIndex = rawValue.IndexOf(" ;");
                        if (commentIndex > 0)
                        {
                            finalValue = rawValue.Substring(0, commentIndex).Trim();
                            LogMessage($"  Found comment at position {commentIndex}, trimmed value: '{finalValue}'");
                        }
                        else
                        {
                            finalValue = rawValue.Trim();
                            LogMessage($"  No comment found, trimmed value: '{finalValue}'");
                        }
                        
                        if (finalValue == ";")
                        {
                            LogMessage("  IMPORTANT: Semicolon key detected!");
                        }
                    }
                    else
                    {
                        finalValue = rawValue.Trim();
                        LogMessage($"  Standard handling, trimmed value: '{finalValue}'");
                    }
                    
                    settings[keyName] = finalValue;
                    LogMessage($"  Added to settings dictionary: '{keyName}'='{finalValue}'");
                }
                else
                {
                    LogMessage($"  Line does not contain '=' separator, skipping");
                }
            }
            
            LogMessage($"Finished reading INI file, found {settings.Count} settings");
            foreach (var kvp in settings)
            {
                LogMessage($"  Setting: '{kvp.Key}'='{kvp.Value}'");
            }
            
            return settings;
        }

        public const int ALTERNATIVE_HOTKEY_ID = 9001;
        
        private void RegisterHotKey()
        {
            uint finalModifiers = modifiers | MOD_NOREPEAT;
            
            LogMessage($"Registering primary hotkey with Windows:");
            LogMessage($"  Window Handle: 0x{messageWindow.Handle.ToInt64():X8}");
            LogMessage($"  Hotkey ID: {HOTKEY_ID}");
            LogMessage($"  Modifiers: 0x{modifiers:X4} (raw), 0x{finalModifiers:X4} (with MOD_NOREPEAT)");
            LogMessage($"  Key value: 0x{key:X4} ({(Keys)key})");
            
            bool success = RegisterHotKey(messageWindow.Handle, HOTKEY_ID, finalModifiers, key);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                LogMessage($"Failed to register primary hotkey! Win32 error code: {errorCode}");
                MessageBox.Show($"Failed to register primary hotkey. Error code: {errorCode}",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                LogMessage("Primary hotkey registered successfully");
            }
            
            if (useAlternativeKey)
            {
                LogMessage($"Registering alternative hotkey with Windows:");
                LogMessage($"  Window Handle: 0x{messageWindow.Handle.ToInt64():X8}");
                LogMessage($"  Hotkey ID: {ALTERNATIVE_HOTKEY_ID}");
                LogMessage($"  Modifiers: 0x{modifiers:X4} (raw), 0x{finalModifiers:X4} (with MOD_NOREPEAT)");
                LogMessage($"  Key value: 0x{alternativeKey:X4} ({(Keys)alternativeKey})");
                
                bool altSuccess = RegisterHotKey(messageWindow.Handle, ALTERNATIVE_HOTKEY_ID, finalModifiers, alternativeKey);
                if (!altSuccess)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    LogMessage($"Failed to register alternative hotkey! Win32 error code: {errorCode}");
                }
                else
                {
                    LogMessage("Alternative hotkey registered successfully");
                }
            }
        }

        private void UnregisterHotKey()
        {
            LogMessage("Unregistering hotkeys");
            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
            
            if (useAlternativeKey)
            {
                UnregisterHotKey(messageWindow.Handle, ALTERNATIVE_HOTKEY_ID);
            }
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            LogMessage("Hotkey pressed event triggered");
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
        private AppSwitcherContext? parentContext;
        
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
        
        public void SetParentContext(AppSwitcherContext context)
        {
            parentContext = context;
        }
        
        private void LogMessage(string message)
        {
            parentContext?.LogMessage(message);
        }
        
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        
        private int messageCount = 0;
        private DateTime lastLogTime = DateTime.MinValue;
        
        protected override void WndProc(ref Message m)
        {
            if (parentContext != null)
            {
                messageCount++;
                if ((DateTime.Now - lastLogTime).TotalSeconds >= 5)
                {
                    LogMessage($"Received {messageCount} Windows messages in the last 5 seconds");
                    messageCount = 0;
                    lastLogTime = DateTime.Now;
                }
                
                if (m.Msg == WM_HOTKEY)
                {
                    int id = m.WParam.ToInt32();
                    int modifierAndKey = m.LParam.ToInt32();
                    int modifiers = modifierAndKey & 0xFFFF;
                    int key = (modifierAndKey >> 16) & 0xFFFF;
                    
                    LogMessage($"WM_HOTKEY message received:");
                    LogMessage($"  ID: {id} (Primary: {AppSwitcherContext.HOTKEY_ID}, Alternative: {AppSwitcherContext.ALTERNATIVE_HOTKEY_ID})");
                    LogMessage($"  Modifiers: 0x{modifiers:X4}");
                    LogMessage($"  Key: 0x{key:X4} ({(Keys)key})");
                    
                    if (id == AppSwitcherContext.HOTKEY_ID || id == AppSwitcherContext.ALTERNATIVE_HOTKEY_ID)
                    {
                        string keyType = id == AppSwitcherContext.HOTKEY_ID ? "primary" : "alternative";
                        LogMessage($"  Invoking HotkeyPressed event for {keyType} hotkey");
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        LogMessage("  Ignoring hotkey with wrong ID");
                    }
                }
                else if (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN)
                {
                    int keyCode = m.WParam.ToInt32();
                    LogMessage($"Key pressed: 0x{keyCode:X4} ({(Keys)keyCode})");
                    
                    if (m.Msg == WM_SYSKEYDOWN && keyCode == (int)Keys.OemSemicolon)
                    {
                        LogMessage("Alt+; key combination detected directly");
                        LogMessage("This suggests the hotkey registration might not be working correctly");
                    }
                }
            }
            
            base.WndProc(ref m);
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }
    }
}
