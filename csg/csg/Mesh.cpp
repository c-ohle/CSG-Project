#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"

HRESULT CTesselatorDbl::Update(ICSGMesh* mesh, CSGVAR v)
{
  auto& m = *static_cast<CMesh*>(mesh); bool inv = false;
  m.pp.setsize(nl != 0 ? np * 2 : np);
  m.ii.setsize(ns * 2 + nl * 6); m.ee.setsize(0);
  for (int i = 0; i < np; i++) m.pp[i] = Vector3R(&pp[i].x);
  if (nl)
  {
    Vector3R dv; if (v.count == 0) conv(&dv.z, 1, v); else conv(&dv.x, 3, v);
    for (int i = 0; i < this->np; i++) m.pp[np + i] = m.pp[i] + dv; inv = dv.z.sign() > 0;
  }
  for (int i = 0; i < ns; i++) m.ii[i] = ss[i];
  for (int i = 0, l = 0, t = ns; i < nl; i++)
  {
    int t1 = ll[i]; int e = (t1 & 0x40000000) != 0;
    int t2 = ll[e ? l : i + 1] & 0x0fffffff; if (e) { l = i + 1; t1 &= 0x0fffffff; }
    m.ii[t++] = t1; m.ii[t++] = np + t1; m.ii[t++] = t2;
    m.ii[t++] = t2; m.ii[t++] = np + t1; m.ii[t++] = np + t2;
  }
  for (int i = 0, t = ns + 6 * nl, k = nl != 0 ? np : 0; i < ns; i += 3, t += 3)
  {
    m.ii[t + 0] = k + m.ii[i + 0];
    m.ii[t + 1] = k + m.ii[i + 2];
    m.ii[t + 2] = k + m.ii[i + 1];
  }
  if (inv) m.invert();
  return 0;
}

HRESULT CTesselatorRat::Update(ICSGMesh* mesh, CSGVAR v)
{
  auto& m = *static_cast<CMesh*>(mesh); bool inv = false;
  m.pp.setsize(nl != 0 ? np * 2 : np);
  m.ii.setsize(ns * 2 + nl * 6); m.ee.setsize(0);
  for (int i = 0; i < np; i++) m.pp[i] = *(Vector3R*)&pp[i].x;
  if (nl)
  {
    Vector3R dv; if (v.count <= 1) conv(&dv.z, 1, v); else conv(&dv.x, 3, v);
    for (int i = 0; i < this->np; i++) m.pp[np + i] = m.pp[i] + dv; inv = dv.z.sign() > 0;
  }
  for (int i = 0; i < ns; i++) m.ii[i] = ss[i];
  for (int i = 0, l = 0, t = ns; i < nl; i++)
  {
    int t1 = ll[i]; int e = (t1 & 0x40000000) != 0;
    int t2 = ll[e ? l : i + 1] & 0x0fffffff; if (e) { l = i + 1; t1 &= 0x0fffffff; }
    m.ii[t++] = t1; m.ii[t++] = np + t1; m.ii[t++] = t2;
    m.ii[t++] = t2; m.ii[t++] = np + t1; m.ii[t++] = np + t2;
  }
  for (int i = 0, t = ns + 6 * nl, k = nl != 0 ? np : 0; i < ns; i += 3, t += 3)
  {
    m.ii[t + 0] = k + m.ii[i + 0];
    m.ii[t + 1] = k + m.ii[i + 2];
    m.ii[t + 2] = k + m.ii[i + 1];
  }
  if (inv) m.invert();
  if (m.ii.n) encode(m.ii.p, false);
  return 0;
}

