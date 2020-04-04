#include "pch.h"
#include "Tesselator.h"


STDMETHODIMP CTesselator::get_Version(LONG* pVal)
{
  auto p = (BYTE*)pVal;
  p[0] = sizeof(void*);
  p[1] = Debug ? 1 : 0;
  p[2] = p[3] = 1;
  return 0;
}
STDMETHODIMP CTesselator::get_Mode(Mode* pVal)
{
  *pVal = mode; return 0;
}
STDMETHODIMP CTesselator::put_Mode(Mode newVal)
{
  mode = newVal; return 0;
}
STDMETHODIMP CTesselator::BeginPolygon()
{
  np = 0; return 0;
}
STDMETHODIMP CTesselator::BeginContour()
{
  fi = np; return 0;
}
STDMETHODIMP CTesselator::AddVertex(DOUBLE x, DOUBLE y, DOUBLE z)
{
  if (np == ppLength) resize(64);
  if (np != fi) pp[np - 1].next = np;
  auto a = &pp[np++]; a->x = x; a->y = y; a->z = z; a->next = fi;
  return 0;
}
STDMETHODIMP CTesselator::EndContour()
{
  return 0;
}
STDMETHODIMP CTesselator::EndPolygon()
{
  auto pro = project(0);
  ns = nl = this->ni = 0; int np = this->np, shv = 1; worstcase:
  for (int i = 0; i < this->np; i++) { auto p = &pp[i]; p->y += p->x * kill; }
  memset(dict, 0, hash * sizeof(int)); int ni = 0;
  for (int i = 0; i < np; i++)
  {
    auto a = &pp[i]; auto b = &pp[a->next];
    auto dx = b->x - a->x; auto dy = b->y - a->y;
    if (dy == 0) { if (dx == 0) continue; shv++; goto worstcase; }
    a->x2 = dy > 0 ? a->x : b->x;
    a->f = a->x - a->y * (a->a = dx / dy);
    kk[ni++] = ab(i, dy > 0 ? i : a->next); a->line = -1; addpt(a->x, a->y, i);
  }
  qsort_s(kk, ni, sizeof(ab), cmp1, this);
  double y1, y2 = 0, lx = 0; int active = 0, nfl = 0;
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
        auto y = pp[j != kk[t].b ? j : next].y;
        if (l != ni) { if (y < y2) y2 = y; continue; }
        if (y > y3) y3 = y; next:
        if (t == 0 && l == ni) { y2 = y3; t = active; l++; }
      }
      ////////////
      for (int t = 0; t < active; t++)
      {
        auto a = &pp[kk[t].a]; a->x1 = a->x2; a->yy = y1;
        a->x2 = a->y == y2 ? a->x : pp[a->next].y == y2 ? pp[a->next].x : a->f + y2 * a->a;
      }
      qsort_s(kk, active, sizeof(ab), cmp2, this);
      int nc = 0, e = active;
      for (int i = 1; i < active; i++)
      {
        auto a = &pp[kk[i - 1].a]; auto b = &pp[kk[i].a];
        if (a->x2 <= b->x2) continue;
        auto y = (b->f - a->f) / (a->a - b->a);
        if (y <= y1) continue;
        if (y > y2) continue;
        b->yy = y2 = y; e = 0;
      }
      for (; e < active; e++)
      {
        auto a = &pp[kk[e].a]; if (a->yy == y2) { a->x2 = lx; continue; }
        a->x2 = lx = a->y == y2 ? a->x : pp[a->next].y == y2 ? pp[a->next].x : a->f + y2 * a->a;
      }
      for (int i = 0, k = 0, dir = 0, t; i < active; i++)
      {
        auto b = &pp[t = kk[i].a]; auto d = t != kk[i].b;
        if ((d ? b->y : pp[b->next].y) == y2) b->next = -1 - b->next;
        auto old = dir; dir += d ? +1 : -1;
        switch ((Mode)((int)mode & 0xff))
        {
        case Mode::EvenOdd:
          if ((old & 1) == 0 && (dir & 1) == 1) { k = i; continue; }
          if ((old & 1) == 1 && (dir & 1) == 0) break;
          goto skip;
        case Mode::Positive:
          if (dir == +1 && old == 0) { k = i; continue; }
          if (old == +1 && dir == 0) break;
          goto skip;
        case Mode::Negative:
          if (dir == -1 && old == 0) { k = i; continue; }
          if (old == -1 && dir == 0) break;
          goto skip;
        case Mode::NonZero:
          if (old == 0) { k = i; continue; }
          if (dir == 0) break;
          goto skip;
        case Mode::AbsGeqTwo:
          if (abs(old) == 1 && abs(dir) == 2) { k = i; continue; }
          if (abs(old) == 2 && abs(dir) == 1) break;
          goto skip;
        case Mode::GeqThree:
          if (old == 2 && dir == 3) { k = i; continue; }
          if (old == 3 && dir == 2) break;
          goto skip;
        }
        auto a = &pp[kk[k].a];
        if (a->x1 == b->x1 && a->x2 == b->x2)
        {
          if (a->line != -1) { pp[nfl++].fl = (kk[k].a) | (a->line << 16); a->line = -1; }
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
        if (this->np + 4 >= ppLength) resize();
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
        b->line = f2 ? -1 : this->ni; if (this->ni == iiLength) __realloc(ii, iiLength = max(ppLength, this->ni << 1));
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
  if ((mode & (Mode::FillFast | Mode::Fill)) != 0) fill();
  if ((mode & Mode::IndexOnly) == 0) { auto f = kill * shv; for (int i = 0; i < this->np; i++) { auto p = &pp[i]; p->y -= p->x * f; } }
  if ((mode & (Mode::Outline | Mode::OutlinePrecise)) != 0) outline();
  if ((mode & Mode::Fill) != 0) optimize();
  if ((mode & Mode::IndexOnly) != 0) { this->np = 0; return 0; }
  if (pro != 0) project(pro << 2);
  if ((mode & Mode::NoTrim) == 0) trim();
  return 0;
}
STDMETHODIMP CTesselator::get_VertexCount(LONG* pVal)
{
  *pVal = np; return 0;
}
STDMETHODIMP CTesselator::VertexAt(LONG i, Vertex* pVal)
{
  if ((ULONG)i >= (ULONG)np) return -1;
  *pVal = *(Vertex*)&pp[i].x; return 0;
}
STDMETHODIMP CTesselator::get_IndexCount(LONG* pVal)
{
  *pVal = ns; return 0;
}
STDMETHODIMP CTesselator::IndexAt(LONG i, LONG* pVal)
{
  if ((ULONG)i >= (ULONG)ns) return -1;
  *pVal = ss[i];  return 0;
}
STDMETHODIMP CTesselator::get_OutlineCount(LONG* pVal)
{
  *pVal = nl; return 0;
}
STDMETHODIMP CTesselator::OutlineAt(LONG i, LONG* pVal)
{
  if ((ULONG)i >= (ULONG)nl) return -1;
  *pVal = ll[i]; return 0;
}

const __m128d abs_mask = _mm_castsi128_pd(_mm_setr_epi32(-1, 0x7FFFFFFF, -1, 0x7FFFFFFF));

int CTesselator::ccw(ts* a, ts* b, ts* c)
{
#if(USESSE) 
  __m128d ma = _mm_load_pd(&a->x), mb = _mm_load_pd(&b->x), mc = _mm_load_pd(&c->x);
  __m128d ab = _mm_sub_pd(mb, ma), ac = _mm_sub_pd(mc, ma);
  __m128d ms = _mm_cross_pd(ab, ac);
  __m128d mv = _mm_and_pd(ms, abs_mask);
  if (_mm_comile_sd(mv, _mm_set_sd(1e-3)) != 0)
  {
    //if (_mm_comieq_sd(mv, _mm_setzero_pd()) != 0) return 0;
    if (_mm_comile_sd(_mm_div_sd(mv, _mm_max_sd(_mm_dot_pd(ab), _mm_dot_pd(ac))), _mm_set_sd(1e-12)) != 0)
      return 0;
  }
  return _mm_comile_sd(ms, _mm_setzero_pd()) != 0 ? -1 : +1;
#else 
  auto bx = b->x - a->x; auto by = b->y - a->y;
  auto cx = c->x - a->x; auto cy = c->y - a->y;
  auto s = bx * cy - by * cx; if (s == 0) return 0;
  if (abs(s) < 1e-3)
  {
    auto p = max(bx * bx + by * by, cx * cx + cy * cy);
    s /= p;
    if (abs(s) < 1e-12)
      return 0;
  }
  return s < 0 ? -1 : +1;
#endif
}
void CTesselator::fill()
{
  qsort_s(ii, ni, sizeof(ab), cmp4, this);
  if (ni > ppLength) resize(iiLength);
  for (int i = 0; i < ni; i++)
  {
    auto c = &pp[i]; c->ic = 0;
    c->fl = compare(pp[ii[i].a].y, pp[ii[i].b].y);
  }
  if (ni > ssLength) __realloc(ss, ssLength = iiLength);
  for (int i = 0; i < ni; i++) ss[i] = i;
  qsort_s(ss, ni, sizeof(int), cmp3, this); // y-max //create trapezoidal map on dict in O(n)
  memset(dict, 0, (mi = ni) * sizeof(int)); fs l1, l2;
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
      if (c->ic == 0) { c->ic = 1; c->f = a->x - a->y * (c->a = (b->x - a->x) / (b->y - a->y)); }
      auto x = c->f + pt->y * c->a; //auto x = a.x + (pt.y - a.y) * (b.x - a.x) / (b.y - a.y);
      auto s = compare(pt->x, x);
      if (s < 0 && (l1.k == -1 || x < l1.x)) l1 = fs(k, x, c->fl, s);
      if (s > 0 && (l2.k == -1 || x > l2.x)) l2 = fs(k, x, c->fl, s);
    }
    for (int l = 0; l < 2; l++)
    {
      auto p = l == 1 ? &l2 : &l1;
      if (p->k == -1 || p->d1 * p->d2 != 1) continue;
      if (mi + 2 > dictLength) __realloc(dict, dictLength = dictLength << 1);
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
        if (ccw(&pp[t1], &pp[t2], &pp[t3]) != -1) continue; //possible: fans after sequence of left turns  
        if (ns + 3 >= ssLength) __realloc(ss, ssLength = ssLength << 1);
        ss[ns++] = t1; ss[ns++] = t3; ss[ns++] = t2;
        dict[l == -1 ? i : l + 1] = n; break;
      }
}
void CTesselator::outline()
{
  if (ll == 0 || ni > llLength) __realloc(ll, llLength = iiLength);
  if (np + (ni << 1) > dictLength) __realloc(dict, dictLength = max(ppLength, np) + (iiLength << 1));
  memset(dict, 0, np * sizeof(int));
  for (int i = 0, k, j = np; i < ni; i++)
  {
    dict[j] = i; dict[j + 1] = dict[k = ii[i].a]; dict[k] = j; j += 2;
  }
  for (int i = 0, t; i < np; i++)
  {
    if ((t = dict[i]) == 0) continue;
    for (auto ab = nl; ;)
    {
      auto u = ii[dict[t]]; //for (auto j = t; j != 0; j = dict[j + 1]) { auto xx = ii[dict[j]]; }
      if ((mode & Mode::OutlinePrecise) != 0)
      {
        if (dict[t + 1] != 0) //branches
        {
          pp[u.a].next = 1 << 20;
          if (nl != ab)
          {
            for (auto j = dict[t + 1]; j != 0; j = dict[j + 1])
            {
              auto v = ii[dict[j]];
              auto d = ccw(&pp[ll[nl - 1]], &pp[u.a], &pp[u.b]) -
                ccw(&pp[ll[nl - 1]], &pp[u.a], &pp[v.b]);
              if (d > 0) continue;
              if (d == 0 && ccw(&pp[u.a], &pp[u.b], &pp[v.b]) <= 0) continue;
              auto q = dict[t]; dict[t] = dict[j]; dict[j] = q; u = v;
            }
          }
        }
        if (nl != ab)
        {
          if (pp[u.a].next == (1 << 20) && ccw(&pp[ll[nl - 1]], &pp[u.a], &pp[u.b]) == 0) goto skip;
          if (u.b == i && pp[ll[ab]].next == (1 << 20) && ccw(&pp[u.a], &pp[ll[ab]], &pp[ll[ab + 1]]) == 0)
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
static int mod(int i, int k)
{
  auto r = i % 3; return i - r + (r + k) % 3;
}
void CTesselator::optimize()
{
  if (ns > iiLength) __realloc(ii, iiLength = ssLength);
  if (hash + ns > dictLength) __realloc(dict, dictLength = hash + ssLength);
  if (ns > kkLength) __realloc(kk, kkLength = ssLength);
  for (int i = 0; i < ns; i++) kk[i] = ab(-1, 0); memset(dict, 0, hash * sizeof(int));
  for (int i = 0; i < ns; i++)
  {
    auto l = ab(ss[i], ss[mod(i, 1)]);
    int h = l.hashcode() % hash, t = dict[h] - 1;
    for (; t != -1 && ii[t] != l; t = dict[hash + t] - 1);
    if (t != -1) { kk[kk[i].a = t].a = i; continue; }
    h = (ii[i] = ab(l.b, l.a)).hashcode() % hash;
    dict[hash + i] = dict[h]; dict[h] = i + 1;
  }
  for (int i = 0, t; i < ns; i++)
  {
    if (kk[i].b == 1) continue; auto k = kk[i].a; if (k == -1) continue; //tests++;
    int u1, u2, v2, i1 = ss[i], i2 = ss[u1 = mod(i, 1)], i3 = ss[u2 = mod(i, 2)], k3 = ss[v2 = mod(k, 2)];
    if (circum(i1, i2, i3, k3)) { kk[i].b = kk[k].b = 1; continue; } //ok
    ss[i] = k3; ss[k] = i3; int j = i, v1 = mod(k, 1);
    if ((t = kk[u1].a) != -1) { kk[t].b = 0; if (t < j) j = t; }
    if ((t = kk[v1].a) != -1) { kk[t].b = 0; if (t < j) j = t; }
    if ((t = kk[i].a = kk[v2].a) != -1) { kk[t].a = i; kk[t].b = 0; if (t < j) j = t; }
    if ((t = kk[k].a = kk[u2].a) != -1) { kk[t].a = k; kk[t].b = 0; if (t < j) j = t; }
    kk[kk[u2].a = v2].a = u2; if (j < i) i = j - 1;
  }
}
bool CTesselator::circum(int i1, int i2, int i3, int i4)
{
#if(USESSE)
  __m128d ma = _mm_load_pd(&pp[i1].x), mb = _mm_load_pd(&pp[i2].x);
  __m128d mc = _mm_load_pd(&pp[i3].x), md = _mm_load_pd(&pp[i4].x);
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
  const auto& a = *(Vector2*)&pp[i1].x; const auto& b = *(Vector2*)&pp[i2].x;
  const auto& c = *(Vector2*)&pp[i3].x; const auto& d = *(Vector2*)&pp[i4].x;
  auto ab = (a + b) * 0.5;
  auto bc = (b + c) * 0.5;
  auto va = ~(b - a);
  auto vb = ~(c - b);
  auto f = (vb ^ (ab - bc)) / (va ^ vb);
  auto v = ab + va * f;
  return (v - d).Dot() >= (v - a).Dot();
#endif
}
void CTesselator::resize(int c)
{
  __realloc(pp, ppLength = max(c, ppLength << 1));
  if (kkLength < ppLength << 1) __realloc(kk, kkLength = ppLength << 1);
  if (hash + ppLength > dictLength) __realloc(dict, dictLength = hash + ppLength);
}
int CTesselator::project(int m)
{
  if (m == 0 && (m = (((int)mode >> 15) & 0x12) | (((int)mode >> 17) & 1)) == 0) return m;
  for (int i = 0; i < np; i++)
  {
    auto p = &pp[i]; if ((m & 0x40) != 0) p->y = -p->y;
    auto x = p->x; auto y = p->y; auto z = p->z;
    switch (m & 0xf)
    {
    case 1: case 8: p->x = z; p->y = x; p->z = y; break;
    case 2: case 4: p->x = y; p->y = z; p->z = x; break;
    }
    if ((m & 0x10) != 0) p->y = -p->y;
  }
  return m;
}
int CTesselator::addpt(double x, double y, int v)
{
  int h = (int)((UINT)(hashcode(x) + hashcode(y) * 13) % hash), i = dict[h] - 1;
  for (; i != -1; i = dict[hash + i] - 1) if (pp[i].x == x && pp[i].y == y) return i;
  if ((i = v) < 0)
  {
    if (np == ppLength) resize(); //todo: check necessary? possible?
    auto b = &pp[-v - 1]; auto n = b->next; auto c = &pp[n < 0 ? -n - 1 : n];
    auto p = &pp[i = np++]; p->x = x; p->y = y; p->z = b->z + (y - b->y) * (c->z - b->z) / (c->y - b->y);
  }
  dict[hash + i] = dict[h]; dict[h] = i + 1; return i;
}
void CTesselator::trim()
{
  memset(dict, 0, this->np * sizeof(int)); auto np = 0;
  for (int i = 0; i < ns; i++) dict[ss[i]] = 1;
  for (int i = 0; i < nl; i++) dict[ll[i] & 0x0fffffff] = 1;
  for (int i = 0; i < this->np; i++) if (dict[i] != 0) { if (np != i) { auto d = &pp[np].x; auto s = &pp[i].x; d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; } dict[i] = np++; }
  if (this->np == np) return; this->np = np;
  for (int i = 0; i < ns; i++) ss[i] = dict[ss[i]];
  for (int i = 0; i < nl; i++) ll[i] = dict[ll[i] & 0x0fffffff] | (ll[i] & 0x40000000);
}
