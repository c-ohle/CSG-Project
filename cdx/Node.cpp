#include "pch.h"
#include "Factory.h"
#include "Scene.h"

CVertices* CVertices::first;
CIndices* CIndices::first;
CTexture* CTexture::first;

extern CComPtr<ID3D11Device> device;
extern CComPtr<ID3D11DeviceContext> context;

CScene* getscene(CNode* p)
{
  if (*(void**)p != *(void**)p->parent) return static_cast<CScene*>(p->parent);
  return getscene(static_cast<CNode*>(p->parent));
}

HRESULT CNode::AddNode(BSTR name, ICDXNode** p)
{
  CHR(getscene(this)->AddNode(name, p));
  static_cast<CNode*>(*p)->parent = this; return 0;
}

HRESULT CNode::get_Scene(ICDXScene** p)
{
  if (!parent) return 0;
  if (*(void**)this == *(void**)parent) return static_cast<CNode*>(parent)->get_Scene(p);
  (*p = static_cast<CScene*>(parent))->AddRef(); return 0;
}
HRESULT CNode::get_Parent(ICDXNode** p)
{
  if (!parent || *(void**)this != *(void**)parent) return 0;
  (*p = static_cast<CNode*>(parent))->AddRef(); return 0;
}
HRESULT CNode::put_Parent(ICDXNode* p)
{
  return E_NOTIMPL;
}

static bool operator ==(XMFLOAT2 a, XMFLOAT2 b)
{
  return a.x == b.x && a.y == b.y;
}

bool equals(ID3D11Buffer* buffer, const void* p, UINT n)
{
  D3D11_BUFFER_DESC bd; buffer->GetDesc(&bd);
  if (bd.ByteWidth != n) return false;
  bd.Usage = D3D11_USAGE_STAGING;
  bd.CPUAccessFlags = D3D11_CPU_ACCESS_READ; bd.BindFlags = bd.MiscFlags = bd.StructureByteStride = 0;
  CComPtr<ID3D11Buffer> tmp;
  auto hr = device.p->CreateBuffer(&bd, 0, &tmp.p);
  context.p->CopyResource(tmp.p, buffer);
  D3D11_MAPPED_SUBRESOURCE map;
  hr = context.p->Map(tmp.p, 0, D3D11_MAP_READ, 0, &map);
  n = memcmp(p, map.pData, n);
  context->Unmap(tmp.p, 0);
  return n == 0;
}

//static GUID __hash128(const void* p, UINT n)
//{
//  XMASSERT(((size_t)p & 7) == 0);
//  __m128i v = _mm_cvtsi32_si128(n), f = _mm_set1_epi32(13); n >>= 4;
//  for (UINT i = 0; i < n; i++) v = _mm_add_epi64(_mm_mul_epi32(v, f), _mm_load_si128(((__m128i const*)p) + i));
//  GUID id; _mm_store_si128((__m128i*) & id, v); return id;
//}

