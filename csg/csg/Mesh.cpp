#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"

HRESULT CTesselatorRat::Update(ICSGMesh* mesh, CSGVAR v, UINT flags)
{
  auto& m = *static_cast<CMesh*>(mesh); m.resetee(); bool inv = false;
  UINT mo = (flags >> 16) & 0x0f, nn = nl != 0 ? max(1, flags & 0x0000ffff) : 0, nb = ns ? nn : nn + 1, nt = np;
  if (mo & 3)
  {
    for (UINT i = nt = 0; i < (UINT)np; i++)
    {
      auto& t = pp.p[i]; const Vector3R& p = *(const Vector3R*)&t.x;
      if (t.ic = (&t.x)[mo & 1].sign()) pp.p[t.t1 = nt++].t2 = i;
    }
  }
  m.pp.setsize(np + nt * nn);
  for (UINT i = 0; i < (UINT)np; i++) m.pp[i] = *(Vector3R*)&pp[i].x;
  if (nn)
  {
    switch (mo)
    {
    case 0:
    {
      Rational dz; conv(&dz, 1, v); inv = dz.sign() > 0;
      for (UINT i = 0, n = nn * np; i < n; i++)
      {
        const auto& s = m.pp[i]; auto& d = m.pp[np + i];
        d.x = s.x; d.y = s.y; d.z = s.z + dz;
      }
      break;
    }
    case 1:
    case 2:
    {
      double a; conv(&a, 1, v); if (!a) a = 2 * M_PI * nn / (nn + 1); inv = a > 0;
      for (UINT j = 1, l = np; j <= nn; j++)
      {
        auto sc = Vector2R::SinCos(a * j / nn, (char)(flags >> 24));
        for (UINT i = 0; i < nt; i++, l++)
        {
          const auto& s = m.pp[pp[i].t2]; auto& d = m.pp[l];
          d.x = mo == 1 ? s.x : s.x * sc.x;
          d.y = mo == 1 ? s.y * sc.x : s.y;
          d.z = mo == 1 ? s.y * sc.y : s.x * sc.y;
        }
      }
      break;
    }
    }
  }
  m.ii.setsize(ns * 2 + (nl + nt - np) * nb * 6);
  memcpy(m.ii.p, ss.p, ns << 2); UINT t = ns;
  for (UINT j = 0, k1 = 0, k2 = np; j < nb; j++, k1 = k2, k2 = j != nn ? k2 + nt : 0)
    for (UINT i = 0, l = 0; i < (UINT)nl; i++)
    {
      UINT t1 = ll[i]; auto e = (t1 & 0x40000000) != 0;
      UINT t2 = ll[e ? l : i + 1] & 0x0fffffff; if (e) { l = i + 1; t1 &= 0x0fffffff; }
      UINT l1 = nt == np ? k1 + t1 : k1 == 0 || !pp.p[t1].ic ? t1 : k1 + pp.p[t1].t1;
      UINT l2 = nt == np ? k2 + t1 : k2 == 0 || !pp.p[t1].ic ? t1 : k2 + pp.p[t1].t1;
      UINT l3 = nt == np ? k1 + t2 : k1 == 0 || !pp.p[t2].ic ? t2 : k1 + pp.p[t2].t1;
      UINT l4 = nt == np ? k2 + t2 : k2 == 0 || !pp.p[t2].ic ? t2 : k2 + pp.p[t2].t1;
      if (l1 != l2) { m.ii.p[t + 0] = l1; m.ii.p[t + 1] = l2; m.ii.p[t + 2] = l3; t += 3; }
      if (l3 != l4) { m.ii.p[t + 0] = l3; m.ii.p[t + 1] = l2; m.ii.p[t + 2] = l4; t += 3; }
    }
  for (UINT i = 0, k = np + nt * (nn - 1); i < (UINT)ns; i += 3, t += 3)
  {
    m.ii.p[t + 0] = nt == np ? k + ss[i + 0] : !pp.p[ss[i + 0]].ic ? ss[i + 0] : k + pp.p[ss[i + 0]].t1;
    m.ii.p[t + 1] = nt == np ? k + ss[i + 2] : !pp.p[ss[i + 2]].ic ? ss[i + 2] : k + pp.p[ss[i + 2]].t1;
    m.ii.p[t + 2] = nt == np ? k + ss[i + 1] : !pp.p[ss[i + 1]].ic ? ss[i + 1] : k + pp.p[ss[i + 1]].t1;
  }
  _ASSERT(t == m.ii.n);
  if (inv) m.invert();
  m.flags = !nn && m.ii.n ? 2 : 0;
  /// todo: the other cases
  if (nn && mo == 0 && nn == 1 && simplepoly())
  {
    UINT e = m.ii.n - ns;
    for (UINT i = 0; i < (UINT)ns; i += 3) { encode(m.ii.p + i, i == 0); encode(m.ii.p + e + i, i == 0); }
    for (UINT i = ns, k = 1; i < e; i += 3, k++) encode(m.ii.p + i, k & 1); m.flags |= 1;
  }
  ///
  return 0;
}

