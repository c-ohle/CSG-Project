#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"


HRESULT CTesselatorRat::Skeleton(ICSGMesh* mesh, CSGVAR va)
{
  if (!mesh)
  {
    if (np == 1) { csg.pp.setsize(32); csg.np = 0; }
    if (csg.np == csg.pp.n) csg.pp.setsize(csg.pp.n << 1);
    conv(&csg.pp[csg.np++].x, 2, va); csg.ff.getptr(np)[np - 1] = csg.np;
    return 0;
  }
  auto& me = *static_cast<CMesh*>(mesh); me.rtgen = getrtid();
  int polys = 0; for (int i = 0; i < np; i++) if (pp[i].next < i) polys++;
  carray<carray<carray<Vector2R>>> data; data.setsize(polys);
  for (int i = 0, k, j = 0, u = 0; i < np; i = k, j++)
  {
    for (k = i; pp[k].next > k; k++); k++;
    auto& a = data[j]; a.setsize(k - i);
    for (UINT t = 0; t < a.n; t++)
    {
      auto nc = csg.ff[i + t] - u;
      auto& b = a[t]; b.setsize(1 + nc); b[0] = *(const Vector2R*)&pp[i + t].x;
      for (int s = 1; s <= nc; s++) b[s] = *(const Vector2R*)&csg.pp[u++];
    }
  }
  auto zmax = (Rational)INT_MAX; auto z1 = zmax;
  for (UINT i = 0; i < data.n; i++) z1 = min(z1, data[i][0][1].x);
  csg.dictpp(64);
  int nt = 0; auto tt = csg.tt.getptr(128);
  int ni = 0; auto ii = csg.ii.getptr(128);
  int ne = 0; auto ee = csg.ee.getptr(32);
  int nf = 0; auto ff = csg.ff.getptr(128);
  mode = (CSG_TESS)(CSG_TESS_POSITIVE | CSG_TESS_OUTLINEPRECISE);
  for (; ; )
  {
    for (int i = 0; i < ni; i++)
    {
      auto c = ii[(i << 2) + 2]; auto& a = data[c & 0xfff][(c >> 12) & 0xfff];
      auto l = (c >> 24) + 1; if (l >= (int)a.n || a[l].x != z1) continue;
      ii[(i << 2) + 1] = -1; ii[(i << 2) + 2] = (c & 0x00ffffff) | (l << 24);
    }
    for (int i = 0; i < (int)data.n; i++)
    {
      auto& a = data[i]; if (!z1.Equals(a[0][1].x)) continue;
      auto n = a.n; ii = csg.ii.getptr((ni + n) << 2);
      for (int j = 0, t = ni; j < (int)n; j++, ni++)
      {
        ii[(ni << 2) + 0] = csg.addpp(Vector3R(a[j][0].x, a[j][0].y, z1));
        ii[(ni << 2) + 1] = -1;
        ii[(ni << 2) + 2] = i | (j << 12) | (1 << 24);
        ii[(ni << 2) + 3] = t + (j + 1) % n;
      }
    }
    BeginPolygon();
    for (int i = 0, a; (a = i) < ni;)
    {
      BeginContour();
      do { addvertex(csg.pp[ii[(i << 2) + 0]]); } while (ii[(i++ << 2) + 3] != a);
      EndContour();
    }
    EndPolygon();
    int bn = 0; ii = csg.ii.getptr((ni + this->np) << 2);
    for (int i = 0, l; i < this->nl; i = l + 1)
    {
      for (l = i; (this->ll[l] & 0x40000000) == 0; l++); auto at = bn;
      for (int j = i; j <= l; j++)
      {
        auto a = csg.addpp(*(Vector3R*)&this->pp[this->ll[j] & 0x0fffffff].x);
        int x = (ni + bn++) << 2, t = 0; for (; t < ni && ii[t << 2] != a; t++); //t = ni;
        if (t == ni) { ii[x] = a; ii[x + 1] = ii[x + 2] = ii[x + 3] = -1; }
        else { ii[x] = ii[t <<= 2]; ii[x + 1] = ii[t + 1]; ii[x + 2] = ii[t + 2]; ii[x + 3] = ii[t + 3]; }
      }
      for (int k = 0, dn = bn - at, j; k < dn; k++)
      {
        int i1 = at + k, i2 = at + (k + 1) % dn;
        if (ii[((ni + i1) << 2) + 3] != -1 && ii[ii[((ni + i1) << 2) + 3] << 2] == ii[(ni + i2) << 2]) continue;
        const auto& p1 = *(Vector2R*)&csg.pp[ii[(ni + i1) << 2]];
        const auto& p2 = *(Vector2R*)&csg.pp[ii[(ni + i2) << 2]]; auto pv = p2 - p1;
        for (j = 0; j < ni; j++)
        {
          int t1 = ii[j << 2], t2 = ii[ii[(j << 2) + 3] << 2]; if (t1 == t2) continue;
          const auto& o1 = *(Vector2R*)&csg.pp[t1];
          const auto& o2 = *(Vector2R*)&csg.pp[t2]; auto ov = o2 - o1;
          if ((0 ^ (ov ^ pv)) != 0) continue;
          if ((0 ^ (ov & pv)) <= 0) continue; Rational::mach m = 0;
          auto f = (ov.x.sign() < 0 ? -ov.x : ov.x) > (ov.y.sign() < 0 ? -ov.y : ov.y) ? (p2.x - o1.x) / ov.x : (p2.y - o1.y) / ov.y;
          auto t = f.sign() <= 0 || f.CompareTo(1) > 0; m.dispose(); if (t) continue;
          if ((0 ^ (ov.x * (p1.y - o1.y) - ov.y * (p1.x - o1.x))) != 0) continue;
          ii[((ni + i1) << 2) + 1] = ii[(j << 2) + 1];
          ii[((ni + i1) << 2) + 2] = ii[(j << 2) + 2];
          break;
        }
        if (j == ni) { me.clear(); return 0; } //should be impossible
      }
      for (int k = at + 1; k < bn; k++) ii[((ni + k - 1) << 2) + 3] = k;
      ii[((ni + bn - 1) << 2) + 3] = at;
    }
    memcpy(ii, ii + (ni << 2), (ni = bn) << 4); ee = csg.ee.getptr(ni << 1);
    if (nf < ni) { ff = csg.ff.getptr(ni); for (; nf < ni; ff[nf++] = 0); }
    else nf = ni;
    for (int i = 0; i < ni; i++)
    {
      if (ii[(i << 2) + 1] != -1) continue;
      auto i1 = ii[i << 2]; auto i2 = ii[ii[(i << 2) + 3] << 2];
      auto& a = csg.pp[i1];
      auto v = 0 | (*(Vector2R*)&a - *(Vector2R*)&csg.pp[i2]).Normalize();
      auto c = ii[(i << 2) + 2];
      auto w = (double)data[c & 0xfff][(c >> 12) & 0xfff][c >> 24].y;
      auto h = tan((90 - w) * (M_PI / 180)); double dx = (double)v.x, dy = (double)v.y;
      auto l = sqrt(dx * dx + dy * dy);
      auto p = 0 | Vector4R::PlaneFromPointNormal(a, Vector3R(-v.y, v.x, (float)(h * l)));
      auto x = 0; for (; x < ne && !ee[x].Equals(p); x += 2); //dict?
      if (x == ne) { ee[ne] = p; ne += 2; }
      ii[(i << 2) + 1] = x; ff[i] = 0;
    }
    for (int i = 0; i < ni; i++)
    {
      int k = ii[(i << 2) + 3], i1 = ii[(i << 2) + 1], i2 = ii[(k << 2) + 1];
      if (ff[k] == (i1 | (i2 << 16))) continue; ff[k] = i1 | (i2 << 16);
      auto v = 0 | (*(const Vector3R*)&ee[i1].x ^ *(const Vector3R*)&ee[i2].x);
      auto& e = ee[(k << 1) + 1]; *(Vector3R*)&e.x = v.z.sign() > 0 ? v : -v; e.w = 0; _ASSERT(v.z.sign() != 0);
    }
    auto z2 = zmax;
    for (int i = 0; i < (int)data.n; i++)
    {
      auto& a = data[i];
      for (int j = 0; j < (int)a.n; j++)
      {
        auto& b = a[j];
        for (int k = 1; k < (int)b.n; k++)
        {
          if (b[k].x <= z1) continue;
          if (b[k].x < z2) { z2 = b[k].x; break; }
        }
      }
    }
    for (int i = 0; i < ni; i++)
    {
      auto& p = csg.pp[ii[i << 2]];
      auto& v = *(Vector3R*)&csg.ee[(i << 1) + 1].x;
      for (int k = 0, j; k < ni; k++)
      {
        if (k == i) continue;
        if ((j = ii[(k << 2) + 3]) == i) continue;
        auto& e = ee[ii[(k << 2) + 1]]; Rational::mach m = 0; Vector2R t; Rational z, f;
        auto d = *(Vector3R*)&e.x & v; if (d.sign() == 0) goto ex;
        f = e.DotCoord(p) / d;
        z = p.z - v.z * f; if (z >= z2 || z <= z1) goto ex;
        t = Vector2R(p.x - v.x * f, p.y - v.y * f);
        if ((t - *(Vector2R*)&csg.pp[ii[k << 2]].x ^ *(Vector2R*)&ee[(k << 1) + 1].x).sign() < 0) goto ex;
        if ((t - *(Vector2R*)&csg.pp[ii[j << 2]].x ^ *(Vector2R*)&ee[(j << 1) + 1].x).sign() > 0) goto ex;
        m.fetch(); z2 = m | z; ex: m.dispose();
      }
    }
    for (int i = 0; i < ni; i++)
    {
      auto& p = csg.pp[ii[i << 2]];
      auto& n = *(Vector3R*)&csg.ee[(i << 1) + 1].x;
      ii[i << 2] |= csg.addpp(Vector3R(
        0 | p.x + n.x * z2 / n.z - n.x * p.z / n.z,
        0 | p.y + n.y * z2 / n.z - n.y * p.z / n.z, z2)) << 16;
    }
    tt = csg.tt.getptr(nt + ni * 3);
    for (int i = 0; i < ni; i++)
    {
      if (ee[ii[(i << 2) + 1]].z.sign() == 0) continue;
      tt[nt++] = ii[(i << 2) + 1]; tt[nt++] = ii[i << 2]; tt[nt++] = ii[ii[(i << 2) + 3] << 2];
    }
    if (z2 == zmax) break;
    for (int i = 0; i < ni; i++) ii[i << 2] >>= 16; z1 = z2;
  }
  ff = csg.ff.getptr(ne >> 1); memset(ff, 0, (ne >> 1) * sizeof(int));
  for (int k = 0, j; k < nt; k += 3) { j = tt[k] >> 1; tt[k] = ff[j]; ff[j] = k + 1; }
  ni = 0; mode = (CSG_TESS)(CSG_TESS_POSITIVE | CSG_TESS_FILL); //Planes.n = 0; Planes.Ensure(ne >> 1);
  for (int i = 0, k; i < ne; i += 2)
  {
    if ((k = ff[i >> 1]) == 0) continue;
    setnormal(*(Vector3R*)&ee[i].x);
    BeginPolygon();
    for (; k != 0; k = tt[k - 1])
    {
      BeginContour();
      addvertex(csg.pp[tt[k + 0] & 0xffff]);
      addvertex(csg.pp[tt[k + 1] & 0xffff]);
      addvertex(csg.pp[tt[k + 1] >> 16]);
      addvertex(csg.pp[tt[k + 0] >> 16]);
      EndContour();
    }
    EndPolygon(); auto ic = this->ns; if (ic == 0) continue;
    ii = csg.ii.getptr(ni + ic); auto ab = ni;
    for (int t = 0; t < ic; t++) ii[ni++] = csg.addpp(*(Vector3R*)&this->pp[this->ss[t]].x);
    for (int t = ab; t < ni; t += 3) encode((UINT*)ii + t, t == ab);
    //Planes.p[Planes.n++] = ee[i];
  }
  ni = join(ni, 1); csg.trim(ni);
  me.resetee();
  me.pp.copy(csg.pp.p, csg.np);
  me.ii.copy((const UINT*)csg.ii.p, ni);
  return 0;
}