void CTesselatorRat::initplanes(CMesh& m)
{
  if (m.ii.n == 0) return;
  if (decode(m.ii.p))
  {
    UINT c = 1; for (UINT i = 3; i < m.ii.n; i += 3) if (decode(m.ii.p + i)) c++;
    m.ee.setsize(c);
    for (UINT i = 0, k = 0; i < m.ii.n; i += 3)
      if (decode(m.ii.p + i))
        m.ee[k++] = 0 | Vector4R::PlaneFromPoints(m.pp[m.ii[i + 0]], m.pp[m.ii[i + 1]], m.pp[m.ii[i + 2]]);
    return;
  }
  auto nd = m.ii.n / 3;
  auto ff = csg.ff.getptr(nd + m.pp.n);
  auto ii = csg.ii.getptr(nd + m.ii.n);
  csg.dictee(64); memset(ff + nd, -1, m.pp.n * sizeof(int)); //for (int i = 0; i < m.pp.n; i++) ff[nd + i] = -1;
  for (UINT i = 0, k = 0, l = 0, x = 0, i1, i2, i3; k < nd; i += 3, k++)
  {
    const auto& a = m.pp[i1 = m.ii[i + 0]];
    const auto& b = m.pp[i2 = m.ii[i + 1]];
    const auto& c = m.pp[i3 = m.ii[i + 2]]; if ((ii[k] = i) == 0) goto m1;
    const auto& e = csg.ee[l];
    if (ff[nd + i1] != l) if ((0 ^ e.DotCoord(a)) != 0) goto m1;
    if (ff[nd + i2] != l) if ((0 ^ e.DotCoord(b)) != 0) goto m1;
    if (ff[nd + i3] != l) if ((0 ^ e.DotCoord(c)) != 0) goto m1;
    switch (x != -1 ? x : (x = ((const Vector3R*)&e)->LongAxis()))
    {
    case 0: if ((0 ^ (b.y - a.y) * (c.z - a.z) - (b.z - a.z) * (c.y - a.y)) != e.x.sign()) goto m1; break;
    case 1: if ((0 ^ (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z)) != e.y.sign()) goto m1; break;
    case 2: if ((0 ^ (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) != e.z.sign()) goto m1; break;
    }
    ff[k] = l; goto m2; m1: //TRACE(L"plane %i at %i\n", csg.ne, i);
    ff[k] = l = csg.addee(0 | Vector4R::PlaneFromPoints(a, b, c)); x = -1; m2:
    ff[nd + i1] = ff[nd + i2] = ff[nd + i3] = l;
  }
  qsort(ff, ii, 0, nd - 1);
  for (UINT i = 0, j = 0; i < nd; i++, j += 3) for (UINT k = 0; k < 3; k++) ii[nd + j + k] = m.ii[ii[i] + k];
  m.ii.copy((const UINT*)ii + nd, m.ii.n);
  for (UINT i = 0, k = 0; i < m.ii.n; i += 3, k++) encode(m.ii.p + i, k == 0 || ff[k - 1] != ff[k]);
  m.ee.copy(csg.ee.p, csg.ne);
}

HRESULT CTesselatorRat::Cut(ICSGMesh* mesh, CSGVAR vplane)
{
  auto& m = *static_cast<CMesh*>(mesh);
  if (!m.ee.n) initplanes(m); if (vplane.vt == 0) return 0;
  Vector4R plane; conv(&plane.x, 4, vplane);
  int np = m.pp.n, xe = np, mf = 0; auto ff = csg.ff.getptr(np + m.ee.n + 1);
  for (int i = 0; i < np; i++) mf |= ff[i] = 1 << (1 + (0 ^ plane.DotCoord(m.pp[i])));
  if (mf == 1) return 0;
  if (mf == 4) { m.clear(); return 0; }
  auto vv = csg.pp.getptr(csg.np = np); for (int i = 0; i < np; i++) vv[i] = m.pp[i];
  auto nk = 0; auto kk = csg.ii.getptr(m.ii.n); int ss[4];
  auto nt = 0; auto tt = csg.tt.getptr(128); csg.clearab();
  mode = (CSG_TESS)(CSG_TESS_POSITIVE | CSG_TESS_INDEXONLY | CSG_TESS_NOTRIM | CSG_TESS_FILL);
  for (int e = 0, h = 0, b, fl, x; h < (int)m.ii.n; h = b, e++)
  {
    for (b = h, fl = 0; ;)
    {
      fl |= ff[m.ii[b]] | ff[m.ii[b + 1]] | ff[m.ii[b + 2]];
      b += 3; if (b == m.ii.n || decode(m.ii.p + b)) break;
    }
    if ((fl & 4) == 0)
    {
      if (fl == 2) if (!((const Vector3R*)&plane)->Equals(*((const Vector3R*)&m.ee[e]))) continue; //backface
      kk = csg.ii.getptr(nk + (b - h)); for (int i = h; i < b; i++) kk[nk++] = m.ii[i]; ff[xe++] = e; continue;
    }
    if ((fl & 1) == 0) continue;
    setnormal(*(const Vector3R*)&m.ee[e].x);
    beginsex(); vv = csg.pp.getptr(csg.np + (x = (b - h) / 3 + 1)); tt = csg.tt.getptr(nt + x);
    for (int i = h; i < b; i += 3)
    {
      int ns = 0;
      for (int k = 0; k < 3; k++)
      {
        int z, ik, iz, f1 = ff[ik = m.ii[i + k]], f2 = ff[iz = m.ii[i + (z = (k + 1) % 3)]];
        if ((f1 & 4) == 0) ss[ns++] = ik; if (f1 == f2 || ((f1 | f2) & 2) != 0) continue;
        auto t = csg.getab(iz, ik); if (t != -1) { ss[ns++] = t; continue; }
        csg.setab(ik, iz, ss[ns++] = csg.np);
        vv[csg.np++] = 0 | plane.Intersect(m.pp[ik], m.pp[iz]);
      }
      for (int k = 0, l = ns - 1; k < ns; l = k++)
      {
        addsex(ss[l], ss[k]);
        if (ss[l] < np && ff[ss[l]] != 2) continue;
        if (ss[k] < np && ff[ss[k]] != 2) continue;
        tt[nt++] = ss[k]; tt[nt++] = ss[l];
      }
    }
    endsex();
    filloutlines();
    kk = csg.ii.getptr(nk + this->ns); auto j = nk;
    for (int i = 0; i < this->ns; i++) kk[nk++] = this->ll[this->ss[i]] & 0x0fffffff;
    for (int i = j; i < nk; i += 3) encode((UINT*)kk + i, i == j); ff[xe++] = e;
  }
  if (nt != 0)
  {
    beginsex();
    for (int i = 0; i < nt; i += 2) addsex(tt[i], tt[i + 1]);
    endsex();
    if (this->nl != 0)
    {
      setnormal(*(const Vector3R*)&plane.x);
      filloutlines();
      kk = csg.ii.getptr(nk + this->ns); auto j = nk;
      for (int i = 0; i < this->ns; i++) kk[nk++] = this->ll[this->ss[i]] & 0x0fffffff;
      for (int i = j; i < nk; i += 3) encode((UINT*)kk + i, i == j); ff[xe++] = -1;
    }
  }
  m.ee.setsize(xe); xe -= np; auto dd = m.ee.p;
  for (int i = 0, k; i < xe; i++) if ((k = ff[np + i]) == -1) dd[i] = plane; else if (i != k) dd[i] = dd[k];
  csg.trim(nk);
  m.pp.copy(csg.pp.p, csg.np);
  m.ii.copy((const UINT*)csg.ii.p, nk);

  m.ee.setsize(0); if (m.ii.n != 0) encode(m.ii.p, false);

  return 0;
}

HRESULT CTesselatorRat::Join(ICSGMesh* pa, ICSGMesh* pb, CSG_JOIN op)
{
  auto& a = *static_cast<CMesh*>(pa);
  auto& b = *static_cast<CMesh*>(pb);
  if (!a.ee.n) initplanes(a);
  if (!b.ee.n) initplanes(b);
  UINT ni = 0, ne = 0, an = a.pp.n, bn = b.pp.n, cn, dz, mp = op & 3;
  auto ii = csg.ii.getptr(a.ii.n + b.ii.n); int ss[2];
  auto tt = csg.tt.getptr((cn = an + bn) + b.ee.n);
  auto ff = csg.ff.getptr(dz = a.ee.n + b.ee.n); memset(ff, 0, dz * sizeof(int));
  csg.dictee(a.ee.n + b.ee.n);
  for (UINT i = 0; i < a.ee.n; i++) csg.addee(a.ee[i]);
  for (UINT i = 0; i < b.ee.n; i++) if ((tt[cn + i] = csg.addee(mp == 1 ? -b.ee[i] : b.ee[i])) < (int)a.ee.n) ff[tt[cn + i]] = 8;
  csg.dictpp(cn);
  for (UINT i = 0; i < an; i++) tt[/* */i] = csg.addpp(a.pp[i]);
  for (UINT i = 0; i < bn; i++) tt[an + i] = csg.addpp(b.pp[i]);
  csg.begindot(); auto dir = a.ee.n > b.ee.n;
  for (UINT i = 0, pc = bn != 0 ? csg.ne : 0; i < pc; i++)
  {
    UINT e = dir ? pc - 1 - i : i; if (ff[e] != 0) continue;
    UINT af = 0, bf = 0, cf = dir ? 6 : 3; rep:
    if ((cf & 1) != 0)
    {
      for (UINT t = 0; t < an; t++) af |= csg.dot(e, t);
      if (af == 1 || af == 4)
      {
        if (mp == 1 ? (af == 1 && bf == 6) : (af == 4 && bf == 3)) goto ex;
        ff[e] = mp == 0 ? 3 : 1; continue;
      }
    }
    if ((cf & 2) != 0)
    {
      for (UINT t = 0; t < bn; t++) bf |= csg.dot(e, tt[an + t]);
      if (bf == 1 || bf == 4)
      {
        if (bf == 4 && af == 3) goto ex;
        ff[e] = mp == 2 ? 1 : 2; continue;
      }
    }
    if ((cf >>= 2) != 0) goto rep;
    if (bf == (mp == 1 ? 6 : 3)) //convex b
    {
      if ((af & (mp == 1 ? 1 : 4)) == 0) continue;
      for (UINT j = 0, k, o = 0, t, f; j < a.ii.n; j = k, o++)
      {
        for (k = j + 3; k < a.ii.n && !decode(a.ii.p + k); k += 3);
        if (ff[o] != 0) continue;
        for (t = j, f = 0; t < k; t++) f |= csg.dot(e, a.ii[t]);
        if (f == (mp == 1 ? 1 : 4)) ff[o] = mp == 2 ? 1 : 2;
      }
      continue;
    }
    if (af == 3) //convex a
    {
      if ((bf & 4) == 0) continue;
      for (UINT j = 0, k, o = 0, t, f, z; j < b.ii.n; j = k, o++)
      {
        for (k = j + 3; k < b.ii.n && !decode(b.ii.p + k); k += 3);
        if (ff[z = tt[cn + o]] != 0) continue;
        for (t = j, f = 0; t < k; t++) f |= csg.dot(e, tt[an + b.ii[t]]);
        if (f == 4) ff[z] = mp == 0 ? 3 : 1;
      }
    }
    continue; ex:;
    if (mp == 0) //a + b
    {
      auto an = a.pp.n; a.pp.setsize(a.pp.n + b.pp.n); for (UINT i = 0; i < b.pp.n; i++) a.pp[an + i] = b.pp[i];
      auto in = a.ii.n; a.ii.setsize(a.ii.n + b.ii.n); for (UINT i = 0; i < b.ii.n; i++) a.ii[in + i] = an + b.ii[i];
      if (csg.ne < a.ee.n + b.ee.n) { a.ee.setsize(0); if (a.ii.p) encode(a.ii.p, false); }
      else { auto en = a.ee.n; a.ee.setsize(a.ee.n + b.ee.n); for (UINT i = 0; i < b.ee.n; i++) a.ee[en + i] = b.ee[i]; }
      return 0;
    }
    if (mp != 1) a.clear(); return 0;
  }
  mode = (CSG_TESS)((mp == 0 ? CSG_TESS_POSITIVE : mp == 1 ? CSG_TESS_ABSGEQTWO : CSG_TESS_GEQTHREE) | CSG_TESS_FILL | CSG_TESS_NOTRIM);
  for (int e = 0, i0, i1, i2; e < (int)csg.ne; e++)
  {
    auto hh = 0;
    switch (ff[e])
    {
    case 1: continue;
    case 2: goto ta;
    case 3: goto tb;
    }
    auto& p = csg.ee[e];
    beginsex();
    for (int r = 0; r < 2; r++)
    {
      auto& m = r == 0 ? a : b; auto d = r == 0 ? 0 : an;
      auto d1 = mp == 1 ? r == 1 : false;
      auto d2 = mp == 0 ? 0 : d1 ? 1 : 0;
      auto d3 = mp == 0 ? 4 : 1; csg.clearab();
      for (int i = 0, o = -1, j; i < (int)m.ii.n; i += 3)
      {
        if (decode(m.ii.p + i)) o++;
        auto f = csg.dot(e, tt[d + m.ii[i + 0]]) | csg.dot(e, tt[d + m.ii[i + 1]]) | csg.dot(e, tt[d + m.ii[i + 2]]);
        if (f == 1 || f == 4) continue;
        if (f == 2)
        {
          for (j = i; i + 3 < (int)m.ii.n && !decode(m.ii.p + (i + 3)); i += 3);
          if (e != (r == 0 ? o : tt[cn + o])) continue;
          for (; j <= i; j += 3)
          {
            addsex(i0 = tt[d + m.ii[j + 0]], i1 = tt[d + m.ii[j + (d1 ? 2 : 1)]]);
            addsex(i1, i2 = tt[d + m.ii[j + (d1 ? 1 : 2)]]); addsex(i2, i0);
          }
          continue;
        }
        if (f == (7 & ~d3)) continue; if ((op & 0x20) != 0) continue; //pure plane retess
        int ns = 0, s = 0; for (; csg.dot(e, tt[d + m.ii[i + s]]) != d3; s++);
        for (int k = 0, u, v; k < 3; k++)
        {
          auto f1 = csg.dot(e, tt[d + m.ii[u = i + (k + s) % 3]]);
          if (f1 == 2) { ss[ns++] = tt[d + m.ii[u]]; continue; }
          auto f2 = csg.dot(e, tt[d + m.ii[v = i + (k + s + 1) % 3]]); if (f1 == f2 || ((f1 | f2) & 2) != 0) continue;
          int t = csg.getab(m.ii[u], m.ii[v]); if (t != -1) { ss[ns++] = t; continue; }
          ss[ns++] = csg.addpp(0 | p.Intersect(m.pp[m.ii[u]], m.pp[m.ii[v]]));
          csg.setab(m.ii[v], m.ii[u], ss[ns - 1]);
        }
        if (ns == 2) addsex(ss[d2 ^ 1], ss[d2]);
      }
    }
    endsex();
    setnormal(*(const Vector3R*)&p); auto nc = this->nl;
    filloutlines();
    auto ic = this->ns; if (ic == 0) { ff[e] = 1; continue; }
    ii = csg.ii.getptr(ni + ic); auto at = ni;
    for (int i = 0, t; i < ic; i++) ii[ni++] = (t = this->ss[i]) < nc ? this->ll[t] & 0x0fffffff : csg.addpp(*(const Vector3R*)&this->pp[t].x);
    goto encode; tb: hh = 1; ta: auto& c = hh == 0 ? a : b;
    for (i1 = 0, i0 = 0; i1 < (int)c.ii.n && !(decode(c.ii.p + i1) && (e == (hh == 0 ? i0++ : tt[cn + i0++]))); i1 += 3);
    for (i2 = i1; ;) { i2 += 3; if (i2 == c.ii.n || decode(c.ii.p + i2)) break; }
    ii = csg.ii.getptr(ni + (i2 - i1)); auto of = hh == 0 ? 0 : an; at = ni;
    for (int i = i1; i < i2; i++) ii[ni++] = tt[of + c.ii[i]]; encode: _ASSERT(at != ni);
    for (int i = at; i < (int)ni; i += 3) encode((UINT*)ii + i, i == at); ne++;
  }
  if ((op & 0x40) == 0 && (ni = join(ni, 0)) == -1)
  {
    if (op == 0x10) { Join(pa, pb, (CSG_JOIN)(0x20 | 0x40)); return Join(pa, pb, (CSG_JOIN)0x80); }
    return -1; //degenerated input mesh
  }
  a.ee.copy(csg.ee.p, csg.ne);
  csg.trim(ni);
  a.pp.copy(csg.pp.p, csg.np);
  a.ii.copy((const UINT*)csg.ii.p, ni);

  a.ee.setsize(0); if (a.ii.n != 0) encode(a.ii.p, false);

  return 0;
}

#if(CSG_EXTENSION)
void CTesselatorRat::setnormal(const Vector3R& v)
{
  auto i = v.LongAxis(); auto s = (&v.x)[i].sign();
  mode = (CSG_TESS)((mode & 0xffff) | ((int)CSG_TESS_NORMX << i) | (s & (int)CSG_TESS_NORMNEG));
}
void CTesselatorRat::addvertex(const Vector3R& v)
{
  if (np == pp.n) resize(64);
  if (np != fi) pp[np - 1].next = np;
  auto a = &pp[np++]; a->next = fi; *(Vector3R*)&a->x = v;
}
void CTesselatorRat::beginsex()
{
  if (dict.n == 0)
  {
    dict.setsize(hash + 32);
    ii.setsize(64);
  }
  np = ns = nl = ni = 0; memset(dict.p, 0, hash * sizeof(int));
}
void CTesselatorRat::addsex(int a, int b)
{
  for (int i = dict[b % hash] - 1; i != -1; i = dict[hash + i] - 1)
    if (ii[i].a == b && ii[i].b == a) { ii[i].a = -1; return; }
  if (ni >= (int)ii.n)
    ii.setsize(ni << 1);
  if (hash + ni >= (int)dict.n)
    dict.setsize(hash + ii.n);
  auto h = a % hash; dict[hash + ni] = dict[h]; dict[h] = ni + 1;
  ii[ni++] = ab(a, b); np = max(np, max(a, b) + 1);
}
void CTesselatorRat::endsex()
{
  int k = 0; for (int i = 0; i < ni; i++) if (ii[i].a != -1) ii[k++] = ii[i]; ni = k;
  _ASSERT((mode & CSG_TESS_OUTLINEPRECISE) == 0); outline();
}
void CTesselatorRat::filloutlines()
{
  BeginPolygon();
  for (int i = 0, f = 0, n = nl; i < n; i++)
  {
    if (f == 0) { BeginContour(); f = 1; }
    auto k = ll[i]; addvertex(csg.pp[k & 0x0fffffff]);
    if ((k & 0x40000000) != 0) { EndContour(); f = 0; }
  }
  EndPolygon();
}
void CTesselatorRat::outline(int* ii, int ni)
{
  beginsex();
  for (int i = 0; i < ni; i += 3)
  {
    addsex(ii[i + 0], ii[i + 1]);
    addsex(ii[i + 1], ii[i + 2]);
    addsex(ii[i + 2], ii[i + 0]);
  }
  endsex();
}
int CTesselatorRat::join(int ni, int fl)
{
  for (int swap = 0; ;)
  {
    auto ii = csg.ii.p; outline(ii, ni); if (nl == 0) break;
    for (int i = 0, n = nl, k, x; i < n; i = k)
    {
      for (k = i + 1; k < n && (ll[k - 1] & 0x40000000) == 0; k++);
      for (int l = k - i, t = 0, t1, t2, t3, u; t < l; t++)
      {
        auto s = Vector3R::Inline(
          csg.pp[t1 = ll[i + t] & 0x0fffffff],
          csg.pp[t2 = ll[i + (t + 1) % l] & 0x0fffffff],
          csg.pp[t3 = ll[i + (t + 2) % l] & 0x0fffffff], 2);
        if (s == 2 && fl == 1) { if (k == n) return ni; swap++; break; }
        if (s == 0 || s == 2) continue;
        if (s < 0) { s = t1; t1 = t2; t2 = t3; t3 = s; }
        for (x = 0; x < ni && !(ii[x] == t1 && ii[x / 3 * 3 + (x + 1) % 3] == t2); x++);
        ii = csg.ii.getptr(ni + 3); u = x / 3 * 3;
        memcpy(ii + (u + 3), ii + u, (ni - u) * sizeof(int)); ni += 3;
        auto c = decode((UINT*)ii + u); ii[x] = ii[u + 3 + (x + 1) % 3] = t3;
        encode((UINT*)ii + u, c); encode((UINT*)ii + (u + 3), false); swap++; break;
      }
    }
    if (swap == 0)
      return -1;
    swap = 0;
  }
  return ni;
}
#endif