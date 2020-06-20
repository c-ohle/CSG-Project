#include "pch.h"
#include "Factory.h"
#include "view.h"
#include "font.h"

CFont* CFont::first;
void SetTexture(ID3D11ShaderResourceView* p);
HRESULT CreateTexture(UINT dx, UINT dy, UINT pitch, DXGI_FORMAT fmt, UINT mips, void* p, ID3D11ShaderResourceView** srv);

void CFont::create()
{
  HDC hdc = GetDC(0);
  hFont = CreateFontW(-(int)roundf(size * GetDeviceCaps(hdc, LOGPIXELSY) / 72), 0, 0, 0,
    style & 1 ? 700 : 400, style & 2 ? 1 : 0, style & 4 ? 1 : 0, style & 8 ? 1 : 0,
    DEFAULT_CHARSET, OUT_TT_PRECIS, 0, CLEARTYPE_QUALITY, 0, name.p);
  auto hof = SelectObject(hdc, hFont);
  TEXTMETRIC tm; GetTextMetrics(hdc, &tm);
  scale = 1.0f / size;
  ascent = tm.tmAscent * scale;
  descent = tm.tmDescent * scale;
  kern.setsize(GetKerningPairsW(hdc, 0, 0));
  GetKerningPairsW(hdc, kern.n, kern.p);
  kerndict.setsize(256 + kern.n); memset(kerndict.p, 0, 256 * sizeof(USHORT));
  for (UINT i = 0; i < kern.n; i++)
  {
    auto& k = kern.p[i]; *(float*)&k.iKernAmount = k.iKernAmount * scale;
    auto hc = (k.wFirst + k.wSecond) & 0xff; kerndict.p[256 + i] = kerndict.p[hc]; kerndict.p[hc] = (USHORT)(256 + i);
  }
  SelectObject(hdc, hof); ReleaseDC(0, hdc);
}
void __kern(const CFont& p, const WCHAR* s, float* px)
{
  auto hc = (s[0] + s[1]) & 0xff;
  for (UINT u = p.kerndict.p[hc]; u; u = p.kerndict.p[u])
  {
    if (*(UINT*)&p.kern.p[u - 256].wFirst != *(const UINT*)s) continue;
    *px += *(float*)&p.kern.p[u - 256].iKernAmount; return;
  }
}

void CFont::create(LPCWSTR s, UINT n)
{
  const int ex = 4; UINT nc = 0; auto cc = (WCHAR*)stackptr;
  for (UINT i = 0; i < n; i++) { auto c = s[i]; if (getdict(c) < 0) { cc[nc++] = c; } }
  if (nc == 0) return; auto space = L' '; if ((style & (4 | 8)) != 0) { cc[nc++] = '|'; space--; }
  if (glyphn + nc > glyphs.n) glyphs.setsize((((glyphs.n + nc) >> 5) + 1) << 5);
  MAT2 m2 = { 0 }; m2.eM11.value = m2.eM22.value = 1;
  BITMAPINFO bi; memset(&bi, 0, sizeof(bi)); bi.bmiHeader.biSize = 40; bi.bmiHeader.biPlanes = 1; bi.bmiHeader.biBitCount = 32;
  HDC hdc = GetDC(0); auto po = SelectObject(hdc, hFont);
  for (UINT i1 = 0, i2 = 0, gi = glyphn; i1 < nc; i1 = i2)
  {
    int mx = ex, my = 0;
    for (UINT i = i1; i < nc; i++, i2++)
    {
      auto gm = (int*)&glyphs[gi + i]; GetGlyphOutlineW(hdc, cc[i], 0, (GLYPHMETRICS*)gm, 0, 0, &m2); if (cc[i] <= space) continue;
      if (cc[i] == ' ') gm[0] = gm[4] & 0xffff; //dx = cellincx
      if (mx + gm[0] + (ex << 1) > 4096 && n > 1) break; //D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION (16384)
      mx += gm[0] + (ex << 1); my = max(my, gm[1] + (ex << 1));
    }
    if (my != 0)
    {
      if (srvn == srvs.n) { srvs.setsize(max(4, srvn << 1)); memset(srvs.p + srvn, 0, (srvs.n - srvn) * sizeof(void*)); }
      bi.bmiHeader.biWidth = mx; bi.bmiHeader.biHeight = -my; BYTE* pp;
      auto dib = CreateDIBSection(0, &bi, 0, (void**)&pp, 0, 0);
      auto ddc = CreateCompatibleDC(hdc);
      auto obmp = SelectObject(ddc, dib);
      auto ofont = SelectObject(ddc, hFont);
      SetTextColor(ddc, 0x00ffffff); SetBkMode(ddc, 1); SetTextAlign(ddc, 24);
      for (UINT i = i1, x = ex; i < i2; i++)
      {
        auto c = cc[i]; if (c <= space) continue; auto gm = (int*)&glyphs.p[gi + i];
        TextOutW(ddc, x - gm[2], ex + gm[3], &c, 1); x += gm[0] + (ex << 1);
      }
      SelectObject(ddc, ofont);
      SelectObject(ddc, obmp);
      DeleteDC(ddc);
      for (UINT k = 0, nk = mx * my; k < nk; k++)
      {
        //XMLoadByte4()
        auto t = (pp[(k << 2)] * 0x4c + pp[(k << 2) + 1] * 0x95 + pp[(k << 2) + 2] * 0x1f) >> 8;
        //t = (t * t) >> 8;
        pp[k] = (byte)t;
      }
      CreateTexture(mx, my, mx, DXGI_FORMAT_A8_UNORM, 1, pp, &srvs.p[srvn++]);
      DeleteObject(dib);
    }
    for (UINT i = i1, x = ex; i < i2; i++)
    {
      auto c = cc[i];
      auto gl = &glyphs.p[glyphn++]; gl->c = c;
      auto hc = c % GLYPH_DICT; gl->next = dict[hc];  dict[hc] = glyphn;
      auto gm = (int*)gl;
      if (c > space)
      {
        gl->srv = srvn - 1;
        gl->x1 = (x - ex) / (float)mx;
        gl->x2 = (x + gm[0] + ex) / (float)mx;
        x += gm[0] + (ex << 1);
      }
      else gl->srv = -1;
      gl->boxx = (gm[0] + (ex << 1)) * scale;
      gl->boxy = my * scale;
      gl->orgx = (gm[2] - ex) * scale;
      gl->orgy = (gm[3] + ex) * scale;
      gl->incx = (gm[4] & 0xffff) * scale; if (c < ' ') gl->incx *= 0.5f;
    }
  }
  SelectObject(hdc, po); ReleaseDC(0, hdc);
}

