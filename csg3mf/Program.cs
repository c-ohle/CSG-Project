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

  class MainFrame : Form
  {
    internal MainFrame()
    {
      Icon = Icon.FromHandle(Native.LoadIcon(Marshal.GetHINSTANCE(GetType().Module), (IntPtr)32512));
      StartPosition = FormStartPosition.WindowsDefaultBounds;
      MainMenuStrip = new MenuStrip(); MenuItem.CommandRoot = TryCommand;
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
          new MenuItem(5027, "Goto &Definition", Keys.F12 )
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
          new MenuItem(1120, "Show &3MF..."),
          new MenuItem(5025 ,"Show &IL Code..."),
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

      var tb1 = new ToolStrip() { Margin = new Padding(15), ImageScalingSize = new Size(24, 24), GripStyle = ToolStripGripStyle.Hidden };
      Application.Idle += (p, e) => { MenuItem.Update(tb1.Items); tb1.Update(); };
      tb1.Items.Add(new MenuItem.Button(5011, "Run", Resources.run));
      tb1.Items.Add(new ToolStripSeparator());
      tb1.Items.Add(new MenuItem.Button(5010, "Run Debug", Resources.rund));
      tb1.Items.Add(new MenuItem.Button(5016, "Step", Resources.stepover));
      tb1.Items.Add(new MenuItem.Button(5015, "Step Into", Resources.stepin));
      tb1.Items.Add(new MenuItem.Button(5017, "Step Out", Resources.stepout));
      tb1.Items.Add(new ToolStripSeparator());
      tb1.Items.Add(new MenuItem.Button(5013, "Stop", Resources.stop));

      splitter = new SplitContainer { Dock = DockStyle.Fill };
      splitter.SplitterDistance = 75;// ClientSize.Width / 2;
      splitter.Panel1.Controls.Add(tb1);
      splitter.Panel2.Controls.Add(view = new View { Dock = DockStyle.Fill });
      Controls.Add(splitter);
      Controls.Add(MainMenuStrip);
      var reg = Application.UserAppDataRegistry;
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
      {
        try { Open(args[1]); return; }
        catch (Exception e) { MessageBox.Show(e.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(0); }
      }
      if (reg.GetValue("lod") is string lod)
      {
        if (!Debugger.IsAttached) reg.DeleteValue("lod", false);
        try { Open(lod); var lwp = reg.GetValue("lwp"); if (lwp is long) setrestore((long)lwp); }
        catch (Exception e) { Debug.WriteLine(e.Message); }
      }
      if (path == null) OnNew(null);
    }
    SplitContainer splitter; View view;
    string path; NeuronEditor edit;
    int TryCommand(int id, object test)
    {
      try { return OnCommand(id, test); }
      catch (Exception e)
      {
        if (e.GetType().IsNestedPrivate) throw;
        MessageBox.Show(e.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); return -1;
      }
    }
    int OnCommand(int id, object test)
    {
      { var x = view.OnCommand(id, test); if (x != 0) return x; }
      { var x = edit.OnCommand(id, test); if (x != 0) return x; }
      switch (id)
      {
        case 1000: return OnNew(test);
        case 1010: return OnOpen(test);
        case 1020: return OnSave(test, false);
        case 1025: return OnSave(test, true);
        case 1050: return OnLastFiles(test);
        case 1100: if (test == null) Close(); return 1;
        case 3015: return view.OnDriver(test);
        case 3016: return view.OnSamples(test);
        case 2210: //Select Box
        case 2211: //Select Pivot
        case 2212: //Select Normals
        case 2213: //Select Wireframe
        case 2214: //Select Outline
        case 2220: //Shadows
          if (test != null) return (view.view.Render & (CDX.Render)(1 << (id - 2210))) != 0 ? 3 : 1;
          view.view.Render ^= (CDX.Render)(1 << (id - 2210)); Application.UserAppDataRegistry.SetValue("fl", (int)view.view.Render);
          view.Invalidate(); return 1;
        case 2300: return view.OnCenter(test);
        case 1120: return ShowXml(test);
      }
      return 0;
    }
    void UpdateTitle()
    {
      Text = string.Format("{0} - {1} {2} Bit {3}", path ?? string.Format("({0})", "Untitled"), Application.ProductName, Environment.Is64BitProcess ? "64" : "32", COM.DEBUG ? "Debug" : "Release");
    }
    unsafe void Open(string path)
    {
      Cursor.Current = Cursors.WaitCursor;
      string script = null;
      if (path == null) { view.cont = new Container(); script = "using static csg3mf.CSG;\n"; }
      else if (path != null && path.EndsWith(".3cs", true, null)) { view.cont = new Container(); script = File.ReadAllText(path); }
      else view.cont = csg3mf.Container.Import3MF(path, out script);
      //var str = COM.SHCreateMemStream();
      //view.cont.Nodes.SaveToStream(str); long x; str.Seek(0, 2, &x);
      //str.Seek(0); view.cont.Nodes.Clear();
      //view.cont.Nodes.LoadFromStream(str); str.Seek(0, 1, &x);
      NeuronEditor.InitNeuron(view.cont, script ?? "");
      var e = new NeuronEditor { Dock = DockStyle.Fill, Tag = view.cont };
      splitter.Panel1.Controls.Add(e); e.BringToFront(); splitter.Panel1.Update();
      if (this.edit != null) this.edit.Dispose(); this.edit = e;
      Neuron.Debugger = (p, v) => this.edit.Show(v);
      view.cont.OnUpdate = view.onstep;
      if (view.IsHandleCreated)
      {
        view.view.Camera = null;
        view.view.Scene = view.cont.Nodes;
        view.OnCenter(null); view.Invalidate();
      }
      this.path = path; UpdateTitle(); if (path != null) mru(path, path);
    }
    int OnNew(object test)
    {
      if (test != null) return 1;
      if (edit != null && edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Open(null);
      return 1;
    }
    int OnOpen(object test)
    {
      if (test != null) return 1; if (edit.askstop()) return 1;
      var dlg = new OpenFileDialog() { Filter = "3MF files|*.3mf;*.3cs|All files|*.*" };
      if (path != null) dlg.InitialDirectory = Path.GetDirectoryName(path);
      if (dlg.ShowDialog(this) != DialogResult.OK) return 1;
      if (edit.askstop()) return 1;
      if (!AskSave()) return 1;
      Open(dlg.FileName); return 1;
    }
    unsafe int OnSave(object test, bool saveas)
    {
      if (test != null) return 1;
      var s = path;
      if (saveas || s == null)
      {
        var dlg = new SaveFileDialog() { Filter = "3MF file|*.3mf|3CS C# template file|*.3cs|All files|*.*", DefaultExt = "3mf" };
        if (s != null) { dlg.InitialDirectory = Path.GetDirectoryName(s); dlg.FileName = Path.GetFileName(s); }
        if (dlg.ShowDialog(this) != DialogResult.OK) return 1; s = dlg.FileName;
      }
      Cursor.Current = Cursors.WaitCursor;
      if (s.EndsWith(".3cs", true, null)) File.WriteAllText(s, edit.EditText);
      else
      {
        var str = COM.SHCreateMemStream();
        view.view.Print(256, 256, 4, 0x00ffffff, str);
        view.cont.Export3MF(s, str, edit.EditText);
      }
      if (path != s) { path = s; UpdateTitle(); mru(path, path); }
      edit.IsModified = view.IsModified = false; Cursor.Current = Cursors.Default;
      return 1;
    }
    int OnLastFiles(object test)
    {
      if (test is string path)
      {
        if (edit.askstop()) return 1;
        if (!AskSave()) return 1;
        mru(null, path); Open(path); return 1;
      }
      var item = test as MenuItem; foreach (var s in mru(null, null)) item.DropDownItems.Add(s).Tag = s;
      return 0;
    }
    bool AskSave()
    {
      if (edit == null) return true;
      if (!edit.IsModified && !view.IsModified) return true;
      switch (MessageBox.Show(this, String.Format(path == null ? "Save changings?" : "Save changings in {0}?", path), Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return !edit.IsModified && !view.IsModified;
      }
      return false;
    }
    int ShowXml(object test)
    {
      if (test != null) return 1;
      var e = view.cont.Export3MF(null, null, null);
      var form = new Form
      {
        Text = string.Format($"{this.Text} - {"3MF XML"}"),
        StartPosition = FormStartPosition.Manual,
        Location = this.Location + new Size(32, 64),
        Width = Width * 2 / 3,
        Height = Height * 2 / 3,
        ShowInTaskbar = false,
        ShowIcon = false
      };
      var edit = new XmlEditor { Dock = DockStyle.Fill, EditText = e.ToString(), ReadOnly = true };
      form.Controls.Add(edit);
      form.Controls.Add(form.MainMenuStrip = new MenuStrip());
      form.MainMenuStrip.Items.AddRange(new ToolStripItem[]
      {
        new MenuItem("&Edit",
          new MenuItem(2030, "Cop&y", Keys.C|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2060, "Select &all", Keys.A|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(2065, "&Find...", Keys.F|Keys.Control ),
          new MenuItem(2066, "Find forward", Keys.F3|Keys.Control ),
          new MenuItem(2067, "Find next", Keys.F3 ),
          new MenuItem(2068, "Find prev", Keys.F3|Keys.Shift ),
          new ToolStripSeparator(),
          new MenuItem(2062, "Toggle", Keys.T|Keys.Control ),
          new MenuItem(2063, "Toggle all", Keys.T|Keys.Shift|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5027, "Goto &Definition", Keys.F12 )
        )
       });
      var t = MenuItem.CommandRoot; MenuItem.CommandRoot = edit.OnCommand;
      form.StartPosition = FormStartPosition.CenterParent; form.ShowDialog(this); MenuItem.CommandRoot = t;
      return 1;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (e.Cancel = !AskSave()) return; view.timer = null; edit.OnFormClosing(this);
      var reg = Application.UserAppDataRegistry;
      reg.SetValue("lwp", getrestore(), Microsoft.Win32.RegistryValueKind.QWord);
      if (path != null) reg.SetValue("lod", path); else reg.DeleteValue("lod", false);
    }
    protected unsafe override void WndProc(ref Message m)
    { // WM_SYSCOMMAND CLOSE -> WM_CLOSE  
      if (m.Msg == 0x112 && m.WParam == (IntPtr)0xf060) { Native.PostMessage(m.HWnd, 0x0010, null, null); return; }
      base.WndProc(ref m);
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

    unsafe class View : UserControl, ISink
    {
      internal Container cont;
      internal IView view;
      internal Action timer;
      long drvsettings = 0x400000000;
      protected unsafe override void OnHandleCreated(EventArgs _)
      {
        var reg = Application.UserAppDataRegistry;
        var drv = reg.GetValue("drv"); if (drv is long v) drvsettings = v;
        Factory.SetDevice((uint)drvsettings);
        view = Factory.CreateView(Handle, this, (uint)(drvsettings >> 32));
        view.Render = (Render)reg.GetValue("fl", (int)(Render.BoundingBox | Render.Coordinates | Render.Wireframe | Render.Shadows));
        view.BkColor = 0xffcccccc;
        view.Scene = cont.Nodes;
        OnCenter(null);

        AllowDrop = true;
        ContextMenuStrip = new ContextMenu();
        ContextMenuStrip.Opening += (x, y) => { var p = mainover(); if (p != null && !p.IsSelect && !p.IsStatic) { p.Select(); Invalidate(); } };
        ContextMenuStrip.Items.AddRange(new ToolStripItem[] {
          new MenuItem(2010, "&Undo"),
          new MenuItem(2011, "&Redo"),
          new ToolStripSeparator(),
          new MenuItem(2035, "&Group", Keys.Control | Keys.G),
          new MenuItem(2036, "U&ngroup", Keys.Control | Keys.U),
          new ToolStripSeparator(),
          new MenuItem(2150, "Intersection A && B", Keys.Alt | Keys.I),
          new MenuItem(2151, "Union A | B", Keys.Alt | Keys.U),
          new MenuItem(2152, "Substract A - B", Keys.Alt | Keys.D),
          new MenuItem(2153, "Difference A ^ B"),
          new MenuItem(2154, "Cut Plane", Keys.Alt | Keys.C),
          new ToolStripSeparator(),
          new MenuItem(4020, "Static"),
          new ToolStripSeparator(),
          new MenuItem(2100, "Properties...")});
      }
      internal int OnCommand(int id, object test)
      {
        switch (id)
        {
          case 2010: //Undo
            if (!Focused) return 0;
            if (undos == null || undoi == 0) return 8;
            if (test == null) { undos[undoi - 1](); undoi--; Invalidate(); }
            return 1;
          case 2011: //Redo
            if (!Focused) return 0;
            if (undos == null || undoi >= undos.Count) return 8;
            if (test == null) { undos[undoi](); undoi++; Invalidate(); }
            return 1;
          case 2060: //SelectAll
            if (!Focused) return 0;
            if (test != null) return 1;
            foreach (var p in view.Scene.Nodes()) p.IsSelect = !p.IsStatic; Invalidate();
            return 1;
          case 4020: return OnStatic(test);
          case 2015: return OnDelete(test);
          case 2035: return OnGroup(test);
          case 2036: return OnUngroup(test);
        }
        return 0;
      }

      int OnStatic(object test)
      {
        var a = view.Scene.Selection();
        if (!a.Any()) return 0; var all = a.All(p => p.IsStatic);
        if (test != null) return all ? 3 : 1;
        var aa = a.Where(p => p.IsStatic == all).ToArray();
        execute(() => { foreach (var p in aa) p.IsStatic = !p.IsStatic; });
        return 1;
      }
      int OnGroup(object test)
      {
        var scene = view.Scene;
        var a = scene.Select(1); if (a.Take(2).Count() != 2) return 0;
        if (test != null) return 1;

        var mi = new CSG.Rational.Vector3(+int.MaxValue, +int.MaxValue, +int.MaxValue);
        var ma = new CSG.Rational.Vector3(-int.MaxValue, -int.MaxValue, -int.MaxValue);
        foreach (var p in scene.Select(2).Select(i => scene[i]))
        {
          if (p.Mesh == null) continue;
          var m = p.Transform; for (var t = p; !t.IsSelect; m *= (t = t.Parent).Transform) ;
          foreach (var v in p.Mesh.Vertices())
          {
            var t = v.Transform(m);
            if (t.x < mi.x) mi.x = t.x; if (t.x > ma.x) ma.x = t.x;
            if (t.y < mi.y) mi.y = t.y; if (t.y > ma.y) ma.y = t.y;
            if (t.z < mi.z) mi.z = t.z; if (t.z > ma.z) ma.z = t.z;
          }
        }
        var mp = (mi + ma) / 2;
        var ii = a.ToArray(); var gr = scene.AddNode("Group"); scene.Remove(gr.Index);
        gr.Transform = CSG.Rational.Matrix.Translation(mp.x, mp.y, mi.z);
        execute(() =>
        {
          scene.Select();
          if (gr != null)
          {
            scene.Insert(ii[0], gr); var m = !gr.Transform;
            for (int i = 0; i < ii.Length; i++) { var p = scene[ii[i] + 1]; p.Transform *= m; p.Parent = gr; }
            gr.IsSelect = true; gr = null;
          }
          else
          {
            gr = scene[ii[0]]; scene.Remove(ii[0]); var m = gr.Transform;
            for (int i = 0; i < ii.Length; i++) { var p = scene[ii[i]]; p.Transform *= m; p.IsSelect = true; }
          }
        });
        return 1;
      }
      int OnUngroup(object test)
      {
        var scene = view.Scene;
        var a = scene.Select(1).Where(i => scene.Select(i << 8).Any());
        if (!a.Any()) return 0; if (test != null) return 1;
        var ii = a.ToArray(); INode[] pp = null;
        var tt = scene.Select(1).SelectMany(i => scene.Select(i << 8)).ToArray();
        var hh = tt.Select(i => scene[i].Parent.Index).ToArray();
        execute(() =>
        {
          scene.Select();
          if (pp == null)
          {
            pp = ii.Select(i => scene[i]).ToArray(); var cc = tt.Select(i => scene[i]).ToArray();
            for (int i = 0; i < cc.Length; i++) cc[i].Transform *= scene[hh[i]].Transform;
            for (int i = ii.Length - 1; i >= 0; i--) scene.Remove(ii[i]);
            for (int i = 0; i < cc.Length; i++) cc[i].IsSelect = true;
          }
          else
          {
            for (int i = 0; i < ii.Length; i++) scene.Insert(ii[i], pp[i]);
            for (int i = 0; i < tt.Length; i++) { var t1 = scene[tt[i]]; var t2 = scene[hh[i]]; t1.Parent = t2; t1.Transform *= !t2.Transform; }
            for (int i = 0; i < pp.Length; i++) pp[i].IsSelect = true; pp = null;
          }
        });
        return 1;
      }
      int OnDelete(object test)
      {
        if (!Focused) return 0;
        var a = view.Scene.Select(2);
        if (!a.Any()) return 0;
        if (test != null) return 1;
        var ii = a.ToArray(); INode[] pp = null; int[] tt = null;
        execute(() =>
        {
          var scene = view.Scene; scene.Select();
          if (pp == null)
          {
            pp = ii.Select(i => scene[i]).ToArray();
            tt = pp.Select(p => p.Parent != null ? p.Parent.Index : -1).ToArray();
            for (int i = ii.Length - 1; i >= 0; i--) scene.Remove(ii[i]);
          }
          else
          {
            for (int i = 0; i < ii.Length; i++) scene.Insert(ii[i], pp[i]);
            for (int i = 0; i < tt.Length; i++) if (tt[i] != -1) pp[i].Parent = scene[tt[i]];
            foreach (var t in pp.Where(p => !pp.Contains(p.Parent))) t.IsSelect = true;
            pp = null; tt = null;
          }
        });
        return 1;
      }

      internal int OnDriver(object test)
      {
        if (test is ToolStripMenuItem item)
        {
          var ss = Factory.Devices.Split('\n');
          for (int i = 1; i < ss.Length; i += 2)
            item.DropDownItems.Add(new ToolStripMenuItem(ss[i + 1]) { Tag = ss[i], Checked = ss[i] == ss[0] });
          return 0;
        }
        Cursor = Cursors.WaitCursor; Factory.SetDevice(uint.Parse((string)test));
        drvsettings = (drvsettings >> 32 << 32) | uint.Parse((string)test);
        Application.UserAppDataRegistry.SetValue("drv", drvsettings, Microsoft.Win32.RegistryValueKind.QWord);
        Cursor = Cursors.Default; return 1;
      }
      internal int OnSamples(object test)
      {
        if (test is ToolStripMenuItem item)
        {
          var ss = view.Samples.Split('\n');
          for (int i = 1; i < ss.Length; i++)
            item.DropDownItems.Add(new ToolStripMenuItem($"{ss[i]} Samples") { Tag = ss[i], Checked = ss[i] == ss[0] });
          return 0;
        }
        Cursor = Cursors.WaitCursor; view.Samples = (string)test;
        drvsettings = (drvsettings & 0xffffffff) | ((long)uint.Parse((string)test) << 32);
        Application.UserAppDataRegistry.SetValue("drv", drvsettings, Microsoft.Win32.RegistryValueKind.QWord);
        Cursor = Cursors.Default; return 1;
      }
      internal int OnCenter(object test) //todo: undo
      {
        if (!view.Scene.Nodes().Any(p => p.Mesh != null && p.Mesh.VertexCount != 0)) return 0;
        if (test != null) return 1;
        float abst = 100; view.Command(Cmd.Center, &abst);
        Invalidate(); return 1;
      }

      Action<int> tool;
      INode mainover()
      {
        if (view.MouseOverNode == -1) return null;
        var p = view.Scene[view.MouseOverNode]; for (; p.Parent != null; p = p.Parent) ; return p;
      }

      protected override void OnMouseDown(MouseEventArgs e)
      {
        Focus(); if (e.Button != MouseButtons.Left) return;
        var keys = ModifierKeys;
        if (view.Camera == null) return;
        var main = mainover();
        if (main == null) tool = camera_free();
        else
          switch (keys)
          {
            case Keys.None: tool = main.IsStatic ? tool_select() : obj_movxy(main); break;
            case Keys.Control: tool = main.IsStatic ? camera_movxy() : obj_drag(main); break;
            case Keys.Shift: tool = main.IsStatic ? camera_movz() : obj_movz(main); break;
            case Keys.Alt: tool = main.IsStatic ? camera_rotz(0) : obj_rotz(main); break;
            case Keys.Control | Keys.Shift: tool = main.IsStatic ? camera_rotx() : obj_rot(main, 0); break;
            case Keys.Control | Keys.Alt: tool = main.IsStatic ? camera_rotz(1) : obj_rot(main, 1); break;
            case Keys.Control | Keys.Alt | Keys.Shift: if (main.IsStatic) main.Select(); else obj_rot(main, 2); break;
            default: tool = tool_select(); break;
          }
        if (tool != null) Capture = true; Invalidate();
      }
      protected override void OnMouseMove(MouseEventArgs e)
      {
        if (tool != null) { tool(0); Invalidate(); return; }
        //var i = view.MouseOverNode;
        //FindForm().Text = i != -1 ? $"over: {i} {view.Scene[i].Name} {view.MouseOverPoint}" : "";
      }
      protected override void OnMouseUp(MouseEventArgs e)
      {
        if (tool == null) return;
        tool(1); tool = null; Capture = true; Invalidate();
      }
      protected override void OnLostFocus(EventArgs e)
      {
        if (tool == null) return;
        tool(1); tool = null; Invalidate();
      }
      protected override void OnMouseWheel(MouseEventArgs e)
      {
        if (tool != null) return;
        if (view.MouseOverNode == -1) return;
        var m = view.Camera.TransformF;//.GetTransformF();
        var node = view.Scene[view.MouseOverNode];
        var v = (view.MouseOverPoint * node.GetTransformF()) - m.mp;
        var l = v.Length; // (float)Math.Sqrt(v.Length);
        var t = Environment.TickCount;
        view.Camera.TransformF = m * (v * (l * 0.01f * e.Delta /* * DpiScale*/ * (1f / 120))); Invalidate();
        if (t - lastwheel > 500) addundo(undo(view.Camera, m)); lastwheel = t;
      }
      static int lastwheel;

      Action<int> camera_free()
      {
        var mb = float4x3.Identity; view.Command(Cmd.GetBox, &mb);
        var boxmin = *(float3*)&mb._11; var boxmax = *(float3*)&mb._22;
        var pm = (boxmin + boxmax) * 0.5f; var tm = (float4x3)pm;
        var cm = view.Camera.TransformF; var p1 = (float2)Cursor.Position; bool moves = false;
        return id =>
        {
          if (id == 0)
          {
            var v = (Cursor.Position - p1) * -0.01f;
            if (!moves && v.LengthSq < 0.03) return; moves = true;
            view.Camera.TransformF = cm * !tm * float4x3.RotationAxis(cm.mx, -v.y) * float4x3.RotationZ(v.x) * tm;
          }
          if (id == 1) addundo(undo(view.Camera, cm));
        };
      }
      Action<int> camera_movxy()
      {
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        view.SetPlane(wp = new float3(0, 0, wp.z)); var p1 = view.PickPlane(); var p2 = p1;
        var camera = view.Camera; var m = camera.TransformF;
        return id =>
        {
          if (id == 0) { p2 = view.PickPlane(); camera.TransformF = m * (p1 - p2); }
          if (id == 1) addundo(undo(camera, m));
        };
      }
      Action<int> camera_movz()
      {
        var camera = view.Camera; var m = camera.TransformF;
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        view.SetPlane(float4x3.RotationY(Math.PI / 2) * float4x3.RotationZ(((float2)m.mz).Angel) * wp);
        var p1 = view.PickPlane(); var p2 = p1; //var mover = move(camera);
        return id =>
        {
          if (id == 0) { p2 = view.PickPlane(); camera.TransformF = m * new float3(0, 0, view.PickPlane().x - p1.x); }
          if (id == 1) addundo(undo(camera, m));
        };
      }
      Action<int> camera_rotz(int mode)
      {
        var camera = view.Camera; var m = camera.TransformF; var rot = m.mp; //var scene = camera.Ancestor<Scene>(); 
        if (mode != 0)
        {
          var p = view.Scene.Selection().FirstOrDefault();
          if (p != null) rot = p.GetTransformF().mp;
        }
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        view.SetPlane(new float3(rot.x, rot.y, wp.z)); var a1 = view.PickPlane().Angel; //var mover = move(camera);
        return id =>
        {
          if (id == 0) { var p2 = view.PickPlane(); camera.TransformF = m * -rot * float4x3.RotationZ(a1 - view.PickPlane().Angel) * rot; }
          if (id == 1) addundo(undo(camera, m));
        };
      }
      Action<int> camera_rotx()
      {
        var camera = view.Camera; var m = camera.TransformF;
        view.SetPlane(m * m.mz); var p1 = view.PickPlane(); var p2 = p1; //var mover = move(camera);
        return id =>
        {
          if (id == 0) { p2 = view.PickPlane(); camera.TransformF = float4x3.RotationX(Math.Atan(p2.y) - Math.Atan(p1.y)) * m; Invalidate(); }
          if (id == 1) addundo(undo(camera, m));
        };
      }
      Action<int> tool_select()
      {
        var over = view.Scene[view.MouseOverNode];
        var wp = view.MouseOverPoint * over.GetTransformF();
        view.SetPlane(wp = new float3(0, 0, wp.z)); var p1 = view.PickPlane(); var p2 = p1;
        return id =>
        {
          if (id == 0) { p2 = view.PickPlane(); }
          if (id == 4)
          {
            var dc = new DC(view); dc.Transform = wp; var dp = p2 - p1;
            dc.Color = 0x808080ff; dc.FillRect(p1.x, p1.y, dp.x, dp.y);
            dc.Color = 0xff8080ff; dc.DrawRect(p1.x, p1.y, dp.x, dp.y);
          }
          if (id == 1)
          {
            if (p1 == p2) { view.Scene.Select(); return; }
            float4 t; ((float2*)&t)[0] = p1; ((float2*)&t)[1] = p2;
            view.Command(Cmd.SelectRect, &t);
          }
        };
      }

      Action<int> obj_movxy(INode main)
      {
        var ws = main.IsSelect; if (!ws) main.Select();
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        view.SetPlane(wp = new float3(0, 0, wp.z)); var p1 = view.PickPlane(); var p2 = p1;
        Action<int, float4x3> mover = null;
        return id =>
        {
          if (id == 0)
          {
            p2 = view.PickPlane(); if (p2 == p1) return; var dm = (float4x3)(p2 - p1);
            (mover ?? (mover = getmover()))(0, dm);
          }
          if (id == 1)
          {
            if (mover != null) mover(2, 0);
            else if (ws) main.Select();
          }
        };
      }
      Action<int> obj_movz(INode main)
      {
        var ws = main.IsSelect; if (!ws) main.Select();
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        var bo = float4x3.Identity; view.Command(Cmd.GetBoxSel, &bo);
        var miz = bo.mx.z; var lov = miz != float.MaxValue ? miz : 0; var boxz = lov; var ansch = Math.Abs(boxz) < 0.1f;
        view.SetPlane(float4x3.RotationY(Math.PI / 2) * float4x3.RotationZ(((float2)view.Camera.TransformF.my).Angel) * wp);
        var p1 = view.PickPlane(); var p2 = p1; var mover = getmover();
        return id =>
        {
          if (id == 0)
          {
            p2 = view.PickPlane(); if (p2 == p1) return; var dz = p1.x - p2.x;
            if (miz != float.MaxValue) { var ov = boxz + dz; if (ov < 0 && ov > -0.5f && lov >= 0) { if (!ansch) { ansch = true; } dz = -boxz; ov = 0; } else ansch = false; lov = ov; }
            mover(0, float4x3.Translation(0, 0, dz));
          }
          if (id == 1) mover(2, 0);
        };
      }
      Action<int> obj_rotz(INode main)
      {
        if (!main.IsSelect) main.Select();
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        var scene = main.Parent; var ms = scene != null ? scene.GetTransformF() : 1;
        var mw = main.GetTransformF(); var mp = mw.mp;
        view.SetPlane(new float3(mp.x, mp.y, wp.z));
        var v = Math.Abs(mw._13) > 0.8f ? new float2(mw._21, mw._22) : new float2(mw._11, mw._12);
        var w0 = v.Angel;
        var raster = angelstep(); ms = ms * -mp;
        var a1 = view.PickPlane().Angel; var mover = getmover();
        return id =>
        {
          if (id == 0)
          {
            var a2 = view.PickPlane().Angel; var rw = raster(w0 + a2 - a1);
            mover(0, ms * float4x3.RotationZ(rw - w0) * !ms);
          }
          if (id == 1) mover(2, 0);
        };
      }
      Action<int> obj_rot(INode main, int xyz)
      {
        if (!main.IsSelect) main.Select();
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        var wm = main.GetTransformF(main.Parent);
        var op = wp * !main.GetTransformF();
        var mr = (xyz == 0 ?
          float4x3.RotationY(+(Math.PI / 2)) * new float3(op.x, 0, 0) : xyz == 1 ?
          float4x3.RotationX(-(Math.PI / 2)) * new float3(0, op.y, 0) :
          new float3(0, 0, op.z)) * wm;
        var w0 = Math.Abs(mr._33) > 0.9f ? Math.Atan2(mr._12, mr._22) : Math.Atan2(mr._13, mr._23);
        view.SetPlane(float4x3.RotationZ(-w0) * mr * (main.Parent != null ? main.Parent.GetTransformF() : 1));
        var a1 = view.PickPlane().Angel; var angelgrid = angelstep(); var mover = getmover();
        return (id) =>
        {
          if (id == 0)
          {
            var a2 = view.PickPlane().Angel; var rw = angelgrid(w0 + a2 - a1);
            switch (xyz)
            {
              case 0: mr = float4x3.RotationX(rw - w0); break;
              case 1: mr = float4x3.RotationY(rw - w0); break;
              case 2: mr = float4x3.RotationZ(rw - w0); break;
            }
            mover(0, !wm * mr * wm);
          }
          if (id == 1) mover(2, 0);
        };
      }
      Action<int> obj_drag(INode main)
      {
        var ws = main.IsSelect; if (!ws) main.IsSelect = true;
        var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransformF();
        var p1 = (float2)Cursor.Position;
        return id =>
        {
          if (id == 0)
          {
            var p2 = (float2)Cursor.Position; if ((p2 - p1).LengthSq < 10 /* * DpiScale*/) return;
            if (!ws) main.Select(); ws = false; if (!AllowDrop) return;
            var path = Path.Combine(Path.GetTempPath(), string.Join("_", main.Name.Trim().Split(Path.GetInvalidFileNameChars())) + ".3mf");
            try
            {
              var cont = new Container(); //var dest = Factory.CreateScene();
              var scene = view.Scene;
              var ii = scene.Select(2).ToArray();
              var pp = ii.Select(i => scene[i]).ToArray();
              var tt = pp.Select(p => Array.IndexOf(pp, p.Parent)).ToArray();
              for (int i = 0; i < pp.Length; i++) cont.Nodes.Insert(i, pp[i]);
              for (int i = 0; i < pp.Length; i++) if (tt[i] != -1) cont.Nodes[i].Parent = cont.Nodes[tt[i]];
              cont.Export3MF(path, null, null);
              var data = new DataObject(); data.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
              DoDragDrop(data, DragDropEffects.Copy);
            }
            catch (Exception e) { Debug.WriteLine(e.Message); }
            finally { File.Delete(path); }
          }
          if (id == 1 && ws) main.IsSelect = false;
        };
      }
#if (false)
      static Action<int, object> obj_drop(ISelector pc, Node scene)
      {
        var drop = getdrop(pc, ".xo", out float3 pt); var nodes = drop as Node[];
        if (drop is string path)
        {
          if (path.EndsWith(".png", true, null) || path.EndsWith(".jpg", true, null) || path.EndsWith(".dds", true, null))
          {
            var texture = GetTexture(path); var size = texture.Size * 0.001f;
            nodes = import("PicBox.xo", out float3 xxx);
            var mesh = (Mesh)nodes[0]; mesh["Size"] = (float3)size; mesh.texture = texture; mesh.Textrans = new float3x4 { _11 = 1 / size.x, _22 = 1 / size.y, _33 = 1 };
          }
          else nodes = import(path, ref pt);
        }
        if (nodes == null) return null; var view = (View)pc.View;
        pc.SetPlane(pt); foreach (var p in nodes) scene.Add(p);
        var mover = move(nodes.OfType<Group>()); mover(0, pc.Pick());
        return id =>
        {
          if (id == 0) { mover(0, pc.Pick()); }
          if (id == 2) { foreach (var t in nodes) t.Remove(); }
          if (id == 1)
          {
            var doc = scene.Ancestor<Document>(); view.Focus();
            doc.addundo(doc.select(nodes, null), remove(nodes, true), doc.select(null, doc.Selection)); doc.Select(nodes);
          }
        };
      }
#endif
      static Func<double, double> angelstep()
      {
        var seg1 = 0.0; var hang = 0; var count = 0; var len = Math.PI / 4; var modula = 2 * Math.PI;
        return val =>
        {
          var seg2 = Math.Floor(val / len); var len2 = len * 0.33f;
          if (0 == count++) seg1 = seg2;
          if (seg2 == seg1) { hang = 0; return val; }
          if (Math.Abs(seg2 * len - val) < len2) { if (hang != 1) { hang = 1; /*Program.play(30);*/ } return seg2 * len; }
          var d = seg1 * len - val; if (modula != 0) d = Math.IEEERemainder(d, modula);
          if (Math.Abs(d) < len2) { val = seg1 * len; if (hang != 2) { hang = 2; /*Program.play(30);*/ } } else { seg1 = seg2; hang = 0; }
          return val;
        };
      }
      public bool IsModified { get => undoi != 0; set { undos = null; undoi = 0; } }
      List<Action> undos; int undoi;
      void addundo(Action p)
      {
        if (p == null) return;
        if (undos == null) undos = new List<Action>();
        undos.RemoveRange(undoi, undos.Count - undoi);
        undos.Add(p); undoi = undos.Count;
      }
      void execute(Action p)
      {
        p(); addundo(p); Invalidate();
      }
      Action undo(INode p, float4x3 m)
      {
        if (m == p.TransformF) return null;
        return () => { var t = p.TransformF; p.TransformF = m; m = t; };
      }
      Action undo(IEnumerable<Action> a)
      {
        var b = a.OfType<Action>().ToArray(); if (b.Length == 0) return null;
        if (b.Length == 1) return b[0];
        return () => { for (int i = 0; i < b.Length; i++) b[i](); Array.Reverse(b); };
      }
      Action<int, float4x3> getmover()
      {
        var pp = view.Scene.Selection().ToArray();
        var mm = pp.Select(p => p.TransformF).ToArray();
        return (id, m) =>
        {
          if (id == 0) { for (int i = 0; i < pp.Length; i++) pp[i].TransformF = mm[i] * m; }
          if (id == 2) addundo(undo(pp.Select((p, i) => undo(p, mm[i]))));
        };
      }

      internal void onstep()
      {
        Invalidate();
      }

      IFont font = Factory.GetFont("Arial", 13, System.Drawing.FontStyle.Bold);
      //Stopwatch sw;

      void ISink.Render()
      {
        tool?.Invoke(4);
        var dc = new DC(view);
        dc.SetOrtographic();

        var ss = cont.Infos;
        dc.Font = font; dc.Color = 0x80000000;
        var y = 10 + font.Ascent;
        for (int i = 0; i < ss.Count; i++, y += font.Height)
          dc.DrawText(10, y, ss[i]);

#if (false)
        var p = dc.GetTextExtent("Test GetTextExtent");
        dc.DrawText(Width - p.x, font.Ascent, "Test GetTextExtent");
        
        var sw = this.sw ?? (this.sw = new Stopwatch());
        var rnd = new Random();
        sw.Restart();
        for(int i = 0; i < 100000; i++)
        {
          dc.Color = 0x80000000 | (uint)rnd.Next();
          dc.DrawText(100 + (float)(rnd.NextDouble()*800), 100 + (float)(rnd.NextDouble() * 800), "Alles nur eine Frage der Zeit");
        }
        sw.Stop();
        dc.Color = 0xff000000;
        dc.DrawText(10, 50,$"{sw.ElapsedMilliseconds} ms");
#endif

#if(false)
        dc.Transform *= float4x3.Translation(0, 300, 0);
         dc.Color = 0x80ff0000; dc.FillRect(00, 00, 40, 15);
         dc.Color = 0x8000ff00; dc.FillRect(10, 10, 40, 15);
         dc.Color = 0x800000ff; dc.FillRect(20, 20, 40, 15);
         
         //dc.Transform *= 3;
         dc.Color = 0x80ff0000; dc.FillEllipse(00, 50 + 00, 40, 15);
         dc.Color = 0x8000ff00; dc.FillEllipse(10, 50 + 10, 40, 15);
         dc.Color = 0x800000ff; dc.FillEllipse(20, 50 + 20, 40, 15);
         
         dc.Font = Factory.GetFont("Arial", 72, 0);
         dc.Color = 0xff000000;
         dc.DrawText(100, 100, "Hello World!");
         dc.DrawText(100, 200, "Text 123 Ready");
#endif
      }
    }
  }

  class MenuItem : ToolStripMenuItem
  {
    private int id; internal static Func<int, object, int> CommandRoot;
    public MenuItem(string text, params ToolStripItem[] items)
    {
      Text = text; DropDownItems.AddRange(items);
    }
    public MenuItem(int id, string text, Keys keys = Keys.None)
    {
      this.id = id; Text = text; ShortcutKeys = keys;
    }
    protected override void OnDropDownShow(EventArgs e)
    {
      base.OnDropDownShow(e);
      var items = DropDownItems;
      Update(items);
    }
    public static void Update(ToolStripItemCollection items)
    {
      for (int i = 0; i < items.Count; i++)
      {
        var mi = items[i] as MenuItem;
        if (mi == null) { var bu = items[i] as Button; if (bu != null) bu.Enabled = (CommandRoot(bu.id, bu) & 1) != 0; continue; }
        if (mi.id == 0) continue;
        var hr = CommandRoot(mi.id, mi); mi.Enabled = (hr & 1) != 0; mi.Checked = (hr & 2) != 0;
        if (!mi.HasDropDownItems) continue; mi.Visible = false;
        foreach (var e in mi.DropDownItems.OfType<ToolStripMenuItem>()) items.Insert(++i, new MenuItem(mi.id, e.Text) { Tag = e.Tag, Checked = e.Checked });
        mi.DropDownItems.Clear();
      }
    }
    protected override void OnDropDownClosed(EventArgs e)
    {
      base.OnDropDownClosed(e); var items = DropDownItems;
      for (int i = 0; i < items.Count; i++) { items[i].Enabled = true; if (items[i].Tag != null) items.RemoveAt(i--); }
    }
    protected override void OnClick(EventArgs e)
    {
      if (id != 0) CommandRoot(id, Tag);
    }
    internal class Button : ToolStripButton
    {
      internal int id;
      public Button(int id, string text, Image img)
      {
        this.id = id; Text = text; Image = img; DisplayStyle = ToolStripItemDisplayStyle.Image;
        AutoSize = false; Size = new Size(40, 40);
      }
      protected override void OnClick(EventArgs e) => CommandRoot(id, Tag);

    }
  }

  class ContextMenu : ContextMenuStrip
  {
    internal ContextMenu() : base() { }
    internal ContextMenu(IContainer container) : base(container) { }
    protected override void OnOpening(CancelEventArgs e)
    {
      base.OnOpening(e);
      var v = Tag as CodeEditor; if (v != null) { Items.Clear(); v.OnContextMenu(Items); }
      MenuItem.Update(Items); e.Cancel = Items.Count == 0;
    }
  }


}
