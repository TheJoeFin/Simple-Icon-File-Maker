using WinUIEx;

namespace Simple_Icon_File_Maker;

public sealed partial class MainWindow : WindowEx
{
    public readonly nint WindowHandle;

    public MainWindow()
    {
        InitializeComponent();

        Content = null;
        WindowHandle = this.GetWindowHandle();
    }
}