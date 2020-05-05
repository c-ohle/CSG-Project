#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Mesh.h"

/*
struct float2 
{ 
  float x, y; 
  float2(float x, float y) { this->x = x; this->y = y; }
  float2 operator + (const float2& b) const { return float2(x + b.x, y + b.y); }
  float2 operator * (double b) const { return float2(x * b, y * b); }
};

void AddQSpline(ICSGTesselator* tess, float2* pp, int np, float flat = 0.1f)
{
  auto t0 = pp[0]; _ASSERT(np >= 3);
  for (int i = 1; i < np;)
  {
    auto t1 = pp[i++]; auto t2 = i == np - 1 ? pp[i++] : (pp[i - 1] + pp[i]) * 0.5f;
    void spline(ICSGTesselator * tess, float2 p1, float2 p2, float a, float b)
    {
      var t = (a + b) * 0.5f; var s = 1f - t;
      var p = t0 * (s * s) + t1 * (t * s * 2) + t2 * (t * t);
      var d = Math.Abs(normalize(p - p1) ^ normalize(p2 - p)); if (!(d > flat)) return;
      spline(p1, p, a, t); AddVertex(p); spline(p, p2, t, b);
    }
    spline(t0, t2, 0, 1); tess->AddVertex(t0 = t2);
  }
}
void AddCSpline(ICSGTesselator* tess, float2* pp, int np, float flat = 0.1f)
{
}

void GlyphRun(ICSGTesselator* tess, LPCSTR text, UINT length, HFONT hfont, float flat)
{
  auto StackPtr = (BYTE*)_alloca(0x10000);
  auto hdcnull = ::GetDC(0);
  auto po = ::SelectObject(hdcnull, hfont); const float f = (1.0f / 0x10000);
  MAT2 m2 = { 0 }; m2.eM11.value = m2.eM22.value = 1;
  float x = 0;
  for (int i = 0; i < length; i++)
  {
    //if (i != 0) { if (kern.TryGetValue((((uint)text[i] << 16) | text[i - 1]), out float k)) x += k * size; }
    GLYPHMETRICS gm = { 0 };
    auto nc = ::GetGlyphOutlineW(hdcnull, text[i], GGO_NATIVE, &gm, 1 << 20, StackPtr, &m2);
    auto  vv = (float2*)(StackPtr + nc);
    for (auto ph = StackPtr; ph - StackPtr < nc;) //TTPOLYGONHEADER 
    {
      auto  cb = ((int*)ph)[0]; //auto  dwType = ((int*)ph)[1]; //24
      tess->BeginContour();
      vv[0].x = x + ((int*)ph)[2] * f; vv[0].y = ((int*)ph)[3] * f; tess->AddVertex(vv[0]);
      for (auto pc = ph + 16; pc - ph < cb;) //TTPOLYCURVE
      {
        auto  wType = ((USHORT*)pc)[0]; //TT_PRIM_LINE 1, TT_PRIM_QSPLINE 2, TT_PRIM_CSPLINE 3
        auto  cpfx = ((USHORT*)pc)[1]; auto  pp = (int*)(pc + 4);
        for (int t = 0; t < cpfx; t++) { vv[t + 1].x = x + pp[t << 1] * f; vv[t + 1].y = pp[(t << 1) + 1] * f; }
        if (wType == 2) AddQSpline(tess, vv, cpfx + 1, flat);
        else if (wType == 3) AddCSpline(tess, vv, cpfx + 1, flat);
        else for (int t = 0; t < cpfx; t++) tess->AddVertex(vv[t + 1]);
        vv[0] = vv[cpfx]; pc += 4 + (cpfx << 3);
      }
      tess->EndContour(); ph += cb;
    }
    x += gm.gmCellIncX;
  }
  ::SelectObject(hdcnull, po);
}
*/