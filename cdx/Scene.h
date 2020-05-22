#pragma once

struct CTexture
{
  UINT refcount = 1;// , hash;
  CComPtr<ID3D11ShaderResourceView> p;
  CComPtr<IStream> str;
  static CTexture* first; CTexture* next;
  CTexture() { Critical crit; next = first; first = this; }
  ~CTexture() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  ULONG __stdcall AddRef(void) { return InterlockedIncrement(&refcount); }
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

struct Material
{
  UINT i = 0, n = 0, color = 0xff808080;
  CComPtr<CTexture> tex;
  void serialize(struct Archive& ar);
  void update(struct CScene* scene);
};

struct CCSGVAR : CSGVAR
{
  CCSGVAR(const XMFLOAT4X3A& m) { vt = CSG_TYPE_FLOAT; count = 12; *(const float**)&p = &m._11; }
  CCSGVAR(ICSGVector* p, UINT c, UINT l = 0) { vt = CSG_TYPE_RATIONAL; count = c; length = l; *(ICSGVector**)&this->p = p; }
  CCSGVAR(XMFLOAT2* p, UINT n) { vt = CSG_TYPE_FLOAT; count = 2; length = n; *(XMFLOAT2**)&this->p = p; }
  CCSGVAR(XMFLOAT3* p, UINT n) { vt = CSG_TYPE_FLOAT; count = 3; length = n; *(XMFLOAT3**)&this->p = p; }
  CCSGVAR(UINT* p, UINT n) { vt = CSG_TYPE_INT; count = 1; length = n; *(UINT**)&this->p = p; }
  static XMMATRIX copy(ICSGVector* source) { XMFLOAT4X3A m; CCSGVAR r(m); source->GetValue(0, &r); return XMLoadFloat4x3A(&m); }
};

struct CVertices
{
  UINT refcount = 1, hash; CComPtr<ID3D11Buffer> p; sarray<XMFLOAT3> hull;
  static CVertices* first; CVertices* next;
  CVertices() { Critical crit; next = first; first = this; }
  ~CVertices() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  ULONG __stdcall AddRef(void) { return InterlockedIncrement(&refcount); }
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

struct CIndices
{
  UINT refcount = 1, hash; CComPtr<ID3D11Buffer> p;
  static CIndices* first; CIndices* next;
  CIndices() { Critical crit; next = first; first = this; }
  ~CIndices() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  ULONG __stdcall AddRef(void) { return InterlockedIncrement(&refcount); }
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

struct CTexCoords : sarray<XMFLOAT2>
{
  UINT refcount = 1;
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
};

//#define NODE_FL_SELECT    1
//#define NODE_FL_BUILDOK   2

struct CNode : public ICDXNode
{
  sarray<WCHAR> name; //UINT flags;
  CComPtr<ICSGVector> transform;
  CComPtr<ICSGMesh> mesh;
  carray<Material> materials;
  CComPtr<CTexCoords> texcoords;
  XMMATRIX matrix = XMMatrixIdentity();
  IUnknown* parent = 0;
  CComPtr<CVertices> vb;
  CComPtr<CIndices> ib;
  void update();
  void update(XMFLOAT3* pp, UINT np, UINT* ii, UINT ni, float smooth = 0, void* tex = 0, UINT fl = 0);
  void serialize(struct Archive& ar);
  XMMATRIX gettrans(IUnknown* scene)
  {
    if (parent == scene) return matrix;
    return matrix * static_cast<CNode*>(parent)->gettrans(scene);
  }
  UINT refcount = 1;
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICDXNode) || riid == __uuidof(IAgileObject))
    {
      *p = static_cast<ICDXNode*>(this); InterlockedIncrement(&refcount); return 0;
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
#ifdef __WIN32
  void* operator new(size_t s) { return _aligned_malloc(s, 16); }
  void operator delete(void* p) { _aligned_free(p); }
#endif
  HRESULT __stdcall get_Name(BSTR* p) { *p = SysAllocStringLen(name.p, name.n); return 0; }
  HRESULT __stdcall put_Name(BSTR p) { name.setsize2(p ? SysStringLen(p) : 0); memcpy(name.p, p, name.n << 1);  return 0; }
  HRESULT __stdcall get_Parent(ICDXNode** p);
  HRESULT __stdcall put_Parent(ICDXNode* p);
  HRESULT __stdcall get_Scene(ICDXScene** p);
  HRESULT __stdcall get_TransformF(XMFLOAT4X3* p)
  {
    XMStoreFloat4x3(p, matrix); return 0;
  }
  HRESULT __stdcall put_TransformF(XMFLOAT4X3 p)
  {
    matrix = XMLoadFloat4x3(&p); transform.Release(); return 0;
  }
  HRESULT __stdcall get_Transform(CSGVAR* p)
  {
    ICSGVector* v; CHR(_CSGFactory()->CreateVector(12, &v));
    if (!transform.p) { XMFLOAT4X3A m; XMStoreFloat4x3A(&m, matrix); v->SetValue(0, CCSGVAR(m)); }
    else v->Copy(0, transform.p, 0, 12);
    *p = CCSGVAR(v, 12); return 0;
  }
  HRESULT __stdcall put_Transform(CSGVAR p)
  {
    if (p.vt == CSG_TYPE_RATIONAL && p.count == 12)
    {
      auto s = *(ICSGVector**)&p.p;
      if (!transform.p) _CSGFactory()->CreateVector(12, &transform.p);
      transform.p->Copy(0, s, p.length, 12); matrix = CCSGVAR::copy(transform.p);
      return 0;
    }
    return E_INVALIDARG;
  }
  HRESULT __stdcall SetTransform(CSGVAR m)
  {
    if (!transform.p) _CSGFactory()->CreateVector(12, &transform.p);
    transform.p->SetValue(0, m); matrix = CCSGVAR::copy(transform.p);
    return 0;
  }
  HRESULT __stdcall get_Mesh(ICSGMesh** p)
  {
    if (*p = mesh.p) mesh.p->AddRef(); return 0;
  }
  HRESULT __stdcall put_Mesh(ICSGMesh* p)
  {
    mesh = (ICSGMesh*)p; vb.Release(); ib.Release(); return 0;
  }
  HRESULT __stdcall get_Color(UINT* p)
  {
    *p = materials.n != 0 ? materials.p[0].color : 0xff808080; return 0;
  }
  HRESULT __stdcall put_Color(UINT p)
  {
    if (materials.n == 0) materials.setsize(1);
    materials.p[0].color = p;
    return 0;
  }
  HRESULT __stdcall get_MaterialCount(UINT* p)
  {
    *p = materials.n; return 0;
  }
  HRESULT __stdcall put_MaterialCount(UINT p)
  {
    materials.setsize(p); return 0;
  }
  HRESULT __stdcall GetMaterial(UINT i, UINT* start, UINT* count, UINT* color, IStream** tex)
  {
    if (i >= materials.n) return E_INVALIDARG;
    auto& m = materials.p[i]; *start = m.i; *count = m.n; *color = m.color;
    if (*tex = (m.tex.p ? m.tex.p->str.p : 0)) (*tex)->AddRef();
    return 0;
  }
  HRESULT __stdcall SetMaterial(UINT i, UINT start, UINT count, UINT color, IStream* tex)
  {
    if (i >= materials.n) return E_INVALIDARG;
    auto& m = materials.p[i];
    if (start != -1) m.i = start;
    if (count != -1) m.n = count;
    m.color = color;
    if (!tex) { m.tex.Release(); return 0; }
    { Critical crit; for (auto p = CTexture::first; p; p = p->next) if (p->str.p == tex) { m.tex = p; return 0; } }
    m.tex.Release(); m.tex.p = new CTexture(); m.tex->str = tex;
    return 0;
  }
  HRESULT __stdcall GetTexturCoords(CSGVAR* m);
  HRESULT __stdcall SetTexturCoords(CSGVAR m);
  HRESULT __stdcall AddNode(BSTR name, ICDXNode** p);

  sarray<XMFLOAT3>* gethull()
  {
    auto& a = vb.p->hull;
    if (!a.n)
    {
      UINT nv; mesh.p->get_VertexCount(&nv); a.setsize(nv);
      mesh.p->CopyBuffer(0, 0, CCSGVAR(a.p, nv));
    }
    return &a;
  }
};

struct CScene : public ICDXScene
{
  sarray<CNode*> nodes; UINT count = 0;
  ~CScene()
  {
    Clear();
    //for (UINT i = 0; i < count; i++)
    //{
    //  auto p = nodes.p[i]; p->parent = 0; 
    //  p->relres(); p->Release(); 
    //}
  }
  UINT refcount = 1;
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICDXScene) || riid == __uuidof(IAgileObject))
    {
      InterlockedIncrement(&refcount); *p = static_cast<ICDXScene*>(this); return 0;
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
  HRESULT __stdcall get_Count(UINT* p) { *p = count; return 0; }
  HRESULT __stdcall GetNode(UINT i, ICDXNode** p)
  {
    if (i >= count) return E_INVALIDARG;
    (*p = nodes.p[i])->AddRef(); return 0;
  }
  HRESULT __stdcall Remove(UINT i);
  HRESULT __stdcall Clear();
  HRESULT __stdcall AddNode(BSTR name, ICDXNode** p)
  {
    auto t = new CNode(); t->parent = this; t->put_Name(name);
    auto a = nodes.getptr(count + 1, 2); a[count++] = t; (*p = t)->AddRef();
    return 0;
  }
  HRESULT __stdcall SaveToStream(IStream* str);
  HRESULT __stdcall LoadFromStream(IStream* str);
};

//struct HullPoints : sarray<XMFLOAT3>, IUnknown
//{
//  static HullPoints* get(const CNode& node)
//  {
//    HullPoints* pts = 0; UINT ns = sizeof(pts);
//    node.vb.p->GetPrivateData(__uuidof(ICDXNode), &ns, &pts);
//    if (!pts)
//    {
//      pts = new HullPoints(); node.vb.p->SetPrivateDataInterface(__uuidof(ICDXNode), pts);
//      UINT nv; node.mesh.p->get_VertexCount(&nv); pts->setsize(nv);
//      node.mesh.p->CopyBuffer(0, 0, CCSGVAR(pts->p, nv));
//    }
//    pts->Release(); return pts;
//  }
//  UINT refcount = 1;
//  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
//  {
//    return E_NOINTERFACE;
//  }
//  ULONG __stdcall AddRef(void)
//  {
//    return InterlockedIncrement(&refcount);
//  }
//  ULONG __stdcall Release(void)
//  {
//    auto count = InterlockedDecrement(&refcount);
//    if (!count) delete this;
//    return count;
//  }
//};
