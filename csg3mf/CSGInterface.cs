using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace csg3mf
{
  public unsafe static class CSG
  {
    public static readonly IFactory Factory = COM.CreateInstance<IFactory>(IntPtr.Size == 8 ? (COM.DEBUG ? "csg64d.dll" : "csg64.dll") : (COM.DEBUG ? "csg32d.dll" : "csg32.dll"), typeof(CFactory).GUID);
    //public static readonly IFactory Factory = (IFactory)new CFactory(); //or registered
    public static ITesselator Tesselator => tess ?? (tess = Factory.CreateTessalator(Unit.Rational));
    [ThreadStatic] static ITesselator tess;
    [ComImport, Guid("54ca8e82-bdb3-41db-8ed5-3b890279c431")]
    public class CFactory { }

    public enum Unit { Double = 0, Rational = 1 }

    [ComImport, Guid("2a576402-2276-435d-bd1a-640ff1c19f90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IFactory
    {
      int Version { get; }
      ITesselator CreateTessalator(Unit unit);
      IVector CreateVector(int length);
      IMesh CreateMesh();
    }

    [Flags]
    public enum Mode
    {
      EvenOdd = 0x01, NonZero = 0x02, Positive = 0x04, Negative = 0x08, AbsGeqTwo = 0x10, GeqThree = 0x20,
      Fill = 0x0100, FillFast = 0x0200, IndexOnly = 0x0800,
      Outline = 0x1000, OutlinePrecise = 0x2000, NoTrim = 0x4000,
      NormX = 0x10000, NormY = 0x20000, NormZ = 0x40000, NormNeg = 0x80000
    }

    [ComImport, Guid("d210bdc1-65a3-43f7-a296-bf8d4bb7b962"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface ITesselator
    {
      Mode Mode { get; set; }
      void SetNormal(Variant v);
      void BeginPolygon();
      void BeginContour();
      void AddVertex(Variant v);
      void EndContour();
      void EndPolygon();
      int VertexCount { get; }
      void GetVertex(int i, ref Variant p);
      int IndexCount { get; }
      int GetIndex(int i);
      int OutlineCount { get; }
      int GetOutline(int i);
      void Update(IMesh mesh, Variant z, int flags = 0);
      void Cut(IMesh a, Variant plane);
      void Join(IMesh a, IMesh b, JoinOp op);
    }

    public enum JoinOp { Union = 0, Difference = 1, Intersection = 2 }

    [Flags]
    public enum MeshCheck { DupPoints = 1, BadIndex = 2, UnusedPoint = 4, Openings = 8, Planes = 16 }

    [ComImport, Guid("BE338702-B776-4178-AA13-963B4EB53EDF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IMesh
    {
      void Update(Variant vertices, Variant indices);
      void CopyTo(IMesh p);
      void Transform(Variant m);
      void CopyBuffer(int ib, int ab, ref Variant p);
      int VertexCount { get; }
      void GetVertex(int i, ref Variant p);
      void SetVertex(int i, Variant p);
      int IndexCount { get; }
      int GetIndex(int i);
      void SetIndex(int i, int p);
      int PlaneCount { get; }
      void GetPlane(int i, ref Variant p);
      void WriteToStream(COM.IStream str);
      void ReadFromStream(COM.IStream str);
      void CreateBox(Variant a, Variant b);
      MeshCheck Check(MeshCheck m = 0);
    }

    public enum Op1 { Copy = 0, Neg = 1, TransPM = 2, Inv3x4 = 3, Dot2 = 4, Dot3 = 5, Norm3 = 6, Num = 7, Den = 8, Lsb = 9, Msb = 10, Trunc = 11, Floor = 12, Ceil = 13, Round = 14, Rnd10 = 15, Com = 16 }
    public enum Op2 { Add = 0, Sub = 1, Mul = 2, Div = 3, Mul3x4 = 4, PlaneP3 = 5, PlanePN = 6, Pow = 7 }

    [ComImport, Guid("db6ebd51-d2fc-4d75-b2af-543326aeed48"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IVector
    {
      int Length { get; }
      string GetString(int i, int digits = 64, int flags = 0x3);
      int GetHashCode(int i, int n);
      bool Equals(int i, IVector pb, int ib, int c);
      int CompareTo(int i, IVector pb, int ib);
      void Copy(int i, IVector pb, int ib, int c);
      void GetValue(int i, ref Variant v);
      void SetValue(int i, Variant v);
      void Execute1(Op1 op, int i, IVector pa, int ia);
      void Execute2(Op2 op, int i, IVector pa, int ia, IVector pb, int ib);
      void SinCos(int i, double a, int prec); // 0:R4; 1:R8; 10..52: rational on unit circle; -1..-28: decimal digits
    }

    public enum VarType { Int = 1, Float = 2, Double = 3, Decimal = 4, Rational = 5, Bstr = 6 }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct Variant
    {
      [FieldOffset(0)] internal ushort vt;
      [FieldOffset(8)] internal void* vp;
      public Variant(int* p, int n) { vp = p; vt = (ushort)((int)VarType.Int | (n << 8)); }
      public Variant(float* p, int n) { vp = p; vt = (ushort)((int)VarType.Float | (n << 8)); }
      public Variant(double* p, int n) { vp = p; vt = (ushort)((int)VarType.Double | (n << 8)); }
      public Variant(decimal* p, int n) { vp = p; vt = (ushort)((int)VarType.Decimal | (n << 8)); }
      public Variant(int* p, int n, int c) { Variant v; v.vp = p; v.vt = (ushort)((int)VarType.Int | (n << 8)); ((int*)&v)[1] = c; this = v; }
      public Variant(float* p, int n, int c) { Variant v; v.vp = p; v.vt = (ushort)((int)VarType.Float | (n << 8)); ((int*)&v)[1] = c; this = v; }
      public Variant(double* p, int n, int c) { Variant v; v.vp = p; v.vt = (ushort)((int)VarType.Double | (n << 8)); ((int*)&v)[1] = c; this = v; }
      public Variant(IVector p, int n, int c) { Variant v; ((int*)&v)[0] = (int)VarType.Rational | (n << 8); ((int*)&v)[1] = c; var z = Marshal.GetIUnknownForObject(p); ((long*)&v)[1] = (long)z; Marshal.Release(z); this = v; }
      public static implicit operator Variant(int p) { Variant v; ((int*)&v)[2] = p; v.vt = (ushort)VarType.Int; return v; }
      public static implicit operator Variant(float p) { Variant v; ((float*)&v)[2] = p; v.vt = (ushort)VarType.Float; return v; }
      public static implicit operator Variant(double p) { Variant v; ((double*)&v)[1] = p; v.vt = (ushort)VarType.Double; return v; }
      public static implicit operator Variant(decimal p) { Variant v; ((decimal*)&v)[0] = p; v.vt = (ushort)VarType.Decimal; return v; }
      public static implicit operator Variant(string s) { Variant v; v.vt = (ushort)VarType.Bstr; var p = Marshal.StringToBSTR(s); v.vp = p.ToPointer(); Marshal.FreeBSTR(p); return v; }
      //public static implicit operator Variant(Vector3* p) => new Variant(&p->x, 3);
    }

    public struct Rational : IEquatable<Rational>
    {
      readonly IVector p; readonly int i;
      public override string ToString() => p.GetString(i);
      public string ToString(int digits, int fl = 3) => p.GetString(i, digits, fl);
      Rational(IVector p, int i) { this.p = p; this.i = i; }
      public override int GetHashCode()
      {
        return base.GetHashCode();
      }
      public override bool Equals(object p) => p is Rational b && Equals(b);
      public bool Equals(Rational b) => p.Equals(i, b.p, b.i, 1);
      public int CompareTo(Rational b) => p.CompareTo(i, b.p, b.i);
      public Rational Round() => op(Op1.Round, this);
      public Rational Round(int digits) => op(Op1.Rnd10 | (Op1)(digits << 8), this);
      public static Rational Pow(Rational a, int e) => op(Op2.Pow, a, e);
      public int Sign => p.CompareTo(i, null, 0);
      public Rational Num => op(Op1.Num, this);
      public Rational Den => op(Op1.Den, this);
      public Rational Msb => op(Op1.Msb, this);
      public Rational Com => op(Op1.Com, this);
      public static implicit operator Variant(Rational p) => new Variant(p.p, 1, p.i);
      public static implicit operator Rational(Variant v) => ctor(v);
      public static implicit operator Rational(int v) => ctor((Variant)v);
      public static implicit operator Rational(float v) => ctor(v);
      public static implicit operator Rational(double v) => ctor(v);
      public static implicit operator Rational(decimal v) => ctor(v);
      public static implicit operator Rational(string v) => ctor(v);
      public static explicit operator int(Rational v) { Variant t; *(int*)&t = 1; v.p.GetValue(v.i, ref t); return ((int*)&t)[2]; }
      public static explicit operator float(Rational v) { Variant t; *(int*)&t = 2; v.p.GetValue(v.i, ref t); return ((float*)&t)[2]; }
      public static explicit operator double(Rational v) { Variant t; *(int*)&t = 3; v.p.GetValue(v.i, ref t); return ((double*)&t)[1]; }
      public static explicit operator decimal(Rational v) { Variant t; *(int*)&t = 4; v.p.GetValue(v.i, ref t); return ((decimal*)&t)[0]; }
      public static Rational operator -(Rational a) => op(Op1.Neg, a);
      public static Rational operator +(Rational a, Rational b) => op(Op2.Add, a, b);
      public static Rational operator -(Rational a, Rational b) => op(Op2.Sub, a, b);
      public static Rational operator *(Rational a, Rational b) => op(Op2.Mul, a, b);
      public static Rational operator /(Rational a, Rational b) => op(Op2.Div, a, b);
      public static bool operator ==(Rational a, Rational b) => a.p.Equals(a.i, b.p, b.i, 1);
      public static bool operator !=(Rational a, Rational b) => !a.p.Equals(a.i, b.p, b.i, 1);
      public static bool operator <=(Rational a, Rational b) => a.p.CompareTo(a.i, b.p, b.i) <= 0;
      public static bool operator >=(Rational a, Rational b) => a.p.CompareTo(a.i, b.p, b.i) >= 0;
      public static bool operator <(Rational a, Rational b) => a.p.CompareTo(a.i, b.p, b.i) < 0;
      public static bool operator >(Rational a, Rational b) => a.p.CompareTo(a.i, b.p, b.i) > 0;
      static Rational op(Op1 op, Rational a)
      {
        var c = ctor(1); c.p.Execute1(op, c.i, a.p, a.i); return c;
      }
      static Rational op(Op2 op, Rational a, Rational b)
      {
        var c = ctor(1); c.p.Execute2(op, c.i, a.p, a.i, b.p, b.i); return c;
      }
      static Rational ctor(in Variant v)
      {
        var p = ctor(1); p.p.SetValue(p.i, v); return p;
      }
      static Rational ctor(int c)
      {
        //return new Rational(Factory.CreateVector(c), 0);
        ref var b = ref buff; const int size = 512;
        if (b.p == null || b.i + c > size) { b.i = 0; b.p = Factory.CreateVector(size); }
        var r = new Rational(b.p, b.i); b.i += c; return r;
      }
      [ThreadStatic] static (IVector p, int i) buff;

      public readonly struct Vector2 : IEquatable<Vector2>
      {
        public Rational x { get => new Rational(m.p, m.i + 0); set => m.p.Execute1(Op1.Copy, m.i + 0, value.p, value.i); }
        public Rational y { get => new Rational(m.p, m.i + 1); set => m.p.Execute1(Op1.Copy, m.i + 1, value.p, value.i); }
        public override string ToString() => $"{m.ToString(17)}; {y.ToString(17)}";
        public override int GetHashCode() => m.p.GetHashCode(m.i, 2);
        public override bool Equals(object p) => p is Vector3 b && Equals(b);
        public bool Equals(Vector2 b) => m.p.Equals(m.i, b.m.p, b.m.i, 2);
        public Vector2(Rational x, Rational y) { m = ctor(2); this.x = x; this.y = y; }
        public static implicit operator Variant(Vector2 p) => new Variant(p.m.p, 2, p.m.i);
        public static implicit operator Vector2(in (Variant x, Variant y) p) => new Vector2(p.x, p.y);
        public static bool operator ==(in Vector2 a, in Vector2 b) => a.Equals(b);
        public static bool operator !=(in Vector2 a, in Vector2 b) => !a.Equals(b);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.m + b.m, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.m - b.m, a.y - b.y);
        public static Vector2 operator *(Vector2 a, Rational b) => new Vector2(a.m * b, a.y * b);
        public static Vector2 operator /(Vector2 a, Rational b) => new Vector2(a.m / b, a.y / b);
        public Rational LengthSq { get { var p = ctor(1); p.p.Execute1(Op1.Dot2, p.i, m.p, m.i); return p; } }
        public double Length => Math.Sqrt((double)LengthSq);
        public double Angle => Math.Atan2((double)y, (double)x) * (180 / Math.PI);
        public static Vector2 SinCos(double a, int prec = 10) { var p = new Vector2(0); p.m.p.SinCos(p.m.i, a, prec); return p; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly Rational m; internal Vector2(int _) => m = ctor(2);
      }
      public readonly struct Vector3 : IEquatable<Vector3>
      {
        public Rational x { get => new Rational(m.p, m.i + 0); set => m.p.Execute1(Op1.Copy, m.i + 0, value.p, value.i); }
        public Rational y { get => new Rational(m.p, m.i + 1); set => m.p.Execute1(Op1.Copy, m.i + 1, value.p, value.i); }
        public Rational z { get => new Rational(m.p, m.i + 2); set => m.p.Execute1(Op1.Copy, m.i + 2, value.p, value.i); }
        public override string ToString() => $"{m.ToString(17)}; {y.ToString(17)}; {z.ToString(17)}";
        public override int GetHashCode() => m.p.GetHashCode(m.i, 3);
        public override bool Equals(object p) => p is Vector3 b && Equals(b);
        public bool Equals(Vector3 b) => m.p.Equals(m.i, b.m.p, b.m.i, 3);
        public Vector3(Rational x, Rational y, Rational z) { m = ctor(3); this.x = x; this.y = y; this.z = z; }
        public static implicit operator Variant(Vector3 p) => new Variant(p.m.p, 3, p.m.i);
        public static implicit operator Vector3(in (Variant x, Variant y, Variant z) p) => new Vector3(p.x, p.y, p.z);
        public static bool operator ==(in Vector3 a, in Vector3 b) => a.Equals(b);
        public static bool operator !=(in Vector3 a, in Vector3 b) => !a.Equals(b);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.m, -a.y, -a.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.m + b.m, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.m - b.m, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, Rational b) => new Vector3(a.m * b, a.y * b, a.z * b);
        public static Vector3 operator /(Vector3 a, Rational b) => new Vector3(a.m / b, a.y / b, a.z / b);
        public static Vector3 operator ^(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.m - a.m * b.z, a.m * b.y - a.y * b.m);
        public Vector3 Transform(Matrix3x4 m) { var p = new Vector3(0); p.m.p.Execute1(Op1.TransPM, p.m.i, m.m.p, m.m.i); return p; }
        public Rational LengthSq { get { var p = ctor(1); p.p.Execute1(Op1.Dot3, p.i, m.p, m.i); return p; } }
        public Vector3 Normalize() { var p = new Vector3(0); p.m.p.Execute1(Op1.Norm3, p.m.i, m.p, m.i); return p; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal readonly Rational m; internal Vector3(int _) => m = ctor(3);
      }
      public readonly struct Vector4 : IEquatable<Vector4>
      {
        public Rational x { get => new Rational(m.p, m.i + 0); set => m.p.Execute1(Op1.Copy, m.i + 0, value.p, value.i); }
        public Rational y { get => new Rational(m.p, m.i + 1); set => m.p.Execute1(Op1.Copy, m.i + 1, value.p, value.i); }
        public Rational z { get => new Rational(m.p, m.i + 2); set => m.p.Execute1(Op1.Copy, m.i + 2, value.p, value.i); }
        public Rational w { get => new Rational(m.p, m.i + 3); set => m.p.Execute1(Op1.Copy, m.i + 3, value.p, value.i); }
        public override string ToString() => $"{m.ToString(17)}; {y.ToString(17)}; {z.ToString(17)}; {w.ToString(17)}";
        public override int GetHashCode() => m.p.GetHashCode(m.i, 4);
        public override bool Equals(object p) => p is Vector3 b && Equals(b);
        public bool Equals(Vector4 b) => m.p.Equals(m.i, b.m.p, b.m.i, 4);
        public Vector4(Rational x, Rational y, Rational z, Rational w) { m = ctor(4); this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(Vector3 p, Rational w) { m = ctor(4); m.p.Copy(m.i, p.m.p, p.m.i, 3); this.w = w; }
        public static bool operator ==(in Vector4 a, in Vector4 b) => a.Equals(b);
        public static bool operator !=(in Vector4 a, in Vector4 b) => !a.Equals(b);
        public static implicit operator Variant(Vector4 p) => new Variant(p.m.p, 4, p.m.i);
        public static explicit operator Vector3(Vector4 p) => new Vector3(p.m, p.y, p.z);
        public static Vector4 PlaneFromPoints(Vector3 a, Vector3 b, Vector3 c)
        {
          var e = new Vector4(0); for (int i = 0; i < 3; i++) e.m.p.Execute1(Op1.Copy, e.m.i + i, a.m.p, a.m.i + i);
          e.m.p.Execute2(Op2.PlaneP3, e.m.i, b.m.p, b.m.i, c.m.p, c.m.i); return e;
        }
        public static Vector4 PlaneFromPointNormal(Vector3 p, Vector3 n)
        {
          var e = new Vector4(0);
          e.m.p.Execute2(Op2.PlanePN, e.m.i, p.m.p, p.m.i, n.m.p, n.m.i); return e;
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly Rational m; internal Vector4(int _) => m = ctor(4);
      }
      public struct Matrix3x4 : IEquatable<Matrix3x4>
      {
        public override int GetHashCode() => m.p.GetHashCode(m.i, 12);
        public override bool Equals(object p) => p is Matrix3x4 b && Equals(b);
        public bool Equals(Matrix3x4 b) => m.p.Equals(m.i, b.m.p, b.m.i, 12);
        public static implicit operator Variant(Matrix3x4 m) => new Variant(m.m.p, 12, m.m.i);
        public static Matrix3x4 Identity()
        {
          var m = new Matrix3x4(0); var v = stackalloc int[12] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
          m.m.p.SetValue(m.m.i, new Variant(v, 12)); return m;
        }
        public static Matrix3x4 Translation(Rational x, Rational y, Rational z)
        {
          var m = new Matrix3x4(0); var v = stackalloc int[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
          m.m.p.SetValue(m.m.i, new Variant(v, 9)); m[9] = x; m[10] = y; m[11] = z; return m;
        }
        public static Matrix3x4 Scaling(Rational x, Rational y, Rational z)
        {
          var m = new Matrix3x4(0); var v = stackalloc int[12];
          m.m.p.SetValue(m.m.i, new Variant(v, 9)); m[0] = x; m[4] = y; m[8] = z; return m;
        }
        public static Matrix3x4 RotationX(Vector2 sc)
        {
          var m = Identity(); m[4] = m[8] = sc.x; m[7] = -(m[5] = sc.y); return m;
        }
        public static Matrix3x4 RotationY(Vector2 sc)
        {
          var m = Identity(); m[0] = m[8] = sc.x; m[2] = -(m[6] = sc.y); return m;
        }
        public static Matrix3x4 RotationZ(Vector2 sc)
        {
          var m = Identity(); m[0] = m[4] = sc.x; m[3] = -(m[1] = sc.y); return m;
        }
        public Vector3 mx { get => copy(0); set => m.p.Copy(m.i + 0, value.m.p, value.m.i, 3); }
        public Vector3 my { get => copy(3); set => m.p.Copy(m.i + 3, value.m.p, value.m.i, 3); }
        public Vector3 mz { get => copy(6); set => m.p.Copy(m.i + 6, value.m.p, value.m.i, 3); }
        public Vector3 mp { get => copy(9); set => m.p.Copy(m.i + 9, value.m.p, value.m.i, 3); }
        public Rational this[int i]
        {
          get => new Rational(m.p, m.i + i);
          set => m.p.Execute1(Op1.Copy, m.i + i, value.p, value.i);
        }
        public Rational[] Items { get { var m = this; return Enumerable.Range(0, 12).Select(i => m[i]).ToArray(); } }
        public static Matrix3x4 operator *(Matrix3x4 a, Matrix3x4 b)
        {
          var m = new Matrix3x4(0);
          m.m.p.Execute2(Op2.Mul3x4, m.m.i, a.m.p, a.m.i, b.m.p, b.m.i); return m;
        }
        public static Matrix3x4 operator !(Matrix3x4 a)
        {
          var m = new Matrix3x4(0);
          m.m.p.Execute1(Op1.Inv3x4, m.m.i, a.m.p, a.m.i); return m;
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal readonly Rational m; Matrix3x4(int _) => m = ctor(12);
        private Vector3 copy(int i) { var v = new Vector3(0); v.m.p.Copy(v.m.i, m.p, m.i + i, 3); return v; }
      }
    }

    #region example extensions
    //public static void SetNormal(this ITesselator tess, Vector3 v) => tess.SetNormal(&v);
    //public static void AddVertex(this ITesselator tess, Vector3 p) => tess.AddVertex(&p);
    public static void AddVertex(this ITesselator tess, double x, double y)
    {
      var p = (x, y); tess.AddVertex(new Variant(&p.x, 2));
    }
    public static void AddVertex(this ITesselator tess, float x, float y)
    {
      var p = (x, y); tess.AddVertex(new Variant(&p.x, 2));
    }
    public static void AddVertex(this ITesselator tess, decimal x, decimal y)
    {
      var p = (x, y); tess.AddVertex(new Variant(&p.x, 2));
    }
    //public static Vector3 GetVertex(this ITesselator tess, int i) { Vector3 p; Variant v = &p; tess.GetVertex(i, ref v); return p; }
    public static (float x, float y) GetVertexF2(this ITesselator tess, int i) { (float x, float y) p = default; var v = new Variant(&p.x, 2); tess.GetVertex(i, ref v); return p; }
    public static (decimal x, decimal y) GetVertexD2(this ITesselator tess, int i) { (decimal x, decimal y) p = default; var v = new Variant(&p.x, 2); tess.GetVertex(i, ref v); return p; }
    //public static Vector3 GetVertex(this IMesh mesh, int i) { Vector3 p; Variant v = &p; mesh.GetVertex(i, ref v); return p; }
    public static Rational.Vector3 GetVertexR3(this IMesh mesh, int i)
    {
      var p = new Rational.Vector3(0); var v = (Variant)p; mesh.GetVertex(i, ref v); return p;
    }
    public static Rational.Vector4 GetPlaneR4(this IMesh mesh, int i)
    {
      var p = new Rational.Vector4(0); var v = (Variant)p; mesh.GetPlane(i, ref v); return p;
    }
    public static IMesh Clone(this IMesh p) { var d = Factory.CreateMesh(); p.CopyTo(d); return d; }
    public static void InitPlanes(this IMesh mesh) => Tesselator.Cut(mesh, new Variant());
    public static IEnumerable<Rational.Vector3> Vertices(this IMesh mesh) { for (int i = 0, n = mesh.VertexCount; i < n; i++) yield return mesh.GetVertexR3(i); }
    public static IEnumerable<int> Indices(this IMesh mesh) { for (int i = 0, n = mesh.IndexCount; i < n; i++) yield return mesh.GetIndex(i); } 
    public static void Display(params IMesh[] a) => disp?.Invoke(a); internal static Action<IMesh[]> disp; //debug helper
    #endregion
  }

  public unsafe class COM
  {
    public static T CreateInstance<T>(string path, in Guid clsid)
    {
      var t1 = GetProcAddress(LoadLibrary(path), "DllGetClassObject");
      var t2 = Marshal.GetDelegateForFunctionPointer<DllGetClassObject>(t1);
      t2(clsid, typeof(IClassFactory).GUID, out var cf);
      return (T)cf.CreateInstance(IntPtr.Zero, typeof(T).GUID);
    }
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi), SuppressUnmanagedCodeSecurity]
    static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);
    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true), SuppressUnmanagedCodeSecurity]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    delegate int DllGetClassObject(in Guid clsid, in Guid iid, [Out, MarshalAs(UnmanagedType.Interface)] out IClassFactory classFactory);
    [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IClassFactory {[return: MarshalAs(UnmanagedType.IUnknown)] object CreateInstance(IntPtr _, ref Guid clsid); }
    [ComImport, Guid("0000000c-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    public interface IStream
    {
      void Read(void* p, int n, int* r = null);
      void Write(void* p, int n, int* r = null);
      void Seek(long i, int org = 0, long* r = null);
      void SetSize(long n);
    }
#if (DEBUG)
    internal const bool DEBUG = true;
#else
    internal const bool DEBUG = false;
#endif
  }

}
