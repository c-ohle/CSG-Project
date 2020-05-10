#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"


HRESULT CTesselatorRat::Cut(ICSGMesh* mesh, CSGVAR vplane)
{
  auto& m = *static_cast<CMesh*>(mesh);
  if (!m.ee.n) initplanes(m); if (vplane.vt == 0) return 0;
  Vector4R plane; conv(&plane.x, 4, vplane);
  UINT np = m.pp.n, xe = np, mf = 0; auto ff = csg.ff.getptr(np + m.ee.n + 1);
  for (UINT i = 0; i < np; i++) mf |= ff[i] = 1 << (1 + (0 ^ plane.DotCoord(m.pp[i])));
  if (mf == 1) return 0;
  if (mf == 4) { m.clear(); return 0; }
  auto vv = csg.pp.getptr(csg.np = np); for (UINT i = 0; i < np; i++) vv[i] = m.pp[i];
  auto nk = 0; auto kk = csg.ii.getptr(m.ii.n); UINT ss[4];
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
      UINT ns = 0;
      for (int k = 0; k < 3; k++)
      {
        int z, ik, iz, f1 = ff[ik = m.ii[i + k]], f2 = ff[iz = m.ii[i + (z = (k + 1) % 3)]];
        if ((f1 & 4) == 0) ss[ns++] = ik; if (f1 == f2 || ((f1 | f2) & 2) != 0) continue;
        auto t = csg.getab(iz, ik); if (t != -1) { ss[ns++] = t; continue; }
        csg.setab(ik, iz, ss[ns++] = csg.np);
        vv[csg.np++] = 0 | plane.Intersect(m.pp[ik], m.pp[iz]);
      }
      for (UINT k = 0, l = ns - 1; k < ns; l = k++)
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
  xe -= np; if (xe > m.ee.n) m.ee.setsize(xe);
  for (UINT i = 0, k; i < xe; i++) if ((k = ff[np + i]) == -1) m.ee[i] = plane; else if (i != k) m.ee[i] = m.ee[k];
  m.ee.setsize(xe);
  csg.trim(nk);
  m.pp.copy(csg.pp.p, csg.np);
  m.ii.copy((const UINT*)csg.ii.p, nk); m.rtgen = getrtid();
  return 0;
}

concurrency::combinable<ATL::CComPtr<CTesselatorRat>> __tess;
concurrency::critical_section __crit;

HRESULT CTesselatorRat::Join(ICSGMesh* pa, ICSGMesh* pb, CSG_JOIN op)
{
  if (!pb) { memset(pb = (CMesh*)_alloca(sizeof(CMesh)), 0, sizeof(CMesh)); op = (CSG_JOIN)(op | 0x10); }
  auto& a = *static_cast<CMesh*>(pa);
  auto& b = *static_cast<CMesh*>(pb);
  if (!a.ee.n) initplanes(a);
  if (!b.ee.n) initplanes(b);
  UINT ni = 0, ne = 0, an = a.pp.n, bn = b.pp.n, cn, dz, mp = op & 3;
  auto ii = csg.ii.getptr(a.ii.n + b.ii.n);
  auto tt = csg.tt.getptr((cn = an + bn) + b.ee.n);
  auto ff = csg.ff.getptr((dz = a.ee.n + b.ee.n) << 1); memset(ff, 0, dz * sizeof(int)); auto fm = ff + dz;
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
      auto an = a.pp.n; a.pp.setsize(a.pp.n + b.pp.n); for (UINT i = 0; i < b.pp.n; i++) a.pp.p[an + i] = b.pp.p[i];
      auto in = a.ii.n; a.ii.setsize(a.ii.n + b.ii.n); for (UINT i = 0; i < b.ii.n; i++) a.ii.p[in + i] = an + b.ii.p[i];
      if (csg.ne < a.ee.n + b.ee.n) a.resetee();
      else { auto en = a.ee.n; a.ee.setsize(a.ee.n + b.ee.n); for (UINT i = 0; i < b.ee.n; i++) a.ee[en + i] = b.ee[i]; }
      a.rtgen = getrtid(); return 0;
    }
    if (mp != 1) { a.clear(); } return 0;
  }
  mode = (CSG_TESS)((mp == 0 ? CSG_TESS_POSITIVE : mp == 1 ? CSG_TESS_ABSGEQTWO : CSG_TESS_GEQTHREE) | CSG_TESS_FILL | CSG_TESS_NOTRIM);
