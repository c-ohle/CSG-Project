#pragma once

#ifndef STRICT
#define STRICT
#endif

#include "targetver.h"

#define _ATL_APARTMENT_THREADED
#define _ATL_NO_AUTOMATIC_NAMESPACE
#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS	// Einige CString-Konstruktoren sind explizit.
#define ATL_NO_ASSERT_ON_DESTROY_NONEXISTENT_WINDOW

#include "resource.h"
#include <atlbase.h>
#include <atlcom.h>
#include <atlctl.h>

#include <corecrt_math_defines.h>

#if _DEBUG
const bool Debug = true;
#else
const bool Debug = false;
#endif

#define USESSE 1

#if(USESSE)

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