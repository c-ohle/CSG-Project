#pragma once
#include "resource.h"
#include "csg_i.h"
#include "vector.h"
#include "rational.h"

class CTesselatorRat : public ICSGTesselator
{
  struct ts { int next, ic, line, fl, t1, t2; Rational x, y, z, a, f, x1, x2; };
  struct ab
  {
    int a, b; __forceinline ab(int a, int b) { this->a = a; this->b = b; }
    __forceinline int hashcode() { return  a ^ (b * 31); }
    __forceinline bool operator == (ab t) { return a == t.a && b == t.b; }
    __forceinline bool operator != (ab t) { return a != t.a || b != t.b; }
  };
  struct fs
  {
    Rational x; int k, d1, d2;
    __forceinline fs() { k = 0; d1 = 0; d2 = 0; }
    __forceinline fs(int k, const Rational& x, int d1, int d2) { this->x = x; this->k = k; this->d1 = d1; this->d2 = d2; }
  };
  const Rational kill = 128;
  const int hash = 199;
  CSG_TESS mode = (CSG_TESS)(CSG_TESS_FILL | CSG_TESS_POSITIVE);
  int np = 0; carray<ts> pp; int snp;
  int ns = 0, nl = 0, fi = 0; sarray<int> ss, ll;
  int mi = 0; sarray<int> dict;
  int ni = 0; sarray<ab> ii, kk;
  void resize(int c = 0)
  {
    auto i = pp.n; pp.setsize(max(c, (int)pp.n << 1));
    if ((int)kk.n < (pp.n << 1)) kk.setsize(pp.n << 1);
    if (hash + pp.n > dict.n) dict.setsize(hash + pp.n);
  }
  int project(int m)
  {
    if (m == 0 && (m = (((int)mode >> 15) & 0x12) | (((int)mode >> 17) & 1)) == 0) return m;
    for (int i = 0; i < np; i++)
    {
      auto p = &pp[i]; if ((m & 0x40) != 0) p->y = -p->y;
      auto x = *(INT64*)&p->x; auto y = *(INT64*)&p->y; auto z = *(INT64*)&p->z;
      switch (m & 0xf)
      {
      case 1: case 8: *(INT64*)&p->x = z; *(INT64*)&p->y = x; *(INT64*)&p->z = y; break;
      case 2: case 4: *(INT64*)&p->x = y; *(INT64*)&p->y = z; *(INT64*)&p->z = x; break;
      }
      if ((m & 0x10) != 0) p->y = -p->y;
    }
    return m;
  }
  int addpt(const Rational& x, const Rational& y, int v)
  {
    int h = (int)((UINT)(x.GetHashCode() + y.GetHashCode() * 13) % hash), i = dict[h] - 1;
    for (; i != -1; i = dict[hash + i] - 1)
      if (pp[i].x == x && pp[i].y == y)
        return i;
    if ((i = v) < 0)
    {
      if (np == pp.n) resize();
      auto b = &pp[-v - 1]; auto n = b->next; auto c = &pp[n < 0 ? -n - 1 : n];
      auto p = &pp[i = np++];
      p->x = x; p->y = y;
      p->z = 0 | b->z + (y - b->y) * (c->z - b->z) / (c->y - b->y);
    }
    dict[hash + i] = dict[h]; dict[h] = i + 1; return i;
  }
  static int ccw(const ts& a, const ts& b, const ts& c)
  {
    return 0 ^ (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
  }
  void fill()
  {
    qsort_s(ii.p, ni, sizeof(ab), cmp4, this);
    if (ni > (int)pp.n) resize(ii.n);
    for (int i = 0; i < ni; i++)
    {
      auto c = &pp[i]; c->ic = 0;
      c->fl = pp[ii[i].a].y.CompareTo(pp[ii[i].b].y);
    }
    if (ni > (int)ss.n) ss.setsize(ii.n);
    for (int i = 0; i < ni; i++) ss[i] = i;
    qsort_s(ss.p, ni, sizeof(int), cmp3, this); // y-max //create trapezoidal map on dict in O(n)
    memset(dict.p, 0, (mi = ni) * sizeof(int)); fs l1, l2;
    for (int i = 0, lp = -1, active = ni; i < ni; i++)
    {
      auto ip = ii[i].a; if (ip == lp) continue; lp = ip;
      auto pt = &pp[ip]; l1.k = l2.k = -1;
      for (int j = 0, k; j < active; j++)
      {
        auto ab = ii[k = ss[j]]; if (ab.a == ip || ab.b == ip) continue;
        auto c = &pp[k]; auto a = &pp[ab.a]; auto b = &pp[ab.b];
        auto y1 = c->fl < 0 ? a->y : b->y; if (y1 >= pt->y) break;
        auto y2 = c->fl < 0 ? b->y : a->y; if (y2 <= pt->y) { active--; for (auto t = j--; t < active; t++) ss[t] = ss[t + 1]; continue; }
        if (c->ic == 0)
        {
          c->ic = 1;
          c->x1 = 0 | (b->x - a->x) / (b->y - a->y);
          c->x2 = 0 | a->x - a->y * c->x1;
        }
        auto x = 0 | c->x2 + pt->y * c->x1; //auto x = a.x + (pt.y - a.y) * (b.x - a.x) / (b.y - a.y);
        auto s = pt->x.CompareTo(x);
        if (s < 0 && (l1.k == -1 || x < l1.x)) l1 = fs(k, x, c->fl, s);
        if (s > 0 && (l2.k == -1 || x > l2.x)) l2 = fs(k, x, c->fl, s);
      }
      for (int l = 0; l < 2; l++)
      {
        auto p = l == 1 ? &l2 : &l1;
        if (p->k == -1 || p->d1 * p->d2 != 1) continue;
        if (mi + 2 > (int)dict.n) dict.setsize(dict.n << 1);
        auto h = dict[p->k]; auto n = 0;
        if (p->d1 > 0) { n = h; h = mi; }
        else { if (h != 0) dict[(h >> 16) + 1] = mi; else h = mi; h = (h & 0xffff) | (mi << 16); }
        dict[p->k] = h; dict[mi++] = ip; dict[mi++] = n;
      }
    }
    //tess monotones on ss in O(n): 
    for (int i = 0, t; i < ni; i++)
      for (; (t = dict[i] & 0xffff) != 0;)
        for (int t1 = ii[i].a, t2, t3, n, l = -1;/* t != 0*/; t1 = t2, l = t, t = n)
        {
          t2 = dict[t]; t3 = (n = dict[t + 1]) != 0 ? dict[n] : ii[i].b;
          if (ccw(pp[t1], pp[t2], pp[t3]) != -1) continue; //possible: fans after sequence of left turns  
          if (ns + 3 >= (int)ss.n) ss.setsize(ss.n << 1);
          ss[ns++] = t1; ss[ns++] = t3; ss[ns++] = t2;
          dict[l == -1 ? i : l + 1] = n; break;
        }
  }
  void outline()
  {
    if (ni > (int)ll.n) ll.setsize(ii.n);
    if (np + (ni << 1) > (int)dict.n) dict.setsize(max((int)pp.n, np) + (ii.n << 1));
    memset(dict.p, 0, np * sizeof(int));
    for (int i = 0, k, j = np; i < ni; i++) { dict[j] = i; dict[j + 1] = dict[k = ii[i].a]; dict[k] = j; j += 2; }
    for (int i = 0, t; i < np; i++)
    {
      if ((t = dict[i]) == 0) continue;
      for (auto ab = nl; ;)
      {
        auto u = ii[dict[t]]; //for (auto j = t; j != 0; j = dict[j + 1]) { auto xx = ii[dict[j]]; }
        if (mode & CSG_TESS_OUTLINEPRECISE)
        {
          if (dict[t + 1] != 0) //branches
          {
            pp[u.a].next = 1 << 20;
            if (nl != ab)
            {
              for (auto j = dict[t + 1]; j != 0; j = dict[j + 1])
              {
                auto v = ii[dict[j]];
                auto d =
                  ccw(pp[ll[nl - 1]], pp[u.a], pp[u.b]) -
                  ccw(pp[ll[nl - 1]], pp[u.a], pp[v.b]);
                if (d > 0) continue;
                if (d == 0 && ccw(pp[u.a], pp[u.b], pp[v.b]) <= 0) continue;
                auto q = dict[t]; dict[t] = dict[j]; dict[j] = q; u = v;
              }
            }
          }
          if (nl != ab)
          {
            if (pp[u.a].next == (1 << 20) && ccw(pp[ll[nl - 1]], pp[u.a], pp[u.b]) == 0) goto skip;
            if (u.b == i && pp[ll[ab]].next == (1 << 20) && ccw(pp[u.a], pp[ll[ab]], pp[ll[ab + 1]]) == 0)
            {
              nl--; for (int j = ab; j < nl; j++) ll[j] = ll[j + 1];
            }
          }
        }
        ll[nl++] = u.a; skip: dict[u.a] = dict[t + 1];
        if (u.b != i) { t = dict[u.b]; continue; }
        ll[nl - 1] |= 0x40000000; i--; break;
      }
    }
  }
  void trim()
  {
    memset(dict.p, 0, this->np * sizeof(int)); auto np = 0;
    for (int i = 0; i < ns; i++) dict[ss[i]] = 1;
    for (int i = 0; i < nl; i++) dict[ll[i] & 0x0fffffff] = 1;
    for (int i = 0; i < this->np; i++) if (dict[i] != 0) { if (np != i) { auto d = &pp[np].x; auto s = &pp[i].x; d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; } dict[i] = np++; }
    if (this->np == np) return; this->np = np;
    for (int i = 0; i < ns; i++) ss[i] = dict[ss[i]];
    for (int i = 0; i < nl; i++) ll[i] = dict[ll[i] & 0x0fffffff] | (ll[i] & 0x40000000);
  }
  static int mod(int i, int k)
  {
    auto r = i % 3; return i - r + (r + k) % 3;
  }
  __forceinline bool circum(int i1, int i2, int i3, int i4)
  {
#if(USE_SSE)
    __m128d ma = _mm_load_pd((double*)&pp[i1]), mb = _mm_load_pd((double*)&pp[i2]);
    __m128d mc = _mm_load_pd((double*)&pp[i3]), md = _mm_load_pd((double*)&pp[i4]);
    __m128d mab = _mm_mul_pd(_mm_add_pd(ma, mb), _mm_set1_pd(0.5));
    __m128d mbc = _mm_mul_pd(_mm_add_pd(mb, mc), _mm_set1_pd(0.5));
    __m128d mva = _mm_sub_pd(mb, ma); mva = _mm_xor_pd(_mm_shuffle_pd(mva, mva, _MM_SHUFFLE2(0, 1)), _mm_set_pd(0.0, -0.0));
    __m128d mvb = _mm_sub_pd(mc, mb); mvb = _mm_xor_pd(_mm_shuffle_pd(mvb, mvb, _MM_SHUFFLE2(0, 1)), _mm_set_pd(0.0, -0.0));
    __m128d mf = _mm_div_pd(_mm_cross_pd(mvb, _mm_sub_pd(mab, mbc)), _mm_cross_pd(mva, mvb));
    __m128d mv = _mm_add_pd(mab, _mm_mul_pd(mva, mf));
    __m128d m1 = _mm_dot_pd(_mm_sub_pd(mv, md));
    __m128d m2 = _mm_dot_pd(_mm_sub_pd(mv, ma));
    return _mm_comige_sd(m1, m2) != 0;
#else
    auto& a = *(const Vector2*)&pp[i1].next;
    auto& b = *(const Vector2*)&pp[i2].next;
    auto& c = *(const Vector2*)&pp[i3].next;
    auto& d = *(const Vector2*)&pp[i4].next;
    auto ab = (a + b) * 0.5;
    auto bc = (b + c) * 0.5;
    auto va = ~(b - a);
    auto vb = ~(c - b);
    auto f = (vb ^ (ab - bc)) / (va ^ vb);
    auto v = ab + va * f;
    return (v - d).Dot() >= (v - a).Dot();
#endif
  }
  void optimize()
  {
    for (int i = 0; i < np; i++)
    {
      auto& p = pp[i];
      ((double*)&p)[0] = (double)p.x;
      ((double*)&p)[1] = (double)p.y;
    }
    if (ns > (int)ii.n) ii.setsize(ss.n);
    if (hash + ns > (int)dict.n) dict.setsize(hash + ss.n);
    if (ns > (int)kk.n) kk.setsize(ss.n);
    for (int i = 0; i < ns; i++) kk[i] = ab(-1, 0); memset(dict.p, 0, hash * sizeof(int));
    for (int i = 0; i < ns; i++)
    {
      auto l = ab(ss[i], ss[mod(i, 1)]);
      int h = l.hashcode() % hash, t = dict[h] - 1;
      for (; t != -1 && ii[t] != l; t = dict[hash + t] - 1);
      if (t != -1) { kk[kk[i].a = t].a = i; continue; }
      h = (ii[i] = ab(l.b, l.a)).hashcode() % hash;
      dict[hash + i] = dict[h]; dict[h] = i + 1;
    }
    for (int i = 0, s = 0, t; i < ns; i++)
    {
      if (kk[i].b == 1) continue; auto k = kk[i].a; if (k == -1) continue;
      int u1, u2, v2, i1 = ss[i], i2 = ss[u1 = mod(i, 1)], i3 = ss[u2 = mod(i, 2)], k3 = ss[v2 = mod(k, 2)];
      if (circum(i1, i2, i3, k3)) { kk[i].b = kk[k].b = 1; continue; } //ok
      ss[i] = k3; ss[k] = i3; int j = i, v1 = mod(k, 1);
      if ((t = kk[u1].a) != -1) { kk[t].b = 0; if (t < j) j = t; }
      if ((t = kk[v1].a) != -1) { kk[t].b = 0; if (t < j) j = t; }
      if ((t = kk[i].a = kk[v2].a) != -1) { kk[t].a = i; kk[t].b = 0; if (t < j) j = t; }
      if ((t = kk[k].a = kk[u2].a) != -1) { kk[t].a = k; kk[t].b = 0; if (t < j) j = t; }
      kk[kk[u2].a = v2].a = u2; if (j < i) i = j - 1; if (s++ == ns) break;
    }
  }
  static int cmp1(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorRat*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    return c.pp[a.b].y.CompareTo(c.pp[b.b].y);
  }
  static int cmp2(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorRat*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    int t = c.pp[a.a].x1.CompareTo(c.pp[b.a].x1);
    return t != 0 ? t : c.pp[a.a].x2.CompareTo(c.pp[b.a].x2);
  };
  static int cmp4(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorRat*)pc; auto& a = *(ab*)pa; auto& b = *(ab*)pb;
    auto i = c.pp[a.a].y.CompareTo(c.pp[b.a].y);
    if (i == 0) i = a.a - b.a; return i;
  };
  static int cmp3(void* pc, const void* pa, const void* pb)
  {
    auto& c = *(CTesselatorRat*)pc; auto& a = *(int*)pa; auto& b = *(int*)pb;
    return (c.pp[c.pp[a].fl < 0 ? c.ii[a].a : c.ii[a].b].y).CompareTo(c.pp[c.pp[b].fl < 0 ? c.ii[b].a : c.ii[b].b].y);
  }
  bool simplepoly()
  {
    if (np != snp) return false;
    for (UINT i = 0; i < (UINT)np - 1; i++)
      for (UINT k = i + 1; k < (UINT)np; k++)
        if (pp.p[i].a.Equals(pp.p[k].a) && pp.p[i].f.Equals(pp.p[k].f))
          return false;
    return true;
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
  HRESULT __stdcall Cut(ICSGMesh* a, CSGVAR plane);
  HRESULT __stdcall Join(ICSGMesh* a, ICSGMesh* b, CSG_JOIN op);
  //CSG extension
  struct _csg
  {
    sarray<int> ff, ii, dd, tt, tab; UINT nb = 0;
    carray<Vector3R> pp; UINT np;
    carray<Vector4R> ee; UINT ne;
    void dictee(UINT n)
    {
      ne = 0; if (ee.n < n) ee.setsize(n);
      if (dd.n < 1103 + ee.n) dd.setsize(1103 + ee.n);
      memset(dd.p, 0, 1103 * sizeof(int));
    }
    void dictpp(UINT n)
    {
      np = 0; if (pp.n < n) pp.setsize(n);
      if (dd.n < 1103 + pp.n) dd.setsize(1103 + pp.n);
      memset(dd.p, 0, 1103 * sizeof(int));
    }
    int addee(const Vector4R& v)
    {
      int h = (int)((UINT)v.GetHashCode() % 1103), i = dd[h] - 1;
      for (; i != -1; i = dd[1103 + i] - 1) if (ee[i].Equals(v)) return i;
      if (ee.n == ne) { ee.setsize(ne << 1); if (dd.n < 1103 + ee.n) dd.setsize(1103 + ee.n); }
      ee[i = ne++] = v; dd[1103 + i] = dd[h]; dd[h] = i + 1; return i;
    }
    int addpp(const Vector3R& v)
    {
      int h = (int)((UINT)v.GetHashCode() % 1103), i = dd[h] - 1;
      for (; i != -1; i = dd[1103 + i] - 1) if (pp[i].Equals(v)) return i;
      if (pp.n == np) { pp.setsize(np << 1); if (dd.n < 1103 + pp.n) dd.setsize(1103 + pp.n); }
      pp[i = np++] = v; dd[1103 + i] = dd[h]; dd[h] = i + 1; return i;
    }
    void clearab()
    {
      nb = 0; if (tab.n == 0) tab.setsize(64);
    }
    int getab(int a, int b)
    {
      UINT t = 0; for (; t < nb && (tab[t] != a || tab[t + 1] != b); t += 3);
      if (t == nb) return -1;
      auto r = tab[t + 2]; for (nb -= 3; t < nb; t++) tab[t] = tab[t + 3]; return r;
    }
    void setab(int a, int b, int v)
    {
      if (nb + 3 > tab.n) tab.setsize(tab.n << 1);
      tab[nb++] = a; tab[nb++] = b; tab[nb++] = v;
    }
    sarray<ULONGLONG> dots; int dotx;
    void begindot()
    {
      auto n = ne * (dotx = (np >> 4) + 1);
      if (dots.n < n) dots.setsize(((n >> 5) + 1) << 5);
      memset(dots.p, 0, n * sizeof(ULONGLONG));
    }
    int dot(int e, int p)
    {
      int x = e * dotx + (p >> 4), y = (p & 0xf) << 2, v = (int)(dots[x] >> y) & 0xf;
      if (v == 0) dots[x] |= (ULONGLONG)(v = 1 << (1 + (0 ^ ee[e].DotCoord(pp[p])))) << y;
      return v;
    }
    void trim(UINT ni)
    {
      ff.getptr(np); memset(ff.p, 0, np * sizeof(int));
      for (UINT i = 0; i < ni; i++) ff[ii[i]] = 1; int xp = 0;
      for (UINT i = 0; i < np; i++) if (ff[i] != 0) { if (i != xp) pp[xp] = pp[i]; ff[i] = xp++; }
      if (np != xp) { np = xp; for (UINT i = 0; i < ni; i++) ii[i] = ff[ii[i]]; }
    }
  };
  _csg csg;
  void initplanes(struct CMesh& m);
  void addvertex(const Vector3R& v);
  void setnormal(const Vector3R& v);
  void beginsex();
  void addsex(int a, int b);
  void endsex();
  void filloutlines();
  void outline(int* ii, int ni);
  int join(int ni, int fl);
};

