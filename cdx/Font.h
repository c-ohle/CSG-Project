#pragma once

struct GLYPH { float boxx, boxy, orgx, orgy, incx, x1, x2; int srv, next; WCHAR c; };
#define GLYPH_DICT 64

struct CFont : ICDXFont
{
  UINT refcount = 1;
  sarray<WCHAR> name; float size; UINT style;
  HFONT hFont = 0; float scale, ascent, descent;
  carray<ID3D11ShaderResourceView*> srvs; UINT srvn = 0;
  sarray<GLYPH> glyphs; UINT glyphn = 0; USHORT dict[GLYPH_DICT] = { 0 };
  sarray<KERNINGPAIR> kern; sarray<USHORT> kerndict;
  static CFont* first; CFont* next;
  CFont() { Critical crit; next = first; first = this; }
  ~CFont() { relres(0); auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  static void relres(); void relres(int);
  int getdict(WCHAR c)
  {
    auto t = c % GLYPH_DICT;
    for (t = dict[t] - 1; t >= 0; t = glyphs.p[t].next - 1) if (glyphs.p[t].c == c) return t;
    return -1;
  }
  void create(); void create(LPCWSTR s, UINT n);
  XMFLOAT2 getextent(LPCWSTR s, UINT n);
  void draw(CView*, XMFLOAT2 xy, LPCWSTR s, UINT n);
  HRESULT __stdcall get_Name(BSTR* p) { *p = SysAllocString(name.p); return 0; }
  HRESULT __stdcall get_Size(FLOAT* p) { *p = size; return 0; }
  HRESULT __stdcall get_Style(UINT* p) { *p = style; return 0; }
  HRESULT __stdcall get_Ascent(FLOAT* p) { if (!hFont) create(); *p = ascent * size; return 0; };
  HRESULT __stdcall get_Descent(FLOAT* p) { if (!hFont) create(); *p = descent * size; return 0; };
  HRESULT __stdcall get_Height(FLOAT* p) { if (!hFont) create(); *p = (ascent + descent) * size; return 0; };
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICDXFont) || riid == __uuidof(IAgileObject))
    {
      *p = static_cast<ICDXFont*>(this); InterlockedIncrement(&refcount); return 0;
    }
    return E_NOINTERFACE;
  }
  ULONG __stdcall AddRef(void)
  {
    return InterlockedIncrement(&refcount);
  }
  ULONG __stdcall Release(void)
  {
    auto count = InterlockedDecrement(&refcount);
    if (!count)
    {
      Critical crit;
      if (refcount != 0) 
        return refcount;
      delete this;
    }
    return count;
  }
};
