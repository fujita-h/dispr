using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Dispr;

internal class TrayApplicationContext : ApplicationContext
{
    private const string AppName = "dispr";
    private const string AutoStartRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _preventSleepItem;
    private readonly ToolStripMenuItem _onlyOnAcPowerItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly System.Windows.Forms.Timer _timer;
    private Settings _settings;

    public TrayApplicationContext()
    {
        _settings = Settings.Load();

        _enabledItem = new ToolStripMenuItem("画面ロックを防止する")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        _enabledItem.Click += OnEnabledChanged;

        _preventSleepItem = new ToolStripMenuItem("スリープも防止する")
        {
            Checked = _settings.PreventSleep,
            CheckOnClick = true
        };
        _preventSleepItem.Click += OnSettingChanged;

        _onlyOnAcPowerItem = new ToolStripMenuItem("AC電源接続時のみ有効")
        {
            Checked = _settings.OnlyOnAcPower,
            CheckOnClick = true
        };
        _onlyOnAcPowerItem.Click += OnSettingChanged;

        _autoStartItem = new ToolStripMenuItem("ログイン時に自動起動")
        {
            Checked = _settings.AutoStart,
            CheckOnClick = true
        };
        _autoStartItem.Click += OnAutoStartChanged;

        var optionsMenu = new ToolStripMenuItem("オプション");
        optionsMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _preventSleepItem,
            _onlyOnAcPowerItem,
            _autoStartItem
        });

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += OnExit;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _enabledItem,
            optionsMenu,
            new ToolStripSeparator(),
            exitItem
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer { Interval = 30000 };
        _timer.Tick += (_, _) => ApplyExecutionState();
        _timer.Start();

        SyncAutoStart();
        ApplyExecutionState();
    }

    private static Icon LoadIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("Dispr.Resources.icon.ico");
        if (stream != null)
        {
            return new Icon(stream);
        }

        return SystemIcons.Application;
    }

    private void ApplyExecutionState()
    {
        if (!_settings.Enabled)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _notifyIcon.Text = $"{AppName} - 無効";
            return;
        }

        if (_settings.OnlyOnAcPower &&
            SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _notifyIcon.Text = $"{AppName} - 一時停止 (バッテリー駆動中)";
            return;
        }

        uint flags = NativeMethods.ES_CONTINUOUS | NativeMethods.ES_DISPLAY_REQUIRED;

        if (_settings.PreventSleep)
        {
            flags |= NativeMethods.ES_SYSTEM_REQUIRED;
        }

        NativeMethods.SetThreadExecutionState(flags);
        _notifyIcon.Text = $"{AppName} - 有効";
    }

    private void OnEnabledChanged(object? sender, EventArgs e)
    {
        _settings.Enabled = _enabledItem.Checked;
        _settings.Save();
        ApplyExecutionState();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        _settings.PreventSleep = _preventSleepItem.Checked;
        _settings.OnlyOnAcPower = _onlyOnAcPowerItem.Checked;
        _settings.Save();
        ApplyExecutionState();
    }

    private void OnAutoStartChanged(object? sender, EventArgs e)
    {
        _settings.AutoStart = _autoStartItem.Checked;
        _settings.Save();
        SyncAutoStart();
    }

    private void SyncAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, writable: true);
            if (key == null) return;

            if (_settings.AutoStart)
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(AppName, exePath);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access may fail in restricted environments
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }
}
