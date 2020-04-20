using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace csgtest
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      SetProcessDPIAware();
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new MainForm());
    }
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool SetProcessDPIAware();
  }
}


