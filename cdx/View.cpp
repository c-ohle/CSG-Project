#include "pch.h"
#include "cdx_i.h"
#include "scene.h"
#include "view.h"

CView* CView::first;
void releasedx();

LRESULT CALLBACK CView::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
  auto view = (CView*)GetWindowLongPtr(hWnd, GWLP_USERDATA);
  switch (message)
  {
  case WM_ERASEBKGND:
    return 1;
  case WM_PAINT:
    ValidateRect(hWnd, 0);
    if (*(UINT64*)&view->size != *(UINT64*)&view->rcclient.right)
    {
      *(UINT64*)&view->size = *(UINT64*)&view->rcclient.right;
      if (view->Resize()) return 0;
    }
    view->Render();
    return 0;
  case WM_MOUSEMOVE:
    if (wParam & (MK_LBUTTON | MK_MBUTTON | MK_RBUTTON)) break;
  case WM_LBUTTONDOWN:
  case WM_MBUTTONDOWN:
  case WM_RBUTTONDOWN:
    view->Pick((short*)&lParam);
    break;
  case WM_SIZE:
    GetClientRect(hWnd, &view->rcclient); InvalidateRect(hWnd, 0, 0);
    break;
  case WM_DESTROY:
    view->hwnd = 0; view->relres();
    break;
  }
  return view->proc(hWnd, message, wParam, lParam);
}

HRESULT CView::get_Camera(ICDXNode** p)
{
  if (*p = camera) camera.p->AddRef();
  return 0;
}
HRESULT CView::put_Camera(ICDXNode* p)
{
  camera = p ? static_cast<CNode*>(p) : 0;
  return 0;
}

