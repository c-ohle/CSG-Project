#pragma once

struct CView : ICDXView
{
  WNDPROC                           proc;
  HWND                              hwnd;
  RECT                              rcclient;
  SIZE                              size = { -1 };
  UINT                              sampels;
  D3D11_VIEWPORT                    viewport;
  CComPtr<IDXGISwapChain>           swapchain;
  CComPtr<ID3D11RenderTargetView>   rtv;
  CComPtr<ID3D11DepthStencilView>   dsv;
  XMVECTOR                          vv[8];
  XMMATRIX                          mm[4];
  CComPtr<ICDXSink>                 sink;
  CComPtr<CScene>                   scene;
  CComPtr<CNode>                    camera;
  UINT                              iover = -1;
  CDX_RENDER                        flags = (CDX_RENDER)0;
  
  float                             vscale = 0.0004f, znear = 0.1f, zfar = 100, minwz = -1;

  static CView* first; CView* next;
  CView() { Critical crit; next = first; first = this; }
  ~CView() { auto p = &first; for (; *p != this; p = &(*p)->next); *p = next; }
  //void dispose()
  //{
  //  hwnd = 0; relres(1 | 2);
  //  auto p = &first; for (; *p != this; p = &(*p)->next); *p = next;
  //}
  //void relres(int fl)
  //{
  //  if (fl & 1) { swapchain.Release(); rtv.Release(); dsv.Release(); }
  //  if (fl & 2 && scene.p) { for (UINT i = 0; i < scene.p->count; i++) scene.p->nodes.p[i]->relres(); }
  //}
  void relres()
  {
    swapchain.Release(); rtv.Release(); dsv.Release();
  }
  void initres()
  {
    if (!hwnd) return;
    Resize(); InvalidateRect(hwnd, 0, 0);
  }
  void inits(int fl);
  HRESULT Resize();
  void SetBuffers();
  void SetColor(UINT i, UINT v);
  void XM_CALLCONV SetVector(UINT i, const XMVECTOR& p);
  void XM_CALLCONV SetMatrix(UINT i, const XMMATRIX& p);
  void setproject();
  void Render();
  void RenderScene();
  void Pick(const short* pt);
  VERTEX* BeginVertices(UINT nv);
  void EndVertices(UINT nv, UINT topo);
  void XM_CALLCONV DrawLine(XMVECTOR a, XMVECTOR b);
  void XM_CALLCONV DrawBox(XMVECTOR a, XMVECTOR b);
  void XM_CALLCONV DrawArrow(XMVECTOR p, XMVECTOR v, float r, int s = 10);
#ifdef __WIN32
  void* operator new(size_t s) { return  _aligned_malloc(s, 16); }
  void operator delete(void* p) { _aligned_free(p); }
#endif
  static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
  UINT refcount = 1;
  HRESULT __stdcall QueryInterface(REFIID riid, void** p)
  {
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICDXView) || riid == __uuidof(IAgileObject))
    {
      InterlockedIncrement(&refcount); *p = static_cast<ICDXView*>(this); return 0;
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
  HRESULT __stdcall get_Samples(BSTR* p);
  HRESULT __stdcall put_Samples(BSTR p)
  {
    UINT c = _wtoi(p); if (c == 0) return E_INVALIDARG;
    relres(); sampels = c; initres(); return 0;
  }
  HRESULT __stdcall get_BkColor(UINT* p);
  HRESULT __stdcall put_BkColor(UINT p);
  HRESULT __stdcall get_Render(CDX_RENDER* p)
  {
    *p = flags; return 0;
  }
  HRESULT __stdcall put_Render(CDX_RENDER p)
  {
    flags = p; return 0;
  }
  HRESULT __stdcall get_Scene(ICDXScene** p)
  {
    if (*p = scene) scene.p->AddRef(); return 0;
  }
  HRESULT __stdcall put_Scene(ICDXScene* p)
  {
    if (camera.p && camera.p->parent) camera.Release();
    scene = static_cast<CScene*>(p); return 0;
  }
  HRESULT __stdcall get_Camera(ICDXNode** p);
  HRESULT __stdcall put_Camera(ICDXNode* p);
  HRESULT __stdcall get_OverNode(UINT* p)
  {
    *p = iover; return 0;
  }
  HRESULT __stdcall get_OverPoint(XMFLOAT3* p)
  {
    XMStoreFloat3(p, vv[VV_OVERPOS]); return 0;
  }
  HRESULT __stdcall get_OverPlane(XMFLOAT4X4* p)
  {
    XMStoreFloat4x4(p, mm[MM_PLANE]); return 0;
  }
  HRESULT __stdcall Draw(CDX_DRAW id, UINT* data);
  HRESULT __stdcall Command(CDX_CMD  cmd, UINT* data);
  HRESULT __stdcall Print(UINT dx, UINT dy, UINT samples, UINT bkcolor, IStream* str);
};
