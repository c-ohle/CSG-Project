#pragma once

#ifndef STRICT
#define STRICT
#endif

#include "targetver.h"

#define _ATL_APARTMENT_THREADED
#define _ATL_NO_AUTOMATIC_NAMESPACE
#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS
#define ATL_NO_ASSERT_ON_DESTROY_NONEXISTENT_WINDOW

#include "resource.h"
#include <atlbase.h>
#include <atlcom.h>
//#include <atlctl.h>

#include <directxmath.h>
#include <directxpackedvector.h>
#include <d3d11.h>

#include "..\csg\csg_i.h"

#define XMASSERT assert
#ifdef _DEBUG
const BOOL Debug = true;
static bool _trace(TCHAR* format, ...)
{
  TCHAR buffer[1000];
  va_list argptr;
  va_start(argptr, format);
  wvsprintf(buffer, format, argptr);
  va_end(argptr);
  OutputDebugString(buffer);
  return true;
}
#define TRACE _trace
#else
const BOOL Debug = false;
#define TRACE (void*)
#endif

using namespace ATL;
using namespace DirectX;
using namespace DirectX::PackedVector;

#define CHR(F) { int __hr; if((__hr = (F)) < 0) return __hr; }

extern void* baseptr, * stackptr;
#define __align16(p) ((size_t)(p) & 0xf ? (void*)((((size_t)(p) >> 4) + 1) << 4) : p)

template<class T>
struct sarray
{
  UINT n = 0; T* p = 0;
  ~sarray() { ::free(p); }
  __forceinline T& operator[](UINT i)
  {
    _ASSERT(i < n); return p[i];
  }
  void setsize(UINT l)
  {
    if (l == n) return;
    auto a = p ? _msize(p) / sizeof(T) : 0;
    if (l > a) p = (T*)::realloc(p, l * sizeof(T));
    n = l;
  }
  void setsize2(UINT l)
  {
    if (l == 0) { if (p) { ::free(p); p = 0; n = 0; } return; }
    setsize(l);
  }
  void freeextra()
  {
    auto a = p ? _msize(p) / sizeof(T) : 0;
    if (a > n) p = n ? (T*)::realloc(p, n * sizeof(T)) : 0;
  }
  void copyto(sarray& b) const
  {
    b.setsize(n); memcpy(b.p, p, n * sizeof(T));
  }
  void copy(const T* s, UINT n)
  {
    setsize(n); memcpy(p, s, n * sizeof(T));
  }
  T* getptr(UINT c, UINT s = 8)
  {
    if (n < c)
      setsize(((c >> s) + 1) << s);
    return p;
  }
  void clear()
  {
    memset(p, 0, n * sizeof(T));
  }
};

template<class T>
struct carray
{
  UINT n = 0; T* p = 0;
  ~carray() { setsize(0); ::free(p); }
  __forceinline T& operator[](UINT i)
  {
    _ASSERT(i < n); return p[i];
  }
  void setsize(UINT l)
  {
    while (n > l) ((WP*)&p[--n])->WP::~WP();
    if (l == n) return;
    auto a = p ? _msize(p) / sizeof(T) : 0;
    if (l > a) p = (T*)::realloc(p, l * sizeof(T));
    while (n < l) ((WP*)&p[n++])->WP::WP();
  }
  void freeextra()
  {
    auto a = p ? _msize(p) / sizeof(T) : 0;
    if (a > n) p = n ? (T*)::realloc(p, n * sizeof(T)) : 0;
  }
  struct WP { T t; };
  void copyto(carray& b) const
  {
    b.setsize(n); for (UINT i = 0; i < n; i++) b.p[i] = p[i];
  }
  void copy(const T* s, UINT n)
  {
    setsize(n); for (UINT i = 0; i < n; i++) p[i] = s[i];
  }
  T* getptr(UINT c, UINT s = 6)
  {
    if (n < c) setsize(((c >> s) + 1) << s);
    return p;
  }
};

ICSGFactory* _CSGFactory();

struct VERTEX { XMFLOAT3 p, n; XMFLOAT2 t; };

#define INV_CB_VS_PER_FRAME (INV_MM_VIEWPROJ | INV_VV_AMBIENT | INV_VV_LIGHTDIR)
#define INV_CB_VS_PER_OBJECT (INV_MM_WORLD)
#define INV_CB_PS_PER_OBJECT (INV_VV_DIFFUSE)
#define INV_MASK (INV_CB_VS_PER_FRAME | INV_CB_VS_PER_OBJECT | INV_CB_PS_PER_OBJECT | INV_TT_DIFFUSE)

