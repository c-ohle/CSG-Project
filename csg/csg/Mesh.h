#pragma once

static bool decode(UINT* p) { return p[0] > p[1]; }
static void encode(UINT* p, bool v)
{
  if (p[0] > p[1] == v) return;
  int t = p[0], x = p[1] > p[2] == v ? 1 : 2;
  p[0] = p[x]; p[x] = p[x ^ 3]; p[x ^ 3] = t;
}

struct CMesh : public ICSGMesh
{
  UINT refcount = 1;
  sarray<UINT> ii;
  carray<Vector3R> pp;
  carray<Vector4R> ee;
  void clear()
  {
    ii.setsize(0); pp.setsize(0); ee.setsize(0);
  }
  void invert()
  {
    for (UINT i = 0; i < ii.n; i += 3) { auto t = ii.p[i + 1]; ii.p[i + 1] = ii.p[i + 2]; ii.p[i + 2] = t; }
  }
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICSGMesh) || riid == __uuidof(IAgileObject))
    {
      InterlockedIncrement(&refcount); *p = static_cast<ICSGMesh*>(this); return 0;
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
    if (!count) delete this;
    return count;
  }
  HRESULT __stdcall CopyBuffer(UINT ib, UINT ab, CSGVAR* p)
  {
    switch (ib)
    {
    case 0:
    {
      auto d = p->count * __sizeof((CSG_TYPE)p->vt);
      for (UINT i = 0, n = min(p->length, pp.n - ab); i < n; i++, *(BYTE**)&p->p += d)
        conv(*p, &pp.p[ab + i].x, 3);
      return 0;
    }
    case 1:
    {
      if (p->count != 1 || p->vt != CSG_TYPE_INT) return E_INVALIDARG;
      memcpy((int*)p->p, ii.p + ab, min(p->length, ii.n - ab) * sizeof(int));
      return 0;
    }
    case 2:
    {
      auto d = p->count * __sizeof((CSG_TYPE)p->vt);
      for (UINT i = 0, n = min(p->length, ee.n - ab); i < n; i++, *(BYTE**)&p->p += d)
        conv(*p, &ee.p[ab + i].x, 3);
      return 0;
    }
    }
    return E_INVALIDARG;
  }
  HRESULT __stdcall get_VertexCount(UINT* p)
  {
    *p = pp.n; return 0;
  }
  HRESULT __stdcall VertexAt(UINT i, CSGVAR* p)
  {
    if ((UINT)i >= (UINT)pp.n) return E_INVALIDARG;
    conv(*p, &pp.p[i].x, 3); return 0;
  }
  HRESULT __stdcall get_IndexCount(UINT* p)
  {
    *p = ii.n; return 0;
  }
  HRESULT __stdcall IndexAt(UINT i, UINT* p)
  {
    if ((UINT)i >= (UINT)ii.n) return -1;
    *p = ii.p[i];  return 0;
  }
  HRESULT __stdcall get_PlaneCount(UINT* p)
  {
    *p = ee.n; return 0;
  }
  HRESULT __stdcall PlaneAt(UINT i, CSGVAR* p)
  {
    if ((UINT)i >= (UINT)ee.n) return -1;
    conv(*p, &ee.p[i].x, 4); return 0;
  }
  HRESULT __stdcall Update(CSGVAR vertices, CSGVAR indices)
  {
    if (indices.count != 1 || indices.vt != CSG_TYPE_INT) return E_INVALIDARG;
    ee.setsize(0); ii.copy((UINT*)indices.p, indices.length);
    pp.setsize(vertices.length);
    if (vertices.vt == CSG_TYPE_RATIONAL) vertices.length = 0;
    auto d = vertices.count * __sizeof((CSG_TYPE)vertices.vt);
    for (UINT i = 0; i < pp.n; i++, vertices.p += d) conv(&pp.p[i].x, 3, vertices);
    if (ii.n) encode(ii.p, false);
    return 0;
  }
  HRESULT __stdcall CopyTo(ICSGMesh* p)
  {
    auto& m = *static_cast<CMesh*>(p);
    ii.copyto(m.ii);
    pp.copyto(m.pp);
    ee.copyto(m.ee);
    return 0;
  }
  HRESULT __stdcall Transform(CSGVAR m)
  {
    if (ee.n) ee.setsize(0);
    if (m.vt == CSG_TYPE_RATIONAL)
    {
      if (m.count == 12)
      {
        auto& p = *static_cast<const CVector*>((ICSGVector*)m.p);
        Matrix3x4R::Transform(pp.p, pp.n, *(const Matrix3x4R*)(&p.val + m.length)); return 0;
      }
    }
    Matrix3x4R t; conv(t.m[0], 12, m);
    Matrix3x4R::Transform(pp.p, pp.n, t); return 0;
  }
  HRESULT __stdcall WriteToStream(IStream* str)
  {
    CHR(writecount(str, 0));
    CHR(writecount(str, pp.n)); for (UINT i = 0; i < pp.n; i++) CHR(pp.p[i].write(str));
    CHR(writecount(str, ii.n / 3)); for (UINT i = 0; i < ii.n; i++) CHR(writecount(str, ii.p[i]));
    return 0;
  }
  HRESULT __stdcall ReadFromStream(IStream* str)
  {
    UINT ct; CHR(readcount(str, ct)); if (ct != 0) return -1; if (ee.p) ee.setsize(0);
    UINT np; CHR(readcount(str, np)); pp.setsize(np); for (UINT i = 0; i < pp.n; i++) CHR(pp[i].read(str));
    UINT ni; CHR(readcount(str, ni)); ii.setsize(ni * 3); for (UINT i = 0; i < ii.n; i++) CHR(readcount(str, ii.p[i]));
    return 0;
  }
  HRESULT __stdcall CreateBox(CSGVAR a, CSGVAR b)
  {
    ee.setsize(0); pp.setsize(8); ii.setsize(36); auto p = pp.p;
    Vector3R ab[2]; conv(&ab[0].x, 3, a); conv(&ab[1].x, 3, b);
    if (ab[0].x > ab[1].x) swap(ab[0].x, ab[1].x);
    if (ab[0].y > ab[1].y) swap(ab[0].y, ab[1].y);
    if (ab[0].z > ab[1].z) swap(ab[0].z, ab[1].z);
    p[0].x = ab[0].x; p[0].y = ab[0].y; p[0].z = ab[0].z;
    p[1].x = ab[1].x; p[1].y = ab[0].y; p[1].z = ab[0].z;
    p[2].x = ab[1].x; p[2].y = ab[1].y; p[2].z = ab[0].z;
    p[3].x = ab[0].x; p[3].y = ab[1].y; p[3].z = ab[0].z;
    p[4].x = ab[0].x; p[4].y = ab[0].y; p[4].z = ab[1].z;
    p[5].x = ab[1].x; p[5].y = ab[0].y; p[5].z = ab[1].z;
    p[6].x = ab[1].x; p[6].y = ab[1].y; p[6].z = ab[1].z;
    p[7].x = ab[0].x; p[7].y = ab[1].y; p[7].z = ab[1].z;
    byte bb[36] = { 0, 2, 1, 2, 0, 3, 0, 1, 4, 1, 5, 4, 1, 2, 5, 2, 6, 5, 2, 3, 6, 3, 7, 6, 3, 0, 7, 0, 4, 7, 4, 5, 6, 6, 7, 4 };
    for (UINT i = 0; i < 36; i++) ii.p[i] = bb[i];
    return 0;
  }

};

