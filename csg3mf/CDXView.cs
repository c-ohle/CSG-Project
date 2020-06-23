using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using static csg3mf.CDX;

namespace csg3mf
{
  unsafe class CDXView : UserControl, ISink, UIForm.ICommandTarget
  {
    internal IView view; long drvsettings = 0x400000000;
    public override string Text { get => "Camera"; set { } }
    protected unsafe override void OnHandleCreated(EventArgs _)
    {
      var reg = Application.UserAppDataRegistry;
      var drv = reg.GetValue("drv"); if (drv is long v) drvsettings = v;
      Factory.SetDevice((uint)drvsettings);
      view = Factory.CreateView(Handle, this, (uint)(drvsettings >> 32));
      view.Render = (Render)reg.GetValue("fl", (int)(Render.BoundingBox | Render.Coordinates | Render.Wireframe | Render.Shadows));
      view.BkColor = 0xffcccccc;
      view.Scene = Tag as IScene;// cont.Nodes;
      OnCenter(null);

      AllowDrop = true;
      ContextMenuStrip = new UIForm.ContextMenu();
      ContextMenuStrip.Opening += (x, y) => { var p = mainover(); if (p != null && !p.IsSelect && !p.IsStatic) { p.Select(); Invalidate(); } };
      ContextMenuStrip.Items.AddRange(new ToolStripItem[] {
          new UIForm.MenuItem(2010, "&Undo"),
          new UIForm.MenuItem(2011, "&Redo"),
          new ToolStripSeparator(),
          new UIForm.MenuItem(2035, "&Group", Keys.Control | Keys.G),
          new UIForm.MenuItem(2036, "U&ngroup", Keys.Control | Keys.U),
          new ToolStripSeparator(),
          new UIForm.MenuItem(2150, "Intersection A && B"),//, Keys.Alt | Keys.I),
          new UIForm.MenuItem(2151, "Union A | B"),//, Keys.Alt | Keys.U),
          new UIForm.MenuItem(2152, "Substract A - B"),//, Keys.Alt | Keys.D),
          new UIForm.MenuItem(2153, "Difference A ^ B"),
          new UIForm.MenuItem(2154, "Cut Plane"),//, Keys.Alt | Keys.C),
          new ToolStripSeparator(),
          //new UIForm.MenuItem(4020, "Static"),
          //new ToolStripSeparator(),
          new UIForm.MenuItem(4021, "Script...", Keys.Shift | Keys.F12),
          new ToolStripSeparator(),
          new UIForm.MenuItem(2100, "Properties...", Keys.Alt | Keys.Enter)});
    }
    public new void Invalidate() { MainFrame.inval = 3; base.Invalidate(); }
    public int OnCommand(int id, object test)
    {
      switch (id)
      {
        case 2010: //Undo
          //if (!Focused) return 0;
          if (undos == null || undoi == 0) return 8;
          if (test == null) { undos[undoi - 1](); undoi--; Invalidate(); }
          return 1;
        case 2011: //Redo
          //if (!Focused) return 0;
          if (undos == null || undoi >= undos.Count) return 8;
          if (test == null) { undos[undoi](); undoi++; Invalidate(); }
          return 1;
        case 2060: //SelectAll
          //if (!Focused) return 0;
          if (test != null) return 1;
          foreach (var p in view.Scene.SelectNodes(-1 << 8)) p.IsSelect = !p.IsStatic; Invalidate();
          return 1;
        //case 4020: return OnStatic(test);
        case 2015: return OnDelete(test);
        case 2035: return OnGroup(test);
        case 2036: return OnUngroup(test);
        case 2300: return OnCenter(test);
        case 3015: return OnDriver(test);
        case 3016: return OnSamples(test);
        case 2210: //Select Box
        case 2211: //Select Pivot
        case 2212: //Select Normals
        case 2213: //Select Wireframe
        case 2214: //Select Outline
        case 2220: //Shadows
          if (test != null) return (view.Render & (CDX.Render)(1 << (id - 2210))) != 0 ? 3 : 1;
          view.Render ^= (CDX.Render)(1 << (id - 2210)); Application.UserAppDataRegistry.SetValue("fl", (int)view.Render);
          Invalidate(); return 1;
        case 65301: //can close
          //if(Tag is IScene) return 1;
          if (test != null) return 1;
          MessageBox.Show("no!"); return 1;
      }
      return 0;
    }