#if(1)
  csg._pp_.copy(csg.pp.p, csg.np);
  concurrency::parallel_for(size_t(0), size_t(csg.ne), [&](size_t _i)
    {
      auto e = (UINT)_i; auto hh = 0;
      switch (ff[e])
      {
      case 1: return;
      case 2: __crit.lock(); goto ta;
      case 3: __crit.lock(); goto tb;
      }
      auto& rt = __tess.local(); if (!rt.p) rt.p = new CTesselatorRat(); auto& tess = *rt.p;
      tess.mode = mode;
      tess.csg.dictpp(32);
      tess.beginsex();
      const auto& plane = csg.ee[e];
      for (int r = 0; r < 2; r++)
      {
        auto& m = r == 0 ? a : b; auto d = r == 0 ? 0 : an;
        auto d1 = mp == 1 ? r == 1 : false;
        auto d2 = mp == 0 ? 0 : d1 ? 1 : 0;
        auto d3 = mp == 0 ? 4 : 1; tess.csg.clearab();
        for (int i = 0, o = -1, j; i < (int)m.ii.n; i += 3)
        {
          if (decode(m.ii.p + i)) o++;
          auto f = csg._dot_(e, tt[d + m.ii[i + 0]]) | csg._dot_(e, tt[d + m.ii[i + 1]]) | csg._dot_(e, tt[d + m.ii[i + 2]]);
          if (f == 1 || f == 4) continue;
          if (f == 2)
          {
            for (j = i; i + 3 < (int)m.ii.n && !decode(m.ii.p + i + 3); i += 3);
            if (e != (r == 0 ? o : tt[cn + o])) continue;
            for (int i0, i1, i2; j <= i; j += 3)
            {
              tess.addsex(i0 = tt[d + m.ii[j + 0]], i1 = tt[d + m.ii[j + (d1 ? 2 : 1)]]);
              tess.addsex(i1, i2 = tt[d + m.ii[j + (d1 ? 1 : 2)]]); tess.addsex(i2, i0);
            }
            continue;
          }
          if (f == (7 & ~d3)) continue;
          if ((op & 0x20) != 0) continue; //pure plane retess
          int ss[2], ns = 0, s = 0; for (; csg._dot_(e, tt[d + m.ii[i + s]]) != d3; s++);
          for (int k = 0, u, v; k < 3; k++)
          {
            auto f1 = csg._dot_(e, tt[d + m.ii[u = i + (k + s) % 3]]);
            if (f1 == 2) { ss[ns++] = tt[d + m.ii[u]]; continue; }
            auto f2 = csg._dot_(e, tt[d + m.ii[v = i + (k + s + 1) % 3]]); if (f1 == f2 || ((f1 | f2) & 2) != 0) continue;
            int t = tess.csg.getab(m.ii[u], m.ii[v]); if (t != -1) { ss[ns++] = t; continue; }
            auto sp = 0 | plane.Intersect(m.pp[m.ii[u]], m.pp[m.ii[v]]);
            ss[ns++] = cn + tess.csg.addpp(sp);
            tess.csg.setab(m.ii[v], m.ii[u], ss[ns - 1]);
          }
          if (ns == 2) tess.addsex(ss[d2 ^ 1], ss[d2]);
        }
      }
      tess.endsex(); auto nnl = tess.nl;
      tess.setnormal(*(const Vector3R*)&plane);
      tess.BeginPolygon();
      for (int i = 0, f = 0, n = tess.nl; i < n; i++)
      {
        if (f == 0) { tess.BeginContour(); f = 1; }
        UINT k = tess.ll[i], j = k & 0x0fffffff; tess.addvertex(j < cn ? csg._pp_[j] : tess.csg.pp[j - cn]);
        if ((k & 0x40000000) != 0) { tess.EndContour(); f = 0; }
      }
      tess.EndPolygon();
      auto ic = tess.ns; if (ic == 0) return;
      __crit.lock(); ii = csg.ii.getptr(ni + ic); auto at = ni;
      //for (int i = 0; i < ic; i++) ii[ni++] = csg.addpp(*(const Vector3R*)&tess.pp[tess.ss[i]].x);
      for (int i = 0; i < tess.np; i++) tess.pp.p[i].ic = -1;
      for (int i = 0; i < ic; i++) { auto& r = tess.pp[tess.ss[i]]; ii[ni++] = r.ic != -1 ? r.ic : (r.ic = csg.addpp(*(const Vector3R*)&r.x)); }
      goto encode; tb: hh = 1; ta: auto& c = hh == 0 ? a : b;
      int i1 = 0, i0 = 0, i2; for (; i1 < (int)c.ii.n && !(decode(c.ii.p + i1) && (e == (hh == 0 ? i0++ : tt[cn + i0++]))); i1 += 3);
      for (i2 = i1; ;) { i2 += 3; if (i2 == c.ii.n || decode(c.ii.p + i2)) break; }
      ii = csg.ii.getptr(ni + (i2 - i1)); auto of = hh == 0 ? 0 : an; at = ni;
      for (int i = i1; i < i2; i++) ii[ni++] = tt[of + c.ii[i]]; encode: _ASSERT(at != ni);
      for (int i = at; i < (int)ni; i += 3) encode((UINT*)ii + i, i == at); fm[ne++] = e;
      __crit.unlock();
    });//, concurrency::static_partitioner());
