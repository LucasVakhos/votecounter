using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;

namespace Rhymers;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        WindowsFormsSettings.EnableFormSkins();
        WindowsFormsSettings.DefaultFont = new Font("Segoe UI", 10F);
        UserLookAndFeel.Default.SetSkinStyle("WXI");
        Application.Run(new MainForm());
    }
}