HRESULT CTesselatorDbl::Update(ICSGMesh* mesh, CSGVAR v, UINT flags)
{
  auto& m = *static_cast<CMesh*>(mesh); m.resetee(); bool inv = false;
  UINT mo = (flags >> 16) & 0x0f, nn = nl != 0 ? max(1, flags & 0x0000ffff) : 0, nb = ns ? nn : nn + 1, nt = np;
  if (mo & 3)
  {
    for (UINT i = nt = 0; i < (UINT)np; i++)
    {
      auto& t = pp.p[i]; const Vector3& p = *(const Vector3*)&t.x;
      if (t.ic = (&t.x)[mo & 1] != 0) pp.p[t.line = nt++].next = i;
    }
  }
  m.pp.setsize(np + nt * nn);
  for (UINT i = 0; i < (UINT)np; i++) m.pp[i] = Vector3R(&pp[i].x);
  if (nn)
  {
    switch (mo)
    {
    case 0:
    {
      Rational dz; conv(&dz, 1, v); inv = dz.sign() > 0;
      for (UINT i = 0, n = nn * np; i < n; i++)
      {
        const auto& s = m.pp[i]; auto& d = m.pp[np + i];
        d.x = s.x; d.y = s.y; d.z = s.z + dz;
      }
      break;
    }
    case 1:
    case 2:
    {
      double a; conv(&a, 1, v); if (!a) a = 2 * M_PI * nn / (nn + 1); inv = a > 0;
      for (UINT j = 1, l = np; j <= nn; j++)
      {
        auto sc = Vector2R::SinCos(a * j / nn, (char)(flags >> 24));
        for (UINT i = 0; i < nt; i++, l++)
        {
          const auto& s = m.pp[pp[i].next]; auto& d = m.pp[l];
          d.x = mo == 1 ? s.x : s.x * sc.x;
          d.y = mo == 1 ? s.y * sc.x : s.y;
          d.z = mo == 1 ? s.y * sc.y : s.x * sc.y;
        }
      }
      break;
    }
    }
  }
  m.ii.setsize(ns * 2 + (nl + nt - np) * nb * 6);
  memcpy(m.ii.p, ss.p, ns << 2); UINT t = ns;
  for (UINT j = 0, k1 = 0, k2 = np; j < nb; j++, k1 = k2, k2 = j != nn ? k2 + nt : 0)
    for (UINT i = 0, l = 0; i < (UINT)nl; i++)
    {
      int t1 = ll[i]; int e = (t1 & 0x40000000) != 0;
      int t2 = ll[e ? l : i + 1] & 0x0fffffff; if (e) { l = i + 1; t1 &= 0x0fffffff; }
      UINT l1 = nt == np ? k1 + t1 : k1 == 0 || !pp.p[t1].ic ? t1 : k1 + pp.p[t1].line;
      UINT l2 = nt == np ? k2 + t1 : k2 == 0 || !pp.p[t1].ic ? t1 : k2 + pp.p[t1].line;
      UINT l3 = nt == np ? k1 + t2 : k1 == 0 || !pp.p[t2].ic ? t2 : k1 + pp.p[t2].line;
      UINT l4 = nt == np ? k2 + t2 : k2 == 0 || !pp.p[t2].ic ? t2 : k2 + pp.p[t2].line;
      if (l1 != l2) { m.ii.p[t + 0] = l1; m.ii.p[t + 1] = l2; m.ii.p[t + 2] = l3; t += 3; }
      if (l3 != l4) { m.ii.p[t + 0] = l3; m.ii.p[t + 1] = l2; m.ii.p[t + 2] = l4; t += 3; }
    }
  for (UINT i = 0, k = np + nt * (nn - 1); i < (UINT)ns; i += 3, t += 3)
  {
    m.ii.p[t + 0] = nt == np ? k + ss[i + 0] : !pp.p[ss[i + 0]].ic ? ss[i + 0] : k + pp.p[ss[i + 0]].line;
    m.ii.p[t + 1] = nt == np ? k + ss[i + 2] : !pp.p[ss[i + 2]].ic ? ss[i + 2] : k + pp.p[ss[i + 2]].line;
    m.ii.p[t + 2] = nt == np ? k + ss[i + 1] : !pp.p[ss[i + 1]].ic ? ss[i + 1] : k + pp.p[ss[i + 1]].line;
  }
  _ASSERT(t == m.ii.n);
  if (inv) m.invert();
  m.flags = !nn && m.ii.n ? 2 : 0; return 0;
}