    int OnCenter(object test) //todo: undo
    {
      if (!view.Scene.Descendants().Any(p => p.Mesh != null && p.Mesh.VertexCount != 0)) return 0;
      if (test != null) return 1;
      float abst = 100; view.Command(Cmd.Center, &abst);
      Invalidate(); return 1;
    }
    //int OnStatic(object test)
    //{
    //  var a = view.Scene.Selection();
    //  if (!a.Any()) return 0; var all = a.All(p => p.IsStatic);
    //  if (test != null) return all ? 3 : 1;
    //  var aa = a.Where(p => p.IsStatic == all).ToArray();
    //  execute(() => { foreach (var p in aa) p.IsStatic = !p.IsStatic; });
    //  return 1;
    //}
    int OnGroup(object test)
    {
      var scene = view.Scene;
      var a = scene.Select(1); if (!a.Any()) return 0;// if (a.Take(2).Count() != 2) return 0;
      if (test != null) return 1;
      var bo = float4x3.Identity; view.Command(Cmd.GetBoxSel, &bo);
      var mi = bo.mx; var ma = *(float3*)&bo._22;
      var mp = (mi + ma) / 2;
      var ii = a.ToArray(); var gr = scene.AddNode("Group"); scene.Remove(gr.Index);
      gr.SetTransform(float4x3.Translation(mp.x, mp.y, mi.z));
      Execute(() =>
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
      Execute(() =>
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
      //if (!Focused) return 0;
      var a = view.Scene.Select(2);
      if (!a.Any()) return 0;
      if (test != null) return 1;
      Execute(undodel(a.ToArray()));
      return 1;
    }
    int OnDriver(object test)
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
    int OnSamples(object test)
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
      {
        if (main.Tag is XNode xn)
        {
          var func = xn.GetMethod<Func<IView, Action<int>>>();
          if (func != null) try { tool = func(view); } catch { }
        }
        if (tool == null)
          switch (keys)
          {
            case Keys.None: tool = main.IsStatic ? tool_select() : obj_movxy(main); break;
            case Keys.Control: tool = main.IsStatic ? camera_movxy() : obj_drag(main); break;
            case Keys.Shift: tool = main.IsStatic ? camera_movz() : obj_movz(main); break;
            case Keys.Alt: tool = main.IsStatic ? camera_rotz(0) : obj_rotz(main); break;
            case Keys.Control | Keys.Shift: tool = main.IsStatic ? camera_rotx() : obj_rot(main, 0); break;
            case Keys.Control | Keys.Alt: tool = main.IsStatic ? camera_rotz(1) : obj_rot(main, 1); break;
            case Keys.Control | Keys.Alt | Keys.Shift: if (main.IsStatic) main.Select(); else tool = obj_rot(main, 2); break;
            default: tool = tool_select(); break;
          }
      }
      if (tool == null) return; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
      if (tool != null) { tool(0); Invalidate(); return; }
      Cursor = (view.MouseOverId & 0x1000) != 0 ? Cursors.Cross : Cursors.Default;

      Debug.WriteLine(view.MouseOverNode + " " + view.MouseOverPoint);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
      if (tool == null) return;
      var t = tool;  tool = null; Capture = false; t(1); Invalidate();
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
      var m = view.Camera.GetTransform();//.GetTransformF();
      var node = view.Scene[view.MouseOverNode];
      var v = (view.MouseOverPoint * node.GetTransform(null)) - m.mp;
      var l = v.Length;
      var t = Environment.TickCount;
      view.Camera.SetTransform(m * (v.Normalize() * (l * 0.1f * e.Delta * (1f / 120)))); Invalidate();
      if (t - lastwheel > 500) AddUndo(undo(view.Camera, m)); lastwheel = t;
    }
    static int lastwheel;

    Action<int> camera_free()
    {
      var mb = float4x3.Identity; view.Command(Cmd.GetBox, &mb);
      var boxmin = *(float3*)&mb._11; var boxmax = *(float3*)&mb._22;
      var pm = (boxmin + boxmax) * 0.5f; var tm = (float4x3)pm;
      var cm = view.Camera.GetTransform(); var p1 = (float2)Cursor.Position; bool moves = false;
      return id =>
      {
        if (id == 0)
        {
          var v = (Cursor.Position - p1) * -0.01f;
          if (!moves && v.LengthSq < 0.03) return; moves = true;
          view.Camera.SetTransform(cm * !tm * float4x3.RotationAxis(cm.mx, -v.y) * float4x3.RotationZ(v.x) * tm);
        }
        if (id == 1) AddUndo(undo(view.Camera, cm));
      };
    }
    Action<int> camera_movxy()
    {
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      view.SetPlane(wp = new float3(0, 0, wp.z)); var p1 = view.PickPlane(); var p2 = p1;
      var camera = view.Camera; var m = camera.GetTransform();
      return id =>
      {
        if (id == 0) { p2 = view.PickPlane(); camera.SetTransform(m * (p1 - p2)); }
        if (id == 1) AddUndo(undo(camera, m));
      };
    }
    Action<int> camera_movz()
    {
      var camera = view.Camera; var m = camera.GetTransform();
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      view.SetPlane(float4x3.RotationY(Math.PI / 2) * float4x3.RotationZ(((float2)m.mz).Angel) * wp);
      var p1 = view.PickPlane(); var p2 = p1; //var mover = move(camera);
      return id =>
      {
        if (id == 0) { p2 = view.PickPlane(); camera.SetTransform(m * new float3(0, 0, view.PickPlane().x - p1.x)); }
        if (id == 1) AddUndo(undo(camera, m));
      };
    }
    Action<int> camera_rotz(int mode)
    {
      var camera = view.Camera; var m = camera.GetTransform(); var rot = m.mp; //var scene = camera.Ancestor<Scene>(); 
      if (mode != 0)
      {
        var p = view.Scene.Selection().FirstOrDefault();
        if (p != null) rot = p.GetTransform(null).mp;
      }
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      view.SetPlane(new float3(rot.x, rot.y, wp.z)); var a1 = view.PickPlane().Angel; //var mover = move(camera);
      return id =>
      {
        if (id == 0) { var p2 = view.PickPlane(); camera.SetTransform(m * -rot * float4x3.RotationZ(a1 - view.PickPlane().Angel) * rot); }
        if (id == 1) AddUndo(undo(camera, m));
      };
    }
    Action<int> camera_rotx()
    {
      var camera = view.Camera; var m = camera.GetTransform();
      view.SetPlane(m * m.mz); var p1 = view.PickPlane(); var p2 = p1; //var mover = move(camera);
      return id =>
      {
        if (id == 0) { p2 = view.PickPlane(); camera.SetTransform(float4x3.RotationX(Math.Atan(p2.y) - Math.Atan(p1.y)) * m); Invalidate(); }
        if (id == 1) AddUndo(undo(camera, m));
      };
    }
    Action<int> tool_select()
    {
      var over = view.Scene[view.MouseOverNode];
      var wp = view.MouseOverPoint * over.GetTransform(null);
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
          //if (p1.x > p2.x) { var o = p1.x; p1.x = p2.x; p2.x = o; }
          //if (p1.y > p2.y) { var o = p1.y; p1.y = p2.y; p2.y = o; }
          float4 t; ((float2*)&t)[0] = p1; ((float2*)&t)[1] = p2;
          view.Command(Cmd.SelectRect, &t);
        }
      };
    }