void XM_CALLCONV CView::DrawLine(XMVECTOR a, XMVECTOR b)
{
  auto p = BeginVertices(2); //XMStoreFloat4A()
  XMStoreFloat4A((XMFLOAT4A*)&p[0], a);
  XMStoreFloat4A((XMFLOAT4A*)&p[1], b);
  EndVertices(2, MO_TOPO_LINELIST | MO_DEPTHSTENCIL_ZWRITE);
}
void XM_CALLCONV CView::DrawBox(XMVECTOR a, XMVECTOR b)
{
  auto p = BeginVertices(10);
  XMStoreFloat4A((XMFLOAT4A*)&p[0], XMVectorSelect(a, b, XMVectorSelectControl(0, 0, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[1], XMVectorSelect(a, b, XMVectorSelectControl(1, 0, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[2], XMVectorSelect(a, b, XMVectorSelectControl(1, 1, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[3], XMVectorSelect(a, b, XMVectorSelectControl(0, 1, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[4], XMVectorSelect(a, b, XMVectorSelectControl(0, 0, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[5], XMVectorSelect(a, b, XMVectorSelectControl(0, 0, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[6], XMVectorSelect(a, b, XMVectorSelectControl(1, 0, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[7], XMVectorSelect(a, b, XMVectorSelectControl(1, 1, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[8], XMVectorSelect(a, b, XMVectorSelectControl(0, 1, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[9], XMVectorSelect(a, b, XMVectorSelectControl(0, 0, 1, 0)));
  EndVertices(10, MO_TOPO_LINESTRIP | MO_DEPTHSTENCIL_ZWRITE);
  p = BeginVertices(6);
  XMStoreFloat4A((XMFLOAT4A*)&p[0], XMVectorSelect(a, b, XMVectorSelectControl(1, 0, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[1], XMVectorSelect(a, b, XMVectorSelectControl(1, 0, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[2], XMVectorSelect(a, b, XMVectorSelectControl(1, 1, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[3], XMVectorSelect(a, b, XMVectorSelectControl(1, 1, 1, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[4], XMVectorSelect(a, b, XMVectorSelectControl(0, 1, 0, 0)));
  XMStoreFloat4A((XMFLOAT4A*)&p[5], XMVectorSelect(a, b, XMVectorSelectControl(0, 1, 1, 0)));
  EndVertices(6, MO_TOPO_LINELIST | MO_DEPTHSTENCIL_ZWRITE);
}
void XM_CALLCONV CView::DrawArrow(XMVECTOR p, XMVECTOR v, float r, int s)
{
  auto fa = (float)(2 * XM_PI) / s++;
  auto rr = XMVectorReplicate(r);
  auto rl = XMVector3ReciprocalLength(v);
  auto r1 = XMVectorMultiply(_mm_shuffle_ps(v, v, _MM_SHUFFLE(0, 1, 0, 2)), rl);  //todo: _mm_shuffle_ps -> XM???
  auto r2 = XMVectorMultiply(_mm_shuffle_ps(v, v, _MM_SHUFFLE(0, 0, 2, 1)), rl);
  auto vv = BeginVertices(s << 1);
  for (int i = 0; i < s; i++)
  {
    auto si = XMVectorReplicate(sinf(i * fa));
    auto co = XMVectorReplicate(cosf(i * fa));
    auto no = XMVectorAdd(XMVectorMultiply(r1, si), XMVectorMultiply(r2, co));
    XMStoreFloat4A((XMFLOAT4A*)&vv[(i << 1) + 0], XMVectorAdd(p, XMVectorMultiply(no, rr)));
    XMStoreFloat4A((XMFLOAT4A*)&vv[(i << 1) + 1], XMVectorAdd(p, v));
    XMStoreFloat4((XMFLOAT4*)&vv[(i << 1) + 0].n, no);
    XMStoreFloat4((XMFLOAT4*)&vv[(i << 1) + 1].n, no);
  }
  EndVertices(s << 1, MO_TOPO_TRIANGLESTRIP | MO_PSSHADER_COLOR3D | MO_DEPTHSTENCIL_ZWRITE | MO_RASTERIZER_NOCULL);
}

void CView::inits(int fl)
{
  if (fl & 1)
  {
    if (!camera.p)
    {
      camera.p = new CNode();
      camera.p->matrix = XMMatrixInverse(0, XMMatrixLookAtLH(XMVectorSet(-3, -6, 3, 0), XMVectorZero(), XMVectorSet(0, 0, 1, 0)));
    }
  }
  if (fl & 2)
  {
    auto& nodes = scene.p->nodes;
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.mesh.p) continue;
      BOOL modified; node.mesh.p->GetModified(&modified);
      if (modified || !node.ib.p || !node.ib.p->p.p) node.update();
    }
  }
}

HRESULT __stdcall CView::Command(CDX_CMD cmd, UINT* data)
{
  switch (cmd)
  {
  case CDX_CMD_CENTER:
  {
    float rand = ((float*)data)[0];
    inits(1 | 2); setproject();
    auto& nodes = scene.p->nodes;
    auto cwm = camera.p->matrix; // gettrans(camera.p->parent ? scene.p : 0);
    auto icw = XMMatrixInverse(0, cwm);
    auto ab = XMVectorSet((rcclient.right - rand) * vscale, (rcclient.bottom - rand) * vscale, 0, 0);
    XMVECTOR box[4]; box[1] = box[3] = -(box[0] = box[2] = g_XMFltMax);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      auto& pts = *node.gethull();
      auto wm = node.gettrans(scene.p); auto tm = wm * icw;
      for (UINT i = 0; i < pts.n; i++)
      {
        auto p = XMVector3Transform(XMLoadFloat3(&pts.p[i]), wm);
        box[0] = XMVectorMin(box[0], p);
        box[1] = XMVectorMax(box[1], p); //not in use
        p = XMVector3Transform(XMLoadFloat3(&pts.p[i]), tm); auto z = XMVectorSplatZ(p) * ab;
        box[2] = XMVectorMin(box[2], p + z);
        box[3] = XMVectorMax(box[3], p - z);
      }
    }

    if (box[0].m128_f32[0] > box[1].m128_f32[0]) return 0;

    auto mp = (box[3] - box[2]) * 0.5;
    float fm = -max(
      mp.m128_f32[0] / ab.m128_f32[0],
      mp.m128_f32[1] / ab.m128_f32[1]);

    camera.p->matrix = XMMatrixTranslation(
      box[2].m128_f32[0] + mp.m128_f32[0],
      box[2].m128_f32[1] + mp.m128_f32[1],
      fm) * cwm;

    auto z1 = box[2].m128_f32[2] - fm;
    auto z2 = box[3].m128_f32[2] - fm;
    
    z1 = powf(10, roundf(log10f(z1)) - 1);
    z2 = powf(10, roundf(log10f(z2)) + 2);
    znear = z1; zfar = z2; minwz = box[0].m128_f32[2];
  }
  break;
  case CDX_CMD_GETBOX:
  {
    auto ma = XMLoadFloat4x3((XMFLOAT4X3*)data);
    auto& nodes = scene.p->nodes;
    XMVECTOR box[4]; box[1] = box[3] = -(box[0] = box[2] = g_XMFltMax);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      auto& pts = *node.gethull();
      auto wm = node.gettrans(scene.p) * ma;
      for (UINT i = 0; i < pts.n; i++)
      {
        auto p = XMVector3Transform(XMLoadFloat3(&pts.p[i]), wm);
        box[0] = XMVectorMin(box[0], p);
        box[1] = XMVectorMax(box[1], p);
      }
    }
    XMStoreFloat4(((XMFLOAT4*)data) + 0, box[0]);
    XMStoreFloat4(((XMFLOAT4*)data) + 1, box[1]);
  }
  break;
  }
  return 0;
}