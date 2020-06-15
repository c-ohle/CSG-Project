using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Windows.Forms;
using System.Xml;

namespace csg3mf
{
  public static unsafe partial class CDX
  {
    public static readonly IFactory Factory = COM.CreateInstance<IFactory>(IntPtr.Size == 8 ? "cdx64.dll" : "cdx32.dll", typeof(CFactory).GUID);

    [ComImport, Guid("4e957503-5aeb-41f2-975b-3e6ae9f9c75a")]
    public class CFactory { }

    [ComImport, Guid("f0993d73-ea2a-4bf1-b128-826d4a3ba584"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IFactory
    {
      int Version { get; }
      string Devices { get; }
      void SetDevice(uint id);
      IView CreateView(IntPtr wnd, ISink sink, uint samples);
      IScene CreateScene(int reserve = 0);
      IFont GetFont(string name, float size, System.Drawing.FontStyle style);
      ITexture GetTexture(COM.IStream str);
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
      Center = 1, //in float border
      GetBox = 2, //in float4x3, out float4[2] 
      GetBoxSel = 3, //in float4x3, out float4[2] 
      SetPlane = 4, //in float4x3
      PickPlane = 5, //in float2, out float2
      SelectRect = 6, //in float2[2] 
    }

    public enum Draw
    {
      Orthographic = 0,
      GetTransform = 1, SetTransform = 2,
      GetColor = 3, SetColor = 4,
      GetFont = 5, SetFont = 6,
      GetTexture = 7, SetTexture = 8,
      GetMapping = 9, SetMapping = 10,
      FillRect = 11,
      FillEllipse = 12,
      GetTextExtent = 13,
      DrawText = 14,
      DrawRect = 15,
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
      void Draw(Draw draw, void* data);
      void Command(Cmd cmd, void* data);
      void Thumbnail(int dx, int dy, int samples, uint bkcolor, COM.IStream str);
    }

    [ComImport, Guid("982A1DBA-0C12-4342-8F58-A34D83956F0D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface ISink
    {
      [PreserveSig] void Render();
    }

    public enum Unit { meter = 1, centimeter = 2, millimeter = 3, micron = 4, foot = 5, inch = 6, }

    [ComImport, Guid("98068F4F-7768-484B-A2F8-21D4F7B5D811"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IScene
    {
      Unit Unit { get; set; }
      int Count { get; }
      INode this[int i] { get; }
      int Select(int i, int f);
      INode AddNode(string name);
      void Remove(int i);
      void Insert(int i, INode p);
      void Clear();
      void SaveToStream(COM.IStream str);
      void LoadFromStream(COM.IStream str);
      object Tag { [return: MarshalAs(UnmanagedType.IUnknown)] get; [param: MarshalAs(UnmanagedType.IUnknown)] set; }
    }

    [ComImport, Guid("2BB87169-81D3-405E-9C16-E4C22177BBAA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface INode
    {
      string Name { get; set; }
      INode Parent { get; set; }
      IScene Scene { get; }
      int Index { get; set; }
      bool IsSelect { get; set; }
      bool IsStatic { get; set; }
      CSG.Rational.Matrix Transform { get; set; }
      CSG.IMesh Mesh { get; set; }
      uint Color { get; set; }
      int MaterialCount { get; set; }
      object Tag { [return: MarshalAs(UnmanagedType.IUnknown)] get; [param: MarshalAs(UnmanagedType.IUnknown)] set; }
      void GetMaterial(int i, out int start, out int count, out uint color, out COM.IStream tex);
      void SetMaterial(int i, int start, int count, uint color, COM.IStream tex);
      void GetTexturCoords(out CSG.Variant v);
      void SetTexturCoords(CSG.Variant v);
      void GetTransform(ref CSG.Variant v);
      void SetTransform(CSG.Variant v);
      INode AddNode(string name);
    }

    [ComImport, Guid("F063C32D-59D1-4A0D-B209-323268059C12"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IFont
    {
      string Name { get; }
      float Size { get; }
      int Style { get; }
      float Ascent { get; }
      float Descent { get; }
      float Height { get; }
    }

    [ComImport, Guid("37E366F0-098E-45FB-9152-54CD33D05B21"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface ITexture
    {
    }

#if(false)
    public static void AddAnnotation(this INode p, object v)
    {
      var e = p.Tag; if (e == null) { p.Tag = v; return; }
      var a = e as object[]; if (a == null) { p.Tag = new object[] { e, v }; return; }
      int i = 0; for (; i < a.Length; i++) if (a[i] == null) { a[i] = v; return; }
      Array.Resize(ref a, i << 1); a[i] = v; p.Tag = a;
    }
    public static object Annotation(this INode p, Type t)
    {
      var e = p.Tag; var a = e as object[]; if (a == null) return t.IsInstanceOfType(e) ? e : null;
      for (int i = 0; i < a.Length && a[i] != null; i++) if (t.IsInstanceOfType(a[i])) return a[i];
      return null;
    }
    public static T Annotation<T>(this INode p) where T : class => p.Annotation(typeof(T)) as T;
    public static void RemoveAnnotation(this INode p, object v)
    {
      var e = p.Tag; var a = e as object[]; if (a == null) { if (e == v) p.Tag = null; return; }
      int i = 0, k = 0; for (; i < a.Length && a[i] != null; i++) if (a[i] != v) a[k++] = a[i]; a[k] = null;
    }
#endif
    public static void SetTransform(this INode p, float4x3 m)
    {
      p.SetTransform(new CSG.Variant((float*)&m, 12));
    }
    public static float4x3 GetTransform(this INode p)
    {
      float4x3 m; p.GetTransform(new CSG.Variant((float*)&m, 12)); return m;
    }
    public static float4x3 GetTransform(this INode p, INode site)
    {
      if (p == site) return 1;
      if (p.Parent == site) return p.GetTransform();
      return p.GetTransform() * p.Parent.GetTransform(site);
    }
    public static CSG.Rational GetTransval(this INode p, ushort i)
    {
      var t = (CSG.Rational)0; var v = (CSG.Variant)t; (&v.vt)[1] = i;
      p.GetTransform(v); return t;
    }
    public static IEnumerable<INode> Descendants(this IScene p)
    {
      for (int i = 0; i < p.Count; i++) yield return p[i];
    }
    public static IEnumerable<INode> Selection(this IScene p)
    {
      for (int a = -1, b; (b = p.Select(a, 1)) != -1; a = b) yield return p[b];
    }
    public static IEnumerable<int> Select(this IScene p, int f)
    {
      for (int a = -1, b; (b = p.Select(a, f)) != -1; a = b) yield return b;
    }
    public static IEnumerable<INode> SelectNodes(this IScene p, int f)
    {
      return p.Select(f).Select(i => p[i]);
    }
    public static IEnumerable<INode> Nodes(this IScene p)
    {
      return p.Select(-1 << 8).Select(i => p[i]);
    }
    public static IEnumerable<INode> Nodes(this INode p)
    { //=> p.Scene.Nodes().Where(t => t.Parent == p);
      var s = p.Scene; return s.Select(p.Index << 8).Select(i => s[i]);
    }
    public static void Select(this IScene scene)
    {
      foreach (var p in scene.Selection()) p.IsSelect = false;
    }
    public static void Select(this INode node)
    {
      node.Scene.Select(); node.IsSelect = true;
    }
    public static void Join(this INode a, INode b, CSG.JoinOp op)
    {
      if (a.Mesh == null || b.Mesh == null) return;
      var m = b.Mesh.Clone(); m.Transform(b.Transform * !a.Transform);
      CSG.Tesselator.Join(a.Mesh, m, op); Marshal.ReleaseComObject(m);
    }
    public static void Union(this INode a, INode b) => Join(a, b, CSG.JoinOp.Union);
    public static void Difference(this INode a, INode b) => Join(a, b, CSG.JoinOp.Difference);
    public static void Intersection(this INode a, INode b) => Join(a, b, CSG.JoinOp.Intersection);
    public static void Cut(this INode a, INode b)
    {
      if (a.Mesh == null) return;
      var t = b.Transform * !a.Transform;
      CSG.Tesselator.Cut(a.Mesh, CSG.Rational.Plane.FromPointNormal(t.mp, t.mz));
    }
    public static INode AddNode(this IScene a, string name, uint color)
    {
      var p = a.AddNode(name); p.Color = color; p.Mesh = CSG.Factory.CreateMesh(); return p;
    }
    public static void Remove(this IScene a, INode b) => a.Remove(b.Index);
    public static void SetMaterial(this INode node, int i, uint color, byte[] tex = null)
    {
      COM.IStream s; if (tex != null) fixed (byte* p = tex) s = COM.SHCreateMemStream(p, tex.Length); else s = null;
      node.SetMaterial(i, -1, -1, color, s);
    }
    public static void SetTexturCoords(this INode node, float2[] a)
    {
      fixed (float2* p = a) node.SetTexturCoords(new CSG.Variant(&p->x, 2, a.Length));
    }
    public static void SetPlane(this IView view, float4x3 p)
    {
      view.Command(Cmd.SetPlane, &p);
    }
    public static float2 PickPlane(this IView view)
    {
      float2 p; p.x = float.NaN; view.Command(Cmd.PickPlane, &p); return p;
    }
    //public static IFont GetFont(System.Drawing.Font p) => Factory.GetFont(p.FontFamily.Name, p.Size, p.Style);
    public struct DC
    {
      IView p;
      public DC(IView p) => this.p = p;
      public void SetOrtographic()
      {
        p.Draw(Draw.Orthographic, null);
      }
      public float4x3 Transform
      {
        get { float4x3 c; p.Draw(Draw.GetTransform, &c); return c; }
        set => p.Draw(Draw.SetTransform, &value);
      }
      public uint Color
      {
        get { uint c; p.Draw(Draw.GetColor, &c); return c; }
        set => p.Draw(Draw.SetColor, &value);
      }
      public IFont Font
      {
        get
        {
          IntPtr t; p.Draw(Draw.GetFont, &t); if (t == IntPtr.Zero) return null;
          var f = (IFont)Marshal.GetObjectForIUnknown(t); Marshal.Release(t); return f;
        }
        set
        {
          if (value == null) { p.Draw(Draw.SetFont, IntPtr.Zero.ToPointer()); return; }
          var t = Marshal.GetIUnknownForObject(value); p.Draw(Draw.SetFont, t.ToPointer()); Marshal.Release(t);
        }
      }
      public ITexture Texture
      {
        get
        {
          IntPtr t; p.Draw(Draw.GetTexture, &t); if (t == IntPtr.Zero) return null;
          var f = (ITexture)Marshal.GetObjectForIUnknown(t); Marshal.Release(t); return f;
        }
        set
        {
          if (value == null) { p.Draw(Draw.SetTexture, IntPtr.Zero.ToPointer()); return; }
          var t = Marshal.GetIUnknownForObject(value); p.Draw(Draw.SetTexture, t.ToPointer()); Marshal.Release(t);
        }
      }
      public float4x3 Mapping
      {
        get { float4x3 c; p.Draw(Draw.GetMapping, &c); return c; }
        set => p.Draw(Draw.SetMapping, &value);
      }
      public void DrawRect(float x, float y, float dx, float dy)
      {
        var r = new float4(x, y, dx, dy); p.Draw(Draw.DrawRect, &r);
      }
      public void FillRect(float x, float y, float dx, float dy)
      {
        var r = new float4(x, y, dx, dy); p.Draw(Draw.FillRect, &r);
      }
      public void FillEllipse(float x, float y, float dx, float dy)
      {
        var r = new float4(x, y, dx, dy); p.Draw(Draw.FillEllipse, &r);
      }
      public float2 GetTextExtent(string s)
      {
        t1 v; v.n = s.Length;
        fixed (char* t = s) { v.s = t; p.Draw(Draw.GetTextExtent, &v); }
        return *(float2*)&v.x;
      }
      public void DrawText(float x, float y, string s)
      {
        t1 v; v.x = x; v.y = y; v.n = s.Length;
        fixed (char* t = s) { v.s = t; p.Draw(Draw.DrawText, &v); }
      }
      struct t1 { public float x, y; public char* s; public int n; }
    }
  }

  public static unsafe partial class CDX
  {
    public struct float2 : IEquatable<float2>
    {
      public float x, y;
      public override string ToString()
      {
        return $"{x:R}; {y:R}";
      }
      public float2(float x, float y)
      {
        this.x = x; this.y = y;
      }
      public float2(double a)
      {
        x = (float)Math.Cos(a); if (Math.Abs(x) == 1) { y = 0; return; }
        y = (float)Math.Sin(a); if (Math.Abs(y) == 1) { x = 0; return; }
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        h2 = ((h2 << 7) | (h1 >> 25)) ^ h1;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(float2 v)
      {
        return x == v.x && y == v.y;
      }
      public override bool Equals(object obj)
      {
        return obj is float2 && Equals((float2)obj);
      }
      public static implicit operator float2((float x, float y) p)
      {
        float2 v; v.x = p.x; v.y = p.y; return v;
      }
      public static implicit operator float2(System.Drawing.Size p)
      {
        float2 v; v.x = p.Width; v.y = p.Height; return v;
      }
      public static implicit operator float2(System.Drawing.Point p)
      {
        float2 v; v.x = p.X; v.y = p.Y; return v;
      }
      public float LengthSq => x * x + y * y;
      public float Length => (float)Math.Sqrt(x * x + y * y);
      public double Angel => Math.Atan2(y, x);
      public float2 Round(int d) => new float2((float)Math.Round(x, d), (float)Math.Round(y, d));
      public static bool operator ==(float2 a, float2 b) { return a.x == b.x && a.y == b.y; }
      public static bool operator !=(float2 a, float2 b) { return a.x != b.x || a.y != b.y; }
      public static float2 operator -(float2 v) { v.x = -v.x; v.y = -v.y; return v; }
      public static float2 operator *(float2 v, float f)
      {
        v.x *= f; v.y *= f; return v;
      }
      public static float2 operator /(float2 v, float f)
      {
        v.x /= f; v.y /= f; return v;
      }
      public static float2 operator /(float2 a, float2 b)
      {
        a.x /= b.x; a.y /= b.y; return a;
      }
      public static float2 operator /(float f, float2 v)
      {
        v.x = f / v.x; v.y = f / v.y; return v;
      }
      public static float2 operator +(float2 a, float2 b) { a.x = a.x + b.x; a.y = a.y + b.y; return a; }
      public static float2 operator -(float2 a, float2 b) { a.x = a.x - b.x; a.y = a.y - b.y; return a; }
      public static float2 operator *(float2 a, float2 b) { a.x = a.x * b.x; a.y = a.y * b.y; return a; }
      public static float2 operator ~(float2 v) { float2 b; b.x = -v.y; b.y = v.x; return b; }
      public static float operator ^(float2 a, float2 b) => a.x * b.y - a.y * b.x;
      public static float operator &(float2 a, float2 b) => a.x * b.x + a.y * b.y;
    }
    public struct float3 : IEquatable<float3>
    {
      public float x, y, z;
      public float2 xy => new float2(x, y);
      public override string ToString()
      {
        return $"{x:R}; {y:R}; {z:R}";
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(float3 v)
      {
        return x == v.x && y == v.y && z == v.z;
      }
      public override bool Equals(object obj)
      {
        return obj is float3 && Equals((float3)obj);
      }
      public float3(float x, float y, float z)
      {
        this.x = x; this.y = y; this.z = z;
      }
      public float LengthSq => x * x + y * y + z * z;
      public float Length => (float)Math.Sqrt(x * x + y * y + z * z);
      public float3 Normalize() { var l = Length; return l != 0 ? this / l : default; }
      public static explicit operator string(float3 p)
      {
        return $"{XmlConvert.ToString(p.x)} {XmlConvert.ToString(p.y)} {XmlConvert.ToString(p.z)}";
      }
      public static explicit operator float3(string s)
      {
        var a = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return new float3(XmlConvert.ToSingle(a[0]), XmlConvert.ToSingle(a[1]), XmlConvert.ToSingle(a[2]));
      }
      public static implicit operator float3(float p)
      {
        float3 b; b.x = p; b.y = b.z = 0; return b;
      }
      public static implicit operator float3(float2 p)
      {
        float3 b; b.x = p.x; b.y = p.y; b.z = 0; return b;
      }
      public static explicit operator float2(float3 p)
      {
        float2 b; b.x = p.x; b.y = p.y; return b;
      }
      public static bool operator ==(float3 a, float3 b)
      {
        return a.x == b.x && a.y == b.y && a.z == b.z;
      }
      public static bool operator !=(float3 a, float3 b)
      {
        return a.x != b.x || a.y != b.y || a.z != b.z;
      }
      public static float3 operator -(float3 v)
      {
        v.x = -v.x; v.y = -v.y; v.z = -v.z; return v;
      }
      public static float3 operator +(float3 a, float3 b)
      {
        a.x += b.x; a.y += b.y; a.z += b.z; return a;
      }
      public static float3 operator -(float3 a, float3 b)
      {
        a.x -= b.x; a.y -= b.y; a.z -= b.z; return a;
      }
      public static float3 operator *(float3 v, float f)
      {
        v.x *= f; v.y *= f; v.z *= f; return v;
      }
      public static float3 operator /(float3 v, float f)
      {
        v.x /= f; v.y /= f; v.z /= f; return v;
      }
      public static float3 operator /(float3 a, float3 b)
      {
        a.x /= b.x; a.y /= b.y; a.z /= b.z; return a;
      }
      public static float3 operator ^(float3 a, float3 b)
      {
        float3 c;
        c.x = a.y * b.z - a.z * b.y;
        c.y = a.z * b.x - a.x * b.z;
        c.z = a.x * b.y - a.y * b.x;
        return c;
      }
      public static float operator &(float3 a, float3 b)
      {
        return a.x * b.x + a.y * b.y + a.z * b.z;
      }
    }
    public struct float4 : IEquatable<float4>
    {
      public float x, y, z, w;
      public float3 xyz => new float3(x, y, z);
      public float4(float x, float y, float z, float w)
      {
        this.x = x; this.y = y; this.z = z; this.w = w;
      }
      public override string ToString()
      {
        return $"{x:R}; {y:R}; {z:R}; {w:R}";
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        var h4 = (uint)w.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2 ^ h4;
        return (int)h1;
      }
      public bool Equals(float4 v)
      {
        return this == v;
      }
      public override bool Equals(object obj)
      {
        return obj is float4 && Equals((float4)obj);
      }
      public static bool operator ==(in float4 a, in float4 b)
      {
        return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
      }
      public static bool operator !=(float4 a, float4 b)
      {
        return !(a == b);
      }
      public static float4 operator *(float4 v, float f)
      {
        v.x *= f; v.y *= f; v.z *= f; v.w *= f; return v;
      }
      public static explicit operator uint(in float4 p)
      {
        uint d;
        ((byte*)&d)[0] = (byte)(p.z * 255);
        ((byte*)&d)[1] = (byte)(p.y * 255);
        ((byte*)&d)[2] = (byte)(p.x * 255);
        ((byte*)&d)[3] = (byte)(p.w * 255); return d;
      }
      public static explicit operator float4(uint p)
      {
        float4 d;
        d.z = ((byte*)&p)[0] * (1.0f / 255);
        d.y = ((byte*)&p)[1] * (1.0f / 255);
        d.x = ((byte*)&p)[2] * (1.0f / 255);
        d.w = ((byte*)&p)[3] * (1.0f / 255); return d;
      }
    }
    public struct float4x3
    {
      public float _11, _12, _13;
      public float _21, _22, _23;
      public float _31, _32, _33;
      public float _41, _42, _43;
      public override int GetHashCode()
      {
        return base.GetHashCode();
      }
      public override bool Equals(object p)
      {
        return p is float4x3 && !((float4x3)p != this);
      }
      public float3 mx => new float3(_11, _12, _13);
      public float3 my => new float3(_21, _22, _23);
      public float3 mz => new float3(_31, _32, _33);
      public float3 mp => new float3(_41, _42, _43);
      public static bool operator ==(in float4x3 a, in float4x3 b)
      {
        return !(a != b);
      }
      public static bool operator !=(in float4x3 a, in float4x3 b)
      {
        return a._11 != b._11 || a._12 != b._12 || a._13 != b._13 || //a._14 != b._14 ||
               a._21 != b._21 || a._22 != b._22 || a._23 != b._23 || //a._24 != b._24 || 
               a._31 != b._31 || a._32 != b._32 || a._33 != b._33 || //a._34 != b._34 ||  
               a._41 != b._41 || a._42 != b._42 || a._43 != b._43;//|| a._44 != b._44;
        //for (int i = 0; i < 12; i++) if ((&a._11)[i] != (&b._11)[i]) return true; return false;
      }
      public static float4x3 Identity => 1;
      public static float4x3 LookAt(float3 eye, float3 pos, float3 up)
      {
        var R2 = (pos - eye).Normalize();
        var R0 = (up ^ R2).Normalize();
        var R1 = R2 ^ R0;
        eye = -eye;
        var D0 = R0 & eye;
        var D1 = R1 & eye;
        var D2 = R2 & eye;
        float4x3 m;
        m._11 = R0.x; m._12 = R1.x; m._13 = R2.x;
        m._21 = R0.y; m._22 = R1.y; m._23 = R2.y;
        m._31 = R0.z; m._32 = R1.z; m._33 = R2.z;
        m._41 = D0; m._42 = D1; m._43 = D2;
        return m;
      }
      public static float4x3 Translation(float x, float y, float z)
      {
        float4x3 m; (&m)->_11 = m._22 = m._33 = 1; m._41 = x; m._42 = y; m._43 = z; return m;
      }
      public static float4x3 Scaling(float x, float y, float z)
      {
        float4x3 m; (&m)->_11 = x; m._22 = y; m._33 = z; return m;
      }
      public static float4x3 RotationX(double a)
      {
        var sc = new float2(a); var m = new float4x3(); m._11 = 1; m._22 = m._33 = sc.x; m._32 = -(m._23 = sc.y); return m;
      }
      public static float4x3 RotationY(double a)
      {
        var sc = new float2(a); var m = new float4x3(); m._22 = 1; m._11 = m._33 = sc.x; m._13 = -(m._31 = sc.y); return m;
      }
      public static float4x3 RotationZ(double a)
      {
        var sc = new float2(a); var m = new float4x3(); m._33 = 1; m._11 = m._22 = sc.x; m._21 = -(m._12 = sc.y); return m;
      }
      public static float4x3 RotationAxis(float3 v, float a)
      {
        var sc = new float2(a); float s = sc.y, c = sc.x, cc = 1 - c;
        var m = new float4x3();
        m._11 = cc * v.x * v.x + c;
        m._21 = cc * v.x * v.y - s * v.z;
        m._31 = cc * v.x * v.z + s * v.y;
        m._12 = cc * v.y * v.x + s * v.z;
        m._22 = cc * v.y * v.y + c;
        m._32 = cc * v.y * v.z - s * v.x;
        m._13 = cc * v.z * v.x - s * v.y;
        m._23 = cc * v.z * v.y + s * v.x;
        m._33 = cc * v.z * v.z + c;
        return m;
      }
      public static implicit operator float4x3(float s)
      {
        return new float4x3() { _11 = s, _22 = s, _33 = s };
      }
      public static implicit operator float4x3(float2 p)
      {
        float4x3 m; *(float*)&m = m._22 = m._33 = 1; *(float2*)&m._41 = p; return m;
      }
      public static implicit operator float4x3(float3 p)
      {
        float4x3 m; *(float*)&m = m._22 = m._33 = 1; *(float3*)&m._41 = p; return m;
      }
      public static float4x3 operator !(in float4x3 p)
      {
        //inv(&v, &v); return v;
        var b0 = p._31 * p._42 - p._32 * p._41;
        var b1 = p._31 * p._43 - p._33 * p._41;
        var b3 = p._32 * p._43 - p._33 * p._42;
        var d1 = p._22 * p._33 + p._23 * -p._32;
        var d2 = p._21 * p._33 + p._23 * -p._31;
        var d3 = p._21 * p._32 + p._22 * -p._31;
        var d4 = p._21 * b3 + p._22 * -b1 + p._23 * b0;
        var de = p._11 * d1 - p._12 * d2 + p._13 * d3; de = 1f / de; //if (det == 0) throw new Exception();
        var a0 = p._11 * p._22 - p._12 * p._21;
        var a1 = p._11 * p._23 - p._13 * p._21;
        var a3 = p._12 * p._23 - p._13 * p._22;
        var d5 = p._12 * p._33 + p._13 * -p._32;
        var d6 = p._11 * p._33 + p._13 * -p._31;
        var d7 = p._11 * p._32 + p._12 * -p._31;
        var d8 = p._11 * b3 + p._12 * -b1 + p._13 * b0;
        var d9 = p._41 * a3 + p._42 * -a1 + p._43 * a0; float4x3 r;
        r._11 = +d1 * de; r._12 = -d5 * de;
        r._13 = +a3 * de;
        r._21 = -d2 * de; r._22 = +d6 * de;
        r._23 = -a1 * de;
        r._31 = +d3 * de; r._32 = -d7 * de;
        r._33 = +a0 * de;
        r._41 = -d4 * de; r._42 = +d8 * de;
        r._43 = -d9 * de; return r;
      }
      public static float4x3 operator *(in float4x3 a, in float4x3 b)
      {
        float x = a._11, y = a._12, z = a._13; float4x3 r;
        r._11 = b._11 * x + b._21 * y + b._31 * z;
        r._12 = b._12 * x + b._22 * y + b._32 * z;
        r._13 = b._13 * x + b._23 * y + b._33 * z; x = a._21; y = a._22; z = a._23;
        r._21 = b._11 * x + b._21 * y + b._31 * z;
        r._22 = b._12 * x + b._22 * y + b._32 * z;
        r._23 = b._13 * x + b._23 * y + b._33 * z; x = a._31; y = a._32; z = a._33;
        r._31 = b._11 * x + b._21 * y + b._31 * z;
        r._32 = b._12 * x + b._22 * y + b._32 * z;
        r._33 = b._13 * x + b._23 * y + b._33 * z; x = a._41; y = a._42; z = a._43;
        r._41 = b._11 * x + b._21 * y + b._31 * z + b._41;
        r._42 = b._12 * x + b._22 * y + b._32 * z + b._42;
        r._43 = b._13 * x + b._23 * y + b._33 * z + b._43; return r;
      }
      public static float3 operator *(float3 a, in float4x3 b)
      {
        float3 c;
        c.x = b._11 * a.x + b._21 * a.y + b._31 * a.z + b._41;
        c.y = b._12 * a.x + b._22 * a.y + b._32 * a.z + b._42;
        c.z = b._13 * a.x + b._23 * a.y + b._33 * a.z + b._43;
        return c;
      }
    }
  }
}
