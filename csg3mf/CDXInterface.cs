using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using static csg3mf.D3DView;

namespace csg3mf
{
  public static unsafe class CDX
  {
    public static readonly IFactory Factory = COM.CreateInstance<IFactory>(IntPtr.Size == 8 ? "cdx64.dll" : "cdx32.dll", typeof(CFactory).GUID);

    [ComImport, Guid("4e957503-5aeb-41f2-975b-3e6ae9f9c75a")]
    public class CFactory { }

    [ComImport, Guid("f0993d73-ea2a-4bf1-b128-826d4a3ba584"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IFactory
    {
      string Devices { get; }
      void SetDevice(uint id);
      IView CreateView(IntPtr wnd, ISink sink, uint samples);
      IScene CreateScene(int reserve = 0);
    }

    public enum Render
    {
      BoundingBox = 0x0001,
      Coordinates = 0x0002,
      Normals = 0x0004,
      Wireframe = 0x0008,
      Outlines = 0x0010,
      Shadows = 0x0400,
    }

    public enum Cmd
    {
      Center = 1
    }

    [ComImport, Guid("4C0EC273-CA2F-48F4-B871-E487E2774492"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IView
    {
      string Samples { get; set; }
      uint BkColor { get; set; }
      Render Render { get; set; }
      IScene Scene { get; set; }
      INode Camera { get; set; }
      int MouseOverNode { get; }
      float3 MouseOverPoint { get; }
      float4x4 MouseOverPlane { get; }
      void Command(Cmd id, IntPtr data);
      void Print(int dx, int dy, int samples, uint bkcolor, COM.IStream str);
    }

    [ComImport, Guid("982A1DBA-0C12-4342-8F58-A34D83956F0D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface ISink
    {
      void Render();
    }

    [ComImport, Guid("98068F4F-7768-484B-A2F8-21D4F7B5D811"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IScene
    {
      int Count { get; }
      INode this[int i] { get; }
      INode AddNode(string name);
      void Remove(int i);
      void Clear();
      void SaveToStream(COM.IStream str);
      void LoadFromStream(COM.IStream str);
    }

    [ComImport, Guid("2BB87169-81D3-405E-9C16-E4C22177BBAA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface INode
    {
      string Name { get; set; }
      INode Parent { get; set; }
      IScene Scene { get; }
      float4x3 TransformF { get; set; }
      CSG.Rational.Matrix Transform { get; set; }
      CSG.IMesh Mesh { get; set; }
      uint Color { get; set; }
      int MaterialCount { get; set; }
      void GetMaterial(int i, out int start, out int count, out uint color, out COM.IStream tex);
      void SetMaterial(int i, int start, int count, uint color, COM.IStream tex);
      void GetTexturCoords(out CSG.Variant v);
      void SetTexturCoords(CSG.Variant v);
      INode AddNode(string name);
      void SetTransform(CSG.Variant v);
    }

    public static float4x3 GetTransformF(this INode p, INode site = null)
    {
      if (p == site) return 1;
      if (p.Parent == site) return p.TransformF;
      return p.TransformF * p.Parent.GetTransformF(site);
    }
    public static IEnumerable<INode> Descendants(this IScene p)
    {
      for (int i = 0; i < p.Count; i++) yield return p[i];
    }
    public static IEnumerable<INode> Descendants(this INode p) => p.Scene.Descendants().Where(t => t.Parent == p);
    public static void Join(this INode a, INode b, CSG.JoinOp op)
    {
      if (a.Mesh == null || b.Mesh == null) return;
      var m = b.Mesh.Clone(); m.Transform(b.Transform * !a.Transform);
      CSG.Tesselator.Join(a.Mesh, m, op);
      Marshal.ReleaseComObject(m);
    }
    public static void Union(this INode a, INode b) => Join(a, b, CSG.JoinOp.Union);
    public static void Difference(this INode a, INode b) => Join(a, b, CSG.JoinOp.Difference);
    public static void Intersection(this INode a, INode b) => Join(a, b, CSG.JoinOp.Intersection);
    public static void Cut(this INode a, INode b)
    {
      if (a.Mesh == null) return;
      var t = b.Transform;
      CSG.Tesselator.Cut(a.Mesh, CSG.Rational.Plane.FromPointNormal(t.mp, t.mz));
    }
    public static INode AddNode(this IScene a, string name, uint color)
    {
      var p = a.AddNode(name); p.Color = color; p.Mesh = CSG.Factory.CreateMesh(); return p;
    }
    public static void Remove(this IScene a, INode b)
    {
      for (int i = 0; i < a.Count; i++) if (a[i] == b)
        {
          a.Remove(i);
          break;
        }
    }
    public static void SetMaterial(this INode node, int i, uint color, byte[] tex = null)
    {
      COM.IStream s; if (tex != null) fixed (byte* p = tex) s = COM.SHCreateMemStream(p, tex.Length); else s = null;
      node.SetMaterial(i, -1, -1, color, s);
    }
    public static void SetTexturCoords(this INode node, float2[] a) 
    {
      fixed (float2* p = a) node.SetTexturCoords(new CSG.Variant(&p->x, 2, a.Length));
    }
  }

}
