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
      CDX.Factory.SetDevice(0xffffffff);
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
      var re = edit.OnCommand(id, test);
      if (re != 0) return re;
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
      edit.IsModified = false; Cursor.Current = Cursors.Default;
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
      if (edit == null || !edit.IsModified) return true;
      switch (MessageBox.Show(this, String.Format(path == null ? "Save changings?" : "Save changings in {0}?", path), Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return !edit.IsModified;
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
      if (e.Cancel = !AskSave()) return; view.Timer = null; edit.OnFormClosing(this);
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

    unsafe class View : UserControl, CDX.ISink
    {
      internal Container cont;
      internal Action Timer;
      internal CDX.IView view;
      long drvsettings = 0x400000000;
      protected unsafe override void OnHandleCreated(EventArgs e)
      {
        var reg = Application.UserAppDataRegistry;
        var drv = reg.GetValue("drv"); if (drv is long v) drvsettings = v;
        CDX.Factory.SetDevice((uint)drvsettings);
        view = CDX.Factory.CreateView(Handle, this, (uint)(drvsettings >> 32));
        view.Render = (CDX.Render)reg.GetValue("fl", (int)(CDX.Render.BoundingBox | CDX.Render.Coordinates | CDX.Render.Wireframe | CDX.Render.Shadows));
        view.BkColor = 0xffcccccc;
        view.Scene = cont.Nodes;
        OnCenter(null);
      }
      internal int OnDriver(object test)
      {
        if (test is ToolStripMenuItem item)
        {
          var ss = CDX.Factory.Devices.Split('\n');
          for (int i = 1; i < ss.Length; i += 2)
            item.DropDownItems.Add(new ToolStripMenuItem(ss[i + 1]) { Tag = ss[i], Checked = ss[i] == ss[0] });
          return 0;
        }
        Cursor = Cursors.WaitCursor; CDX.Factory.SetDevice(uint.Parse((string)test));
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
      internal int OnCenter(object test)
      {
        if (!view.Scene.Nodes().Any(p => p.Mesh != null && p.Mesh.VertexCount != 0)) return 0;
        if (test != null) return 1;
        float abst = 100; view.Command(CDX.Cmd.Center, &abst);
        Invalidate(); return 1;
      }

      Action<int, object> tool;
      protected override void OnMouseDown(MouseEventArgs e)
      {
        if (e.Button != MouseButtons.Left) return;
        if (view.Camera == null) return;
        tool = camera_free(); Capture = true;
      }
      protected override void OnMouseMove(MouseEventArgs e)
      {
        if (tool != null) { tool(0, null); return; }
        //var i = view.MouseOverNode;
        //this.Text = i != -1 ? $"over: {i} {view.Scene[i].Name} {view.MouseOverPoint}" : "";
      }
      protected override void OnMouseUp(MouseEventArgs e)
      {
        if (tool == null) return;
        tool(1, null); tool = null; Capture = true;
      }
      protected override void OnLostFocus(EventArgs e)
      {
        if (tool == null) return;
        tool(1, null); tool = null;
      }
      protected override void OnMouseWheel(MouseEventArgs e)
      {
        if (tool != null) return;
        if (view.MouseOverNode == -1) return;
        var camera = view.Camera.GetTransformF();
        var node = view.Scene[view.MouseOverNode];
        var v = (view.MouseOverPoint * node.GetTransformF()) - camera.mz;
        var l = v.Length; // (float)Math.Sqrt(v.Length);
        //var t = Environment.TickCount; var cm = camera;
        setcwma(camera * (v * (l * 0.01f * e.Delta /* * DpiScale*/ * (1f / 120))));
        //if (t - lastwheel > 500) AddUndo(setcwmb(cm)); lastwheel = t;
      }
      unsafe Action<int, object> camera_free()
      {
        var m = float4x3.Identity; view.Command(CDX.Cmd.GetBox, &m);
        var boxmin = *(float3*)&m._11; var boxmax = *(float3*)&m._22;
        //float3box box; var pbox = (float3*)&box; //var recs = cont.Nodes;
        //boxempty(pbox); //box.min = box.max = 0;//for (int i = 0; i < recs.Count; i++) recs[i].getbox(recs[i].gettrans(), pbox);
        //foreach (var node in view.Scene.Nodes())
        //{
        //  var m = node.Mesh; if (m == null) continue;
        //  var t = node.GetTransformF();
        //  foreach (var v in m.VerticesF3()) boxadd(&v, &t, pbox);
        //}
        var pm = (boxmin + boxmax) * 0.5f; var tm = (float4x3)pm;
        var cm = view.Camera.TransformF; var p1 = (float2)Cursor.Position; bool moves = false;
        return (id, p) =>
        {
          if (id == 0)
          {
            var v = (Cursor.Position - p1) * -0.01f;
            if (!moves && v.LengthSq < 0.03) return; moves = true;
            setcwma(cm * !tm * float4x3.RotationAxis(cm.mx, -v.y) * float4x3.RotationZ(v.x) * tm);
          }
          //if (id == 1) { if (moves) AddUndo(setcwmb(cm)); else Select(null); }
        };
      }
      bool setcwma(in float4x3 m)
      {
        if (m == view.Camera.TransformF) return false;
        view.Camera.TransformF = m; Invalidate(); return true;
      }

      internal void onstep()
      {
        Invalidate();
      }

      CDX.IFont font = CDX.Factory.GetFont("Arial", 13, System.Drawing.FontStyle.Bold);

      void CDX.ISink.Render()
      {
        var dc = new CDX.DC(view);
        dc.SetOrtographic();

        var ss = cont.Infos;
        dc.Font = font;
        dc.Color = 0x80000000;
        var y = 10 + font.Ascent;
        for (int i = 0; i < ss.Count; i++, y += font.Height)
          dc.DrawText(10, y, ss[i]);

        // dc.Transform *= move(0, 300, 0);
        // dc.Color = 0x80ff0000; dc.FillRect(00, 00, 40, 15);
        // dc.Color = 0x8000ff00; dc.FillRect(10, 10, 40, 15);
        // dc.Color = 0x800000ff; dc.FillRect(20, 20, 40, 15);
        // 
        // //dc.Transform *= scale(3, 3, 3);
        // dc.Color = 0x80ff0000; dc.FillEllipse(00, 50 + 00, 40, 15);
        // dc.Color = 0x8000ff00; dc.FillEllipse(10, 50 + 10, 40, 15);
        // dc.Color = 0x800000ff; dc.FillEllipse(20, 50 + 20, 40, 15);
        // 
        // dc.Font = CDX.Factory.GetFont("Arial", 72, 0);
        // dc.Color = 0xff000000;
        // dc.DrawText(100, 100, "Hello World!");
        // dc.DrawText(100, 200, "Text 123 Ready");
        // //dc.DrawText(100, 100, "He");


      }
    }

#if(false)
    unsafe class View : D3DView
    {
      internal int flags; internal float4x3 camera;
      float z1 = 0.1f, z2 = 1000, vscale, minwz;
      internal Container cont;
      protected override void OnHandleCreated(EventArgs e)
      {
        OnCenter(null); base.OnHandleCreated(e);
      }
      internal void setcamera()
      {
        vscale = 0.0004f / DpiScale;
        camera = !LookAt(new float3(-6, -6, 4), new float3(0, 0, 0), new float3(0, 0, 1));
      }
      bool setcwma(in float4x3 m)
      {
        if (m == camera) return false;
        camera = m; Invalidate(); return true;
      }
      protected override void OnRender(IDisplay dc)
      {
        dc.State = 0x1001015c; //dc.DepthStencil = DepthStencil.ZWrite; dc.Rasterizer = Rasterizer.CullFront; dc.BlendState = BlendState.Opaque; dc.VertexShader = VertexShader.Lighting; dc.PixelShader = PixelShader.Color3D; dc.Topology = Topology.TriangleListAdj;
        var size = dc.Viewport; var shadows = (flags & (1 << 10)) != 0;
        var vp = size * (vscale * z1);
        dc.SetProjection(!camera * float4x4.PerspectiveOffCenter(vp.x, -vp.x, -vp.y, vp.y, z1, z2));
        dc.Ambient = 0x00404040;
        var lightdir = normalize(new float3(0.4f, 0.3f, -1)) & camera; lightdir.z = Math.Abs(lightdir.z);
        dc.Light = shadows ? lightdir * 0.3f : lightdir; int transp = 0;
        var recs = cont.Nodes;
        for (int i = 0; i < recs.Count; i++)
        {
          var p = recs[i]; if (p.vertexbuffer == null) continue;
          for (int k = 0, t = 0; k < p.Materials.Length; k++)
          {
            ref var m = ref p.Materials[k]; if ((m.Color >> 24) != 0xff) { transp++; continue; }
            if (t++ == 0) { dc.Select(p, 1); dc.SetTransform(p.gettrans()); }
            if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
            else dc.PixelShader = PixelShader.Color3D;
            dc.Color = m.Color; dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
          }
        }
        dc.Select();
        if (shadows)
        {
          var t1 = dc.State; dc.Light = lightdir; dc.LightZero = this.minwz;
          dc.State = 0x2100050c; // dc.VertexShader = VertexShader.World; dc.GeometryShader = GeometryShader.Shadows; dc.PixelShader = PixelShader.Null; dc.Rasterizer = Rasterizer.CullNone; dc.DepthStencil = DepthStencil.TwoSide;
          for (int i = 0; i < recs.Count; i++)
          {
            var p = recs[i]; if (p.vertexbuffer == null) continue; if ((p.Materials[0].Color >> 24) != 0xff) continue;
            dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
          }
          dc.State = t1; dc.Ambient = 0; dc.Light = lightdir * 0.7f; dc.BlendState = BlendState.AlphaAdd;
          for (int i = 0; i < recs.Count; i++)
          {
            var p = recs[i]; if (p.vertexbuffer == null) continue;
            for (int k = 0, t = 0; k < p.Materials.Length; k++)
            {
              ref var m = ref p.Materials[k]; if ((m.Color >> 24) != 0xff) continue;
              if (t++ == 0) dc.SetTransform(p.gettrans()); dc.Color = m.Color;
              if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
              else dc.PixelShader = PixelShader.Color3D;
              dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
            }
          }
          dc.State = t1; dc.Light = lightdir; dc.Ambient = 0x00404040;
          dc.Clear(CLEAR.STENCIL);
        }
        if (true)
        {
          if ((flags & 0x03) != 0)
          {
            dc.SetTransform(1); dc.State = 0x0000011c; //dc.VertexShader = VertexShader.Default; dc.PixelShader = PixelShader.Color; dc.Rasterizer = Rasterizer.CullNone;
            float3box box; var pb = (float3*)&box; boxempty(pb);
            for (int i = 0; i < recs.Count; i++) { var p = recs[i]; if (p.vertexbuffer != null) p.getbox(p.gettrans(), pb); }
            if ((flags & 0x01) != 0) //Box
            {
              dc.Color = 0xffffffff; long f1 = 0x4c8948990, f2 = 0xdecddabd9;
              for (int t = 0; t < 12; t++, f1 >>= 3, f2 >>= 3)
                dc.DrawLine(boxcor(pb, (int)(f1 & 7)), boxcor(pb, (int)(f2 & 7)));
            }
            if ((flags & 0x02) != 0) //Pivot
            {
              float f = length(box.max - box.min) * 0.5f, t1 = 0.25f * f, t2 = 0.1f * f, t3 = 0.02f * f;
              dc.SetTransform(1); float3 p1 = new float3(), p2;
              dc.Color = 0xffff0000; dc.DrawLine(p1, p2 = new float3(box.max.x + t1, 0, 0)); dc.DrawArrow(p2, new float3(t2, 0, 0), t3);
              dc.Color = 0xff00ff00; dc.DrawLine(p1, p2 = new float3(0, box.max.y + t1, 0)); dc.DrawArrow(p2, new float3(0, t2, 0), t3);
              dc.Color = 0xff0000ff; dc.DrawLine(p1, p2 = new float3(0, 0, box.max.z + t1)); dc.DrawArrow(p2, new float3(0, 0, t2), t3);
            }
          }
          if ((flags & 0x08) != 0) //Wireframe
          {
            dc.Color = 0x40000000; dc.State = 0x0003111c; //dc.Rasterizer = Rasterizer.Wireframe; dc.BlendState = BlendState.Alpha; dc.VertexShader = VertexShader.Default; dc.PixelShader = PixelShader.Color;
            for (int i = 0; i < recs.Count; i++)
            {
              var p = recs[i]; if (p.vertexbuffer == null) continue;
              dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
            }
          }
          if ((flags & 0x10) != 0) //Outline
          {
            dc.Color = 0xff000000; dc.State = 0x2200015c; //dc.GeometryShader = GeometryShader.Outline3D; dc.VertexShader = VertexShader.World; dc.PixelShader = PixelShader.Color3D;
            dc.Light = camera[3];
            for (int i = 0; i < recs.Count; i++)
            {
              var p = recs[i]; if (p.vertexbuffer == null) continue;
              dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
            }
            dc.Light = lightdir;
          }
        }
        if (transp != 0)
        {
          dc.State = 0x1001115c; //dc.BlendState = BlendState.Alpha; dc.VertexShader = VertexShader.Lighting; dc.PixelShader = PixelShader.Color3D; dc.Rasterizer = Rasterizer.CullFront;
          for (int i = 0; i < recs.Count; i++)
          {
            var p = recs[i]; if (p.vertexbuffer == null) continue;
            for (int k = 0, t = 0; k < p.Materials.Length; k++)
            {
              ref var m = ref p.Materials[k]; if ((m.Color >> 24) == 0xff) continue;
              if (t++ == 0) { dc.Select(p, 1); dc.SetTransform(p.gettrans()); }
              if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
              else dc.PixelShader = PixelShader.Color3D;
              dc.Color = m.Color; dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
            }
          }
          dc.Select();
        }
        dc.SetProjection(float4x4.OrthoCenter(0, size.x, size.y, 0, 0, 1));
        dc.SetTransform(1);
        dc.State = 0x0000001c; //dc.PixelShader = PixelShader.Color; dc.DepthStencil = DepthStencil.Default; dc.Rasterizer = Rasterizer.CullNone; 
        dc.Color = 0xff000000;
        float x = dc.Viewport.x - 8, y = 8 + dc.Font.Ascent, y1 = y, dy = dc.Font.Height; string s;
        for (int i = 0; i < cont.Infos.Count; i++, y1 += dy) if ((s = cont.Infos[i]) != null) dc.DrawText(8, y1, s);
        s = Adapter; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        s = $"{GetFPS()} fps"; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        s = $"{DpiScale * 96} dpi"; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        if ((CSG.Factory.Version & 0x100) != 0) { s = "Debug"; dc.DrawText(x - dc.Measure(s), y, s); y += dy; }
      }
      protected override int OnDispatch(int id, ISelector pc)
      {
        switch (id & 0xffff)
        {
          case 0x0201: //WM_LBUTTONDOWN           
            //if (pc.Hover is Node)
            //{
            //  var keys = Control.ModifierKeys;
            //  switch (keys)
            //  {
            //    case Keys.None: pc.SetTool(obj_sel(pc)); break;
            //    case Keys.Control: pc.SetTool(camera_movxy(pc)); break;
            //    case Keys.Shift: pc.SetTool(camera_movz(pc)); break;
            //    case Keys.Alt: pc.SetTool(camera_rotz(pc)); break;
            //    case Keys.Control | Keys.Shift: pc.SetTool(camera_rotx(pc)); break;
            //  }
            //  return 1;
            //}
            if (cont.Nodes.Count != 0) pc.SetTool(camera_free(pc));
            return 1;
          case 0x020A: //WM_MOUSEWHEEL
            {
              if (!(pc.Hover is Container.Node)) return 1;
              var v = (pc.Point * pc.Transform) - camera[3];
              var l = (float)Math.Sqrt(length(v));
              var t = Environment.TickCount; var cm = camera;
              setcwma(camera * (float4x3)(v * (l * 0.01f * (id >> 16) * DpiScale * (1f / 120))));
              //if (t - lastwheel > 500) AddUndo(setcwmb(cm)); lastwheel = t;
              return 1;
            }
        }
        return 0;
      }
      Action<int, object> camera_free(ISelector pc)
      {
        float3box box; var pbox = (float3*)&box; var recs = cont.Nodes;
        boxempty(pbox); for (int i = 0; i < recs.Count; i++) recs[i].getbox(recs[i].gettrans(), pbox);
        var pm = (box.min + box.max) * 0.5f; var tm = (float4x3)pm;
        var cm = camera; var p1 = (float2)Cursor.Position; bool moves = false;
        return (id, p) =>
        {
          if (id == 0)
          {
            var v = (Cursor.Position - p1) * -0.01f;
            if (!moves && dot(v) < 0.03) return; moves = true;
            setcwma(cm * !tm * rotaxis(cm[0], -v.y) * rotz(v.x) * tm);
          }
          //if (id == 1) { if (moves) AddUndo(setcwmb(cm)); else Select(null); }
        };
      }
      internal int OnCenter(object test)
      {
        if (!cont.Nodes.Any(p => p.indexbuffer != null && p.indexbuffer.count != 0)) return 0;
        if (test != null) return 1;
        var r = center(cont.Nodes, (float2)ClientSize / DpiScale - new float2(100, 100), vscale, camera);
        z1 = (float)Math.Pow(10, Math.Round(Math.Log10(r.z1)) - 1);
        z2 = (float)Math.Pow(10, Math.Round(Math.Log10(r.z2)) + 2); minwz = r.wb.min.z;
        setcwma(r.cm); return 1;
      }
      static (float4x3 cm, float z1, float z2, float3box wb) center(IEnumerable<Container.Node> nodes, float2 size, float scale, in float4x3 cwm)
      {
        float4x3 box; float3box wb; boxempty((float3*)&wb);
        box._31 = size.x * scale;
        box._32 = size.y * scale;
        var min = (float3*)&box + 0; boxempty(min);
        var max = (float3*)&box + 1; var icw = !cwm;
        foreach (var p in nodes)
        {
          if (p.vertexbuffer == null) continue;
          var wm = p.gettrans();
          p.getbox(wm, &wb.min);
          p.getbox(wm * icw, min, (float2*)(min + 2));
        }
        if (min->x == float.MaxValue) return default;
        float nx = (max->x - min->x) * 0.5f;
        float ny = (max->y - min->y) * 0.5f;
        float fm = -Math.Max(nx / box._31, ny / box._32);
        return (new float3(min->x + nx, min->y + ny, fm) + cwm, min->z - fm, max->z - fm, wb);
      }
      static (float4x3 cm, float z1, float z2, float3box wb) center(in float4x3 cm, in float4x4 proj, IEnumerable<float3> points)
      {
        float aspekt = proj._22 / proj._11, a = proj._22; float3box wb; boxempty((float3*)&wb);
        float ax = a / aspekt, _ax = 1 / ax, ay = a, _ay = 1 / ay; var vmat = !cm;
        var mi = new float3(float.MaxValue, float.MaxValue, float.MaxValue); var ma = -mi;
        var m1 = (float4x4)(float4x3)1; var m2 = m1;
        m1._31 = +_ax; m1._32 = +_ay; m1 = vmat * m1;
        m2._31 = -_ax; m2._32 = -_ay; m2 = vmat * m2;
        foreach (var p in points) { mi = min(mi, p * m1); ma = max(ma, p * m2); boxadd(&p, &wb.min); }
        if (mi.x == float.MaxValue) return default;
        float mx = 0.5f * (ax * ma.x - ax * mi.x) / ax, nx = ma.x - mi.x - mx;
        float my = 0.5f * (ay * ma.y - ay * mi.y) / ay, ny = ma.y - mi.y - my;
        float fm = Math.Min(ny * -ay, nx * -ax); // near = bmi.Min.Z - fm; far = bma.Max.Z - fm;
        return (new float3(mi.x + nx, mi.y + ny, fm) + cm, mi.z - fm, ma.z - fm, wb);
      }
      internal static System.Drawing.Bitmap CreatePreview(int dx, int dy, float4x3 camera, bool shadows, IEnumerable<Container.Node> nodes)
      {
        return Print(dx, dy, 4, 0x00000000, dc =>
        {
          var size = dc.Viewport;// * DpiScale;
          var proj = float4x4.PerspectiveFov(20 * (float)(Math.PI / 180), size.x / size.y, 0.1f, 1000);
          var r = center(camera, proj, nodes.SelectMany(n => { var m = n.gettrans(); return n.vertexbuffer.GetPoints().Select(p => p * m); }));
          proj = float4x4.PerspectiveFov(20 * (float)(Math.PI / 180), size.x / size.y, r.z2 * 0.5f, r.z2 * 2);
          dc.SetProjection(!r.cm * proj);
          var lightdir = normalize(new float3(0.4f, 0.3f, -1)) & r.cm; lightdir.z = Math.Abs(lightdir.z);
          dc.Light = shadows ? lightdir * 0.3f : lightdir; dc.Select();
          dc.Ambient = 0x00404040; //var rh = true;
          dc.State = /*rh ? 0x1001015c : */0x1002015c;
          foreach (var p in nodes)
          {
            dc.SetTransform(p.gettrans());
            for (int k = 0; k < p.Materials.Length; k++)
            {
              ref var m = ref p.Materials[k]; if ((m.Color >> 24) != 0xff) continue; dc.Color = m.Color;
              if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
              else dc.PixelShader = PixelShader.Color3D;
              dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
            }
          }
          if (shadows)
          {
            var t1 = dc.State; dc.Light = lightdir; dc.LightZero = r.wb.min.z;
            dc.State = 0x2100050c; // dc.VertexShader = VertexShader.World; dc.GeometryShader = GeometryShader.Shadows; dc.PixelShader = PixelShader.Null; dc.Rasterizer = Rasterizer.CullNone; dc.DepthStencil = DepthStencil.TwoSide;
            foreach (var p in nodes)
            {
              if ((p.Materials[0].Color >> 24) != 0xff) continue;
              dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
            }
            dc.State = t1; dc.Ambient = 0; dc.Light = lightdir * 0.7f; dc.BlendState = BlendState.AlphaAdd;
            foreach (var p in nodes)
            {
              dc.SetTransform(p.gettrans());
              for (int k = 0; k < p.Materials.Length; k++)
              {
                ref var m = ref p.Materials[k]; if ((m.Color >> 24) != 0xff) continue; dc.Color = m.Color;
                if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
                else dc.PixelShader = PixelShader.Color3D;
                dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
              }
            }
            dc.Clear(CLEAR.STENCIL);
          }
          dc.Light = lightdir; dc.Ambient = 0x00404040; dc.State = 0x1001115c; //dc.BlendState = BlendState.Alpha; dc.VertexShader = VertexShader.Lighting; dc.PixelShader = PixelShader.Color3D; dc.Rasterizer = Rasterizer.CullFront;
          foreach (var p in nodes)
          {
            dc.SetTransform(p.gettrans());
            for (int k = 0; k < p.Materials.Length; k++)
            {
              ref var m = ref p.Materials[k]; if ((m.Color >> 24) == 0xff) continue; dc.Color = m.Color;
              if (m.texture != null) { dc.Texture = m.texture; dc.PixelShader = PixelShader.Texture; }
              else dc.PixelShader = PixelShader.Color3D;
              dc.DrawMesh(p.vertexbuffer, p.indexbuffer, m.StartIndex, m.IndexCount);
            }
          }
        });
      }
      internal void onstep()
      {
        Invalidate();
        var r = center(cont.Nodes, (float2)ClientSize / DpiScale - new float2(100, 100), vscale, camera);
        if (r.z1 == r.z2 || camera == r.cm) return;
        z1 = (float)Math.Pow(10, Math.Round(Math.Log10(r.z1)) - 1);
        z2 = (float)Math.Pow(10, Math.Round(Math.Log10(r.z2)) + 2); minwz = r.wb.min.z;
        var ab = camera; //camera = r.cm;
        var tf = 1000.0f / Stopwatch.Frequency;
        var t1 = Stopwatch.GetTimestamp(); this.Timer = ani;
        void ani()
        {
          var dt = ((Stopwatch.GetTimestamp() - t1) * tf) - 1000; if (dt < 0) return;
          var f = Math.Min(1, dt / 1000); var t = f == 1 ? f : (float)(f <= 0.5f ? Math.Pow(f * 2, 3) : 2 - Math.Pow((1 - f) * 2, 3)) * 0.5f;
          camera[3] = ab[3] * (1 - t) + r.cm[3] * t; Invalidate(); if (f == 1) Timer = null;
        }
      }
    }
#endif

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
      var v = Tag as CodeEditor; if (v != null) { Items.Clear(); v.OnContextMenu(Items); }
      MenuItem.Update(Items); e.Cancel = Items.Count == 0;
      base.OnOpening(e);
    }
  }


}