void CNode::update(XMFLOAT3* pp, UINT np, UINT* ii, UINT ni, float smooth, void* tex, UINT fl)
{
  auto kk = (int*)__align16(stackptr); auto tt = (int*)__align16(kk + ni); XMASSERT(ni != 0);
  //auto kk = (int*)stackptr; auto tt = kk + ni; 
  DWORD e; _BitScanReverse(&e, ni); //auto e = msb(ni); 
  e = 1 << (e + 1); auto w = e - 1; //if(e > 15) e = 15 // 64k?
  auto dict = tt; memset(dict, 0, e << 2);
  for (UINT i = ni - 1, m = 0b010010; (int)i >= 0; i--, m = (m >> 1) | ((m & 1) << 5))
  {
    int j = i - (m & 3), k = j + ((m >> 2) & 3), v = j + ((m >> 1) & 3), h;
    dict[e] = k = ii[i] | (ii[k] << 16); dict[e + 1] = v;
    dict[e + 2] = dict[h = (k ^ ((k >> 16) * 31)) & w]; dict[h] = e; e += 3;
  }
  for (UINT i = 0, m = 0b100100; i < ni; i++, m = (m << 1) & 0b111111 | (m >> 5))
  {
    int j = i - (m & 3), k = (ii[j + ((m >> 2) & 3)]) | (ii[i] << 16), h = (k ^ ((k >> 16) * 31)) & w, t;
    for (t = dict[h]; t != 0; t = dict[t + 2]) if (dict[t] == k) { dict[t] = -1; break; }
    kk[i] = ii[t != 0 ? dict[t + 1] : j + ((m >> 1) & 3)];
  }
  auto vv = (VERTEX*)tt; auto vsmooth = XMVectorReplicate(smooth);
  for (UINT i = 0; i < np; i++)
  {
    XMStoreFloat4A((XMFLOAT4A*)&vv[i].p, XMLoadFloat4((XMFLOAT4*)&pp[i]));
    *(UINT64*)&vv[i].t.x = 0;
  }
  /////////////////
  for (UINT i = 0; i < ni; i += 3)
  {
    auto v1 = XMLoadFloat4A((XMFLOAT4A*)&vv[ii[i + 0]].p);
    auto v2 = XMLoadFloat4A((XMFLOAT4A*)&vv[ii[i + 1]].p);
    auto v3 = XMLoadFloat4A((XMFLOAT4A*)&vv[ii[i + 2]].p);
    auto vn = XMVector3Normalize(XMVector3Cross(v2 - v1, v3 - v1));
    for (int k = 0, j; k < 3; k++)
    {
      for (j = ii[i + k]; ;)
      {
        auto c = *(UINT*)&vv[j].t.x; if (c == 0) { XMStoreFloat3(&vv[j].n, vn); *(UINT*)&vv[j].t.x = 1; break; }
        auto nt = XMLoadFloat3(&vv[j].n);
        if (XMVector3LessOrEqual(XMVector3LengthSq((c == 1 ? nt : XMVector3Normalize(nt)) - vn), vsmooth))
        {
          XMStoreFloat3(&vv[j].n, vn + nt);
          *(UINT*)&vv[j].t.x = c + 1; break;
        }
        auto l = *(UINT*)&vv[j].t.y; if (l != 0) { j = l - 1; continue; }
        *(UINT*)&vv[j].t.y = np + 1; vv[np].p = vv[j].p; *(UINT64*)&vv[np].t.x = 0; j = np++;
      }
      kk[i + k] = (kk[i + k] << 16) | j;
    }
  }
  XMMATRIX mt; if ((fl & 1) != 0) mt = XMLoadFloat4x3((XMFLOAT4X3*)tex);
  for (UINT i = 0; i < np; i++)
  {
    XMStoreFloat3(&vv[i].n, XMVector3Normalize(XMLoadFloat3(&vv[i].n)));
    if ((fl & 1) == 0) { *(UINT64*)&vv[i].t = 0; continue; }
    XMStoreFloat2(&vv[i].t, XMVector3Transform(XMLoadFloat4A((XMFLOAT4A*)&vv[i].p), mt));
  }
  if ((fl & 2) != 0)
  {
    for (UINT i = 0, j; i < ni; i++)
    {
      auto p = ((XMFLOAT2*)tex)[i];
      if (vv[j = kk[i] & 0xffff].t == p) continue;
      if (*(UINT64*)&vv[j].t != 0)
      {
        vv[np] = vv[j];
        for (UINT t = i; t < ni; t++)
          if ((kk[t] & 0xffff) == j && ((XMFLOAT2*)tex)[t] == p)
            *(USHORT*)&kk[t] = (USHORT)(np & 0xffff);
        j = np++;
      }
      vv[j].t = p;
    }
  }

  UINT nn = sizeof(GUID), bv = np * sizeof(VERTEX), bk = ni * sizeof(int);
  UINT hc_vb = bv; for (UINT i = 0, n = min(1000, bv >> 2); i < n; i++) hc_vb = hc_vb * 13 + ((UINT*)vv)[i];
  UINT hc_ib = bk; for (UINT i = 0, n = min(1000, bk >> 2); i < n; i++) hc_ib = hc_ib * 13 + ((UINT*)kk)[i];
  {
    Critical crit;
    for (auto p = CVertices::first; p; p = p->next) if (p->hash == hc_vb && equals(p->p.p, vv, bv)) { vb = p; break; }
    for (auto p = CIndices::first; p; p = p->next) if (p->hash == hc_ib && equals(p->p.p, kk, bk)) { ib = p; break; }
  }
  if (vb.p && ib.p) return;

  D3D11_BUFFER_DESC bd = { 0, D3D11_USAGE_IMMUTABLE, 0, 0, 0, 0 }; D3D11_SUBRESOURCE_DATA data = { 0 };
  if (!vb.p)
  {
    vb.p = new CVertices(); vb.p->hash = hc_vb;
    bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
    bd.ByteWidth = np * sizeof(VERTEX); data.pSysMem = vv;
    device.p->CreateBuffer(&bd, &data, &vb.p->p.p);
  }
  if (!ib.p)
  {
    ib.p = new CIndices(); ib.p->hash = hc_ib;
    bd.BindFlags = D3D11_BIND_INDEX_BUFFER;
    bd.ByteWidth = ni * sizeof(int); data.pSysMem = kk;
    device.p->CreateBuffer(&bd, &data, &ib.p->p.p);
  }
}

