#include "pch.h"
#include "TesselatorDbl.h"
#include "TesselatorRat.h"
#include "Factory.h"
#include "Mesh.h"

thread_local UINT Rational::buffer[];

HRESULT CFactory::get_Version(UINT* pVal)
{
  auto p = (BYTE*)pVal;
  p[0] = sizeof(void*);
  p[1] = Debug ? 1 : 0;
  p[2] = 0;
  p[3] = 1;
  return S_OK;
}
HRESULT CFactory::CreateTesselator(CSG_UNIT unit, ICSGTesselator** ptess)
{
  switch (unit)
  {
  case CSG_UNIT_DOUBLE: *ptess = new CTesselatorDbl(); return 0;
  case CSG_UNIT_RATIONAL: *ptess = new CTesselatorRat(); return 0;
  }
  return E_NOTIMPL;
}
HRESULT CFactory::CreateVector(UINT len, ICSGVector** p)
{
  *p = CVector::Create(max(len, 1));
  return 0;
}
HRESULT CFactory::CreateMesh(ICSGMesh** p)
{
  *p = new CMesh(); return 0;
}

void conv(Rational* rr, UINT nr, const CSGVAR& v)
{
  UINT count = min(v.count, nr);
  const void* s = (const void*)v.p; if (!count) { count = 1; s = v.vt == CSG_TYPE_DECIMAL ? &v.p - 1 : &v.p; }
  for (UINT i = 0; i < count; i++)
    switch (v.vt)
    {
    case CSG_TYPE_INT: rr[i] = ((const int*)s)[i]; continue;
    case CSG_TYPE_FLOAT: rr[i] = ((const float*)s)[i]; continue;
    case CSG_TYPE_DOUBLE: rr[i] = ((const double*)s)[i]; continue;
    case CSG_TYPE_DECIMAL: rr[i] = ((const DECIMAL*)s)[i]; continue;
    case CSG_TYPE_RATIONAL: rr[i] = (&static_cast<const CVector*>(((const ICSGVector*)s))->val)[v.length + i]; continue;
    case CSG_TYPE_STRING:
    {
      auto p = (LPCWSTR)s; UINT n = 0;  for (; !(p[n] > ' ' && p[n + 1] <= ' '); n++) if (!p[n]) goto ex;
      rr[i] = Rational::Parse(p, n + 1); s = p + n + 2; *(LPWSTR*)&v.p = (LPWSTR)s; continue;
    }
    }
ex: for (; count < nr; count++) rr[count] = 0;
}
void conv(CSGVAR& v, const Rational* rr, UINT nr)
{
  UINT count = min(v.count, nr);
  void* s = (void*)v.p; if (!count) { count = 1; s = v.vt == CSG_TYPE_DECIMAL ? &v.p - 1 : &v.p; }
  for (UINT i = 0; i < count; i++)
    switch (v.vt)
    {
    case CSG_TYPE_INT: ((int*)s)[i] = (int)(double)rr[i]; continue;
    case CSG_TYPE_FLOAT: ((float*)s)[i] = (float)(double)rr[i]; continue;
    case CSG_TYPE_DOUBLE: ((double*)s)[i] = (double)rr[i]; continue;
    case CSG_TYPE_DECIMAL: ((DECIMAL*)s)[i] = (DECIMAL)rr[i]; continue;
    case CSG_TYPE_RATIONAL: (&static_cast<CVector*>(((ICSGVector*)s))->val)[v.length + i] = rr[i]; continue;
    case CSG_TYPE_STRING:
    {
      auto t = rr[i].ToString(v.length ? v.length : 64, 0x1000 | v.dummy); lstrcpy((LPWSTR)s, t);
      if (i + 1 < count) { s = ((LPWSTR)s) + ((UINT*)t)[-1] + 1; ((LPWSTR)s)[-1] = ' '; } continue;
    }
    }
}
void conv(double* rr, UINT nr, const CSGVAR& v)
{
  UINT count = min(v.count, nr);
  const void* s = (const int*)v.p; if (!count) { count = 1; s = v.vt == CSG_TYPE_DECIMAL ? &v.p - 1 : &v.p; }
  for (UINT i = 0; i < count; i++)
    switch (v.vt)
    {
    case CSG_TYPE_INT: rr[i] = ((const int*)s)[i]; continue;
    case CSG_TYPE_FLOAT: rr[i] = ((const float*)s)[i]; continue;
    case CSG_TYPE_DOUBLE: rr[i] = ((const double*)s)[i]; continue;
    case CSG_TYPE_DECIMAL: VarR8FromDec(&((const DECIMAL*)s)[i], &rr[i]); continue;
    case CSG_TYPE_RATIONAL: rr[i] = (double)(&static_cast<const CVector*>(((const ICSGVector*)s))->val)[v.length + i]; continue;
    }
  for (; count < nr; count++) rr[count] = 0;
}
void conv(CSGVAR& v, const double* rr, UINT nr)
{
  UINT count = min(v.count, nr);
  void* s = (int*)v.p; if (!count) { count = 1; s = v.vt == CSG_TYPE_DECIMAL ? &v.p - 1 : &v.p; }
  for (UINT i = 0; i < count; i++)
    switch (v.vt)
    {
    case CSG_TYPE_INT: ((int*)s)[i] = (int)rr[i]; continue;
    case CSG_TYPE_FLOAT: ((float*)s)[i] = (float)rr[i]; continue;
    case CSG_TYPE_DOUBLE: ((double*)s)[i] = rr[i]; continue;
    case CSG_TYPE_DECIMAL: VarDecFromR8(rr[i], &((DECIMAL*)s)[i]); continue;
    case CSG_TYPE_RATIONAL: (&static_cast<CVector*>(((ICSGVector*)s))->val)[v.length + i] = rr[i]; continue;
    }
}


