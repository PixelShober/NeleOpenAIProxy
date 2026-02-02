using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace NeleDesktop.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly Window _window;
    private HwndSource? _source;
    private int _currentId;

    public HotkeyService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public event EventHandler? HotkeyPressed;

    public void Initialize()
    {
        _source = PresentationSource.FromVisual(_window) as HwndSource;
        _source?.AddHook(WndProc);
    }

    public bool Register(string gestureText)
    {
        Unregister();

        if (string.IsNullOrWhiteSpace(gestureText))
        {
            return false;
        }

        if (_source is null)
        {
            return false;
        }

        var converter = new KeyGestureConverter();
        if (converter.ConvertFromString(gestureText) is not KeyGesture gesture)
        {
            return false;
        }

        var modifiers = ConvertModifiers(gesture.Modifiers);
        if (modifiers == 0)
        {
            return false;
        }

        var key = KeyInterop.VirtualKeyFromKey(gesture.Key);
        _currentId = GetHashCode();
        return RegisterHotKey(_source.Handle, _currentId, modifiers, (uint)key);
    }

    public void Unregister()
    {
        if (_source is null || _currentId == 0)
        {
            return;
        }

        UnregisterHotKey(_source.Handle, _currentId);
        _currentId = 0;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == _currentId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= ModWin;
        }

        return result;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