#define MO_DEPTHSTENCIL_MASK		0x0000000f
#define MO_DEPTHSTENCIL_SHIFT		0
#define MO_DEPTHSTENCIL_COUNT		7
#define MO_DEPTHSTENCIL_ZWRITE	0x00000001
#define MO_DEPTHSTENCIL_STEINC	0x00000002
#define MO_DEPTHSTENCIL_STEDEC	0x00000003
#define MO_DEPTHSTENCIL_CLEARZ	0x00000004
#define MO_DEPTHSTENCIL_TWOSID	0x00000005
#define MO_DEPTHSTENCIL_CLEARS	0x00000006

#define MO_BLENDSTATE_MASK			0x000000f0
#define MO_BLENDSTATE_SHIFT			4
#define MO_BLENDSTATE_COUNT			3
#define MO_BLENDSTATE_SOLID			0x00000000
#define MO_BLENDSTATE_ALPHA			0x00000010
#define MO_BLENDSTATE_ALPHAADD	0x00000020

#define MO_RASTERIZER_MASK			0x00000f00
#define MO_RASTERIZER_SHIFT			8
#define MO_RASTERIZER_COUNT			6 //3 lh + 3 rh
#define MO_RASTERIZER_SOLID			0x00000000
#define MO_RASTERIZER_WIRE			0x00000100
#define MO_RASTERIZER_NOCULL		0x00000200

#define MO_SAMPLERSTATE_MASK		0x0000f000
#define MO_SAMPLERSTATE_SHIFT		12
#define MO_SAMPLERSTATE_COUNT		4
#define MO_SAMPLERSTATE_DEFAULT	0x00000000
#define MO_SAMPLERSTATE_VBORDER	0x00001000
#define MO_SAMPLERSTATE_FONT		0x00002000
#define MO_SAMPLERSTATE_IMAGE		0x00003000

#define MO_PSSHADER_MASK				0x000f0000
#define MO_PSSHADER_SHIFT				16
#define MO_PSSHADER_COUNT				5
#define MO_PSSHADER_COLOR 			0x00000000
#define MO_PSSHADER_TEXTURE			0x00010000
#define MO_PSSHADER_FONT				0x00020000
#define MO_PSSHADER_COLOR3D			0x00030000
#define MO_PSSHADER_TEXTURE3D		0x00040000
//#define MO_PSSHADER_SPEC3D			0x00050000
#define MO_PSSHADER_NULL				0x000f0000

#define MO_GSSHADER_MASK				0x00f00000
#define MO_GSSHADER_SHIFT				20
#define MO_GSSHADER_COUNT				2
#define MO_GSSHADER_SHADOW			0x00100000
#define MO_GSSHADER_OUTL3D			0x00200000

#define MO_VSSHADER_MASK				0x0f000000
#define MO_VSSHADER_SHIFT				24
#define MO_VSSHADER_COUNT				2
#define MO_VSSHADER_WORLD				0x01000000

#define MO_TOPO_MASK						0xf0000000
#define MO_TOPO_SHIFT						28
#define MO_TOPO_LINELIST				0x20000000
#define MO_TOPO_LINESTRIP				0x30000000
#define MO_TOPO_TRIANGLELIST		0x40000000
#define MO_TOPO_TRIANGLESTRIP		0x50000000
#define MO_TOPO_TRIANGLELISTADJ 0xC0000000


#define FDIS_HITTEST			0x0001
#define FDIS_REGION 			0x0002
#define FDIS_REGIONTEST		0x0004	
#define FDIS_ORTHOGRAPH	  0x0008	
#define FDIS_RECORD			  0x0010

#define VV_BKCOLOR	0
#define VV_AMBIENT	1
#define VV_LIGHTDIR 2
#define VV_DIFFUSE	3
#define VV_VIRTPOS	4
#define VV_OVERPOS	5
#define VV_RCSIZE	  6
#define VV_DPI		  7

#define MM_VIEWPROJ 0
#define MM_WORLD		1
#define MM_PLANE		2
#define MM_TEXTURE	3
//#define MM_OVERWLD	4

#define TT_DIFFUSE	0

#define INV_MM_VIEWPROJ				(0x000100 << MM_VIEWPROJ) 
#define INV_MM_WORLD					(0x000100 << MM_WORLD) 
#define INV_VV_AMBIENT				(0x000001 << VV_AMBIENT) 
#define INV_VV_DIFFUSE				(0x000001 << VV_DIFFUSE) 
#define INV_VV_LIGHTDIR				(0x000001 << VV_LIGHTDIR)
#define INV_TT_DIFFUSE				(0x010000 << TT_DIFFUSE)   
