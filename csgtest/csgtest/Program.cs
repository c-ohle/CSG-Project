using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
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

  struct Vector3
  {
    public double x, y, z;
    public Vector3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    public static explicit operator PointF(in Vector3 p) => new PointF((float)p.x, (float)p.y);
    public static implicit operator Vector3(PointF p) => new Vector3(p.X, p.Y,0);
  }
}