XMFLOAT2 CFont::getextent(LPCWSTR s, UINT n)
{
  XMFLOAT2 p(0, (ascent + descent) * size);// p->x = 0; p->y = height;
  for (UINT i = 0, j; i < n; i++)
  {
    if ((j = getdict(s[i])) == -1) { create(s + i, n - i); j = getdict(s[i]); }
    if (i != 0) __kern(*this, s + (i - 1), &p.x);
    auto pg = glyphs.p + j;
    p.x += pg->incx;
  }
  p.x *= size;
  return p;
}

void CFont::draw(CView* view, XMFLOAT2 p, LPCWSTR s, UINT n)
{
  if (!hFont) create();
  UINT mode = MO_TOPO_TRIANGLESTRIP | MO_PSSHADER_FONT | MO_BLENDSTATE_ALPHA | MO_SAMPLERSTATE_FONT | MO_RASTERIZER_FRCULL;
  auto f = 1 / size; p.x *= f; p.y *= f;
  for (int a = 0, b = 0, j, bsrv = -1, csrv = -1, nc = 0; ; b++)
  {
    if (b < (int)n)
    {
      if ((j = getdict(s[b])) == -1) { create(s + b, n - b); j = getdict(s[b]); }
      bsrv = glyphs.p[j].srv;
    }
    if (b == n || (bsrv != csrv && bsrv != -1) || nc == 64) //256 * sizeof(vertex) 8k blocks
    {
      if (nc != 0)
      {
        SetTexture(srvs.p[csrv]);
        auto vv = view->BeginVertices(nc << 2);
        for (; a < b; a++)
        {
          if (a != 0) __kern(*this, s + (a - 1), &p.x);
          auto pg = glyphs.p + getdict(s[a]);
          if (pg->srv != -1)
          {
            vv[0].p.x = vv[2].p.x = (p.x + pg->orgx) * size;
            vv[0].p.y = vv[1].p.y = (p.y - pg->orgy) * size;
            vv[1].p.x = vv[3].p.x = vv[0].p.x + pg->boxx * size;
            vv[3].p.y = vv[2].p.y = vv[0].p.y + pg->boxy * size;
            vv[0].t.x = vv[2].t.x = pg->x1;
            vv[1].t.x = vv[3].t.x = pg->x2;
            vv[2].t.y = vv[3].t.y = 1; vv += 4;
          }
          p.x += pg->incx; //if (ExtraSpace != 0 && s[a] == ' ') x += ExtraSpace * f;
        }
        view->EndVertices(nc << 2, mode); mode = 0;
      }
      if (b == n) break;
      if (bsrv != -1) csrv = bsrv; nc = 0;
    }
    if (bsrv != -1) nc++;
  }
}

void CFont::relres(int fl)
{
  if (hFont) { DeleteObject(hFont); hFont = 0; }
  for (UINT i = 0; i < srvn; i++) srvs.p[i]->Release();
  srvn = glyphn = 0; memset(dict, 0, sizeof(dict));
}

HRESULT __stdcall CFactory::GetFont(BSTR name, FLOAT size, UINT style, ICDXFont** p)
{
  { Critical crit; for (auto t = CFont::first; t; t = t->next) if (!lstrcmp(name, t->name.p) && t->size == size && t->style == style) { (*p = t)->AddRef(); return 0; } }
  auto t = new CFont();
  t->name.setsize(SysStringLen(name) + 1); memcpy(t->name.p, name, t->name.n << 1);
  t->size = size; t->style = style;
  *p = t; return 0;
}
