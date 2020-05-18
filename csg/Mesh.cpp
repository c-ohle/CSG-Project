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
  m.flags = !nn && m.ii.n ? MESH_FL_SHELL : 0; m.flags |= MESH_FL_MODIFIED;
  /// todo: the other cases
  if (nn && mo == 0 && nn == 1 && simplepoly())
  {
    UINT e = m.ii.n - ns;
    for (UINT i = 0; i < (UINT)ns; i += 3) { encode(m.ii.p + i, i == 0); encode(m.ii.p + e + i, i == 0); }
    for (UINT i = ns, k = 1; i < e; i += 3, k++) encode(m.ii.p + i, k & 1); m.flags |= MESH_FL_ENCODE;
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
  m.flags = !nn && m.ii.n ? MESH_FL_SHELL : 0; m.flags |= MESH_FL_MODIFIED; return 0;
}

HRESULT CMesh::Check(CSG_MESH_CHECK check, CSG_MESH_CHECK* p)
{
  sarray<UINT> tt; *p = (CSG_MESH_CHECK)0; if (!check) check = (CSG_MESH_CHECK)0xffff; 
  if (check & CSG_MESH_CHECK_DUP_POINTS)
  {
    UINT ts = max(pp.n, 32), * ht = tt.getptr(ts); memset(ht, 0, ts * sizeof(UINT));
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
  if (check & CSG_MESH_CHECK_DUP_PLANES)
  {
    UINT ts = max(ee.n, 32), * ht = tt.getptr(ts); memset(ht, 0, ts * sizeof(UINT));
    for (UINT i = 0, k; i < ee.n; i++)
    {
      const auto& e = ee.p[i];
      UINT& hc = ht[e.GetHashCode() % ts];
      if (!hc) { hc = i + 1; continue; }
      for (k = hc - 1; k < i && !e.Equals(ee.p[k]); k++);
      if (k == i) continue;
      *p = (CSG_MESH_CHECK)(*p | CSG_MESH_CHECK_DUP_PLANES); break;
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
    if (flags & MESH_FL_ENCODE && ee.n != 0)
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
  if (ee.n) ee.setsize(0); flags |= MESH_FL_MODIFIED;
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
  UINT l = -1; ee.setsize(0); 
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
    for (UINT i = 0; i < 12; i++) ii.p[i] = bb[i]; flags = MESH_FL_ENCODE | MESH_FL_SHELL | MESH_FL_MODIFIED;
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
    for (UINT i = 0; i < 36; i++) ii.p[i] = bb[i]; flags = MESH_FL_ENCODE | MESH_FL_MODIFIED;
  }
  return 0;
}

HRESULT CTesselatorRat::Stretch(ICSGMesh* mesh, CSGVAR v)
{
  Vector3R dir; conv(&dir.x, 3, v);
  if (!dir.x.sign() && !dir.y.sign() && !dir.z.sign()) return 0;
  auto& m = *static_cast<CMesh*>(mesh);
  csg.dictpp(m.pp.n << 1);
  auto ff = ll.getptr(m.pp.n << 1); memset(ff, -1, (m.pp.n << 1) * sizeof(int));
  UINT ni, * ii = (UINT*)ss.getptr(ni = m.ii.n);
  beginsex(); int sig = 0;
  for (UINT i = 0, k = 0, e = -1; i < m.ii.n; i += 3, k++)
  {
    sig = (m.flags & MESH_FL_ENCODE) && !decode(m.ii.p + i) ? sig : 0 ^ Vector3R::Dot(dir,
      m.ee.n ? *(Vector3R*)&m.ee[++e].x :
      Vector3R::Ccw(m.pp.p[m.ii.p[i + 0]], m.pp.p[m.ii.p[i + 1]], m.pp.p[m.ii.p[i + 2]]));
    for (int j = 0, h; j < 3; j++)
    {
      auto& t = ff[(sig < 0 ? 0 : m.pp.n) + m.ii.p[h = i + j]];
      ii[h] = t != -1 ? t : (t = csg.addpp(sig < 0 ? m.pp.p[m.ii.p[h]] : m.pp.p[m.ii.p[h]] + dir));
    }
    if (sig < 0)
    {
      addsex(m.ii.p[i + 0], m.ii.p[i + 1]);
      addsex(m.ii.p[i + 1], m.ii.p[i + 2]);
      addsex(m.ii.p[i + 2], m.ii.p[i + 0]);
    }
  }
  for (int i = 0; i < this->ni; i++)
  {
    if (this->ii[i].a == -1) continue;
    ii = (UINT*)ss.getptr(ni + 6);
    ii[ni + 0] = ff[this->ii[i].a];
    ii[ni + 2] = ii[ni + 5] = ff[this->ii[i].b];
    if ((ii[ni + 1] = ii[ni + 3] = ff[m.pp.n + this->ii[i].a]) == -1) return 0x8C066001;
    if ((ii[ni + 4] = ff[m.pp.n + this->ii[i].b]) == -1) return 0x8C066001;
    ni += 6;
  }
  m.pp.copy(csg.pp.p, csg.np);
  m.ii.copy((UINT*)ii, ni);
  m.resetee(); m.flags &= ~MESH_FL_SHELL; m.flags |= MESH_FL_MODIFIED;
  return 0;
}
