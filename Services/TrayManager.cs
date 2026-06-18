using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace StandUpReminder.Services;

public sealed class TrayManager : IDisposable
{
    private const uint TrayIconId = 1;
    private readonly Action _openMain;
    private readonly HwndSource _source;
    private readonly IntPtr _iconHandle;
    private readonly bool _ownsIconHandle;
    private readonly ContextMenu _menu;
    private bool _disposed;

    public TrayManager(Action openMain, Action immediateRest, Action pause, Action openRecords, Action exit)
    {
        _openMain = openMain;
        (_iconHandle, _ownsIconHandle) = LoadTrayIcon();
        _menu = BuildMenu(openMain, immediateRest, pause, openRecords, exit);

        var parameters = new HwndSourceParameters("StandUpReminderTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        AddIcon();
    }

    public void ShowBalloon(string title, string text)
    {
        var data = CreateData();
        data.uFlags = NativeMethods.NifInfo;
        data.szInfoTitle = title;
        data.szInfo = text;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var data = CreateData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref data);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _menu.IsOpen = false;
        if (_ownsIconHandle)
        {
            NativeMethods.DestroyIcon(_iconHandle);
        }
        _disposed = true;
    }

    private ContextMenu BuildMenu(Action openMain, Action immediateRest, Action pause, Action openRecords, Action exit)
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(CreateItem("打开主界面", openMain));
        menu.Items.Add(CreateItem("立即休息", immediateRest));
        menu.Items.Add(CreateItem("暂停提醒 30 分钟", pause));
        menu.Items.Add(CreateItem("今日记录", openRecords));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem("退出", () =>
        {
            var result = MessageBox.Show("退出后将不再提醒休息，确认退出吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                exit();
            }
        }));
        return menu;
    }

    private static MenuItem CreateItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WmTrayIcon)
        {
            return IntPtr.Zero;
        }

        var mouseMessage = lParam.ToInt32();
        if (mouseMessage == NativeMethods.WmLButtonDblClk)
        {
            _openMain();
            handled = true;
        }
        else if (mouseMessage == NativeMethods.WmRButtonUp)
        {
            _menu.IsOpen = true;
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void AddIcon()
    {
        var data = CreateData();
        data.uFlags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref data);
    }

    private static (IntPtr Handle, bool OwnsHandle) LoadTrayIcon()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var handles = new IntPtr[1];
            var ids = new uint[1];
            if (NativeMethods.PrivateExtractIcons(exePath, 0, 32, 32, handles, ids, 1, 0) > 0 && handles[0] != IntPtr.Zero)
            {
                return (handles[0], true);
            }
        }

        return (NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IdiInformation), false);
    }

    private NativeMethods.NotifyIconData CreateData()
    {
        return new NativeMethods.NotifyIconData
        {
            cbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            hWnd = _source.Handle,
            uID = TrayIconId,
            uCallbackMessage = NativeMethods.WmTrayIcon,
            hIcon = _iconHandle,
            szTip = "久坐提醒",
            szInfo = "",
            szInfoTitle = ""
        };
    }
}