#else
  for (int e = 0, i0, i1, i2; e < (int)csg.ne; e++)
  {
    auto hh = 0;
    switch (ff[e])
    {
    case 1: continue;
    case 2: goto ta;
    case 3: goto tb;
    }
    auto& plane = csg.ee[e];
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
          for (j = i; i + 3 < (int)m.ii.n && !decode(m.ii.p + i + 3); i += 3);
          if (e != (r == 0 ? o : tt[cn + o])) continue;
          for (; j <= i; j += 3)
          {
            addsex(i0 = tt[d + m.ii[j + 0]], i1 = tt[d + m.ii[j + (d1 ? 2 : 1)]]);
            addsex(i1, i2 = tt[d + m.ii[j + (d1 ? 1 : 2)]]); addsex(i2, i0);
          }
          continue;
        }
        if (f == (7 & ~d3)) continue;
        if ((op & 0x20) != 0) continue; //pure plane retess
        int ss[2], ns = 0, s = 0; for (; csg.dot(e, tt[d + m.ii[i + s]]) != d3; s++);
        for (int k = 0, u, v; k < 3; k++)
        {
          auto f1 = csg.dot(e, tt[d + m.ii[u = i + (k + s) % 3]]);
          if (f1 == 2) { ss[ns++] = tt[d + m.ii[u]]; continue; }
          auto f2 = csg.dot(e, tt[d + m.ii[v = i + (k + s + 1) % 3]]); if (f1 == f2 || ((f1 | f2) & 2) != 0) continue;
          int t = csg.getab(m.ii[u], m.ii[v]); if (t != -1) { ss[ns++] = t; continue; }
          ss[ns++] = csg.addpp(0 | plane.Intersect(m.pp[m.ii[u]], m.pp[m.ii[v]]));
          csg.setab(m.ii[v], m.ii[u], ss[ns - 1]);
        }
        if (ns == 2) addsex(ss[d2 ^ 1], ss[d2]);
      }
    }
    endsex(); auto nc = this->nl;
    setnormal(*(const Vector3R*)&plane);
    filloutlines();
    auto ic = this->ns; if (ic == 0) { ff[e] = 1; continue; }
    ii = csg.ii.getptr(ni + ic); auto at = ni;
    for (int i = 0, t; i < ic; i++) ii[ni++] = (t = this->ss[i]) < nc ? this->ll[t] & 0x0fffffff : csg.addpp(*(const Vector3R*)&this->pp[t].x);
    goto encode; tb: hh = 1; ta: auto& c = hh == 0 ? a : b;
    for (i1 = 0, i0 = 0; i1 < (int)c.ii.n && !(decode(c.ii.p + i1) && (e == (hh == 0 ? i0++ : tt[cn + i0++]))); i1 += 3);
    for (i2 = i1; ;) { i2 += 3; if (i2 == c.ii.n || decode(c.ii.p + i2)) break; }
    ii = csg.ii.getptr(ni + (i2 - i1)); auto of = hh == 0 ? 0 : an; at = ni;
    for (int i = i1; i < i2; i++) ii[ni++] = tt[of + c.ii[i]]; encode: _ASSERT(at != ni);
    for (int i = at; i < (int)ni; i += 3) encode((UINT*)ii + i, i == at); fm[ne++] = e;
  }
#endif
  if ((op & 0x40) == 0 && (ni = join(ni, 0)) == -1)
  {
    if (op == 0x10) { Join(pa, pb, (CSG_JOIN)(0x20 | 0x40)); return Join(pa, pb, (CSG_JOIN)0x80); }
    return 0x8C066001; //degenerated input mesh
  }
  a.ee.setsize(ne); for (UINT i = 0; i < ne; i++) a.ee[i] = csg.ee[fm[i]];
  csg.trim(ni);
  a.pp.copy(csg.pp.p, csg.np);
  a.ii.copy((const UINT*)csg.ii.p, ni); a.rtgen = getrtid();
  return 0;
}
