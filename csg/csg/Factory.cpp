#include "pch.h"
#include "TesselatorDbl.h"
#include "Factory.h"
#include "TesselatorRat.h"
#include "Mesh.h"

thread_local UINT Rational::buffer[];

HRESULT CCSGFactory::get_Version(UINT* pVal)
{
  auto p = (BYTE*)pVal;
  p[0] = sizeof(void*);
  p[1] = Debug ? 1 : 0;
  p[2] = p[3] = 1;
  return S_OK;
}
HRESULT CCSGFactory::CreateTesselator(CSG_UNIT unit, ICSGTesselator** ptess)
{
  switch (unit)
  {
  case CSG_UNIT_DOUBLE: *ptess = new CTesselatorDbl(); return 0;
  case CSG_UNIT_RATIONAL: *ptess = new CTesselatorRat(); return 0;
  }
  return E_NOTIMPL;
}
HRESULT CCSGFactory::CreateVector(UINT len, ICSGVector** p)
{
  *p = CVector::Create(max(len, 1));
  return 0;
}
HRESULT CCSGFactory::CreateMesh(ICSGMesh** p)
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
    case CSG_TYPE_BSTR: rr[i] = Rational::Parse(((BSTR*)s)[i], SysStringLen(((BSTR*)s)[i])); continue;
    }
  for (; count < nr; count++) rr[count] = 0;
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
    case CSG_TYPE_RATIONAL: rr[i] = (double)(&static_cast<const CVector*>(((const ICSGVector*)s))->val)[i]; continue;
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
    case CSG_TYPE_RATIONAL: (&static_cast<CVector*>(((ICSGVector*)s))->val)[i] = rr[i]; continue;
    }
}