    Action<int> obj_movxy(INode main)
    {
      var ws = main.IsSelect; if (!ws) main.Select();
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      view.SetPlane(wp = new float3(0, 0, wp.z)); var p1 = view.PickPlane();
      Action<int, float4x3> mover = null;
      return id =>
      {
        if (id == 0)
        {
          var p2 = view.PickPlane(); var dp = (p2 - p1).Round(4);
          if (mover == null && dp == default) return;
          (mover ?? (mover = getmover()))(0, dp);
        }
        if (id == 1)
        {
          if (mover != null) mover(2, default);
          else if (ws) main.Select();
        }
      };
    }
    Action<int> obj_movz(INode main)
    {
      var ws = main.IsSelect; if (!ws) main.Select();
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      var bo = float4x3.Identity; view.Command(Cmd.GetBoxSel, &bo);
      var miz = bo.mx.z; var lov = miz != float.MaxValue ? miz : 0; var boxz = lov; var ansch = Math.Abs(boxz) < 0.1f;
      view.SetPlane(float4x3.RotationY(Math.PI / 2) * float4x3.RotationZ(((float2)view.Camera.GetTransform().my).Angel) * wp);
      var p1 = view.PickPlane(); var p2 = p1; var mover = getmover();
      return id =>
      {
        if (id == 0)
        {
          p2 = view.PickPlane(); if (p2 == p1) return; var dz = (float)Math.Round(p1.x - p2.x, 4);
          if (miz != float.MaxValue) { var ov = boxz + dz; if (ov < 0 && ov > -0.5f && lov >= 0) { if (!ansch) { ansch = true; } dz = -boxz; ov = 0; } else ansch = false; lov = ov; }
          mover(0, float4x3.Translation(0, 0, dz));
        }
        if (id == 1) mover(2, default);
      };
    }
    Action<int> obj_rotz(INode main)
    {
      if (!main.IsSelect) main.Select();
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      var scene = main.Parent; var ms = scene != null ? scene.GetTransform(null) : 1;
      var mw = main.GetTransform(null); var mp = mw.mp;
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
        if (id == 1) mover(2, default);
      };
    }
    Action<int> obj_rot(INode main, int xyz)
    {
      if (!main.IsSelect) main.Select();
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
      var wm = main.GetTransform(main.Parent);
      var op = wp * !main.GetTransform(null);
      var mr = (xyz == 0 ?
        float4x3.RotationY(+(Math.PI / 2)) * new float3(op.x, 0, 0) : xyz == 1 ?
        float4x3.RotationX(-(Math.PI / 2)) * new float3(0, op.y, 0) :
        new float3(0, 0, op.z)) * wm;
      var w0 = Math.Abs(mr._33) > 0.9f ? Math.Atan2(mr._12, mr._22) : Math.Atan2(mr._13, mr._23);
      view.SetPlane(float4x3.RotationZ(-w0) * mr * (main.Parent != null ? main.Parent.GetTransform(null) : 1));
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
        if (id == 1) mover(2, default);
      };
    }
    Action<int> obj_drag(INode main)
    {
      var ws = main.IsSelect; if (!ws) main.IsSelect = true;
      var wp = view.MouseOverPoint * view.Scene[view.MouseOverNode].GetTransform(null);
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
            var blob = Factory.CreateScene();
            var scene = view.Scene;
            var ii = scene.Select(2).ToArray();
            var pp = ii.Select(i => scene[i]).ToArray();
            var tt = pp.Select(p => Array.IndexOf(pp, p.Parent)).ToArray();
            for (int i = 0; i < pp.Length; i++) { blob.Insert(i, pp[i]); blob[i].Tag = pp[i].Tag; }
            for (int i = 0; i < pp.Length; i++) if (tt[i] != -1) blob[i].Parent = blob[tt[i]];

            var str = COM.SHCreateMemStream();
            var t1 = view.Scene; view.Scene = blob;
            try { view.Thumbnail(256, 256, 4, 0x00fffffe, str); }
            finally { view.Scene = t1; }

            blob.Export3MF(path, str, wp);
            var data = new DataObject(); data.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
            DoDragDrop(data, DragDropEffects.Copy);
          }
          catch (Exception e) { Debug.WriteLine(e.Message); }
          finally { File.Delete(path); }
        }
        if (id == 1 && ws) main.IsSelect = false;
      };
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
      var files = e.Data.GetData(DataFormats.FileDrop) as string[];
      if (files == null || files.Length != 1) return;
      var s = files[0]; if (!s.EndsWith(".3mf", true, null)) return;

      IScene drop; float3 wp;
      try { drop = Import3MF(s, out wp); } catch { return; }
      var scene = view.Scene;
      var pp = drop.Descendants().ToArray();
      var tt = pp.Select(p => p.Parent != null ? p.Parent.Index : -1).ToArray();
      var ab = scene.Count; drop.Clear();
      for (int i = 0; i < pp.Length; i++) scene.Insert(scene.Count, pp[i]);
      for (int i = 0; i < tt.Length; i++) if (tt[i] != -1) scene[ab + i].Parent = scene[ab + tt[i]];
      var rp = pp.Where(p => !pp.Contains(p.Parent)).ToArray();
      var mm = rp.Select(p => p.GetTransform()).ToArray();

      view.Command(Cmd.SetPlane, null); view.SetPlane(wp); //var p1 = view.PickPlane();
      tool = id =>
      {
        if (id == 0)
        {
          var p2 = view.PickPlane(); var dm = (float4x3)p2;
          for (int i = 0; i < rp.Length; i++) rp[i].SetTransform(mm[i] * dm); Invalidate();
        }
        if (id == 1)
        {
          scene.Select(); for (int i = 0; i < rp.Length; i++) rp[i].IsSelect = true;
          AddUndo(undodel(Enumerable.Range(ab, pp.Length).ToArray())); Invalidate();
        }
        if (id == 2) { for (int i = scene.Count - 1; i >= ab; i--) scene.Remove(i); Invalidate(); }
      };
    }
    protected override void OnDragOver(DragEventArgs e)
    {
      if (tool == null) { e.Effect = DragDropEffects.None; return; }
      tool(0); e.Effect = DragDropEffects.Copy;
    }
    protected override void OnDragDrop(DragEventArgs e)
    {
      if (tool == null) { e.Effect = DragDropEffects.None; return; }
      tool(1); tool = null; e.Effect = DragDropEffects.Copy;
    }
    protected override void OnDragLeave(EventArgs e)
    {
      if (tool == null) return;
      tool(2); tool = null;
    }

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
    internal void AddUndo(Action p)
    {
      if (p == null) return;
      if (undos == null) undos = new List<Action>();
      undos.RemoveRange(undoi, undos.Count - undoi);
      undos.Add(p); undoi = undos.Count;
    }
    internal void Execute(Action p)
    {
      p(); AddUndo(p); Invalidate();
    }
    static Action undo(INode p, float4x3 m)
    {
      if (m == p.GetTransform()) return null;
      return () => { var t = p.GetTransform(); p.SetTransform(m); m = t; };
    }
    static Action undo(INode p, CSG.Rational.Matrix m)
    {
      if (m.Equals(p.Transform)) return null;
      return () => { var t = p.Transform; p.Transform = m; m = t; };
    }
    internal static Action undo(IEnumerable<Action> a)
    {
      var b = a.OfType<Action>().ToArray(); if (b.Length == 0) return null;
      if (b.Length == 1) return b[0];
      return () => { for (int i = 0; i < b.Length; i++) b[i](); Array.Reverse(b); };
    }
    Action undodel(int[] ii)
    {
      INode[] pp = null; int[] tt = null;
      return () =>
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
      };
    }
    Action<int, float4x3> getmover()
    {
      var pp = view.Scene.Selection().ToArray();
      var mm = pp.Select(p => p.Transform).ToArray();
      return (id, m) =>
      {
        if (id == 0) { for (int i = 0; i < pp.Length; i++) pp[i].SetTransform((float4x3)mm[i] * m); }
        if (id == 2) AddUndo(undo(pp.Select((p, i) => undo(p, mm[i]))));
      };
    }

    //IFont font = Factory.GetFont("Wingdings", 10, FontStyle.Regular);
    //IFont font = Factory.GetFont("Arial", 10, FontStyle.Regular);
    IFont font = GetFont(SystemFonts.MenuFont);
    ITexture checkboard;
    //Stopwatch sw;

    void ISink.Render()
    {
      tool?.Invoke(4);
      var dc = new DC(view);
      var scene = view.Scene;

      if (true)
      {
        for (int a = -1, b; (b = scene.Select(a, 1)) != -1; a = b)
        {
          var p = scene[b]; if (!(p.Tag is XNode xp)) continue;
          var draw = xp.GetMethod<Action<DC>>(); if (draw == null) continue;
          dc.Transform = p.GetTransform(null);
          try { DC.icatch = b + 1; draw(dc); } catch (Exception e) { Debug.WriteLine(e.Message); }
        }
      }

      if (true)
      {
        if (checkboard == null) checkboard = GetTexture(256, 256, 1, gr =>
        {
          gr.FillRectangle(Brushes.White, 0, 0, 256, 1);
          gr.FillRectangle(Brushes.White, 0, 0, 1, 256);
        });
        dc.Transform = 1;
        dc.Color = 0xfe000000;
        var t1 = dc.Texture; dc.Texture = checkboard;
        dc.Mapping = 1; dc.FillRect(-100, -100, 200, 200); dc.Texture = t1;
      }

      var infos = XScene.From(scene).Infos;
      if (infos.Count != 0)
      {
        dc.SetOrtographic(); dc.Font = font; dc.Color = 0xff000000;
        float y = 10 + font.Ascent, dy = font.Height;
        for (int i = 0; i < infos.Count; i++, y += dy) dc.DrawText(10, y, infos[i]);
      }
      if (true)
      {
        dc.SetOrtographic();
        dc.Font = font; dc.Color = 0xff000000;
        float y = 10 + font.Ascent, dy = font.Height, x = ClientSize.Width - 10f;
        var s = $"Vertexbuffer {Factory.GetInfo(0)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
        s = $"Indexbuffer {Factory.GetInfo(1)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
        s = $"Mappings {Factory.GetInfo(2)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
        s = $"Textures {Factory.GetInfo(3)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
        s = $"Fonts {Factory.GetInfo(4)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
        s = $"Views {Factory.GetInfo(5)}"; dc.DrawText(x - dc.GetTextExtent(s).x, y, s); y += dy;
      }

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

#if (false)
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
