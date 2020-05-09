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
#include <atlctl.h>

#include <corecrt_math_defines.h>

#pragma warning(disable:4530)
#include <ppl.h>

#if _DEBUG
const bool Debug = true;
#else
const bool Debug = false;
#endif

#ifdef _DEBUG
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
#define TRACE (void*)
#endif

#if(!_M_X64)
#define _BitScanReverse64 _InlineBitScanReverse64
#else
#endif

#define USE_SSE _M_X64

#if(USE_SSE)

__forceinline __m128d __vectorcall _mm_cross_pd(const __m128d a, const __m128d b)
{
  __m128d t = _mm_mul_pd(a, _mm_shuffle_pd(b, b, _MM_SHUFFLE2(0, 1)));
  t = _mm_hsub_pd(t, t); return t;
}
__forceinline __m128d __vectorcall _mm_dot_pd(const __m128d a)
{
  __m128d t = _mm_mul_pd(a, a); t = _mm_hadd_pd(t, t); return t;
}

#endif

template <class T>
void qsort(int* a, T* b, int l, int r)
{
  int i = l, j = r;
  auto p = a[(l + r) >> 1];
  while (i <= j)
  {
    while (a[i] < p) i++;
    while (a[j] > p) j--;
    if (i <= j)
    {
      auto t1 = a[i]; a[i] = a[j]; a[j] = t1;
      auto t2 = b[i]; b[i] = b[j]; b[j] = t2;   i++; j--;
    }
  }
  if (l < j) qsort(a, b, l, j);
  if (i < r) qsort(a, b, i, r);
}

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

UINT getrtid();

__forceinline bool decode(const UINT* p) { return p[0] > p[1]; }
static void encode(UINT* p, bool v)
{
  if (p[0] > p[1] == v) return;
  int t = p[0], x = p[1] > p[2] == v ? 1 : 2;
  p[0] = p[x]; p[x] = p[x ^ 3]; p[x ^ 3] = t;
}

#define CHR(F) { int __hr; if((__hr = (F)) < 0) return __hr; }

static int writecount(IStream* str, UINT c)
{
  BYTE bb[8]; int e = 0;
  for (; c >= 0x80; bb[e++] = c | 0x80, c >>= 7); bb[e++] = c;
  return str->Write(bb, e, 0);
}
static int readcount(IStream* str, UINT& pc)
{
  pc = 0;
  for (UINT shift = 0; ; shift += 7)
  {
    UINT b = 0; CHR(str->Read(&b, 1, 0));
    pc |= (b & 0x7F) << shift; if ((b & 0x80) == 0) break;
  }
  return 0;
}

template<class T>
void swap(T& a, T& b) { T t = a; a = b; b = t; }
