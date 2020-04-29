using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using csg3mf.Viewer;
using static csg3mf.Viewer.D3DView;

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
      Application.Run(new MainFram());
    }
  }

  class MainFram : Form
  {
    unsafe class View : D3DView
    {
      internal View() { CSG.disp = Display; }
      internal void Display(CSG.IMesh[] a)
      {
        var n = a.Length + 1;
        if (nodes.Count > n) nodes.RemoveRange(a.Length, nodes.Count - n);
        else while (nodes.Count < n) nodes.Add(new Node());
        nodes[0].trans = 1;
        for (int i = 0; i < a.Length; i++)
        {
          var p = nodes[i + 1]; p.Parent = nodes[0];
          p.Texture = null; p.Color = 0xff808080;
          p.Transform = CSG.Rational.Matrix.Identity(); p.Mesh = a[i]; p.update();
        }
        setcamera(); Invalidate(); Refresh();
      }
      internal int flags; internal float3x4 camera;
      float nearplane = 0.1f, farplane = 10000, vscale;
      internal List<Node> nodes = new List<Node>();
      protected override void OnHandleCreated(EventArgs e)
      {
        OnCenter(null); base.OnHandleCreated(e);
      }
      internal void setcamera()
      {
        vscale = 0.0004f / DpiScale;
        camera = !LookAt(new float3(-6, -6, 4), new float3(0, 0, 0), new float3(0, 0, 1));
      }
      bool setcwma(in float3x4 m)
      {
        if (m == camera) return false;
        camera = m; Invalidate(); return true;
      }
      protected override void OnRender(IDisplay dc)
      {
        dc.State = 0x1001015c; //dc.DepthStencil = DepthStencil.ZWrite; dc.Rasterizer = Rasterizer.CullFront; dc.BlendState = BlendState.Opaque; dc.VertexShader = VertexShader.Lighting; dc.PixelShader = PixelShader.Color3D; dc.Topology = Topology.TriangleListAdj;
        var size = dc.Viewport; var shadows = (flags & (1 << 10)) != 0;
        var vp = size * (vscale * nearplane);
        dc.SetProjection(!camera * float4x4.PerspectiveOffCenter(vp.x, -vp.x, -vp.y, vp.y, nearplane, farplane));
        dc.Ambient = 0x00404040;
        var lightdir = normalize(new float3(0.4f, 0.3f, -1)) & camera; lightdir.z = Math.Abs(lightdir.z);
        dc.Light = shadows ? lightdir * 0.3f : lightdir;
        for (int i = 1; i < nodes.Count; i++)
        {
          var p = nodes[i]; if (p.vertexbuffer == null) continue;
          dc.Select(p, 1); dc.SetTransform(p.gettrans()); dc.Color = p.Color;
          if (p.texture != null) { dc.Texture = p.texture; dc.PixelShader = PixelShader.Texture; }
          else dc.PixelShader = PixelShader.Color3D;
          dc.DrawMesh(p.vertexbuffer, p.indexbuffer, p.StartIndex, p.IndexCount);
        }
        dc.Select();
        if (shadows)
        {
          var t1 = dc.State; dc.Light = lightdir; dc.LightZero = -5;
          dc.VertexShader = VertexShader.World;
          dc.GeometryShader = GeometryShader.Shadows;
          dc.PixelShader = PixelShader.Null;
          dc.Rasterizer = Rasterizer.CullNone;
          dc.DepthStencil = DepthStencil.TwoSide;
          for (int i = 1; i < nodes.Count; i++)
          {
            var p = nodes[i]; if (p.vertexbuffer == null || p.StartIndex != 0) continue;
            dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
          }
          dc.State = t1;
          dc.Ambient = 0;
          dc.Light = lightdir * 0.7f;
          dc.BlendState = BlendState.AlphaAdd;
          for (int i = 1; i < nodes.Count; i++)
          {
            var p = nodes[i]; if (p.vertexbuffer == null) continue;
            dc.SetTransform(p.gettrans()); dc.Color = p.Color;
            if (p.texture != null) { dc.Texture = p.texture; dc.PixelShader = PixelShader.Texture; }
            else dc.PixelShader = PixelShader.Color3D;
            dc.DrawMesh(p.vertexbuffer, p.indexbuffer, p.StartIndex, p.IndexCount);
          }
          dc.State = t1;
          dc.Clear(CLEAR.STENCIL);
        }
        dc.VertexShader = VertexShader.Default;
        dc.PixelShader = PixelShader.Color;
        dc.Rasterizer = Rasterizer.CullNone;
        if (true)
        {
          var t1 = dc.State;
          if ((flags & 0x03) != 0)
          {
            dc.SetTransform(1);
            float3box box; var pbox = (float3*)&box;
            boxempty(pbox); for (int i = 1; i < nodes.Count; i++) nodes[i].getbox(nodes[i].gettrans(), pbox);
            if ((flags & 0x01) != 0) //Box
            {
              dc.Color = 0xffffffff; long f1 = 0x4c8948990, f2 = 0xdecddabd9;
              for (int t = 0; t < 12; t++, f1 >>= 3, f2 >>= 3)
                dc.DrawLine(boxcor(pbox, (int)(f1 & 7)), boxcor(pbox, (int)(f2 & 7)));
            }
            if ((flags & 0x02) != 0) //Pivot
            {
              dc.SetTransform(1); float3 p1 = new float3(), p2;
              dc.Color = 0xffff0000; dc.DrawLine(p1, p2 = new float3(box.max.x + 0.25f, 0, 0)); dc.DrawArrow(p2, new float3(0.1f, 0, 0), 0.02f);
              dc.Color = 0xff00ff00; dc.DrawLine(p1, p2 = new float3(0, box.max.y + 0.25f, 0)); dc.DrawArrow(p2, new float3(0, 0.1f, 0), 0.02f);
              dc.Color = 0xff0000ff; dc.DrawLine(p1, p2 = new float3(0, 0, box.max.z + 0.25f)); dc.DrawArrow(p2, new float3(0, 0, 0.1f), 0.02f);
            }
            dc.State = t1;
          }
          if ((flags & 0x08) != 0) //Wireframe
          {
            dc.Color = 0x40000000; dc.BlendState = BlendState.Alpha;
            dc.Rasterizer = Rasterizer.Wireframe;
            for (int i = 1; i < nodes.Count; i++)
            {
              var p = nodes[i]; if (p.vertexbuffer == null || p.StartIndex != 0) continue;
              dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
            }
            dc.State = t1;
          }
          if ((flags & 0x10) != 0) //Outline
          {
            dc.Color = 0xff000000;
            dc.GeometryShader = GeometryShader.Outline3D;
            dc.PixelShader = PixelShader.Color3D;
            dc.VertexShader = VertexShader.World;
            dc.Light = camera[3];
            for (int i = 1; i < nodes.Count; i++)
            {
              var p = nodes[i]; if (p.vertexbuffer == null || p.StartIndex != 0) continue;
              dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer);
            }
            dc.State = t1;
          }
        }
        dc.SetProjection(float4x4.OrthoCenter(0, size.x, size.y, 0, 0, 1));
        dc.SetTransform(1);
        dc.PixelShader = PixelShader.Color;
        dc.DepthStencil = DepthStencil.Default;
        dc.Rasterizer = Rasterizer.CullNone;
        dc.Select();

        dc.Color = 0xff000000;
        float x = dc.Viewport.x - 8, y = 8 + dc.Font.Ascent, dy = dc.Font.Height; string s;
        //s = "";// string.Join(", ", Buffer.cache.Values.Select(p => p.Target).GroupBy(p => p != null ? p.GetType().Name : "(null)").Select(p => string.Format("{1} {0}", p.Key, p.Count())));
        //dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        s = Adapter; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        s = $"{GetFPS()} fps"; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
        s = $"{DpiScale * 96} dpi"; dc.DrawText(x - dc.Measure(s), y, s); y += dy;
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
            if (nodes.Count > 1) pc.SetTool(camera_free(pc));
            return 1;
          case 0x020A: //WM_MOUSEWHEEL
            {
              if (!(pc.Hover is Node)) return 1;
              var v = (pc.Point * pc.Transform) - camera[3];
              var l = (float)Math.Sqrt(length(v));
              var t = Environment.TickCount; var cm = camera;
              setcwma(camera * (float3x4)(v * (l * 0.01f * (id >> 16) * DpiScale * (1f / 120))));
              //if (t - lastwheel > 500) AddUndo(setcwmb(cm)); lastwheel = t;
              return 1;
            }
        }
        return 0;
      }
      Action<int, object> camera_free(ISelector pc)
      {
        float3box box; var pbox = (float3*)&box;
        boxempty(pbox); for (int i = 1; i < nodes.Count; i++) nodes[i].getbox(nodes[i].gettrans(), pbox);
        var pm = (box.min + box.max) * 0.5f; var tm = (float3x4)pm;
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
        if (nodes.Count <= 1) return 0;
        if (test != null) return 1;
        var m = camera;
        center(nodes, (float2)ClientSize / DpiScale - new float2(32, 32), vscale, ref m);
        setcwma(m); return 1;
      }
      static void center(IEnumerable<Node> nodes, float2 size, float scale, ref float3x4 cwm)
      {
        float3x4 box;
        box._31 = size.x * scale;
        box._32 = size.y * scale;
        var min = (float3*)&box + 0; boxempty(min);
        var max = (float3*)&box + 1;
        foreach (var p in nodes) p.getbox(p.gettrans() * !cwm, min, (float2*)(min + 2));
        if (min->x == float.MaxValue) return;
        float nx = (max->x - min->x) * 0.5f;
        float ny = (max->y - min->y) * 0.5f;
        float fm = -Math.Max(nx / box._31, ny / box._32);
        cwm = new float3(min->x + nx, min->y + ny, fm) + cwm; // var near = min->Z - fm;
        var far = max->z - fm; //farplane = Math.Max(1000, Math.Min(10000, far * 10));
      }
      static void center(ref float3x4 cm, ref float4x4 proj, IEnumerable<float3> points)
      {
        float aspekt = proj._22 / proj._11, a = proj._22;
        float ax = a / aspekt, _ax = 1 / ax, ay = a, _ay = 1 / ay; var vmat = !cm;
        var mi = new float3(float.MaxValue, float.MaxValue, float.MaxValue); var ma = -mi;
        var m1 = (float4x4)(float3x4)1; var m2 = m1;
        m1._31 = +_ax; m1._32 = +_ay; m1 = vmat * m1;
        m2._31 = -_ax; m2._32 = -_ay; m2 = vmat * m2;
        foreach (var p in points) { mi = min(mi, p * m1); ma = max(ma, p * m2); }
        if (mi.x == float.MaxValue) return;
        float mx = 0.5f * (ax * ma.x - ax * mi.x) / ax, nx = ma.x - mi.x - mx;
        float my = 0.5f * (ay * ma.y - ay * mi.y) / ay, ny = ma.y - mi.y - my;
        float fm = Math.Min(ny * -ay, nx * -ax); // near = bmi.Min.Z - fm; far = bma.Max.Z - fm;
        cm = new float3(mi.x + nx, mi.y + ny, fm) + cm;
      }
      internal static System.Drawing.Bitmap preview(int dx, int dy, float3x4 camera, bool shadows, IEnumerable<Node> nodes)
      {
        return Print(dx, dy, 4, 0x00000000, dc =>
        {
          var size = dc.Viewport;// * DpiScale;
          var proj = float4x4.PerspectiveFov(20 * (float)(Math.PI / 180), size.x / size.y, 0.1f, 1000);
          center(ref camera, ref proj, nodes.SelectMany(n => { var m = n.gettrans(); return n.vertexbuffer.GetPoints().Select(p => p * m); }));
          dc.SetProjection(!camera * proj);
          var lightdir = normalize(new float3(0.4f, 0.3f, -1)) & camera; lightdir.z = Math.Abs(lightdir.z);
          dc.Light = shadows ? lightdir * 0.3f : lightdir; dc.Select();
          dc.Ambient = 0x00404040; //var rh = true;
          dc.State = /*rh ? 0x1001015c : */0x1002015c;
          foreach (var p in nodes)
          {
            dc.SetTransform(p.gettrans()); dc.Color = p.Color;
            if (p.texture != null) { dc.Texture = p.texture; dc.PixelShader = PixelShader.Texture; }
            else dc.PixelShader = PixelShader.Color3D;
            dc.DrawMesh(p.vertexbuffer, p.indexbuffer, p.StartIndex, p.IndexCount);
          }
          if (shadows)
          {
            var t1 = dc.State; dc.Light = lightdir; dc.LightZero = -5;
            dc.VertexShader = VertexShader.World;
            dc.GeometryShader = GeometryShader.Shadows;
            dc.PixelShader = PixelShader.Null;
            dc.Rasterizer = Rasterizer.CullNone;
            dc.DepthStencil = DepthStencil.TwoSide;
            foreach (var p in nodes) { if (p.StartIndex != 0) continue; dc.SetTransform(p.gettrans()); dc.DrawMesh(p.vertexbuffer, p.indexbuffer); }
            dc.State = t1;
            dc.Ambient = 0;
            dc.Light = lightdir * 0.7f;
            dc.BlendState = BlendState.AlphaAdd;
            foreach (var p in nodes)
            {
              dc.SetTransform(p.gettrans()); dc.Color = p.Color;
              if (p.texture != null) { dc.Texture = p.texture; dc.PixelShader = PixelShader.Texture; }
              else dc.PixelShader = PixelShader.Color3D;
              dc.DrawMesh(p.vertexbuffer, p.indexbuffer, p.StartIndex, p.IndexCount);
            }
          }
        });
      }
    }

    internal MainFram()
    {
      StartPosition = FormStartPosition.WindowsDefaultBounds;
      MainMenuStrip = new MenuStrip(); MenuItem.CommandRoot = OnCommand;
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
          new MenuItem(5011, "&Run Release", Keys.F5|Keys.Control ),
          new ToolStripSeparator(),
          new MenuItem(5014, "&Compile", Keys.F6 ),
          new MenuItem(5010, "Start &Debug", Keys.F5 ),
          new MenuItem(5016, "Step &Over", Keys.F10 ),
          new MenuItem(5015, "Step &Into", Keys.F11 ),
          new MenuItem(5017, "Step Ou&t", Keys.F11|Keys.Shift ),
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
      splitter = new SplitContainer { Dock = DockStyle.Fill };
      splitter.SplitterDistance = 75;// ClientSize.Width / 2;
      splitter.Panel2.Controls.Add(view = new View { Dock = DockStyle.Fill });
      Controls.Add(splitter);
      Controls.Add(MainMenuStrip);
      var reg = Application.UserAppDataRegistry; view.flags = (int)reg.GetValue("fl", 0x00ffff03);
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
    string path; Neuron neuron; NeuronEditor edit;
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
          if (test != null) return (view.flags & (1 << (id - 2210))) != 0 ? 3 : 1;
          view.flags ^= 1 << (id - 2210); Application.UserAppDataRegistry.SetValue("fl", view.flags);
          view.Invalidate(); return 1;
        case 2300: return view.OnCenter(test);
      }
      return 0;
    }
    void UpdateTitle()
    {
      Text = string.Format("{0} - {1} {2} Bit {3}", path ?? string.Format("({0})", "Untitled"), Application.ProductName, Environment.Is64BitProcess ? "64" : "32", COM.DEBUG ? "Debug" : "Release");
    }
    void Open(string path)
    {
      Cursor.Current = Cursors.WaitCursor;
      string script = null;
      if (path != null && path.EndsWith(".3mf", true, null)) view.nodes = Node.Import3MF(path, out script);
      else view.nodes = new List<Node> { new Node() };
      neuron = new Neuron(); NeuronEditor.InitNeuron(neuron, script ?? "using static csg3mf.CSG;\n");
      var e = new NeuronEditor { Dock = DockStyle.Fill, Tag = neuron };
      splitter.Panel1.Controls.Add(e);
      if (this.edit != null) this.edit.Dispose(); this.edit = e;
      Neuron.Debugger = (p, v) => this.edit.Show(v);
      foreach (var p in view.nodes) p.update();
      view.setcamera(); view.Invalidate();
      if (view.IsHandleCreated) view.OnCenter(null);
      this.path = path; UpdateTitle(); if (path != null) mru(path, path);
    }
    int OnNew(object test)
    {
      if (test == null) Open(null);
      return 1;
    }
    int OnOpen(object test)
    {
      if (test != null) return 1;
      var dlg = new OpenFileDialog() { Filter = "3MF files|*.3mf|All files|*.*" };
      if (path != null) dlg.InitialDirectory = Path.GetDirectoryName(path);
      if (dlg.ShowDialog(this) != DialogResult.OK) return 1;
      Open(dlg.FileName); return 1;
    }
    int OnSave(object test, bool saveas)
    {
      if (view.nodes.Count <= 1) return 0;
      if (test != null) return 1;
      var s = path;
      if (saveas || s == null)
      {
        var dlg = new SaveFileDialog() { Filter = "3MF files|*.3mf|All files|*.*", DefaultExt = "3mf" };
        if (s != null) { dlg.InitialDirectory = Path.GetDirectoryName(s); dlg.FileName = Path.GetFileName(s); }
        if (dlg.ShowDialog(this) != DialogResult.OK) return 1; s = dlg.FileName;
      }
      Cursor.Current = Cursors.WaitCursor;
      var bmp = View.preview(256, 256, view.camera, (view.flags & (1 << 10)) != 0, view.nodes.Skip(1).Where(p => p.vertexbuffer != null));
      bmp.RotateFlip(RotateFlipType.RotateNoneFlipX); //bmp.Save("C:\\Users\\cohle\\Desktop\\test.png", System.Drawing.Imaging.ImageFormat.Png); goto raus;
      Node.Export3MF(view.nodes, s, bmp, edit.EditText);
      if (path != s) { path = s; UpdateTitle(); mru(path, path); }
      edit.IsModified = false; Cursor.Current = Cursors.Default;
      return 1;
    }
    int OnLastFiles(object test)
    {
      if (test is string path) { mru(null, path); Open(path); return 1; }
      var item = test as MenuItem; foreach (var s in mru(null, null)) item.DropDownItems.Add(s).Tag = s;
      return 0;
    }
    bool AskSave()
    {
      if (!edit.IsModified) return true;
      switch (MessageBox.Show(this, String.Format(path == null ? "Save changings?" : "Save changings in {0}?", path), Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation))
      {
        case DialogResult.No: return true;
        case DialogResult.Yes: OnCommand(1020, null); return !edit.IsModified;
      }
      return false;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (e.Cancel = !AskSave()) return;
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
        var mi = items[i] as MenuItem; if (mi == null || mi.id == 0) continue;
        var hr = CommandRoot(mi.id, mi); mi.Enabled = (hr & 1) != 0; mi.Checked = (hr & 2) != 0;
        if (!mi.HasDropDownItems) continue;
        mi.Visible = false; //i++; while (items.Count > i && items[i].GetType() == typeof(MenuItem)) items.RemoveAt(i);
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

  class Neuron : ICustomTypeDescriptor
  {
    private object[] data;
    public override bool Equals(object p)
    {
      if (p is Archive) { Serialize((Archive)p); return true; }
      return base.Equals(p);
    }
    public override int GetHashCode()
    {
      return base.GetHashCode();
    }
    public virtual void Serialize(Archive ar)
    {
      if (ar.IsStoring) ar.WriteNeuron(data); else data = ar.ReadNeuron();
    }
    public virtual object Invoke(int id, object p)
    {
      switch (id)
      {
        case 0: return data; //IsDynamic
        case 1: data = p as object[]; break; //to overwrite notify 
        case 2: return ToString(); //to overwrite ScriptEditor Title
        case 3: Invoke("."); break; //to overwrite onstart
        case 4: Invoke("Dispose"); break; //to overwrite onstop
                                          //case 5: break; //onactivate
                                          //case 6: break; //ondeactivate
                                          //case 7: break; //addundo
      }
      return null;
    }
    public Delegate GetMethod(string name)
    {
      if (data == null) return null; var a = (object[])data[0];
      for (int i = a.Length - 1; ; i--)
      {
        var de = a[i] as Delegate; if (de != null) { if (de.Method.Name == name) return de; continue; }
        var dm = a[i] as DynamicMethod; if (dm == null) return null; if (dm.Name != name) continue;
        return GetDelegate(i);
      }
    }
    public void Invoke(string name)
    {
      var m = GetMethod(name) as Action<Neuron>; if (m != null) m(this);
    }
    public void Invoke<T1>(string name, T1 p1)
    {
      if (GetMethod(name) is Action<Neuron, T1> m) m(this, p1);
    }
    public void Invoke<T1, T2>(string name, T1 p1, T2 p2)
    {
      if (GetMethod(name) is Action<Neuron, T1, T2> m) m(this, p1, p2);
    }
    public TResult Invoke<T1, TResult>(TResult r, string name, T1 p1)
    {
      return GetMethod(name) is Func<Neuron, T1, TResult> m ? m(this, p1) : r;
    }
    public TResult Invoke<T1, T2, TResult>(TResult r, string name, T1 p1, T2 p2)
    {
      return GetMethod(name) is Func<Neuron, T1, T2, TResult> m ? m(this, p1, p2) : r;
    }
    #region ComponentModel support
    public Delegate GetProperty(string name, bool set = false)
    {
      if (data == null) return null; var a = (object[])data[0];
      for (int i = 2; i < a.Length && a[i] != null; i++)
      {
        var de = a[i] as Delegate; if (de != null && de.Method.Name == name && (de.Method.ReturnType == typeof(void)) == set) return de;
        var dm = a[i] as DynamicMethod; if (dm != null && dm.Name == name && (dm.ReturnType == typeof(void)) == set) return GetDelegate(i);
      }
      return null;
    }
    public T GetProperty<T>(string name)
    {
      var p = GetProperty(name) as Func<Neuron, T>; return p != null ? p(this) : default(T);
    }
    public void SetProperty<T>(string name, T v)
    {
      var p = GetProperty(name, true) as Action<Neuron, T>; if (p != null) p(this, v);
    }
    string name(int i)
    {
      var a = (object[])data[0];
      if (a[i] is Delegate de) return de.Method.Name;
      if (a[i] is DynamicMethod dm) return dm.Name;
      return null;
    }
    static void asign(PropertyDescriptor pd)
    {
      if (pd.Converter is TC) return;
      var bc = pd.Attributes[typeof(Converter)] as Converter; if (bc == null) return;
      var tc = new TC(bc); var t = typeof(PropertyDescriptor);
      t.GetField("converter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pd, tc);
      var ed = bc.GetEditor(null); if (ed == null) return;
      t.GetField("editors", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pd, new object[] { ed });
      t.GetField("editorCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pd, 1);
      t.GetField("editorTypes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pd, new Type[] { typeof(System.Drawing.Design.UITypeEditor) });
    }
    class PD<T> : PropertyDescriptor
    {
      Neuron p; int i; object box; TypeConverter tc;
      public PD(Neuron p, int i, string s) : base(s, null) { this.p = p; this.i = i; }
      public override object GetValue(object component)
      {
        var get = p.GetDelegate(i) as Func<Neuron, T>; var v = get(p);
        if (v == null ? box == null : v.Equals(box)) return box; return box = v;
      }
      public override void SetValue(object component, object value)
      {
        var set = (Action<Neuron, T>)p.GetDelegate(i - 1); set(p, (T)value);
        //var h = base.GetValueChangedHandler(component); if (h != null) h(component, EventArgs.Empty);
      }
      public override TypeConverter Converter
      {
        get { if (tc == null) { var c = Attributes[typeof(Converter)]; tc = c != null ? new TC((Converter)c) : base.Converter; } return tc; }
      }
      public override Type ComponentType
      {
        get { return p.GetType(); }
      }
      public override Type PropertyType
      {
        get { return typeof(T); }
      }
      public override bool IsReadOnly
      {
        get { return p.name(i - 1) != Name; }// !(p.data[i - 1] is Delegate) && !(p.data[i - 1] is MethodInfo); }
      }
      protected override Attribute[] AttributeArray
      {
        get
        {
          var aa = base.AttributeArray; if (aa.Length != 0 || p.name(i + 1) != string.Empty) return aa;
          var tt = p.GetDelegate(i + 1) as Func<Neuron, Attribute[]>;
          return base.AttributeArray = aa = tt(p);
        }
        set { base.AttributeArray = value; }
      }
      public override bool ShouldSerializeValue(object component)
      {
        return false;
      }
      public override bool CanResetValue(object component)
      {
        return false;
      }
      public override void ResetValue(object component)
      {
      }
      public override object GetEditor(Type editorBaseType)
      {
        var tc = Converter as TC; if (tc != null) return tc.conv.GetEditor(editorBaseType);
        return base.GetEditor(editorBaseType);
      }
      public Action undo(object value)
      {
        var n = p; var v = (T)value; var i = this.i;
        return () => { var t = ((Func<Neuron, T>)n.GetDelegate(i))(n); ((Action<Neuron, T>)n.GetDelegate(i - 1))(n, v); v = t; };
      }
    }
    class TC : TypeConverter
    {
      internal TC(Converter p) { conv = p; }
      internal Converter conv; object po, ps;
      public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
      {
        return sourceType == typeof(string) && conv.ConvertFromString != null;
      }
      public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
      {
        return destinationType == typeof(string) && conv.ConvertToString != null;
      }
      public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
      {
        return conv.StandardValues != null || conv.StandardValuesExclusive != null;
      }
      public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
      {
        return conv.StandardValuesExclusive != null;
      }
      public override bool GetPropertiesSupported(ITypeDescriptorContext context)
      {
        return conv.GetProperties != null;
      }
      public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
      {
        return conv.CreateInstance != null;
      }
      public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
      {
        return new TypeConverter.StandardValuesCollection((conv.StandardValuesExclusive ?? conv.StandardValues)(context) as System.Collections.ICollection);
      }
      public override object CreateInstance(ITypeDescriptorContext context, System.Collections.IDictionary propertyValues)
      {
        return conv.CreateInstance(context, propertyValues);
      }
      public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
      {
        var pp = TypeDescriptor.GetProperties(context.PropertyDescriptor.PropertyType, attributes);
        for (int i = 0; i < pp.Count; i++) asign(pp[i]); return pp;
      }
      public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
      {
        if (object.ReferenceEquals(po, value) && ps != null) return ps;
        return ps = conv.ConvertToString(context, po = value);
      }
      public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
      {
        return conv.ConvertFromString(context, (string)value);
      }
    }
    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
    {
      var a = TypeDescriptor.GetProperties(GetType(), attributes);
      for (int i = 0; i < a.Count; i++) asign(a[i]);
      if (data != null)
      {
        var b = ((ICustomTypeDescriptor)this).GetProperties();
        if (b != null) for (int i = 0; i < a.Count; i++) b.Add(a[i]); a = b;
      }
      return a;
    }
    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
    {
      if (data == null) return null;
      var a = (object[])data[0]; PropertyDescriptorCollection pp = null;
      for (int i = 2; i < a.Length && a[i] != null; i++)
      {
        var s = name(i); if (string.IsNullOrEmpty(s)) continue;
        var m = a[i] as MethodInfo ?? ((Delegate)a[i]).Method; if (m.ReturnType == typeof(void)) continue;
        (pp ?? (pp = new PropertyDescriptorCollection(null, false))).Add(
          (PropertyDescriptor)Activator.CreateInstance(typeof(PD<>).MakeGenericType(m.ReturnType), this, i, m.Name));
      }
      return pp;
    }
    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
    {
      return this;
    }
    string ICustomTypeDescriptor.GetClassName()
    {
      return TypeDescriptor.GetClassName(GetType());
    }
    AttributeCollection ICustomTypeDescriptor.GetAttributes()
    {
      return TypeDescriptor.GetAttributes(GetType());
    }
    string ICustomTypeDescriptor.GetComponentName()
    {
      return TypeDescriptor.GetComponentName(GetType());
    }
    TypeConverter ICustomTypeDescriptor.GetConverter()
    {
      return TypeDescriptor.GetConverter(GetType());
    }
    EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
    {
      return TypeDescriptor.GetDefaultEvent(GetType());
    }
    PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
    {
      return new PD<string>(this, 0, "Name");
      //return TypeDescriptor.GetDefaultProperty(GetType());
    }
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
    {
      return TypeDescriptor.GetEvents(GetType(), attributes);
    }
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
    {
      return TypeDescriptor.GetEvents(GetType());
    }
    object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
    {
      var p = TypeDescriptor.GetEditor(GetType(), editorBaseType); if (p != null) return p;
      if (editorBaseType == typeof(ComponentEditor) && Debugger != null) return new CE();
      return null;
    }
    class CE : ComponentEditor
    {
      public override bool EditComponent(ITypeDescriptorContext context, object component) { Debugger((Neuron)component, null); return false; }
    }
    #endregion
    #region Debug support
    public static Action<Neuron, Exception> Debugger; //todo: make private, possible with new exception handling
    public static Action<CancelEventArgs> Finalize;
    [ThreadStatic]
    static internal int state, dbgpos;
    [ThreadStatic]
    static unsafe int* sp;
    unsafe void dbgstp(int i)
    {
      var t = ((int[])data[1])[(dbgpos = i) >> 5];
      if (t == 0 || (t & (1 << (i & 31))) == 0)
      {
        if (state == 0) return;
        if (state == 1 && sp > &i + 1) return;
        if (state == 3 && sp >= &i) return;
      }
      if (Debugger == null || state == 7) return;
      sp = &i; state = 7; Debugger(this, null);
    }
    [ThreadStatic]
    static internal unsafe int* stack;
    unsafe static void dbgstk(int id, int* pi, void* pv)
    {
      if (pv == null)
      {
        var p = stack; if (p == null) return; // >
        for (; ; ) { var v = p[0]; p = p[1] != 0 ? p + p[1] : null; if ((v & 0xffff) == id) { stack = p; return; } }
      }
      if (pi[0] != 0) return;
      if (id == 0 && stack != null && stack <= pi) stack = null; //after exceptions
      pi[0] = id | ((int)(((int*)pv) - pi) << 16);
      pi[1] = stack != null ? (int)(stack - pi) : 0; stack = pi;
    }
    internal static object get(object p, string s) { return p.GetType().InvokeMember(s, BindingFlags.GetProperty, null, p, null, null, null, null); }
    internal static void put(object p, string s, object v) { p.GetType().InvokeMember(s, BindingFlags.SetProperty, null, p, new object[] { v }, null, null, null); }
    internal static object call(object p, string s, params object[] a)
    {
      return p.GetType().InvokeMember(s, BindingFlags.InvokeMethod, null, p, a, null);
    }
    unsafe Delegate GetDelegate(int i)
    {
      var a = (object[])data[0];
      var de = a[i] as Delegate; if (de != null) return de;
      var dm = (DynamicMethod)a[i];
      var list = Archive.TypeHelper.GetTokens(dm.GetDynamicILInfo());
      if (list[0] != null) return (Delegate)(a[i] = list[0]);
      var tt = Archive.TypeHelper.GetParams(dm); Type type;
      if (dm.ReturnType != typeof(void)) { Array.Resize(ref tt, tt.Length + 1); tt[tt.Length - 1] = dm.ReturnType; type = Expression.GetFuncType(tt); }
      else type = Expression.GetActionType(tt);
      list[0] = a[i] = de = dm.CreateDelegate(type); return de;
    }
    public unsafe static void DebugException(object unk, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
      if (state == 7) return;
      for (var p = (int*)stack; p != null; p = p[1] != 0 ? p + p[1] : null)
      {
        var id = p[0] & 0xffff; if (id != 0) continue;
        TypedReference tr;
        ((IntPtr*)&tr)[0] = (IntPtr)(p + (p[0] >> 16));
        ((IntPtr*)&tr)[1] = typeof(object).TypeHandle.Value;
        var x = TypedReference.ToObject(tr); while (x is object[]) x = ((object[])x)[0];
        Debugger((Neuron)x, e.Exception); break;
      }
    }
    #endregion
  }

  class Converter : Attribute
  {
    public Converter() { }
    public Converter(Converter p)
    {
      ConvertFromString = p.ConvertFromString;
      ConvertToString = p.ConvertToString;
      StandardValues = p.StandardValues;
      StandardValuesExclusive = p.StandardValuesExclusive;
      EditValue = p.EditValue;
      GetProperties = p.GetProperties;
      CreateInstance = p.CreateInstance;
      PaintValue = p.PaintValue;
    }
    public Func<ITypeDescriptorContext, string, object> ConvertFromString;
    public Func<ITypeDescriptorContext, object, string> ConvertToString;
    public Func<ITypeDescriptorContext, object> StandardValues;
    public Func<ITypeDescriptorContext, object> StandardValuesExclusive;
    public Func<ITypeDescriptorContext, object, object> EditValue;
    public Func<ITypeDescriptorContext, object> GetProperties;
    public Func<ITypeDescriptorContext, System.Collections.IDictionary, object> CreateInstance;
    public Action<Graphics, Rectangle, object> PaintValue;
    internal object GetEditor(Type editorBaseType) { return editor ?? (editor = new TE { converter = this }); }
    object editor;
    class TE : UITypeEditor
    {
      internal Converter converter;
      public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
      {
        return converter.EditValue != null ? UITypeEditorEditStyle.Modal : UITypeEditorEditStyle.None;
      }
      public override bool GetPaintValueSupported(ITypeDescriptorContext context)
      {
        return converter.PaintValue != null;
      }
      public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
      {
        return converter.EditValue(context, value);
      }
      public override void PaintValue(PaintValueEventArgs e)
      {
        converter.PaintValue(e.Graphics, e.Bounds, e.Value);
      }
    }
  }

  sealed unsafe class Archive
  {
    #region Secure Serialize
    public void Serialize<T>(ref T v)
    {
      if (IsStoring) Writer<T>.Write(this, v); else v = Reader<T>.Read(this);
    }
    private static class Writer<T>
    {
      public static readonly Action<Archive, T> Write;
      static Writer()
      {
        var t = typeof(T); //Debug.WriteLine("Writer() " + t);
        if (t.IsValueType && (t.IsPrimitive || t.IsEnum || t.GetMethod("Serialize") == null)) { Write = wp(TypeHelper.SizeOf(t)); return; }
        var pa = Expression.Parameter(typeof(Archive)); var pb = Expression.Parameter(t); Expression e;
        if (t.IsArray) e = Expression.Call(pa, typeof(Archive).GetMethod("wa", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(t = t.GetElementType()), pb, Expression.Constant(TypeHelper.IsBlittable(t) && t.GetMethod("Serialize") == null ? TypeHelper.SizeOf(t) : 0));
        else if (t == typeof(string)) e = Expression.Call(pa, typeof(Archive).GetMethod("ws", BindingFlags.Instance | BindingFlags.NonPublic), pb);
        else if (t == typeof(object)) e = Expression.Call(pa, typeof(Archive).GetMethod("wo", BindingFlags.Instance | BindingFlags.NonPublic), pb);
        else
        {
          var mi = t.GetMethod("Serialize");
          if (mi != null)
          {
            if (t.IsValueType) e = Expression.Call(pb, mi, pa);
            else e = Expression.IfThen(Expression.Call(pa, typeof(Archive).GetMethod("wc", BindingFlags.Instance | BindingFlags.NonPublic), pb), Expression.Call(pb, mi, pa));
          }
          //else if (blittable(t)) e = Expression.Call(pa, typeof(Archive).GetMethod("wp", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(t), pb, Expression.Constant(_sizeof(t)));
          else throw new Exception("todo");
        }
        Write = Expression.Lambda<Action<Archive, T>>(e, pa, pb).Compile();
      }
      static Action<Archive, object> po;
      static Action<Archive, object> wo() { return po ?? (po = (ar, p) => Write(ar, (T)p)); }
      static Action<Archive, T> wp(int c) { return (ar, v) => { var r = __makeref(v); ar.Write(*((void**)&r), c); }; }
    }
    private static class Reader<T>
    {
      public static readonly Func<Archive, T> Read;
      static Reader()
      {
        var t = typeof(T); //Debug.WriteLine("Reader() " + t);
        if (t.IsValueType && (t.IsPrimitive || t.IsEnum || t.GetMethod("Serialize") == null)) { Read = rp(TypeHelper.SizeOf(t)); return; }
        var pa = Expression.Parameter(typeof(Archive)); Expression e;
        if (t.IsArray) e = Expression.Call(pa, typeof(Archive).GetMethod("ra", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(t = t.GetElementType()), Expression.Constant(TypeHelper.IsBlittable(t) && t.GetMethod("Serialize") == null ? TypeHelper.SizeOf(t) : 0));
        else if (t == typeof(string)) e = Expression.Call(pa, typeof(Archive).GetMethod("rs", BindingFlags.Instance | BindingFlags.NonPublic));
        else if (t == typeof(object)) e = Expression.Call(pa, typeof(Archive).GetMethod("ro", BindingFlags.Instance | BindingFlags.NonPublic));
        else
        {
          var mi = t.GetMethod("Serialize");
          if (mi != null)
          {
            var v = Expression.Variable(t);
            if (t.IsValueType) e = Expression.Block(new ParameterExpression[] { v }, Expression.Assign(v, Expression.New(t)), Expression.Call(v, mi, pa), v);
            else
            {
              var s = Expression.Variable(typeof(bool));
              e = Expression.Convert(Expression.Call(pa, typeof(Archive).GetMethod("rc", BindingFlags.Instance | BindingFlags.NonPublic), s), t);
              e = Expression.Block(new ParameterExpression[] { v, s }, Expression.Assign(v, e), Expression.IfThen(s, Expression.Call(v, mi, pa)), v);
            }
          }
          //else if (blittable(t)) e = Expression.Call(pa, typeof(Archive).GetMethod("rp", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(t), Expression.Constant(_sizeof(t)));
          else throw new Exception("todo");
        }
        Read = Expression.Lambda<Func<Archive, T>>(e, pa).Compile();
      }
      static Func<Archive, object> po;
      static Func<Archive, object> ro() { return po ?? (po = ar => Read(ar)); }
      static Func<Archive, T> rp(int c) { return (ar) => { var v = default(T); var r = __makeref(v); ar.Read(*((void**)&r), c); return v; }; }
    }
    private void wa<T>(T[] a, int c)
    {
      if (a == null) { WriteCount(0); return; }
      var n = a.Length; WriteCount(n + 1);
      if (c == 0) for (int i = 0; i < n; i++) Writer<T>.Write(this, a[i]);
      else { var h = GCHandle.Alloc(a, GCHandleType.Pinned); Serialize(h.AddrOfPinnedObject().ToPointer(), a.Length * c); h.Free(); }
    }
    private T[] ra<T>(int c)
    {
      var n = ReadCount() - 1; if (n < 0) return null;
      var a = new T[n]; if (c == 0) for (int i = 0; i < n; i++) a[i] = Reader<T>.Read(this);
      else { var h = GCHandle.Alloc(a, GCHandleType.Pinned); Serialize(h.AddrOfPinnedObject().ToPointer(), a.Length * c); h.Free(); }
      return a;
    }
    private void ws(string s)
    {
      WriteString(s, true);
      //if (s == null) { WriteByte(0); return; }
      //var n = s.Length; WriteCount(n + 1); for (int i = 0; i < n; i++) WriteCount(s[i]);
    }
    private string rs()
    {
      return ReadString(true);
      //var n = ReadCount() - 1; if (n == -1) return null;
      //var p = stackalloc char[n]; for (int i = 0; i < n; i++) p[i] = (char)ReadCount();
      //return new string(p, 0, n);  
    }
    private bool wc(object p)
    {
      if (p == null) { WriteByte(0); return false; }
      var x = index(p); WriteCount(x + 2); if (x >= 0) return false;
      var t = p.GetType(); WriteType(t); register(7, p); return true;
    }
    private object rc(out bool s)
    {
      var x = ReadCount(); s = false; if (x == 0) return null; if (x >= 2) return access(7, x - 2);
      var t = ReadType(); var p = Activator.CreateInstance(t, true); register(7, p); s = true; return p;
    }
    private void wo(object p)
    {
      var t = p != null ? p.GetType() : typeof(string); WriteType(t);
      ((Action<Archive, object>)typeof(Writer<>).MakeGenericType(t).
        GetMethod("wo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null))(this, p);
    }
    private object ro()
    {
      return ((Func<Archive, object>)typeof(Reader<>).MakeGenericType(ReadType()).
        GetMethod("ro", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null))(this);
    }
    #endregion

    byte[] p; int i; int[] tmap; Dictionary<object, int> dictw; Dictionary<int, object> dictr; List<object> blobs;
    public bool IsStoring;
    //public int Version;
    public Archive(byte[] a = null)
    {
      p = a ?? new byte[4096]; IsStoring = a == null; tmap = new int[9]; blobs = new List<object>();
      if (IsStoring) dictw = new Dictionary<object, int>(); else dictr = new Dictionary<int, object>();
    }
    internal void Reset(bool write)
    {
      //p = ar.p; blobs = ar.blobs; tmap = ar.tmap; dictw = ar.dictw; dictr = ar.dictr;
      IsStoring = write; i = 0; blobs.Clear(); for (int t = 0; t < tmap.Length; t++) tmap[t] = 0;
      if (dictw != null) dictw.Clear(); else if (IsStoring) dictw = new Dictionary<object, int>();
      if (dictr != null) dictr.Clear(); else if (!IsStoring) dictr = new Dictionary<int, object>();
    }

    public byte[] ToArray()
    {
      if (IsStoring) Array.Resize(ref p, i); return p;
    }
    public void WriteCount(int c)
    {
      ensure(5); for (; c >= 0x80; c >>= 7) p[i++] = (byte)(c | 0x80); p[i++] = (byte)c;
    }
    public int ReadCount()
    {
      int c = 0; for (int shift = 0; ; shift += 7) { var b = p[i++]; c |= (b & 0x7F) << shift; if ((b & 0x80) == 0) break; }
      return c;
    }
    public void Serialize(void* v, int n)
    {
      if (IsStoring) Write(v, n); else Read(v, n);
    }
    public void WriteString(string s, bool cach = false)
    {
      if (s == null) { WriteByte(0); return; }
      var n = s.Length; if (n == 0) { WriteByte(1); return; }
      if (cach) { var x = index(s); WriteCount(x + 3); if (x >= 0) return; register(0, s); }
      WriteCount(n + 2); for (int i = 0; i < n; i++) WriteCount(s[i]);
    }
    public string ReadString(bool cach = false)
    {
      var x = ReadCount(); if (x == 0) return null; if (x == 1) return string.Empty;
      if (cach) { if (x >= 3) return (string)access(0, x - 3); x = ReadCount(); }
      var n = x - 2; var p = stackalloc char[n]; for (int i = 0; i < n; i++) p[i] = (char)ReadCount();
      var s = new string(p, 0, n); if (cach) register(0, s); return s;
    }
    public void WriteObject(object p)
    {
      if (p == null) { WriteByte(0); return; }
      var x = index(p); WriteCount(x + 2); if (x >= 0) return;
      var t = p.GetType(); WriteType(t); register(7, p); p.Equals(this);
    }
    public object ReadObject()
    {
      var x = ReadCount(); if (x == 0) return null; if (x >= 2) return access(7, x - 2);
      var t = ReadType(); var p = Activator.CreateInstance(t); register(7, p); p.Equals(this); return p;
    }

    internal void WriteByte(byte v)
    {
      ensure(1); p[i++] = v;
    }
    internal byte ReadByte()
    {
      return p[i++];
    }

    public void WriteCom(object p)
    {
      throw new NotImplementedException();
      //if (p == null) { WriteByte(0); return; }
      //var x = index(p); WriteCount(x + 2); if (x >= 0) return;
      //codx.Factory.Write(this, p); register(7, p);
    }
    public object ReadCom()
    {
      throw new NotImplementedException();
      //var x = ReadCount(); if (x == 0) return null;
      //if (x >= 2) return codx.Factory.GetInstance(access(7, x - 2)); //todo: check, bad trick to increment managed reference, is there a better way?
      //var p = codx.Factory.Read(this); register(7, p); return p;
    }
    public void Serialize<T>(ref T p, bool cache) where T : class
    {
      if (typeof(T).IsImport) { if (IsStoring) WriteCom(p); else p = (T)ReadCom(); return; }
      if (IsStoring) WriteObject(p); else p = (T)ReadObject();
    }
    public void Serialize(ref string s, bool cache)
    {
      if (IsStoring) WriteString(s, cache); else s = ReadString(cache);
    }

    public void Write<T>(T v)
    {
      Writer<T>.Write(this, v);
    }
    public T Read<T>()
    {
      return Reader<T>.Read(this);
    }
    void Write(void* v, int n)
    {
      ensure(n); if (n <= 8) { for (int t = 0; t < n; t++) p[i++] = ((byte*)v)[t]; return; }
      Marshal.Copy((IntPtr)v, p, i, n); i += n; //fixed (byte* t = p) Native.memcpy(t + i, v, (IntPtr)n); i += n;
    }
    void Read(void* v, int n)
    {
      if (n <= 8) { for (int t = 0; t < n; t++) ((byte*)v)[t] = p[i++]; return; }
      Marshal.Copy(p, i, (IntPtr)v, n); i += n; //fixed (byte* t = p) Native.memcpy(v, t + i, (IntPtr)n); i += n;
    }

    static class __sizeof<T> { internal static readonly int n = Marshal.SizeOf(typeof(T)); } //todo: remove
    void ensure(int n)
    {
      if (i + n > p.Length) Array.Resize(ref p, Math.Max(i + n, p.Length << 1));
    }
    void register(int t, object p)
    {
      var x = tmap[t]++; if (IsStoring) dictw.Add(p, x); else dictr.Add(x | (t << 27), p);
    }
    int index(object p)
    {
      int x; if (dictw.TryGetValue(p, out x)) return x; return -1;
    }
    object access(int t, int x)
    {
      return dictr[x | (t << 27)];
    }

    internal static Assembly[] Assemblys
    {
      get { return assemblys ?? (assemblys = AppDomain.CurrentDomain.GetAssemblies()/*.Where(p => !p.IsDynamic).ToArray()*/); }
      set { assemblys = value; }
    }

    internal void WriteNeuron(object[] a)
    {
      if (a == null) { WriteByte(0); return; }
      WriteCount(a.Length + 1);
      var s = (object[])a[0]; var x = index(s); WriteCount(x + 1);
      if (x < 0)
      {
        register(8, s); WriteCount(s.Length); writeblob(s[0] as byte[]);
        if (s.Length > 1)
        {
          writeblob(s[1] as byte[]);
          var g = Array.IndexOf(s, null, 2); WriteCount(s.Length - g);
          for (int i = 2; i < s.Length; i++)
          {
            if (i == g) continue; var p = s[i];
            WriteDm(p is Delegate ? TypeHelper.GetOwner(((Delegate)p).Method) : (DynamicMethod)p);
          }
        }
      }
      if (s.Length < 2) return; var ff = (byte[])s[1];
      var k = 1; if ((ff[0] & 1) != 0) writeblob(a[k++] as int[]);
      for (int i = 0, n = ff[1]; i < n; i++) { var p = (Array)a[k++]; WriteCount(p.Length); WriteType(p.GetType().GetElementType()); }
    }
    internal object[] ReadNeuron()
    {
      var n = ReadCount() - 1; if (n < 0) return null;
      var a = new object[n]; var x = ReadCount();
      if (x != 0) a[0] = access(8, x - 1);
      else
      {
        var s = new object[ReadCount()]; s[0] = readblob<byte>();
        if (s.Length > 1)
        {
          s[1] = readblob<byte>(); var g = s.Length - ReadCount();
          for (int i = 2; i < s.Length; i++) if (i != g) s[i] = ReadDm();
        }
        s = CacheClass(s); register(8, s); a[0] = s;
      }
      var ss = (object[])a[0]; if (ss.Length < 2) return a; var ff = (byte[])ss[1];
      int k = 1; if ((ff[0] & 1) != 0) a[k++] = readblob<int>();
      for (int i = 0, c = ff[1]; i < c; i++) { var l = ReadCount(); var t = ReadType(); a[k++] = Array.CreateInstance(t, l); }
      return a;
    }

    static internal object[] CacheClass(object[] b)
    {
      for (int j = cache.Count - 1, i; j >= 0; j--)
      {
        var a = cache[j].Target as object[]; if (a == null) continue;
        if (a.Length != b.Length) continue;
        var sa = a[0] as byte[]; var sb = b[0] as byte[];
        if (sa != null && sb != null && !equals(sa, sb)) continue;
        if (sa != null ^ sb != null) continue;
        for (i = 2; i < a.Length; i++)
        {
          if (a[i] == b[i]) continue;
          var dm = b[i] as DynamicMethod; if (dm == null) break;
          var de = a[i] as Delegate; if (de == null) break;
          if (TypeHelper.GetOwner(de.Method) != dm) break;
        }
        if (i == a.Length) return a;
      }
      cache.Add(new WeakReference(b)); return b;
    }

    static bool equals<T>(T[] a, T[] b)
    {
      if (a.Length != b.Length) return false;
      var h1 = GCHandle.Alloc(a, GCHandleType.Pinned);
      var h2 = GCHandle.Alloc(b, GCHandleType.Pinned);
      var c = Native.memcmp(h1.AddrOfPinnedObject().ToPointer(), h2.AddrOfPinnedObject().ToPointer(), (void*)(a.Length * __sizeof<T>.n));
      h1.Free(); h2.Free(); return c == 0;
    }
    void serial<T>(T[] a)
    {
      if (a.Length == 0) return;
      var h = GCHandle.Alloc(a, GCHandleType.Pinned);
      Serialize(h.AddrOfPinnedObject().ToPointer(), a.Length * __sizeof<T>.n); h.Free();
    }
    void writeblob<T>(T[] a)
    {
      if (a == null) { WriteByte(0); return; }
      for (int i = 0; i < blobs.Count; i++)
      {
        var b = blobs[i] as T[]; if (b == null || b.Length != a.Length) continue;
        if (!equals(a, b)) continue;
        WriteCount(1); WriteCount(i); return;
      }
      WriteCount(2 + a.Length); serial(a); blobs.Add(a);
    }
    T[] readblob<T>() where T : struct
    {
      var t = ReadCount(); if (t == 0) return null;
      if (t == 1) return (T[])blobs[ReadCount()];
      var a = new T[t - 2]; serial(a); blobs.Add(a); return a;
    }

    internal void Header<T>(ref T[] a)
    {
      if (IsStoring) WriteCount(a != null ? a.Length + 1 : 0);
      else { var c = ReadCount() - 1; a = c >= 0 ? new T[c] : null; }
    }

    void WriteByte(int c)
    {
      ensure(1); p[i++] = checked((byte)c);
    }
    void WriteBytes(byte[] a)
    {
      if (a == null) { WriteByte(0); return; }
      var n = a.Length; WriteCount(n + 1); fixed (byte* p = a) Write(p, n);
    }
    //byte[] ReadBytes()
    //{
    //  var n = ReadCount() - 1; if (n < 0) return null;
    //  var a = new byte[n]; fixed (byte* p = a) Read(p, n); return a;
    //}

    internal static List<WeakReference> cache = new List<WeakReference>(); //dynamics only

    static bool Equals(DynamicMethod a, DynamicMethod b, int recu = 0)
    {
      if (a.Name != b.Name) return false;
      if (a.ReturnType != b.ReturnType) return false;
      if (a.InitLocals != b.InitLocals) return false;
      var da = a.GetDynamicILInfo(); var db = b.GetDynamicILInfo();
      if (!Equals(TypeHelper.GetCode(da), TypeHelper.GetCode(db))) return false;
      if (!Equals(TypeHelper.GetExceptions(da), TypeHelper.GetExceptions(db))) return false;
      if (!Equals(TypeHelper.GetLocalSignature(da), TypeHelper.GetLocalSignature(db))) return false;
      //if (flat) return true;

      var ta = TypeHelper.GetTokens(da); var tb = TypeHelper.GetTokens(db); if (ta.Count != tb.Count) return false;
      for (int i = 2; i < ta.Count; i++)
      {
        var pa = ta[i]; var pb = tb[i]; if (pa.GetType() != pb.GetType()) return false;
        if (pa.Equals(pb)) continue; //if (pa is Type[]) continue;
        if (pa is string) return false;
        if (pa is byte[]) { if (!Equals((byte[])pa, (byte[])pb)) return false; continue; }
        if (TypeHelper.IsGenericFieldInfo(pa))
        {
          if (!TypeHelper.GetFieldHandle(pa).Equals(TypeHelper.GetFieldHandle(pb))) return false;
          if (!TypeHelper.GetContext(pa).Equals(TypeHelper.GetContext(pb))) return false;
          continue;
        }
        if (TypeHelper.IsGenericMethodInfo(pa))
        {
          if (!TypeHelper.GetMethodHandle(pa).Equals(TypeHelper.GetMethodHandle(pb))) return false;
          if (!TypeHelper.GetContext(pa).Equals(TypeHelper.GetContext(pb))) return false;
          continue;
        }
        if (pa is DynamicMethod)
        {
          if ((DynamicMethod)pa == a) { if ((DynamicMethod)pb == b) continue; return false; }
          if ((DynamicMethod)pb == b) { if ((DynamicMethod)pa == a) continue; return false; }
          if (recu > 10) continue; //todo: remove
          if (Equals((DynamicMethod)pa, (DynamicMethod)pb, recu + 1)) continue;
        }
        return false;
      }
      var sa = TypeHelper.GetParams(a); var sb = TypeHelper.GetParams(b); if (sa.Length != sb.Length) return false;
      for (int i = 0; i < sa.Length; i++) if (sa[i] != sb[i]) return false;
      return true;
    }
    void WriteDm(DynamicMethod dm)
    {
      var x = index(dm); WriteCount(x + 1); if (x >= 0) return;
      WriteByte(dm.InitLocals ? 1 : 0); WriteString(dm.Name, true); WriteType(dm.ReturnType); WriteTypes(TypeHelper.GetParametersNoCopy(dm)); register(1, dm);
      var il = dm.GetDynamicILInfo();
      WriteBytes(TypeHelper.GetCode(il));
      WriteCount(TypeHelper.GetMaxStackSize(il));
      WriteBytes(TypeHelper.GetExceptions(il));
      var tokens = TypeHelper.GetTokens(il);
      WriteCount(tokens.Count);
      for (int i = 2; i < tokens.Count; i++)
      {
        var p = tokens[i];
        if (p is string) { WriteByte(1); WriteString((string)p, true); continue; }
        if (p is RuntimeTypeHandle) { WriteByte(2); WriteType(Type.GetTypeFromHandle((RuntimeTypeHandle)p)); continue; }
        if (p is DynamicMethod) { WriteByte(3); WriteDm((DynamicMethod)p); continue; }
        if (p is RuntimeMethodHandle)
        {
          var mb = MethodBase.GetMethodFromHandle((RuntimeMethodHandle)p);
          if (mb is MethodInfo) { WriteByte(4); WriteMethod((MethodInfo)mb); continue; }
          if (mb is ConstructorInfo) { WriteByte(5); WriteCtor((ConstructorInfo)mb); continue; }
        }
        if (p is RuntimeFieldHandle) { WriteByte(6); WriteField(FieldInfo.GetFieldFromHandle((RuntimeFieldHandle)p)); continue; }
        if (p is byte[])
        {
          //if (((byte[])p)[0] == 1) { WriteByte(8); writesig((byte[])p); continue; } //StdCall
          System.Diagnostics.Debug.Assert(((byte[])p)[0] == 2);
          WriteByte(7); writesig((byte[])p); continue; //Cdecl
        }//WriteBytes((byte[])p); continue; }
        if (TypeHelper.IsGenericFieldInfo(p))
        {
          WriteByte(6); WriteField(FieldInfo.GetFieldFromHandle(TypeHelper.GetFieldHandle(p), TypeHelper.GetContext(p))); continue;
        }
        if (TypeHelper.IsGenericMethodInfo(p))
        {
          var mb = MethodInfo.GetMethodFromHandle(TypeHelper.GetMethodHandle(p), TypeHelper.GetContext(p));
          if (mb is MethodInfo) { WriteByte(4); WriteMethod((MethodInfo)mb); continue; }
          if (mb is ConstructorInfo) { WriteByte(5); WriteCtor((ConstructorInfo)mb); continue; }
        }
        throw new Exception();
      }
      writesig(TypeHelper.GetLocalSignature(il));
    }
    DynamicMethod ReadDm()
    {
      var x = ReadCount() - 1; if (x >= 0) return (DynamicMethod)access(1, x);
      var fm = ReadByte(); var dm = new DynamicMethod(ReadString(true), ReadType(), ReadTypes(), typeof(object), true); //typeof(Neuron)); 
      var xm = tmap[1]; register(1, dm); dm.InitLocals = (fm & 1) != 0;
      var il = dm.GetDynamicILInfo(); //il.SetCode(ReadBytes(), ReadCount()); il.SetExceptions(ReadBytes());
      var i3 = ReadCount() - 1; var i4 = this.i; this.i += i3; var i5 = ReadCount();
      var i6 = ReadCount() - 1; var i7 = this.i; this.i += i6;
      fixed (byte* p = this.p) { il.SetCode(p + i4, i3, i5); il.SetExceptions(p + i7, i6); }
      var tokens = Archive.TypeHelper.GetTokens(il);
      var nt = ReadCount(); tokens.Capacity = nt;
      for (int i = 2; i < nt; i++)
        switch (ReadByte())
        {
          case 1: il.GetTokenFor(ReadString(true)); break;
          case 2: il.GetTokenFor(ReadType().TypeHandle); break;
          case 3: il.GetTokenFor(ReadDm()); break;
          case 4: { var m = ReadMethod(); var t = m.DeclaringType; if (t.IsGenericType) il.GetTokenFor(m.MethodHandle, t.TypeHandle); else il.GetTokenFor(m.MethodHandle); } break;
          case 5: { var m = ReadCtor(); var t = m.DeclaringType; if (t.IsGenericType) il.GetTokenFor(m.MethodHandle, t.TypeHandle); else il.GetTokenFor(m.MethodHandle); } break;
          case 6: { var f = ReadField(); var t = f.DeclaringType; if (t.IsGenericType) il.GetTokenFor(f.FieldHandle, t.TypeHandle); else il.GetTokenFor(f.FieldHandle); } break;
          case 7: readsig(il, 2); break; //Cdecl
                                         //case 8: readsig(il, 1); break; //StdCall
        }
      readsig(il, 7); //LOCAL_SIG
      for (int i = cache.Count - 1; i >= 0; i--)
      {
        var t1 = cache[i].Target; if (t1 == null) { cache.RemoveAt(i); continue; }
        var t2 = t1 as DynamicMethod;
        if (t2 != null && Equals(t2, dm)) { dictr[xm | (1 << 27)] = t2; return t2; }
      }
      cache.Add(new WeakReference(dm)); return dm;
    }

    void writesig(byte[] sig)
    {
      fixed (byte* p = sig)
      {
        int ha = (sig[1] & 0x80) != 0 ? 3 : 2;
        int nt = 0; for (int i = ha; i < sig.Length;) if (p[i++] == 0x21) { nt++; i += IntPtr.Size; }//ELEMENT_TYPE_INTERNAL
        WriteCount(sig.Length - nt * IntPtr.Size); WriteCount(nt); int ab = 1;
        for (int i = ab; i < sig.Length;)
        {
          var c = sig[i++]; if (c != 0x21 || i <= ha) continue;
          Write(p + ab, i - ab); var h = *(IntPtr*)(p + i); i += IntPtr.Size; ab = i;
          WriteType(TypeHelper.GetType(h));
        }
        Write(p + ab, sig.Length - ab);
      }
    }
    void readsig(DynamicILInfo il, byte v)
    {
      var nb = ReadCount() + ReadCount() * IntPtr.Size;
      var bb = stackalloc byte[nb]; bb[0] = v; bb[1] = ReadByte();
      int x = 2; if ((bb[1] & 0x80) != 0) bb[x++] = ReadByte();
      for (; x < nb;)
      {
        var c = ReadByte(); bb[x++] = c; if (c != 0x21) continue;
        var t = ReadType(); *(IntPtr*)(bb + x) = t.TypeHandle.Value; x += IntPtr.Size;
      }
      if (v == 7) { il.SetLocalSignature(bb, nb); return; }
      var cc = new byte[nb]; Marshal.Copy((IntPtr)bb, cc, 0, nb); il.GetTokenFor(cc);
    }

    static Assembly[] assemblys;
    void WriteAssembly(Assembly a)
    {
      var x = index(a); WriteCount(x + 1); if (x >= 0) return;
      var s = a.FullName; WriteString(s.Substring(0, s.IndexOf(','))); register(2, a);
    }
    Assembly ReadAssembly()
    {
      var x = ReadCount() - 1; if (x >= 0) return (Assembly)access(2, x);
      var s = ReadString(); var aa = Assemblys;
      for (int i = 0; i < aa.Length; i++)
      {
        var a = aa[i]; var ss = a.FullName; if (!ss.StartsWith(s) || ss[s.Length] != ',') continue;
        register(2, a); return a;
      }
      for (int t = 0; t < 2; t++)
      {
        var ss = (t == 0 ? Assembly.GetEntryAssembly() : Assembly.GetExecutingAssembly()).GetReferencedAssemblies();
        for (int i = 0; i < ss.Length; i++)
        {
          if (ss[i].Name != s) continue;
          var a = Assembly.Load(ss[i]); Assemblys = null; register(2, a); return a;
        }
      }
      //var ra = Assembly.GetEntryAssembly().GetReferencedAssemblies();
      //for (int i = 0; i < ra.Length; i++)
      //{
      //  var b = ra[i]; var ss = b.FullName; if (!ss.StartsWith(s) || ss[s.Length] != ',') continue;
      //  var a = Assembly.Load(b); Assemblys = null; register(2, a); return a;
      //}
      throw new Exception();
    }

    void WriteType(Type t)
    {
      var x = index(t); WriteCount(x + 1); if (x >= 0) return;
      if (t.IsArray) { WriteByte(2); WriteType(t.GetElementType()); return; }
      if (t.IsPointer) { WriteByte(3); WriteType(t.GetElementType()); return; }
      if (t.IsByRef) { WriteByte(4); WriteType(t.GetElementType()); return; }
      if (t.IsGenericType && !t.IsGenericTypeDefinition) { WriteByte(5); WriteType(t.GetGenericTypeDefinition()); WriteTypes(t.GetGenericArguments()); return; }
      if (t.IsNested) { WriteByte(6); WriteType(t.DeclaringType); WriteString(t.Name); register(3, t); return; }
      WriteByte(1); WriteAssembly(t.Assembly); WriteString(t.Namespace, true); WriteString(t.Name); register(3, t);
    }
    Type ReadType()
    {
      var x = ReadCount() - 1; if (x >= 0) return (Type)access(3, x);
      var f = ReadCount();
      if (f == 1) { var t = ReadAssembly().GetType(ReadTypeName(), true); register(3, t); return t; }
      if (f == 2) { var t = ReadType().MakeArrayType(); return t; }
      if (f == 3) { var t = ReadType().MakePointerType(); return t; }
      if (f == 4) { var t = ReadType().MakeByRefType(); return t; }
      if (f == 5) { var t = ReadType().MakeGenericType(ReadTypes()); return t; }
      if (f == 6) { var t = ReadType().GetNestedType(ReadString(), BindingFlags.Public | BindingFlags.NonPublic); register(3, t); return t; }
      return null;
    }

    //public Func<string, string> TypeResolver;
    string ReadTypeName()
    {
      //var s = ReadString(true) + "." + ReadString();
      //return TypeResolver != null ? TypeResolver(s) : s;
      return string.Format("{0}.{1}", ReadString(true), ReadString());
    }
    void WriteTypes(ParameterInfo[] a)
    {
      WriteCount(a.Length); for (int i = 0; i < a.Length; i++) WriteType(a[i].ParameterType);
    }
    void WriteTypes(Type[] a)
    {
      WriteCount(a.Length); for (int i = 0; i < a.Length; i++) WriteType(a[i]);
    }
    Type[] ReadTypes()
    {
      var n = ReadCount(); if (n == 0) return Type.EmptyTypes;
      var a = new Type[n]; for (int i = 0; i < n; i++) a[i] = ReadType(); return a;
    }
    void WriteCtor(ConstructorInfo p)
    {
      var x = index(p); WriteCount(x + 1); if (x >= 0) return;
      WriteType(p.DeclaringType); WriteTypes(TypeHelper.GetParametersNoCopy(p)); register(4, p);
    }
    ConstructorInfo ReadCtor()
    {
      var x = ReadCount() - 1; if (x >= 0) return (ConstructorInfo)access(4, x);
      var t = ReadType(); var p = t.GetConstructor(ReadTypes()); register(4, p); return p;
    }
    void WriteField(FieldInfo fi)
    {
      var x = index(fi); WriteCount(x + 1); if (x >= 0) return;
      WriteByte(fi.IsStatic ? 2 : 0); WriteType(fi.DeclaringType); WriteString(fi.Name); register(5, fi);
    }
    FieldInfo ReadField()
    {
      var x = ReadCount() - 1; if (x >= 0) return (FieldInfo)access(5, x);
      var f = ReadByte(); var t = ReadType(); var s = ReadString();
      var fi = t.GetField(s, ((f & 2) != 0 ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic | BindingFlags.Public);
      register(5, fi); return fi;
    }
    void WriteMethod(MethodInfo p)
    {
      var x = index(p); WriteCount(x + 1); if (x >= 0) return;
      var f = (p.IsGenericMethod ? (1) : 0) | (p.IsStatic ? 2 : 0) | (p.IsPublic ? 0 : 4) | (p.IsSpecialName ? 8 : 0);
      WriteCount(f); WriteType(p.DeclaringType); WriteString(p.Name, true); WriteTypes(TypeHelper.GetParametersNoCopy(p)); if ((f & 8) != 0) WriteType(p.ReturnType);
      if ((f & 1) != 0) WriteTypes(p.GetGenericArguments()); register(6, p);
    }
    MethodInfo ReadMethod()
    {
      var x = ReadCount() - 1; if (x >= 0) return (MethodInfo)access(6, x);//list[x];
      var f = ReadCount(); var t = ReadType(); var s = ReadString(true); var a = ReadTypes();
      var b = ((f & 2) != 0 ? BindingFlags.Static : BindingFlags.Instance) | ((f & 4) != 0 ? BindingFlags.NonPublic : BindingFlags.Public);
      if ((f & 8) != 0)
      {
        var r = ReadType(); var mm = t.GetMember(s, MemberTypes.Method, b);
        for (int i = 0; i < mm.Length; i++)
        {
          var me = (MethodInfo)mm[i]; if (me.ReturnType != r) continue;
          var tt = TypeHelper.GetParametersNoCopy(me); if (tt.Length != a.Length) continue;
          int ia = 0; for (; ia < a.Length && tt[ia].ParameterType == a[ia]; ia++) ; if (ia < a.Length) continue;
          register(6, me); return me;
        }
      }
      if ((f & 1) != 0) //todo: check, there should be a more efficent way over method signatures
      {
        var g = ReadTypes(); var mm = t.GetMember(s, MemberTypes.Method, b);
        for (int i = 0, j; i < mm.Length; i++)
        {
          var meth = (MethodInfo)mm[i]; if (!meth.IsGenericMethodDefinition) continue;
          var gg = meth.GetGenericArguments(); if (gg.Length != g.Length) continue;

          var aa = TypeHelper.GetParametersNoCopy(meth); //var aa = meth.GetParameters(); 
          if (aa.Length != a.Length) continue;
          for (j = 0; j < g.Length; j++)
          {
            var at = gg[j].GenericParameterAttributes;
            if ((at & GenericParameterAttributes.ReferenceTypeConstraint) != 0) if (g[j].IsValueType) break;
            if ((at & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) if (!g[j].IsValueType) break;
          }
          if (j < g.Length) continue;
          for (j = 0; j < a.Length; j++)
          {
            var t1 = aa[j].ParameterType; var t2 = a[j];
            if (!t1.ContainsGenericParameters) if (t1 == t2) continue; else break;
            if (t1.IsByRef) { if (!t2.IsByRef) break; t1 = t1.GetElementType(); t2 = t2.GetElementType(); }
            while (t1.IsArray && t2.IsArray) { t1 = t1.GetElementType(); t2 = t2.GetElementType(); }
            if (!t1.IsGenericType) continue; if (!t2.IsGenericType) break;
            t1 = t1.GetGenericTypeDefinition(); t2 = t2.GetGenericTypeDefinition(); if (t1 != t2) break;
          }
          if (j < a.Length) continue;
          var gm = meth.MakeGenericMethod(g); register(6, gm); return gm;
        }
      }
      var m = t.GetMethod(s, b, null, a, null);
      register(6, m); return m;
    }
    internal static bool Equals(byte[] a, byte[] b)
    {
      if (a.Length != b.Length) return false;
      fixed (byte* pa = a, pb = b) return Native.memcmp(pa, pb, (void*)a.Length) == 0;
    }
    internal static class TypeHelper
    {
      static Func<DynamicILInfo, byte[]> t1, t2, t3; static Func<DynamicILInfo, int> t4; static Func<DynamicILInfo, List<object>> t5;
      static Func<MethodBase, DynamicMethod> t6; static Func<DynamicMethod, Type[]> t7; static Func<IntPtr, Type> t8;
      static Func<MethodBase, ParameterInfo[]> t9, t10, t11;
      static TypeHelper()
      {
        var tl = typeof(DynamicILInfo); var fl = BindingFlags.Instance | BindingFlags.NonPublic;
        var pa = Expression.Parameter(typeof(DynamicILInfo));
        t1 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_code", fl)), pa).Compile();
        t2 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_exceptions", fl)), pa).Compile();
        t3 = Expression.Lambda<Func<DynamicILInfo, byte[]>>(Expression.Field(pa, tl.GetField("m_localSignature", fl)), pa).Compile();
        t4 = Expression.Lambda<Func<DynamicILInfo, int>>(Expression.Field(pa, tl.GetField("m_maxStackSize", fl)), pa).Compile();
        t5 = Expression.Lambda<Func<DynamicILInfo, List<object>>>(Expression.Field(Expression.Field(pa, tl.GetField("m_scope", fl)), "m_tokens"), pa).Compile();
      }
      internal static bool IsBlittable(Type t)
      {
        if (t.IsPrimitive) return true; if (!t.IsValueType) return false;
        var a = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < a.Length; i++) if (!IsBlittable(a[i].FieldType)) return false;
        return true;
      }
      internal static int SizeOf(Type t)
      {
        switch (Type.GetTypeCode(t))
        {
          case TypeCode.Boolean: return sizeof(bool);
          case TypeCode.Char: return sizeof(char);
          case TypeCode.SByte:
          case TypeCode.Byte: return sizeof(byte);
          case TypeCode.Int16:
          case TypeCode.UInt16: return sizeof(short);
          case TypeCode.Int32:
          case TypeCode.UInt32: return sizeof(int);
          case TypeCode.Int64:
          case TypeCode.UInt64: return sizeof(long);
          case TypeCode.Single: return sizeof(float);
          case TypeCode.Double: return sizeof(double);
          case TypeCode.Decimal: return sizeof(decimal);
          case TypeCode.DateTime: return sizeof(DateTime);
        }
        return Marshal.SizeOf(t);
      }

      internal static byte[] GetCode(DynamicILInfo p) { return t1(p); }
      internal static byte[] GetExceptions(DynamicILInfo p) { return t2(p); }
      internal static byte[] GetLocalSignature(DynamicILInfo p) { return t3(p); }
      internal static int GetMaxStackSize(DynamicILInfo p) { return t4(p); }
      internal static List<object> GetTokens(DynamicILInfo p) { return t5(p); }
      internal static DynamicMethod GetOwner(MethodBase m)
      {
        if (t6 == null)
        {
          var a = m.GetType(); var b = a.GetField("m_owner", BindingFlags.Instance | BindingFlags.NonPublic);
          var p = Expression.Parameter(typeof(MethodBase));
          t6 = Expression.Lambda<Func<MethodBase, DynamicMethod>>(Expression.Field(Expression.Convert(p, a), b), p).Compile();
        }
        return t6(m);

      }
      internal static Type[] GetParams(DynamicMethod a)
      {
        if (t7 == null)
        {
          var p = Expression.Parameter(typeof(DynamicMethod));
          t7 = Expression.Lambda<Func<DynamicMethod, Type[]>>(Expression.Field(p, typeof(DynamicMethod).GetField("m_parameterTypes", BindingFlags.Instance | BindingFlags.NonPublic)), p).Compile();
        }
        return t7(a);
      }
      internal static Type GetType(IntPtr h)
      {
        if (t8 == null)
        {
          var p = Expression.Parameter(typeof(IntPtr));
          t8 = Expression.Lambda<Func<IntPtr, Type>>(Expression.Call(typeof(Type).GetMethod("GetTypeFromHandleUnsafe", BindingFlags.NonPublic | BindingFlags.Static), p), p).Compile();
        }
        return t8(h);
      }
      internal static RuntimeTypeHandle GetContext(object p)//todo: Lambda
      {
        return (RuntimeTypeHandle)p.GetType().GetField("m_context", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
      }
      internal static RuntimeMethodHandle GetMethodHandle(object p)//todo: Lambda
      {
        return (RuntimeMethodHandle)p.GetType().GetField("m_methodHandle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
      }
      internal static RuntimeFieldHandle GetFieldHandle(object p)//todo: Lambda
      {
        return (RuntimeFieldHandle)p.GetType().GetField("m_fieldHandle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
      }
      internal static bool IsGenericFieldInfo(object p)
      {
        return p.GetType().Name == "GenericFieldInfo";
      }
      internal static bool IsGenericMethodInfo(object p)
      {
        return p.GetType().Name == "GenericMethodInfo";
      }
      internal static ParameterInfo[] GetParametersNoCopy(MethodBase meth)
      {
        if (meth is DynamicMethod) return (t11 ?? (t11 = GetParametersNoCopy(meth.GetType())))(meth);
        if (meth is ConstructorInfo) return (t10 ?? (t10 = GetParametersNoCopy(meth.GetType())))(meth);
        return (t9 ?? (t9 = GetParametersNoCopy(meth.GetType())))(meth);
      }
      static Func<MethodBase, ParameterInfo[]> GetParametersNoCopy(Type t)
      {
        var me = t.GetMethod("GetParametersNoCopy", BindingFlags.NonPublic | BindingFlags.Instance);
        var pa = Expression.Parameter(typeof(MethodBase));
        return Expression.Lambda<Func<MethodBase, ParameterInfo[]>>(Expression.Call(Expression.Convert(pa, me.DeclaringType), me), pa).Compile();
      }
    }
  }

}