HRESULT CMesh::Check(CSG_MESH_CHECK check, CSG_MESH_CHECK* p)
{
  sarray<UINT> tt; *p = (CSG_MESH_CHECK)0; if (!check) check = (CSG_MESH_CHECK)0xff;
  if (check & CSG_MESH_CHECK_DUP_POINTS)
  {
    UINT ts = min(pp.n, 1024), * ht = (UINT*)_alloca(ts * sizeof(UINT)); memset(ht, 0, ts * sizeof(UINT));
    for (UINT i = 0, k; i < pp.n; i++)
    {
      const auto& e = pp.p[i];
      UINT& hc = ht[e.GetHashCode() % ts];
      if (!hc) { hc = i + 1; continue; }
      for (k = hc - 1; k < i && !e.Equals(pp[k]); k++);
      if (k == i) continue;
      *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_DUP_POINTS); break;
    }
  }
  if (check & (CSG_MESH_CHECK_BAD_INDEX | CSG_MESH_CHECK_UNUSED_POINT))
  {
    tt.getptr(pp.n); memset(tt.p, 0, pp.n * sizeof(UINT));
    for (UINT i = 0; i < ii.n; i++)
    {
      if (ii[i] < pp.n) { tt[ii[i]] = 1; continue; }
      *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_BAD_INDEX); return 0;
    }
    for (UINT i = 0; i < pp.n; i++)
    {
      if (tt[i]) continue;
      *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_UNUSED_POINT); break;
    }
  }
  if (check & CSG_MESH_CHECK_OPENINGS)
  {
    UINT nk = 0; UINT64* kk = 0;
    for (UINT i = 0; i < ii.n; i += 3)
    {
      for (UINT k = 0, j, e; k < 3; k++)
      {
        UINT64 t = ii[i + k] | ((UINT64)ii[i + (k + 1) % 3] << 32);
        for (j = 0, e = -1; j < nk && kk[j] != t; j++) if (e == -1 && !kk[j]) e = j;
        if (j != nk) { kk[j] = 0; continue; }
        t = (t >> 32) | (t << 32);
        if (e != -1) { kk[e] = t; continue; }
        (kk = (UINT64*)tt.getptr((nk + 1) << 1))[nk++] = t;
      }
    }
    for (UINT i = 0; i < nk; i++)
    {
      if (!kk[i]) continue;
      *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_OPENINGS); break; //openings
    }
  }
  if (check & CSG_MESH_CHECK_PLANES)
  {
    if (flags & 1 && ee.n != 0)
    {
      UINT ne = -1;
      for (UINT i = 0, j = -1, k = 0, l; i < ii.n; i += 3)
      {
        if (decode(ii.p + i)) ne++;
        if (ne >= ee.n) break;
        for (l = 0; l < 3 && (0 ^ ee.p[ne].DotCoord(pp.p[ii[i + l]])) == 0; l++);
        if (l != 3) break;
      }
      if (ne + 1 != ee.n)
        *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_PLANES);
    }
  }
  return 0;
}

HRESULT CMesh::Transform(CSGVAR m)
{
  if (ee.n) ee.setsize(0);
  if (m.vt == CSG_TYPE_RATIONAL)
  {
    if (m.count == 12)
    {
      auto& p = *static_cast<const CVector*>((ICSGVector*)m.p);
      Matrix3x4R::Transform(pp.p, pp.n, *(const Matrix3x4R*)(&p.val + m.length)); return 0;
    }
  }
  if (m.count <= 3)
  {
    Vector3R v; conv(&v.x, m.count <= 1 ? 1 : 3, m);
    if (m.count <= 1) for (UINT i = 0; i < pp.n; i++) pp.p[i] *= v.x;
    else for (UINT i = 0; i < pp.n; i++) pp.p[i] += v; return 0;
  }
  Matrix3x4R t; conv(t.m[0], 12, m);
  Matrix3x4R::Transform(pp.p, pp.n, t); return 0;
}

