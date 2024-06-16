using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using WinUIEx;

namespace Simple_Icon_File_Maker;

public sealed partial class MainWindow : WindowEx
{
    private readonly AppWindow? m_AppWindow;
    public Frame ContentFrame { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();

        this.SystemBackdrop = new MicaBackdrop();
        m_AppWindow = GetAppWindowForCurrentWindow();
        m_AppWindow.SetIcon("SimpleIconMaker.ico");
        m_AppWindow.Title = "Simple Icon File Maker";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }
}