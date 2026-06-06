using System;
using System.Windows.Forms;

namespace ModernTreeListView.Demo;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new DemoForm());
    }
}
