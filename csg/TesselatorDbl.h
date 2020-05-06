#pragma once

#include "resource.h"
#include "csg_i.h"
#include "vector.h"

class CTesselatorDbl : public ICSGTesselator
{
  struct ts { double x, y, z, a, f, x1, x2, yy; int next, ic, line, fl; };
  struct ab
  {
    int a, b; ab(int a, int b) { this->a = a; this->b = b; }
    int hashcode() { return  a ^ (b * 31); }
    bool operator == (ab t) { return a == t.a && b == t.b; }
    bool operator != (ab t) { return a != t.a || b != t.b; }
  };
  struct fs
  {
    double x; int k, d1, d2;
    fs() { x = 0; k = 0; d1 = 0; d2 = 0; }
    fs(int k, double x, int d1, int d2) { this->x = x; this->k = k; this->d1 = d1; this->d2 = d2; }
  };
  const double kill = 1.0 / 128;
  const int hash = 199;
  CSG_TESS mode = (CSG_TESS)(CSG_TESS_FILL | CSG_TESS_POSITIVE);
  int ns = 0, nl = 0, fi = 0; sarray<int> ss, ll; //keep order
  int np = 0; sarray<ts> pp;
  int mi = 0; sarray<int> dict;
  int ni = 0; sarray <ab> ii, kk;
  void fill();
  void outline();
  void optimize();
  void resize(int c = 0);
  void trim();
  int project(int m);
  int addpt(double x, double y, int v);
  static int ccw(ts* a, ts* b, ts* c);
  bool circum(int i1, int i2, int i3, int i4);
  __forceinline static int hashcode(double num)
  {
    if (num == 0.0) return 0;
    __int64 num2 = *(__int64*)(&num);
    return (int)num2 ^ (int)(num2 >> 32);
  }
  __forceinline static int compare(double a, double b)
  {
    if (a < b) return -1;
    if (a > b) return +1;
    return 0;
  }
  static int cmp1(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorDbl*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    return compare(c.pp[a.b].y, c.pp[b.b].y);
  }
  static int cmp2(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorDbl*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    int t = compare(c.pp[a.a].x1, c.pp[b.a].x1);
    return t != 0 ? t : compare(c.pp[a.a].x2, c.pp[b.a].x2);
  };
  static int cmp4(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorDbl*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    auto i = compare(c.pp[a.a].y, c.pp[b.a].y);
    if (i == 0) i = a.a - b.a; return i;
  };
  static int cmp3(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorDbl*)pc; auto& a = *(int*)pa; auto& b = *(int*)pb;
    return compare(
      c.pp[c.pp[a].fl < 0 ? c.ii[a].a : c.ii[a].b].y,
      c.pp[c.pp[b].fl < 0 ? c.ii[b].a : c.ii[b].b].y);
  }
  UINT refcount = 1;
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICSGTesselator) || riid == __uuidof(IAgileObject))
    {
      InterlockedIncrement(&refcount); *p = static_cast<ICSGTesselator*>(this); return 0;
    }
    return E_NOINTERFACE;
  }
  ULONG __stdcall AddRef(void)
  {
    return InterlockedIncrement(&refcount);
  }
  ULONG __stdcall Release(void)
  {
    auto count = InterlockedDecrement(&refcount);
    if (!count) delete this;
    return count;
  }
  HRESULT __stdcall SetNormal(CSGVAR p);
  HRESULT __stdcall BeginPolygon();
  HRESULT __stdcall BeginContour();
  HRESULT __stdcall AddVertex(CSGVAR v);
  HRESULT __stdcall EndContour();
  HRESULT __stdcall EndPolygon();
  HRESULT __stdcall get_Mode(CSG_TESS* p);
  HRESULT __stdcall put_Mode(CSG_TESS v);
  HRESULT __stdcall get_VertexCount(UINT* p);
  HRESULT __stdcall GetVertex(UINT i, CSGVAR v);
  HRESULT __stdcall get_IndexCount(UINT* p);
  HRESULT __stdcall GetIndex(UINT i, UINT* p);
  HRESULT __stdcall get_OutlineCount(UINT* p);
  HRESULT __stdcall GetOutline(UINT i, UINT* p);
  HRESULT __stdcall Update(ICSGMesh* mesh, CSGVAR z, UINT flags);
  HRESULT __stdcall Cut(ICSGMesh* a, CSGVAR plane) { return E_NOTIMPL; }
  HRESULT __stdcall Join(ICSGMesh* a, ICSGMesh* b, CSG_JOIN op) { return E_NOTIMPL; }
  HRESULT __stdcall AddGlyphContour(CSGVAR text, HFONT font, int flat);
};
