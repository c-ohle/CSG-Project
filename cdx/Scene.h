#pragma once

struct CTexture : ICDXTexture
{
  UINT refcount = 1, fl = 0; //1: error 2: A8
  CComPtr<IStream> str; void init();
  CComPtr<ID3D11ShaderResourceView> p;
  static CTexture* first; CTexture* next;
  CTexture() { Critical crit; next = first; first = this; }
  ~CTexture() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  static CTexture* GetTexture(IStream*);
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICDXTexture) || riid == __uuidof(IAgileObject))
    {
      *p = static_cast<ICDXTexture*>(this); InterlockedIncrement(&refcount); return 0;
    }
    return E_NOINTERFACE;
  }
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
  HRESULT __stdcall GetStream(IStream** p) { str.CopyTo(p); return 0; }
};

struct Material
{
  UINT i = 0, n = 0, color = 0xff808080;
  CComPtr<CTexture> tex;
  void serialize(struct Archive& ar);
};

struct CCSGVAR : CSGVAR
{
  CCSGVAR(const XMFLOAT4X3A& m, BYTE n = 12) { vt = CSG_TYPE_FLOAT; count = n; *(const float**)&p = &m._11; }
  CCSGVAR(ICSGVector* p, UINT c, UINT l = 0) { vt = CSG_TYPE_RATIONAL; count = c; length = l; *(ICSGVector**)&this->p = p; }
  CCSGVAR(float p) { vt = CSG_TYPE_FLOAT; count = 0; length = 0; *(float*)&this->p = p; }
  CCSGVAR(const DECIMAL& p) { *(DECIMAL*)this = p; vt = CSG_TYPE_DECIMAL; count = 0; }
  CCSGVAR(XMFLOAT2* p, UINT n) { vt = CSG_TYPE_FLOAT; count = 2; length = n; *(XMFLOAT2**)&this->p = p; }
  CCSGVAR(XMFLOAT3* p, UINT n) { vt = CSG_TYPE_FLOAT; count = 3; length = n; *(XMFLOAT3**)&this->p = p; }
  CCSGVAR(UINT* p, UINT n) { vt = CSG_TYPE_INT; count = 1; length = n; *(UINT**)&this->p = p; }
  CCSGVAR(const DECIMAL* p, BYTE n) { vt = CSG_TYPE_DECIMAL; count = n; *(const DECIMAL**)&this->p = p; }
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
  CTexCoords() { Critical crit; next = first; first = this; }
  ~CTexCoords() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  static CTexCoords* Get(const XMFLOAT2* p, UINT n);
  static CTexCoords* first; CTexCoords* next;
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

#define NODE_FL_SELECT    1
#define NODE_FL_INSEL     2
#define NODE_FL_STATIC    4

struct CNode : public ICDXNode
{
  //~CNode() { auto s = SysAllocStringLen(name.p, name.n); TRACE(L"~CNode %ws\n", s); SysFreeString(s); }
  sarray<WCHAR> name; UINT flags = 0;
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
  struct CScene* getscene();
  CNode* getparent();
  bool ispart(CNode* main)
  {
    for (auto p = this; p; p = p->getparent()) if (p == main) return true;
    return false;
  }
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
  CNode* clone()
  {
    auto p = new CNode();
    p->name.copy(name.p, name.n);
    p->flags = flags;
    p->transform = transform.p;
    p->mesh = mesh.p;
    p->texcoords = texcoords.p;
    p->matrix = matrix;
    p->vb = vb.p;
    p->ib = ib.p;
    p->materials.setsize(materials.n);
    for (UINT i = 0; i < materials.n; i++) p->materials.p[i] = materials.p[i];
    return p;
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
  HRESULT __stdcall get_Index(UINT* p);
  HRESULT __stdcall put_Index(UINT p);
  HRESULT __stdcall get_IsSelect(BOOL* p);
  HRESULT __stdcall put_IsSelect(BOOL p);
  HRESULT __stdcall get_IsStatic(BOOL* p)
  {
    *p = (flags & NODE_FL_STATIC) != 0; return 0;
  }
  HRESULT __stdcall put_IsStatic(BOOL p)
  {
    if (p) flags |= NODE_FL_STATIC; else flags &= ~NODE_FL_STATIC; return 0;
  }
  HRESULT __stdcall get_Transform(CSGVAR* p);
  HRESULT __stdcall put_Transform(CSGVAR p);
  HRESULT __stdcall GetTransform(CSGVAR* m);
  HRESULT __stdcall SetTransform(CSGVAR m);
  HRESULT __stdcall get_Mesh(ICSGMesh** p)
  {
    if (*p = mesh.p) mesh.p->AddRef(); return 0;
  }
  HRESULT __stdcall put_Mesh(ICSGMesh* p)
  {
    mesh = (ICSGMesh*)p; vb.Release(); ib.Release();
    if (materials.n == 0) materials.setsize(1); return 0;
  }
  HRESULT __stdcall get_Color(UINT* p)
  {
    *p = materials.n != 0 ? materials.p[0].color : 0; return 0;
  }
  HRESULT __stdcall put_Color(UINT p)
  {
    if (materials.n == 0) materials.setsize(1);
    materials.p[0].color = p; return 0;
  }
  HRESULT __stdcall get_MaterialCount(UINT* p)
  {
    *p = materials.n; return 0;
  }
  HRESULT __stdcall put_MaterialCount(UINT p)
  {
    materials.setsize(p); return 0;
  }
  HRESULT __stdcall GetMaterial(UINT i, UINT* start, UINT* count, UINT* color, ICDXTexture** tex)
  {
    if (i >= materials.n) return E_INVALIDARG;
    auto& m = materials.p[i];
    if (materials.n == 1 && mesh.p)
    {
      UINT ni; mesh.p->get_IndexCount(&ni);
      m.i = 0; m.n = ni;
    }
    *start = m.i; *count = m.n; *color = m.color;
    if(*tex = m.tex.p) m.tex.p->AddRef();
    //if (*tex = (m.tex.p ? m.tex.p->str.p : 0)) (*tex)->AddRef();
    return 0;
  }
  HRESULT __stdcall SetMaterial(UINT i, UINT start, UINT count, UINT color, ICDXTexture* tex)
  {
    if (i >= materials.n) return E_INVALIDARG;
    auto& m = materials.p[i];
    if (start != -1) m.i = start;
    if (count != -1) m.n = count;
    m.color = color; m.tex = tex ? static_cast<CTexture*>(tex) : 0;
    return 0;
  }
  HRESULT __stdcall GetTexturCoords(CSGVAR* m);
  HRESULT __stdcall SetTexturCoords(CSGVAR m);
  HRESULT __stdcall AddNode(BSTR name, ICDXNode** p);
  CComPtr<IUnknown> tag;
  HRESULT __stdcall get_Tag(IUnknown** p)
  {
    return tag.CopyTo(p);
  }
  HRESULT __stdcall put_Tag(IUnknown* p)
  {
    tag = p; return 0;
  }
};

struct CScene : public ICDXScene
{
  UINT refcount = 1, count = 0;
  CDX_UNIT unit = CDX_UNIT_UNDEF;
  sarray<CNode*> nodes;
  CComPtr<IUnknown> tag;
  ~CScene()
  {
    //TRACE(L"~CScene\n");
    Clear();
  }
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
  HRESULT __stdcall get_Unit(CDX_UNIT* p) { *p = unit; return 0; }
  HRESULT __stdcall put_Unit(CDX_UNIT p) { unit = p; return 0; }
  HRESULT __stdcall get_Count(UINT* p) { *p = count; return 0; }
  HRESULT __stdcall GetNode(UINT i, ICDXNode** p)
  {
    if (i >= count) return E_INVALIDARG;
    (*p = nodes.p[i])->AddRef(); return 0;
  }
  HRESULT __stdcall Select(UINT a, UINT f, UINT* p)
  {
    if (f & 0xf)
    {
      while (++a < count) if (nodes.p[a]->flags & f) { *p = a; return 0; }
    }
    else
    {
      auto t = (f >> 8) < count ? (void*)nodes.p[f >> 8] : this;
      while (++a < count) if (nodes.p[a]->parent == t) { *p = a; return 0; }
    }
    *p = -1; return 1;
  }
  HRESULT __stdcall Remove(UINT i);
  HRESULT __stdcall Insert(UINT i, ICDXNode* p);
  HRESULT __stdcall Clear();
  HRESULT __stdcall AddNode(BSTR name, ICDXNode** p);
  HRESULT __stdcall SaveToStream(IStream* str);
  HRESULT __stdcall LoadFromStream(IStream* str);
  HRESULT __stdcall get_Tag(IUnknown** p)
  {
    return tag.CopyTo(p);
  }
  HRESULT __stdcall put_Tag(IUnknown* p)
  {
    tag = p; return 0;
  }
};