HRESULT CMesh::CreateBox(CSGVAR a, CSGVAR b)
{
  UINT l = -1; ee.setsize(0); flags = 0;
  Vector3R ab[2]; conv(&ab[0].x, 3, a); conv(&ab[1].x, 3, b);
  for (UINT i = 0; i < 3; i++)
  {
    auto s = (&ab[0].x)[i].CompareTo((&ab[1].x)[i]);
    if (s > 0) swap(*(UINT64*)&(&ab[0].x)[i], *(UINT64*)&(&ab[1].x)[i]);
    if (s == 0) { if (l != -1) { pp.setsize(0); ii.setsize(0); return 0; } l = i; }
  }
  if (l != -1)
  {
    pp.setsize(4); ii.setsize(12); auto p = pp.p;
    p[0] = ab[0]; p[1].x = ab[1].x; p[1].y = ab[l == 0 ? 1 : 0].y; p[1].z = ab[0].z;
    p[2] = ab[1]; p[3].x = ab[0].x; p[3].y = ab[l == 0 ? 0 : 1].y; p[3].z = ab[1].z;
    byte bb[12] = { 2, 0, 1, 2, 3, 0, 1, 0, 2, 0, 3, 2 };
    for (UINT i = 0; i < 12; i++) ii.p[i] = bb[i]; flags = 1 | 2;
  }
  else
  {
    pp.setsize(8); ii.setsize(36); auto p = pp.p;
    p[0].x = ab[0].x; p[0].y = ab[0].y; p[0].z = ab[0].z;
    p[1].x = ab[1].x; p[1].y = ab[0].y; p[1].z = ab[0].z;
    p[2].x = ab[1].x; p[2].y = ab[1].y; p[2].z = ab[0].z;
    p[3].x = ab[0].x; p[3].y = ab[1].y; p[3].z = ab[0].z;
    p[4].x = ab[0].x; p[4].y = ab[0].y; p[4].z = ab[1].z;
    p[5].x = ab[1].x; p[5].y = ab[0].y; p[5].z = ab[1].z;
    p[6].x = ab[1].x; p[6].y = ab[1].y; p[6].z = ab[1].z;
    p[7].x = ab[0].x; p[7].y = ab[1].y; p[7].z = ab[1].z;
    byte bb[36] = { 2, 1, 0, 0, 3, 2, 4, 0, 1, 1, 5, 4, 5, 1, 2, 2, 6, 5, 6, 2, 3, 3, 7, 6, 3, 0, 7, 0, 4, 7, 6, 4, 5, 6, 7, 4 }; //encoded
    for (UINT i = 0; i < 36; i++) ii.p[i] = bb[i]; flags = 1;
  }
  return 0;
}

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
  m.ii.copy((const UINT*)csg.ii.p, nk);
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
      auto an = a.pp.n; a.pp.setsize(a.pp.n + b.pp.n); for (UINT i = 0; i < b.pp.n; i++) a.pp.p[an + i] = b.pp.p[i];
      auto in = a.ii.n; a.ii.setsize(a.ii.n + b.ii.n); for (UINT i = 0; i < b.ii.n; i++) a.ii.p[in + i] = an + b.ii.p[i];
      if (csg.ne < a.ee.n + b.ee.n) a.resetee();
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
          for (j = i; i + 3 < (int)m.ii.n && !decode(m.ii.p + i + 3); i += 3);
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
    return 0x8C066001; //degenerated input mesh
  }
  UINT nx = 0; for (UINT i = 0; i < csg.ne; i++) if (ff[i] != 1) nx++;
  a.ee.setsize(nx); for (UINT i = 0, k = 0; i < csg.ne; i++) if (ff[i] != 1) a.ee[k++] = csg.ee[i];
  csg.trim(ni);
  a.pp.copy(csg.pp.p, csg.np);
  a.ii.copy((const UINT*)csg.ii.p, ni);
  return 0;
}

void CTesselatorRat::initplanes(CMesh& m)
{
  if (m.ii.n == 0) return;
  if (m.flags & 1)
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
  m.ee.copy(csg.ee.p, csg.ne); m.flags |= 1;
}
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
      return 0x8C066002;
    swap = 0;
  }
  return ni;
}