void CNode::update()
{
  ib.Release(); vb.Release();
  UINT ni; mesh->get_IndexCount(&ni); if (ni == 0) return;
  UINT np; mesh->get_VertexCount(&np);
  auto pp = (XMFLOAT3*)stackptr;
  auto ii = (UINT*)(pp + np); stackptr = ii + ni;
  mesh->CopyBuffer(0, 0, CCSGVAR(pp, np));
  mesh->CopyBuffer(1, 0, CCSGVAR(ii, ni)); auto tc = texcoords.p && texcoords.p->n == ni ? texcoords.p->p : 0;
  update(pp, np, ii, ni, 0.2f, tc, tc ? 2 : 0); stackptr = pp;
  if (materials.n == 0) materials.setsize(1);
  for (UINT i = 0; i < materials.n; i++)
  {
    auto& ma = materials.p[i];
    if (i == 0 && materials.n == 1) { ma.i = 0; ma.n = ni; }
  }
}

//#include <functional>
//using namespace std; void ReadMap(function<void (int a, int b)> cc)

struct Archive
{
  IStream* str; bool storing; HRESULT hr = 0; UINT version;
  Archive(IStream* str, bool storing)
  {
    this->str = str;
    this->storing = storing;
  }
  void WriteCount(UINT c)
  {
    BYTE bb[8]; int e = 0;
    for (; c >= 0x80; bb[e++] = c | 0x80, c >>= 7); bb[e++] = c;
    str->Write(bb, e, 0);
  }
  UINT ReadCount()
  {
    UINT c = 0;
    for (UINT shift = 0; ; shift += 7)
    {
      UINT b = 0; str->Read(&b, 1, 0);
      c |= (b & 0x7F) << shift; if ((b & 0x80) == 0) break;
    }
    return c;
  }
  template<class T> void Write(const T* p, UINT n = 1)
  {
    auto hr = str->Write(p, n * sizeof(T), 0);
  }
  template<class T> void Read(T* p, UINT n = 1)
  {
    str->Read(p, n * sizeof(T), 0);
  }
  void SerialCount(UINT& c)
  {
    if (storing) WriteCount(c); else c = ReadCount();
  }
  template<class T> void Serialize(T* p, UINT n = 1)
  {
    if (storing) Write(p, n); else Read(p, n);
  }
  template<class T> void Serialize(sarray<T>& p)
  {
    if (storing) { WriteCount(p.n); Write(p.p, p.n); }
    else { p.setsize2(ReadCount()); Read(p.p, p.n); }
  }
  sarray<void*> map; UINT mapcount = 0;
  UINT getmap(void* p) { for (UINT i = 0; i < mapcount; i++) if (map.p[i] == p) return i + 1; return 0; }
  void addmap(void* p) { map.getptr(mapcount + 1)[mapcount++] = p; }
};

void Material::serialize(Archive& ar)
{
  UINT fl; if (ar.storing) ar.WriteCount(fl = (i ? 1 : 0) | (tex.p ? 2 : 0));
  else fl = ar.ReadCount();
  if (fl & 1) ar.SerialCount(i); ar.SerialCount(n);
  ar.Serialize(&color);
  if (!(fl & 2)) return;
  UINT x; IStream* ss, * ds;
  if (ar.storing)
  {
    x = ar.getmap(tex.p); ar.WriteCount(x); if (x != 0) return;
    ar.addmap(tex.p); STATSTG stat; (ss = tex.p->str.p)->Stat(&stat, STATFLAG_NONAME);
    x = *(UINT*)&stat.cbSize; ar.WriteCount(x);
    UINT64 l = 0; ss->Seek(*(LARGE_INTEGER*)&l, 0, 0); ds = ar.str;
  }
  else
  {
    x = ar.ReadCount(); if (x != 0) { tex = (CTexture*)ar.map.p[x - 1]; return; }
    x = ar.ReadCount(); tex.p = new CTexture(); ds = tex.p->str.p = SHCreateMemStream(0, 0); ar.addmap(tex.p); ss = ar.str;
  }
  UINT nv = 32768; auto pv = _alloca(nv);
  while (x) { ULONG c = min(x, nv); x -= c; ss->Read(pv, c, 0); ds->Write(pv, c, 0); }
}
void CNode::serialize(Archive& ar)
{
  if (ar.storing)
  {
    UINT fl = (name.n ? 1 : 0) | (transform.p ? 2 : 0) | (mesh.p ? 4 : 0) | (materials.n ? 8 : 0) | (texcoords.p ? 0x10 : 0);
    ar.WriteCount(fl);
    if (fl & 1) { ar.WriteCount(name.n); ar.Write(name.p, name.n); }
    if (fl & 2) transform.p->WriteToStream(ar.str, 0, 12);
    else { for (UINT t = 0; t < 4; t++) ar.Write(matrix.r[t].m128_f32, 3); }
    if (fl & 4)
    {
      UINT x = ar.getmap(mesh.p); ar.WriteCount(x);
      if (x == 0) { ar.addmap(mesh.p); mesh.p->WriteToStream(ar.str); }
    }
    if (fl & 8) ar.WriteCount(materials.n);
    if (fl & 0x10)
    {
      UINT x = ar.getmap(texcoords.p); ar.WriteCount(x);
      if (x == 0)
      {
        ar.addmap(texcoords.p); ar.WriteCount(texcoords.p->n);
        ar.str->Write(texcoords.p->p, texcoords.p->n * sizeof(XMFLOAT2), 0);
      }
    }
  }
  else
  {
    UINT fl = ar.ReadCount(); XMASSERT(!name.p && !transform.p && !mesh.p && !materials.n);
    if (fl & 1) { name.setsize(ar.ReadCount()); ar.Read(name.p, name.n); }
    if (fl & 2)
    {
      _CSGFactory()->CreateVector(12, &transform.p);
      transform.p->ReadFromStream(ar.str, 0, 12); matrix = CCSGVAR::copy(transform.p);
    }
    else  for (UINT t = 0; t < 4; t++) ar.Read(matrix.r[t].m128_f32, 3);
    if (fl & 4)
    {
      UINT x = ar.ReadCount();
      if (x == 0) { _CSGFactory()->CreateMesh(&mesh.p); mesh.p->ReadFromStream(ar.str); ar.addmap(mesh.p); }
      else mesh = (ICSGMesh*)ar.map.p[x - 1];
    }
    if (fl & 8) materials.setsize(ar.ReadCount());
    if (fl & 0x10)
    {
      UINT x = ar.ReadCount();
      if (x == 0)
      {
        texcoords.p = new CTexCoords(); ar.addmap(texcoords.p);
        texcoords.p->setsize(ar.ReadCount());
        ar.str->Read(texcoords.p->p, texcoords.p->n * sizeof(XMFLOAT2), 0);
      }
      else texcoords = (CTexCoords*)ar.map.p[x - 1];
    }

  }
  for (UINT i = 0; i < materials.n; i++) materials.p[i].serialize(ar);
}

