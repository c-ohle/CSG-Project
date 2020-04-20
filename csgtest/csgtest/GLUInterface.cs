using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace csgtest
{
  static unsafe class GLU
  {
    static Tess tess;
    public static Tess Tesselator => tess ?? (tess = new Tess());
    public enum Winding { EvenOdd, NonZero, Positive, Negative, AbsGeqTwo }
    public class Tess : CSG.ITesselator
    {
      public double Epsilon = 1e-10;
      public bool correctTJunctions = true;
      CSG.Mode CSG.ITesselator.Mode { get => 0; set => throw new NotImplementedException(); }
      void CSG.ITesselator.SetNormal(CSG.Variant v) => throw new NotImplementedException();
      int CSG.ITesselator.OutlineCount => 0;
      int CSG.ITesselator.OutlineAt(int i) => throw new NotImplementedException();
      public void SetWinding(Winding v) => gluTessProperty(tess, 100140, 100130 + (int)v);
      public void SetBoundaryOnly(bool on) => gluTessProperty(tess, 100141, (boundaryonly = on) ? 1 : 0);
      public void BeginPolygon()
      {
        ni = np = 0; gluTessBeginPolygon(tess, null);
      }
      public void BeginContour()
      {
        gluTessBeginContour(tess);
      }
      public void AddVertex(CSG.Variant p)
      {
        Vector3 P = default;
        switch (p.vt)
        {
          case (int)CSG.VarType.Float | (2 << 8): P = *(PointF*)p.vp; break;
          case (int)CSG.VarType.Double| (2 << 8): P.x = ((double*)p.vp)[0]; P.y = ((double*)p.vp)[1]; break;
          default: throw new NotSupportedException(); 
        }
        if (np == maxnp) throw new OutOfMemoryException();
        int i = np++; pp[i] = P; gluTessVertex(tess, &pp[i], (void*)i);
      }
      public void EndContour()
      {
        gluTessEndContour(tess);
      }
      public void EndPolygon()
      {
        gluTessEndPolygon(tess);
        if (!boundaryonly && correctTJunctions) CorrectTJunctions();
      }
      public int IndexCount => ni;
      public int IndexAt(int i) => ii[i];
      public int VertexCount => np;
      public Vector3 VertexAt(int i) => pp[i];
      const int maxni = 10000, maxnp = 5000;
      internal Tess()
      {
        tess = gluNewTess();
        gluTessCallback(tess, 100100, t1 = (ActionInt)onbegin);
        gluTessCallback(tess, 100101, t2 = (ActionPtr)onvertex);
        gluTessCallback(tess, 100102, t3 = (Action)onend);
        gluTessCallback(tess, 100103, t4 = (ActionInt)onerror);
        gluTessCallback(tess, 100104, t5 = (ActionByte)onedge);
        gluTessCallback(tess, 100105, t6 = (ActionCom)oncombine);
        ii = (int*)Marshal.AllocCoTaskMem(maxni * sizeof(int));
        pp = (Vector3*)Marshal.AllocCoTaskMem(maxnp * sizeof(Vector3));
      }
      ~Tess()
      {
        gluDeleteTess(tess);
        Marshal.FreeCoTaskMem((IntPtr)ii);
        Marshal.FreeCoTaskMem((IntPtr)pp);
      }
      void* tess; Delegate t1, t2, t3, t4, t5, t6;
      int ni; int* ii; int np; Vector3* pp; bool boundaryonly;
      void oncombine(Vector3* p, void** data, float* w, void** pr)
      {
        if (np == maxnp) throw new OutOfMemoryException();
        int i = np++; pp[i] = *p; *pr = (void*)i;
      }
      void onbegin(int id) // Points: 0, Lines: 1, LineLoop: 2, LineStrip: 3, Triangles: 4, TriangleStrip: 5, TriangleFan: 6, Quads: 7, QuadStrip: 8, Polygon: 9
      {
      }
      void onedge(byte id)
      {
        //if (id == 0) ii[ni - 1] |= 0x10000000;
      }
      void onvertex(void* data)
      {
        if (ni == maxni) throw new OutOfMemoryException();
        ii[ni++] = (int)data;
      }
      void onend()
      {
        if (boundaryonly) ii[ni - 1] |= 0x40000000;
      }
      void onerror(int id)
      {
        Debug.WriteLine($"glu error {id}");
      }

      static double length(Vector3* v)
      {
        return Math.Sqrt(dot(v));
      }
      static double dot(Vector3* v)
      {
        return v->x * v->x + v->y * v->y + v->z * v->z;
      }
      static double dot(Vector3* a, Vector3* b)
      {
        return a->x * b->x + a->y * b->y + a->z * b->z;
      }
      static double distsq(Vector3* a, Vector3* b)
      {
        double x = a->x - b->x, y = a->y - b->y, z = a->z - b->z;
        return x * x + y * y + z * z;
      }
      static void ccw(Vector3* a, Vector3* b, Vector3* r)
      {
        r->x = a->y * b->z - a->z * b->y;
        r->y = a->z * b->x - a->x * b->z;
        r->z = a->x * b->y - a->y * b->x;
      }
      static void normalize(Vector3* v)
      {
        var l = length(v); v->x /= l; v->y /= l; v->z /= l;
      }
      static void cross(Vector3* a, Vector3* b, Vector3* c, Vector3* r)
      {
        Vector3 u; u.x = b->x - a->x; u.y = b->y - a->y; u.z = b->z - a->z;
        Vector3 v; v.x = c->x - a->x; v.y = c->y - a->y; v.z = c->z - a->z; ccw(&u, &v, r);
      }
      static Vector3 cross(Vector3* a, Vector3* b, Vector3* c)
      {
        Vector3 v; cross(a, b, c, &v); return v;
      }
      static double dot(Vector3* a, Vector3* b, Vector3* c)
      {
        Vector3 v; cross(a, b, c, &v); return dot(&v);
      }

      public void CorrectTJunctions()
      {
        var ii = this.ii; var ni = this.ni; //if (ni != 0) ii[ni - 1] &= 0x0fffffff;
        var pp = this.pp; var np = this.np;
        int rest;
        for (int loop = 0; ; loop++)
        {
          int swap = 0; rest = 0;
          for (int i1 = 0; i1 < ni - 3; i1++)
          {
            var i2 = i1 % 3 != 2 ? i1 + 1 : i1 - 2;
            var i3 = i2 % 3 != 2 ? i2 + 1 : i2 - 2;
            for (int k1 = i1 + 1; k1 < ni; k1++)
            {
              if (ii[k1] != ii[i2]) continue; var k2 = k1 % 3 != 2 ? k1 + 1 : k1 - 2;
              if (ii[k2] != ii[i1]) continue; var k3 = k2 % 3 != 2 ? k2 + 1 : k2 - 2;
              var l1 = cross(&pp[ii[i1]], &pp[ii[i2]], &pp[ii[i3]]); var d1 = dot(&l1);
              var l2 = cross(&pp[ii[k1]], &pp[ii[k2]], &pp[ii[k3]]); var d2 = dot(&l2);
              if (d1 > Epsilon && d2 > Epsilon) continue; rest++;
              var n1 = cross(&pp[ii[i1]], &pp[ii[k3]], &pp[ii[i3]]);
              var n2 = cross(&pp[ii[k1]], &pp[ii[i3]], &pp[ii[k3]]);
              var la = distsq(&l1, &l2);
              var lb = distsq(&n1, &n2); if (lb >= la) break; //no improvement
              var t1 = ii[k2]; ii[k2] = ii[i3];
              var t2 = ii[i2]; ii[i2] = ii[k3];
              ii[k1] = ii[k1]; ii[i1] = ii[i1]; swap++;
              break;
            }
          }
          if (swap == 0) break;
          if (loop == 10) { Debug.WriteLine("error: T-junctions"); return; }
        }
        if (rest == 0) return;
        var t = 0;
        for (int i = 0; i < ni; i += 3)
        {
          var l = dot(&pp[ii[i + 0]], &pp[ii[i + 1]], &pp[ii[i + 2]]); if (l <= 1e-15) continue;
          if (t != i) { ii[t + 0] = ii[i + 0]; ii[t + 1] = ii[i + 1]; ii[t + 2] = ii[i + 2]; }
          t += 3;
        }
        this.ni = t;
      }

      void CSG.ITesselator.VertexAt(int i, ref CSG.Variant p) => throw new NotImplementedException();
      void CSG.ITesselator.Update(CSG.IMesh mesh, CSG.Variant z) => throw new NotImplementedException();
      void CSG.ITesselator.Cut(CSG.IMesh a, CSG.Variant plane) => throw new NotImplementedException();
      void CSG.ITesselator.Join(CSG.IMesh a, CSG.IMesh b, CSG.JoinOp op) => throw new NotImplementedException();
    }
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void* gluNewTess();
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessCallback(void* tess, int which, Delegate callback);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessNormal(void* tess, double x, double y, double z);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessProperty(void* tess, int prop, double value); //WindingRule: 100140, BoundaryOnly: 100141, Tolerance: 100142
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessBeginPolygon(void* tess, void* data);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessBeginContour(void* tess);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessVertex(void* tess, Vector3* coords, void* data);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessEndContour(void* tess);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluTessEndPolygon(void* tess);
    [DllImport("glu32.dll"), SuppressUnmanagedCodeSecurity]
    extern static void gluDeleteTess(void* tess);
    delegate void ActionInt(int type);
    delegate void ActionByte(byte type);
    delegate void ActionPtr(void* data);
    delegate void ActionCom(Vector3* coords, void** data, float* weight, void** outData);
  }
}
