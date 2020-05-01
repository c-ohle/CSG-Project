#include "pch.h"
#include "TesselatorRat.h"

HRESULT CTesselatorRat::get_Mode(CSG_TESS* p)
{
  *p = mode; return 0;
}
HRESULT CTesselatorRat::put_Mode(CSG_TESS newVal)
{
  mode = newVal; return 0;
}
HRESULT CTesselatorRat::SetNormal(CSGVAR n)
{
  double v[3]; conv(v, 3, n);
  int i = abs(v[0]) >= abs(v[1]) && abs(v[0]) >= abs(v[2]) ? 0 : abs(v[1]) >= abs(v[2]) ? 1 : 2;
  int s = v[i] < 0 ? -1 : +1;
  mode = (CSG_TESS)((mode & (CSG_TESS)0xffff) | ((int)CSG_TESS_NORMX << i) | (s & (int)CSG_TESS_NORMNEG));
  return 0;
}
HRESULT CTesselatorRat::BeginPolygon()
{
  np = 0; return 0;
}
HRESULT CTesselatorRat::BeginContour()
{
  fi = np; return 0;
}
HRESULT CTesselatorRat::AddVertex(CSGVAR v)
{
  if (np == pp.n) resize(64);
  if (np != fi) pp[np - 1].next = np;
  auto a = &pp[np++]; a->next = fi;
  conv(&a->x, 3, v); return 0;
}
HRESULT CTesselatorRat::EndContour()
{
  return 0;
}
HRESULT CTesselatorRat::EndPolygon()
{
  auto pro = project(0);
  ns = nl = this->ni = 0; int np = snp = this->np, shv = 1; worstcase:
  for (int i = 0; i < this->np; i++) { auto p = &pp[i]; p->y = 0 | p->y + p->x / kill; }
  memset(dict.p, 0, hash * sizeof(int)); int ni = 0;
  for (int i = 0; i < np; i++)
  {
    auto a = &pp[i]; auto b = &pp[a->next];
    Rational::mach m = 0; m.fetch();
    auto dx = b->x - a->x; auto dy = b->y - a->y; int d;
    if ((d = dy.sign()) == 0) { m.dispose(); if (dx.sign() == 0) continue; shv++; goto worstcase; }
    a->f = m | a->x - a->y * (a->a = m | dx / dy); m.dispose();
    a->x2 = d > 0 ? a->x : b->x;
    kk[ni++] = ab(i, d > 0 ? i : a->next); a->line = -1; addpt(a->x, a->y, i);
  }
  qsort_s(kk.p, ni, sizeof(ab), cmp1, this);
  Rational y1, y2; int active = 0, nfl = 0;
  for (int l = 0; l < ni;)
  {
    for (y1 = pp[kk[l].b].y; ;)
    {
      kk[active++] = kk[l];
      if (++l == ni || (y2 = pp[kk[l].b].y) != y1) break;
    }
    for (auto y3 = y2; ; y1 = y2, y2 = y3)
    {
      for (int t = active, j; t-- != 0;)
      {
        auto next = pp[j = kk[t].a].next;
        if (next < 0)
        {
          pp[j].x1 = pp[j].x2; if (pp[j].line != -1) { pp[nfl++].fl = j | (pp[j].line << 16); pp[j].line = -1; }
          active--; for (int s = t; s < active; s++) kk[s] = kk[s + 1]; goto next;
        }
        const auto& y = pp[j != kk[t].b ? j : next].y;
        if (l != ni) { if (y < y2) y2 = y; continue; }
        if (y > y3) y3 = y;
      next:
        if (t == 0 && l == ni) { y2 = y3; t = active; l++; }
      }
      ////////////
      for (int t = 0; t < active; t++)
      {
        auto a = &pp[kk[t].a];
        a->x1 = a->x2;
        a->x2 = 0 | a->f + y2 * a->a;
      }
      qsort_s(kk.p, active, sizeof(ab), cmp2, this);
      int nc = 0, e = active;
      for (int i = 1; i < active; i++)
      {
        auto a = &pp[kk[i - 1].a]; auto b = &pp[kk[i].a];
        if (a->x2 <= b->x2) continue;
        Rational::mach m = 0; m.fetch();
        auto y = (b->f - a->f) / (a->a - b->a);
        if (y < y2) { y2 = m | y; e = 0; }
        m.dispose();
      }
      for (; e < active; e++)
      {
        auto a = &pp[kk[e].a]; a->x2 = 0 | a->f + y2 * a->a;
      }
      for (int i = 0, k = 0, dir = 0, t; i < active; i++)
      {
        auto b = &pp[t = kk[i].a]; auto d = t != kk[i].b;
        if ((d ? b->y : pp[b->next].y) == y2) b->next = -1 - b->next;
        auto old = dir; dir += d ? +1 : -1;
        switch ((CSG_TESS)((int)mode & 0xff))
        {
        case CSG_TESS_EVENODD:
          if ((old & 1) == 0 && (dir & 1) == 1) { k = i; continue; }
          if ((old & 1) == 1 && (dir & 1) == 0) break;
          goto skip;
        case CSG_TESS_POSITIVE:
          if (dir == +1 && old == 0) { k = i; continue; }
          if (old == +1 && dir == 0) break;
          goto skip;
        case CSG_TESS_NEGATIVE:
          if (dir == -1 && old == 0) { k = i; continue; }
          if (old == -1 && dir == 0) break;
          goto skip;
        case CSG_TESS_NONZERO:
          if (old == 0) { k = i; continue; }
          if (dir == 0) break;
          goto skip;
        case CSG_TESS_ABSGEQTWO:
          if (abs(old) == 1 && abs(dir) == 2) { k = i; continue; }
          if (abs(old) == 2 && abs(dir) == 1) break;
          goto skip;
        case CSG_TESS_GEQTHREE:
          if (old == 2 && dir == 3) { k = i; continue; }
          if (old == 3 && dir == 2) break;
          goto skip;
        }
        auto a = &pp[kk[k].a];
        if (a->x1 == b->x1 && a->x2 == b->x2)
        {
          if (a->line != -1) { pp[nfl++].fl = kk[k].a | (a->line << 16); a->line = -1; }
          if (b->line != -1) goto skip;
          continue;
        }
        if (nc != 0)
        {
          auto c = &pp[pp[nc - 1].ic];
          if (c->x1 == a->x1 && c->x2 == a->x2) //xor
          {
            if (a->line != -1) { pp[nfl++].fl = (kk[k].a) | (a->line << 16); a->line = -1; }
            if (c->line != -1) { pp[nfl++].fl = pp[nc - 1].ic | (c->line << 16); c->line = -1; }
            nc--; goto m1;
          }
        }
        pp[nc++].ic = kk[k].a; m1:
        pp[nc++].ic = t;
        continue; skip: if (b->line != -1) { pp[nfl++].fl = t | (b->line << 16); b->line = -1; }
      }
      for (int i = 0, j; i < nc; i++)
      {
        if (this->np + 4 >= (int)pp.n) resize();
        auto b = &pp[j = pp[i].ic];
        bool f1 = false, f2 = false;
        for (int k = i - 1; k <= i + 1; k += 2)
        {
          if ((UINT)k >= (UINT)nc) continue;
          auto c = &pp[pp[k].ic];
          if (!f1 && b->x1 == c->x1) f1 = true;
          if (!f2 && b->x2 == c->x2) f2 = true;
        }
        if (!f1)
        {
          if (b->line == -1)
            for (int t = 0; t < nfl; t++)
            {
              auto c = &pp[pp[t].fl & 0xffff];
              if (b->f != c->f || b->a != c->a) continue;
              b->line = pp[t].fl >> 16; pp[t].fl = pp[i].ic | (b->line << 16);
              for (nfl--; t < nfl; t++) pp[t].fl = pp[t + 1].fl; break;
            }
          if (b->line != -1)
          {
            if ((i & 1) != 0)
            {
              if (ii[b->line].b == -1) { if (f2) { ii[b->line].b = addpt(b->x2, y2, -1 - j); b->line = -1; } continue; }
              ii[b->line].a = addpt(b->x1, y1, -1 - j);
            }
            else
            {
              if (ii[b->line].a == -1) { if (f2) { ii[b->line].a = addpt(b->x2, y2, -1 - j); b->line = -1; } continue; }
              ii[b->line].b = addpt(b->x1, y1, -1 - j);
            }
          }
        }
        auto k1 = addpt(b->x1, y1, -1 - j);
        if (f1 && b->line != -1) { if (ii[b->line].a == -1) ii[b->line].a = k1; else ii[b->line].b = k1; }
        auto k2 = f2 ? addpt(b->x2, y2, -1 - j) : -1;
        b->line = f2 ? -1 : this->ni; if (this->ni == ii.n) ii.setsize(max((int)pp.n, this->ni << 1));
        ii[this->ni++] = (i & 1) != 0 ? ab(k1, k2) : ab(k2, k1);
      }
      for (int i = 0, j; i < nfl; i++)
      {
        auto c = &pp[j = pp[i].fl & 0xffff];
        auto line = pp[i].fl >> 16; auto t = addpt(c->x1, y1, -1 - j);
        if (ii[line].a == -1) ii[line].a = t; else ii[line].b = t;
      }
      nfl = 0;
      ////////////
      if (y2 == y3) break;
    }
  }
  for (int i = 0, j; i < active; i++)
  {
    auto c = &pp[j = kk[i].a]; if (c->line == -1) continue;
    auto t = addpt(c->x2, y2, -1 - j); if (ii[c->line].a == -1) ii[c->line].a = t; else ii[c->line].b = t;
  }
  if ((mode & (CSG_TESS_FILLFAST | CSG_TESS_FILL)) != 0) fill();
  if ((mode & CSG_TESS_INDEXONLY) == 0)
  {
    auto f = shv != 1 ? kill * shv : kill;
    for (int i = 0; i < this->np; i++) { auto p = &pp[i]; p->y = 0 | p->y - p->x / f; }
  }
  if ((mode & (CSG_TESS_OUTLINE | CSG_TESS_OUTLINEPRECISE)) != 0) outline();
  if ((mode & CSG_TESS_NOTRIM) == 0) trim();
  if ((mode & CSG_TESS_FILL) != 0) optimize();
  if ((mode & CSG_TESS_INDEXONLY) != 0) { this->np = 0; return 0; }
  if (pro != 0) project(pro << 2);
  return 0;
}
HRESULT CTesselatorRat::get_VertexCount(UINT* p)
{
  *p = np; return 0;
}
HRESULT CTesselatorRat::GetVertex(UINT i, CSGVAR* v)
{
  if (i >= (UINT)np) return E_INVALIDARG;
  conv(*v, &pp[i].x, 3); return 0;
}
HRESULT CTesselatorRat::get_IndexCount(UINT* p)
{
  *p = ns; return 0;
}
HRESULT CTesselatorRat::GetIndex(UINT i, UINT* p)
{
  if (i >= (UINT)ns) return E_INVALIDARG;
  *p = ss[i];  return 0;
}
HRESULT CTesselatorRat::get_OutlineCount(UINT* p)
{
  *p = nl; return 0;
}
HRESULT CTesselatorRat::GetOutline(UINT i, UINT* p)
{
  if (i >= (UINT)nl) return E_INVALIDARG;
  *p = ll[i]; return 0;
}
