#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"

//#define PIXELSIZE 	64  // 64 number of units per pixel. It has to be a power of two 
#define ERRSHIFT 		4  	// 4 = 2log(ERRDIV), define only if ERRDIV is a power of 2  
#define MAXGY 			5 	// 5
#define MAXMAXGY 		8 	// 8 	related to MAXVECTORS  
#define MAXVECTORS  257 // 257 must be at least 257  = (2 ^ MAXMAXGY) + 1  
struct LPOINTFX { int x, y; };
struct QS { LPOINTFX a, b, c; };
struct CTX
{
  ICSGTesselator* tess; int x, pixelsize;
  void AddVertex(LPOINTFX p)
  {
    auto l = ((INT64)x << 16) + p.x; if (l > INT_MAX) { Vector2 t((double)x * 0x10000 + p.x, p.y); tess->AddVertex(t); return; }
    p.x = (int)l; CSGVAR v; v.vt = CSG_TYPE_INT; v.count = 2; *(const void**)&v.p = &p; tess->AddVertex(v);
  }
};
static void QSpline2Polyline(CTX& ctx, QS* qs)
{
  int Ax = qs->a.x >> 10;
  int Ay = qs->a.y >> 10;
  int Bx = qs->b.x >> 10;
  int By = qs->b.y >> 10;
  int Cx = qs->c.x >> 10;
  int Cy = qs->c.y >> 10;
  int GX = Bx; int DX, DDX = (DX = (Ax - GX)) - GX + Cx;
  int GY = By; int DY, DDY = (DY = (Ay - GY)) - GY + Cy;
  GX = DDX < 0 ? -DDX : DDX;
  GY = DDY < 0 ? -DDY : DDY;
  GX += GX > GY ? GX + GY : GY + GY;
  for (GY = 1; GX > (ctx.pixelsize << (5 - ERRSHIFT)); GX >>= 2)  GY++;
  if (GY > MAXMAXGY) 	GY = MAXMAXGY;
  int i = 1 << GY;
  if (GY > MAXGY)
  {
    QS qs; DDX = GY - 1;
    qs.a.x = Ax;
    qs.a.y = Ay;
    qs.b.x = (Ax + Bx + 1) >> 1;
    qs.b.y = (Ay + By + 1) >> 1;
    qs.c.x = (Ax + Bx + Bx + Cx + 2) >> 2;
    qs.c.y = (Ay + By + By + Cy + 2) >> 2;
    QSpline2Polyline(ctx, (QS*)&qs);
    qs.a.x = qs.c.x;
    qs.a.y = qs.c.y;
    qs.b.x = (Cx + Bx + 1) >> 1;
    qs.b.y = (Cy + By + 1) >> 1;
    qs.c.x = Cx;
    qs.c.y = Cy;
    QSpline2Polyline(ctx, (QS*)&qs);
    return;
  }
  int nsqs = GY + GY;
  DX = DDX - (DX << ++GY); DDX += DDX;
  DY = DDY - (DY << GY); DDY += DDY;
  GY = (int)Ay << nsqs;
  GX = (int)Ax << nsqs;
  int tmp = 1L << (nsqs - 1);
  do
  {
    GX += DX; DX += DDX; GY += DY; LPOINTFX p;
    p.x = (((GX + tmp) >> nsqs) << 10);
    p.y = (((GY + tmp) >> nsqs) << 10);
    ctx.AddVertex(p);
    DY += DDY;
  } while (--i);
}

HRESULT AddGlyphContour(ICSGTesselator* tess, sarray<int> buff[2], const CSGVAR& text, HFONT font, int flat)
{
  if (text.vt != CSG_TYPE_STRING) return  E_INVALIDARG;
  auto ss = (LPCWSTR)text.p; UINT ns = lstrlen(ss);
  auto hdc = ::GetDC(0);
  auto po = ::SelectObject(hdc, font);
  GCP_RESULTSW re = { 0 }; re.lStructSize = sizeof(GCP_RESULTSW); re.lpDx = buff[1].getptr(ns); re.nGlyphs = ns;
  auto hr = GetCharacterPlacementW(hdc, ss, ns, 0, &re, GCP_USEKERNING);
  if (hr == 0) { ::SelectObject(hdc, po); return E_FAIL; }
  CTX ctx; ctx.tess = tess; ctx.x = 0; ctx.pixelsize = 1 << flat;
  GLYPHMETRICS gm; MAT2 m2 = { 0 }; m2.eM11.value = m2.eM22.value = 1;
  for (UINT i = 0; i < ns; i++)
  {
    auto ph = (TTPOLYGONHEADER*)buff[0].getptr(256);
    int nc = ::GetGlyphOutlineW(hdc, ss[i], GGO_NATIVE, &gm, buff[0].n << 2, ph, &m2);
    if (nc == GDI_ERROR)
    {
      nc = ::GetGlyphOutlineW(hdc, ss[i], GGO_NATIVE, &gm, 0, 0, &m2);
      buff[0].getptr(nc >> 1); i--; continue;
    }
    for (; ((BYTE*)ph - (BYTE*)buff[0].p) < nc; ph = (TTPOLYGONHEADER*)((BYTE*)ph + ph->cb))
    {
      tess->BeginContour(); ctx.AddVertex(*(LPOINTFX*)&ph->pfxStart);
      auto pc = (TTPOLYCURVE*)(ph + 1);
      for (; (BYTE*)pc < (BYTE*)ph + ph->cb; pc = (TTPOLYCURVE*)((BYTE*)pc + sizeof(TTPOLYCURVE) + (pc->cpfx - 1) * sizeof(POINTFX)))
      {
        if (pc->wType == TT_PRIM_LINE) for (UINT t = 0; t < pc->cpfx; t++) ctx.AddVertex(*(LPOINTFX*)&pc->apfx[t]);
        else if (pc->wType == TT_PRIM_QSPLINE)
        {
          LPOINTFX spline[3];
          spline[0] = *(LPOINTFX*)(((BYTE*)pc) - sizeof(POINTFX));
          for (UINT k = 0; k < pc->cpfx;)
          {
            spline[1] = *(LPOINTFX*)&pc->apfx[k++];
            if (k == pc->cpfx - 1) spline[2] = *(LPOINTFX*)&pc->apfx[k++];
            else
            {
              spline[2].x = (*(int*)&pc->apfx[k - 1].x + *(int*)&pc->apfx[k].x) >> 1;
              spline[2].y = (*(int*)&pc->apfx[k - 1].y + *(int*)&pc->apfx[k].y) >> 1;
            }
            QSpline2Polyline(ctx, (QS*)spline); //ctx.AddVertex(spline[2]); 
            spline[0] = spline[2];
          }
        }
      }
      tess->EndContour();
    }
    ctx.x += re.lpDx[i];
  }
  ::SelectObject(hdc, po);
  return 0;
}

HRESULT CTesselatorDbl::AddGlyphContour(CSGVAR text, HFONT font, int flat)
{
  return ::AddGlyphContour(this, &ss, text, font, flat);
}
HRESULT CTesselatorRat::AddGlyphContour(CSGVAR text, HFONT font, int flat)
{
  return ::AddGlyphContour(this, &ss, text, font, flat);
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
    sig = (m.flags & 1) && !decode(m.ii.p + i) ? sig : 0 ^ Vector3R::Dot(dir,
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
  m.resetee(); m.rtgen = getrtid();
  return 0;
}