HRESULT CScene::Remove(UINT i)
{
  if (i >= count) return E_INVALIDARG;
  auto p = nodes.p[i];
  for (UINT k = 0; k < count; k++)
    if (nodes.p[k]->parent == p)
      nodes.p[k]->parent = p->parent;
  p->Release();
  memcpy(nodes.p + i, nodes.p + i + 1, (--count - i) * sizeof(void*));
  return 0;
}
HRESULT CScene::Clear()
{
  if (!count) return  0;
  for (UINT i = 0; i < count; i++)
  {
    auto p = nodes.p[i]; p->parent = 0;
    p->Release();
  }
  count = 0; return 0;
}
HRESULT CScene::SaveToStream(IStream* str)
{
  Archive ar(str, true);
  ar.WriteCount(ar.version = 1);
  ar.WriteCount(count);
  for (UINT i = 0; i < count; i++)
  {
    UINT x = 0; auto p = nodes.p[i];
    if (p->parent != this) for (x = 1; nodes.p[x - 1] != p->parent; x++);
    ar.WriteCount(x);
  }
  for (UINT i = 0; i < count; i++) nodes.p[i]->serialize(ar);
  return ar.hr;
}
HRESULT CScene::LoadFromStream(IStream* str)
{
  Archive ar(str, false);
  if ((ar.version = ar.ReadCount()) > 1) return E_FAIL;
  Clear(); nodes.setsize(count = ar.ReadCount());
  for (UINT i = 0; i < count; i++) nodes.p[i] = new CNode();
  for (UINT i = 0; i < count; i++)
  {
    UINT x = ar.ReadCount();
    nodes.p[i]->parent = x == 0 ? (IUnknown*)this : nodes.p[x - 1];
  }
  for (UINT i = 0; i < count; i++) nodes.p[i]->serialize(ar);
  return 0;
}

HRESULT CNode::GetTexturCoords(CSGVAR* m)
{
  if (texcoords.p) *m = CCSGVAR(texcoords.p->p, texcoords.p->n);
  return 0;
}
HRESULT CNode::SetTexturCoords(CSGVAR m)
{
  ib.Release(); vb.Release();
  if (!m.vt) { texcoords.Release(); return 0; }
  if (m.vt != CSG_TYPE_FLOAT || m.count != 2) return E_INVALIDARG;
  auto scene = getscene(this);
  for (UINT i = 0; i < scene->count; i++)
  {
    auto p = scene->nodes.p[i]->texcoords.p; if (!p) continue;
    if (p->n != m.length || memcmp(p->p, *(void**)&m.p, m.length * sizeof(XMFLOAT2))) continue;
    texcoords = p; return 0;
  }
  texcoords.p = new CTexCoords();
  texcoords.p->setsize(m.length);
  memcpy(texcoords.p->p, *(void**)&m.p, m.length * sizeof(XMFLOAT2));
  return 0;
}
