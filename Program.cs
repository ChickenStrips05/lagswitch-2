using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.IO;


internal static class Program
{   

    // Windows API for hotkey registration
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int HOTKEY_ID = 9000;
    const uint MOD_NONE = 0x0000;
    const uint VK_BACKTICK = 0xC0; // ` key

    static bool isBlocked = false;
    static string ruleName = "BlockAllOutbound";
    
    [STAThread]
    static void Main()
    {
        if (!IsRunningAsAdmin())
        {
            RelaunchAsAdmin();
            return; // Exit current process
        }

        var messageWindow = new MessageWindow();
        RegisterHotKey(messageWindow.Handle, HOTKEY_ID, MOD_NONE, VK_BACKTICK);

        Application.Run(messageWindow);
    }

    static bool IsRunningAsAdmin()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
    static void RelaunchAsAdmin()
    {
        try
        {
            var exeName = Process.GetCurrentProcess()?.MainModule?.FileName;
            if (exeName == null) {return;}

            ProcessStartInfo startInfo = new ProcessStartInfo(exeName)
            {
                UseShellExecute = true,
                Verb = "runas" // 🔐 This triggers the UAC prompt
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show("This app requires administrator privileges.\n\n" + ex.Message,
                            "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public class MessageWindow : Form
    {
        private NotifyIcon trayIcon;


        

public MessageWindow()
        {
            // Hide window completely
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0;
            this.Width = 0;
            this.Height = 0;

            byte[] iconBytes = lagswitch_2.Properties.Resources._lock;
            using var stream = new MemoryStream(iconBytes);
            Icon icon = new Icon(stream);

            // Setup tray icon
            trayIcon = new NotifyIcon()
            {
                Icon = icon,
                Visible = true,
                Text = "LagSwitch 2"
            };
            
            var menu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            };
            menu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = menu;
        }

        protected override void OnLoad(EventArgs e)
        {
            this.Visible = false;
            this.ShowInTaskbar = false;
            base.OnLoad(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleBlock();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        private void ToggleBlock()
        {
            if (isBlocked)
            {
                RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");
                ShowPopup("UNBLOCKED ✅", System.Drawing.Color.LimeGreen);
            }
            else
            {
                RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block protocol=any profile=any");
                ShowPopup("BLOCKED 🔏", System.Drawing.Color.OrangeRed);
            }
            isBlocked = !isBlocked;
        }

        private void RunNetsh(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo("netsh", args)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to execute netsh command.\n" + ex.Message,
                    "Firewall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private static Form? currentPopup = null;
        private void ShowPopup(string message, System.Drawing.Color color)
        {
            currentPopup?.Close();

            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Width = 220,
                Height = 50,
                TopMost = true,
                Opacity = 0.8,
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                ShowInTaskbar = false
            };

            var label = new System.Windows.Forms.Label
            {
                Text = message,
                ForeColor = color,
                BackColor = popup.BackColor,
                Font = new System.Drawing.Font("Segoe UI Emoji", 12, System.Drawing.FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            
            popup.Controls.Add(label);

            if (Screen.PrimaryScreen == null) { return; }

            var screen = Screen.PrimaryScreen.WorkingArea;
            
            popup.Left = (screen.Width - popup.Width) / 2;
            popup.Top = (int)(screen.Height * 0.07);

            currentPopup = popup;

            popup.Shown += async (s, e) =>
            {
                await Task.Delay(1500);
                popup.Close();
            };

            popup.Show();
        }
    }
}
