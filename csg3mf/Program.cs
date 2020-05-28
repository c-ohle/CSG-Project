using csg3mf.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static csg3mf.CDX;

namespace csg3mf
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      Native.SetProcessDPIAware();
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
      Application.Run(new MainFrame());
      Factory.SetDevice(0xffffffff);
    }
  }

  class MainFrame : UIForm
  {
    public MainFrame()
    {
      Icon = Icon.FromHandle(Native.LoadIcon(Marshal.GetHINSTANCE(GetType().Module), (IntPtr)32512));
      StartPosition = FormStartPosition.WindowsDefaultBounds;
      MainMenuStrip = new MenuStrip();
      MainMenuStrip.Items.AddRange(new ToolStripItem[]
      {
        new MenuItem("&File",
          new MenuItem(1000, "&New", Keys.Control | Keys.N),
          new MenuItem(1010, "&Open...", Keys.Control | Keys.O),
          new MenuItem(1020, "&Save", Keys.Control | Keys.S),
          new MenuItem(1025, "Save &as...", Keys.Control | Keys.Shift | Keys.S),
          new ToolStripSeparator(),
          new MenuItem(1040, "&Print...", Keys.Control | Keys.P),
          new ToolStripSeparator(),
          new MenuItem(1050, "Last Files"),
          new ToolStripSeparator(),
          new MenuItem(1100, "&Exit")),
        new MenuItem("&Edit",
          new MenuItem (2010, "&Undo", Keys.Back|Keys. Alt) { Visible = false },
          new MenuItem(2010, "&Undo", Keys.Z|Keys.Control ),
          new MenuItem(2011, "&Redo", Keys.Back|Keys.Control) { Visible = false },
          new MenuItem(2011, "&Redo", Keys.Y|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2020, "&Cut", Keys.X|Keys.Control ),
          new MenuItem(2030, "Cop&y", Keys.C|Keys.Control ),
          new MenuItem(2040, "&Paste", Keys.V|Keys.Control ),
          new MenuItem(2015, "Delete", Keys.Delete) { Visible = false },
          new ToolStripSeparator(),
          new MenuItem(2045, "Make Uppercase", Keys.U|Keys.Control|Keys.Shift ),
          new MenuItem(2046, "Make Lowercase", Keys.U|Keys.Control ),
          new MenuItem(2060, "Select &all", Keys.A|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2065, "&Find...", Keys.F|Keys.Control ),
          new MenuItem(2066, "Find forward", Keys.F3|Keys.Control ),
          new MenuItem(2067, "Find next", Keys.F3 ),
          new MenuItem(2068, "Find prev", Keys.F3|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(2088, "Rename...", Keys.R|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2062, "Toggle", Keys.T|Keys.Control ),
          new MenuItem(2063, "Toggle all", Keys.T|Keys.Shift|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5027, "Goto &Definition", Keys.F12 ),
          new ToolStripSeparator(),
          new MenuItem(2100, "Properties...", Keys.Alt | Keys.Enter)
        ),
        new MenuItem("&Script",
          new MenuItem(5011, "&Run", Keys.F5|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5010, "Run &Debug", Keys.F5 ),
          new MenuItem(5016, "Ste&p", Keys.F10 ),
          new MenuItem(5015, "Step &Into", Keys.F11 ),
          new MenuItem(5017, "Step Ou&t", Keys.F11|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(5014, "&Compile", Keys.F6 ),
          new ToolStripSeparator(),
          new MenuItem(5020, "Toggle &Breakpoint", Keys.F9 ),
          new MenuItem(5021, "Delete All Breakpoints", Keys.F9|Keys.Shift|Keys.Control ),
          //new ToolStripSeparator(),
          //new MenuItem(5040 ,"Break at Exceptions"),
          new ToolStripSeparator(),
          new MenuItem(5013, "&Stop", Keys.F5|Keys.Shift )
        ),
        new MenuItem("&Extra",
          new MenuItem(5100, "&Format", Keys.E|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5110 , "Remove Unused Usings"),
          new MenuItem(5111 , "Sort Usings"),
          new ToolStripSeparator(),
          new MenuItem(5025 ,"&IL Code..."),
          new MenuItem(1120, "3MF Content..."),
          new ToolStripSeparator(),
          new MenuItem(5105 , "&Protect...")
        ),
        new MenuItem("&View",
          new MenuItem(2300, "&Center", Keys.Alt | Keys.C),
          new ToolStripSeparator(),
          new MenuItem(2214, "Outlines"),
          new MenuItem(2210, "Bounding Box"),
          new MenuItem(2211, "Pivot"),
          new MenuItem(2213, "Wireframe"),
          new MenuItem(2212, "Normals"),
          new ToolStripSeparator(),
          new MenuItem(2220, "Shadows")),
        new MenuItem("&Settings",
          new MenuItem(3015, "Driver"),
          new ToolStripSeparator(),
          new MenuItem(3016, "Samples"),
          new ToolStripSeparator(),
          new MenuItem(3017, "Set as Default")),
        new MenuItem("&Help",
          new MenuItem(5050, "&Help", Keys.F1 ),
          new ToolStripSeparator(),
          new MenuItem(5070 , "&About...")
        ),
       });
      Controls.Add(MainMenuStrip);
      //Controls.Add(new StatusBar());// BackColor = Color.FromArgb(80, 85, 130) });

      var reg = Application.UserAppDataRegistry;
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
      {
        try { Open(args[1]); return; }
        catch (Exception ex) { MessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(0); }
      }
      if (reg.GetValue("lod") is string lod)
      {
        if (!Debugger.IsAttached) reg.DeleteValue("lod", false);
        try { var lwp = reg.GetValue("lwp"); if (lwp is long) setrestore((long)lwp); Open(lod); }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
      }
      if (path == null) OnNew(null);
    }
    string path; Container cont; CDXView view; NeuronEditor edit;
    void UpdateTitle()
    {
      Text = string.Format("{0} - {1} {2} Bit {3}", path ?? string.Format("({0})", "Untitled"), Application.ProductName, Environment.Is64BitProcess ? "64" : "32", COM.DEBUG ? "Debug" : "Release");
    }
    void Open(string path)
    {
      string script = null;
      if (path == null) { cont = new Container(null); script = "using static csg3mf.CSG;\n"; }
      else cont = new Container(Import3MF(path, out script, out _));
      DoubleBuffered = true;
      while (Controls[0] is Frame) Controls[0].Dispose(); edit = null;
      this.path = path; UpdateTitle();
      if (script != null)
      {
        NeuronEditor.InitNeuron(cont, script);
        var sv = (ScriptView)ShowView(typeof(ScriptView), cont, DockStyle.Left, ClientSize.Width / 2);
        edit = sv.edit; sv.Text = "Script"; Neuron.Debugger = (p, v) => edit.Show(v);
      }
      view = (CDXView)ShowView(typeof(CDXView), cont.Nodes, DockStyle.Fill);
      view.Text = "Camera"; cont.OnUpdate = view.Invalidate; view.infos = cont.Infos;
      Update(); DoubleBuffered = false;
      if (path != null) mru(path, path);
    }
    protected override int OnCommand(int id, object test)
    {
      switch (id)
      {
        case 1000: return OnNew(test);
        case 1010: return OnOpen(test);
        case 1020: return OnSave(test, false);
        case 1025: return OnSave(test, true);
        case 1050: return OnLastFiles(test);
        case 1100: if (test == null) Close(); return 1;
        case 2100: if (test == null) { var p = (Properties)ShowView(typeof(Properties), view, DockStyle.Left); /*p.SelectedObject = p;*/ } return 1;
        //case 2100: if (test == null) { var p = (PropertyGrid)ShowView(typeof(PropertyGrid)); p.SelectedObject = p; } return 1;
        case 1120: return OnShow3MF(test);
      }
      return base.OnCommand(id, test);
    }
    class Properties : PropertyGrid, ICommandTarget
    {
      class CNode
      {
        internal INode p;
        public string Name { get => p.Name; set => p.Name = value; }
        public Color Color { get => Color.FromArgb(unchecked((int)p.Color)); set => p.Color = (uint)value.ToArgb(); }
        
        public CSG.Rational LocationX { get => p.Transform[09]; set { var t = p.Transform; t[09] = value; p.Transform = t; } }
        public CSG.Rational LocationY { get => p.Transform[10]; set { var t = p.Transform; t[10] = value; p.Transform = t; } }
        public CSG.Rational LocationZ { get => p.Transform[11]; set { var t = p.Transform; t[11] = value; p.Transform = t; } }

        public float fLocationX { get => p.TransformF._41; set { var t = p.TransformF; t._41 = value; p.TransformF = t; } }
        public float fLocationY { get => p.TransformF._42; set { var t = p.TransformF; t._42 = value; p.TransformF = t; } }
        public float fLocationZ { get => p.TransformF._43; set { var t = p.TransformF; t._43 = value; p.TransformF = t; } }
      }

      protected override void OnPropertyValueChanged(PropertyValueChangedEventArgs e)
      {
        var view = (CDXView)Tag;
        view.Invalidate(); Refresh();
      }

      INode[] nodes;
      int ICommandTarget.OnCommand(int id, object test)
      {
        switch (id)
        {
          case 0:
            {
              var view = (CDXView)Tag;
              var scene = view.view.Scene;
              if (nodes != null)
              {
                int i = 0, a = -1, b;
                for (; (b = scene.Select(a, 1)) != -1 && i < nodes.Length && scene[b] == nodes[i]; a = b, i++) ;
                if (b != -1 || i != nodes.Length) nodes = null;
              }
              if (nodes == null)
              {
                nodes = scene.Selection().ToArray();
                SelectedObjects = nodes.Select(p => new CNode { p = p }).ToArray();
                return 0;
              }
              if (ActiveControl != null) return 0;
              Refresh(); return 0;
            }
        }
        return 0;
      }
    }

    int OnNew(object test)
    {
      if (test != null) return 1;
      if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Open(null); return 1;
    }
    int OnOpen(object test)
    {
      if (test != null) return 1; if (edit != null && edit.askstop()) return 1;
      var dlg = new OpenFileDialog() { Filter = "3MF files|*.3mf|All files|*.*" };
      if (path != null) dlg.InitialDirectory = Path.GetDirectoryName(path);
      if (dlg.ShowDialog(this) != DialogResult.OK) return 1;
      if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Cursor.Current = Cursors.WaitCursor; Open(dlg.FileName);
      return 1;
    }
    unsafe int OnSave(object test, bool saveas)
    {
      if (test != null) return 1;
      var s = path;
      if (saveas || s == null)
      {
        var dlg = new SaveFileDialog() { Filter = "3MF file|*.3mf|All files|*.*", DefaultExt = "3mf" };
        if (s != null) { dlg.InitialDirectory = Path.GetDirectoryName(s); dlg.FileName = Path.GetFileName(s); }
        if (dlg.ShowDialog(this) != DialogResult.OK) return 1; s = dlg.FileName;
      }
      Cursor.Current = Cursors.WaitCursor;
      var str = COM.SHCreateMemStream();
      view.view.Thumbnail(256, 256, 4, 0x00ffffff, str);
      cont.Nodes.Export3MF(s, str, edit != null ? edit.EditText : null, null);
      if (path != s) { path = s; UpdateTitle(); mru(path, path); }
      IsModified = false; Cursor.Current = Cursors.Default;
      return 1;
    }
    int OnLastFiles(object test)
    {
      if (test is string path)
      {
        if (edit != null && edit.askstop()) return 1;
        if (!AskSave()) return 1;
        mru(null, path); Cursor.Current = Cursors.WaitCursor; Open(path);
        return 1;
      }
      var item = test as MenuItem; foreach (var s in mru(null, null)) item.DropDownItems.Add(s).Tag = s;
      return 0;
    }
    bool AskSave()
    {
      if (cont == null) return true;
      if (IsModified) return true;
      switch (MessageBox.Show(this, path == null ? "Save changings?" : $"Save changings in {path}?", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return IsModified;
      }
      return false;
    }
    bool IsModified
    {
      get => (edit == null || !edit.IsModified) && !view.IsModified;
      set { view.IsModified = false; if (edit != null) edit.IsModified = false; }
    }
    int OnShow3MF(object test)
    {
      if (test != null) return 1;
      Cursor.Current = Cursors.WaitCursor;
      var el = cont.Nodes.Export3MF(null, null, null, null);
      var view = (XmlEditor)ShowView(typeof(XmlEditor), cont, DockStyle.Fill);
      view.Text = "3MF Content"; view.EditText = el.ToString();
      return 1;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (e.Cancel = !AskSave()) return; IsModified = false; edit?.OnFormClosing(this);
      var reg = Application.UserAppDataRegistry;
      reg.SetValue("lwp", getrestore(), Microsoft.Win32.RegistryValueKind.QWord);
      if (path != null) reg.SetValue("lod", path); else reg.DeleteValue("lod", false);
    }
    List<string> mru(string add = null, string rem = null)
    {
      var reg = Application.UserAppDataRegistry; var ss = reg.GetValue("mru") as string[];
      var list = ss != null ? ss.ToList() : new List<string>();
      if (rem != null) list.RemoveAll(s => string.Compare(s, rem, true) == 0);
      if (add != null) { list.Insert(0, add); if (list.Count > 6) list.RemoveRange(6, list.Count - 6); }
      if (rem != null || add != null) reg.SetValue("mru", list.ToArray()); return list;
    }
    unsafe long getrestore()
    {
      var z = WindowState == FormWindowState.Maximized;
      var r = z ? RestoreBounds : DesktopBounds;
      long code; var s = (short*)&code;
      s[0] = (short)r.Left; s[1] = (short)r.Top; s[2] = (short)r.Width; s[3] = (short)(z ? -r.Height : r.Height);
      return code;
    }
    unsafe void setrestore(long code)
    {
      var s = (short*)&code; var r = new Rectangle(s[0], s[1], s[2], s[3]);
      StartPosition = FormStartPosition.Manual; var z = r.Height < 0; if (z) r.Height = -r.Height;
      if (SystemInformation.VirtualScreen.IntersectsWith(r)) DesktopBounds = r; else StartPosition = FormStartPosition.WindowsDefaultLocation;
      WindowState = z ? FormWindowState.Maximized : FormWindowState.Normal;
    }
    protected unsafe override void WndProc(ref Message m)
    { // WM_SYSCOMMAND CLOSE -> WM_CLOSE  
      if (m.Msg == 0x112 && m.WParam == (IntPtr)0xf060) { Native.PostMessage(m.HWnd, 0x0010, null, null); return; }
      base.WndProc(ref m);
    }
    class ScriptView : UserControl, ICommandTarget
    {
      int ICommandTarget.OnCommand(int id, object test)
      {
        if (id == 0) { MenuItem.Update(tb.Items); tb.Update(); } //ui
        return edit.OnCommand(id, test);
      }
      internal NeuronEditor edit = new NeuronEditor { Dock = DockStyle.Fill }; ToolStrip tb;
      protected override void OnPaintBackground(PaintEventArgs e)
      {
        //base.OnPaintBackground(e);
      }
      protected override void OnLoad(EventArgs e)
      {
        tb = new ToolStrip() { Margin = new Padding(15), ImageScalingSize = new Size(24, 24), GripStyle = ToolStripGripStyle.Hidden };
        tb.Items.Add(new Button(5011, "Run", Resources.run) { Tag = edit });
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(new Button(5010, "Run Debug", Resources.rund) { Tag = edit });
        tb.Items.Add(new Button(5016, "Step", Resources.stepover) { Tag = edit });
        tb.Items.Add(new Button(5015, "Step Into", Resources.stepin) { Tag = edit });
        tb.Items.Add(new Button(5017, "Step Out", Resources.stepout) { Tag = edit });
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(new Button(5013, "Stop", Resources.stop) { Tag = edit });
        edit.Tag = Tag;
        Visible = false;
        Controls.Add(edit);
        Controls.Add(tb);
        Visible = true;
      }
    }
  }
}
