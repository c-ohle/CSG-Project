#pragma once

#include "resource.h"
#include "csg_i.h"
#include "vector.h"

using namespace ATL;

//#pragma warning(disable : 26812)

class ATL_NO_VTABLE CTesselator :
  public CComObjectRootEx<CComSingleThreadModel>,
  public CComCoClass<CTesselator, &CLSID_Tesselator>,
  public ITesselator
{
public:
  __declspec(align(16))
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

  Mode mode;
  int ppLength, dictLength, llLength, ssLength, iiLength, kkLength;
  int ns, nl, fi; int* ss, * ll;
  int np; ts* pp;
  int mi; int* dict;
  int ni; ab* ii, * kk;

  CTesselator()
  {
    mode = (Mode)(Mode::Fill | Mode::Positive);
    ppLength = dictLength = llLength = ssLength = iiLength = kkLength = 0;
    ns = nl = fi = 0; ss = ll = 0;
    np = 0; pp = 0;
    mi = 0; dict = 0;
    ni = 0; ii = kk = 0;
  }
  ~CTesselator()
  {
    _aligned_free(pp); _aligned_free(ii); _aligned_free(kk);
    _aligned_free(ss); _aligned_free(ll); _aligned_free(dict);
  }
  template<class T> __forceinline static void __realloc(T*& p, size_t n)
  {
    p = (T*)_aligned_realloc(p, n * sizeof(T), 16);
  }
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
    auto& c = *(CTesselator*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    return compare(c.pp[a.b].y, c.pp[b.b].y);
  }
  static int cmp2(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselator*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    int t = compare(c.pp[a.a].x1, c.pp[b.a].x1);
    return t != 0 ? t : compare(c.pp[a.a].x2, c.pp[b.a].x2);
  };
  static int cmp4(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselator*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    auto i = compare(c.pp[a.a].y, c.pp[b.a].y);
    if (i == 0) i = a.a - b.a; return i;
  };
  static int cmp3(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselator*)pc; auto& a = *(int*)pa; auto& b = *(int*)pb;
    return compare(
      c.pp[c.pp[a].fl < 0 ? c.ii[a].a : c.ii[a].b].y,
      c.pp[c.pp[b].fl < 0 ? c.ii[b].a : c.ii[b].b].y);
  }

  DECLARE_REGISTRY_RESOURCEID(106)
  DECLARE_NOT_AGGREGATABLE(CTesselator)
  BEGIN_COM_MAP(CTesselator)
    COM_INTERFACE_ENTRY(ITesselator)
  END_COM_MAP()
  DECLARE_PROTECT_FINAL_CONSTRUCT()

  HRESULT FinalConstruct() { return S_OK; }
  void FinalRelease() { }
public:
  STDMETHOD(BeginPolygon)();
  STDMETHOD(BeginContour)();
  STDMETHOD(AddVertex)(DOUBLE x, DOUBLE y, DOUBLE z);
  STDMETHOD(EndContour)();
  STDMETHOD(EndPolygon)();
  STDMETHOD(get_Version)(LONG* pVal);
  STDMETHOD(get_Mode)(Mode* pVal);
  STDMETHOD(put_Mode)(Mode newVal);
  STDMETHOD(get_VertexCount)(LONG* pVal);
  STDMETHOD(VertexAt)(LONG i, Vertex* pVal);
  STDMETHOD(get_IndexCount)(LONG* pVal);
  STDMETHOD(IndexAt)(LONG i, LONG* pVal);
  STDMETHOD(get_OutlineCount)(LONG* pVal);
  STDMETHOD(OutlineAt)(LONG i, LONG* pVal);
};

OBJECT_ENTRY_AUTO(__uuidof(Tesselator), CTesselator)
