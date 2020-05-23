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

static bool tri_intersect_xy(const XMVECTOR a[3], const XMVECTOR b[3])
{
  XMVECTOR mz = XMVectorZero(), ma = mz, mb = mz, mh = g_XMOneHalf;
  for (UINT i1 = 2, i2 = 0; i2 < 3; i1 = i2++)
  {
    auto va = a[i2] - a[i1];
    for (UINT k1 = 2, k2 = 0; k2 < 3; k1 = k2++)
    {
      auto vb = b[k2] - b[k1];
      ma = _mm_or_ps(ma, _mm_cmple_ps(XMVector2Cross(va, b[k2] - a[i1]), mz));
      mb = _mm_or_ps(mb, _mm_cmple_ps(XMVector2Cross(vb, a[i2] - b[k1]), mz));
      auto de = XMVector2Cross(va, vb); if (_mm_movemask_ps(_mm_cmpeq_ps(de, mz))) continue;
      de = XMVectorReciprocal(de); auto vc = a[i1] - b[k1];
      if (!_mm_movemask_ps(XMVectorInBounds(XMVector2Cross(vb, vc) * de - mh, mh))) continue;
      if (!_mm_movemask_ps(XMVectorInBounds(XMVector2Cross(va, vc) * de - mh, mh))) continue;
      return true;
    }
  }
  return !_mm_movemask_ps(ma) || !_mm_movemask_ps(mb);
}
static void selectrect(CView& view, UINT* data)
{
  void* layer = view.scene.p;
  auto& nodes = view.scene.p->nodes;
  for (UINT i = 0; i < view.scene.p->count; i++) nodes.p[i]->flags &= ~(NODE_FL_SELECT | NODE_FL_INSEL);
  XMVECTOR r[5];
  r[0] = XMLoadFloat2((XMFLOAT2*)data + 0); r[4] = r[0];
  r[2] = XMLoadFloat2((XMFLOAT2*)data + 1);
  r[1] = XMVectorPermute<4, 1, 3, 3>(r[0], r[2]);
  r[3] = XMVectorPermute<0, 5, 3, 3>(r[0], r[2]);
  auto vv = (XMVECTOR*)__align16(stackptr);
  for (UINT i = 0; i < view.scene.p->count; i++)
  {
    auto node = nodes.p[i]; if (!node->vb.p) continue;
    auto main = node; for (; main && main->parent != layer; main = (CNode*)main->parent);
    if (!main || main->flags & (NODE_FL_SELECT | NODE_FL_STATIC)) continue;
    auto& pts = *node->gethull();
    auto wm = node->gettrans(view.scene.p);
    XMVector3TransformStream((XMFLOAT4*)vv, sizeof(XMVECTOR), pts.p, sizeof(XMFLOAT3), pts.n, wm);
    UINT ni; node->mesh.p->get_IndexCount(&ni);
    auto ii = (UINT*)(vv + pts.n); node->mesh.p->CopyBuffer(1, 0, CCSGVAR(ii, ni));
    for (UINT t = 0; t < ni; t += 3)
    {
      XMVECTOR tt[] = { vv[ii[t + 0]], vv[ii[t + 1]], vv[ii[t + 2]] };
      if (!tri_intersect_xy(tt, r) && !tri_intersect_xy(tt, r + 2)) continue;
      main->flags |= NODE_FL_SELECT; break;
    }
  }
  for (UINT i = 0; i < view.scene.p->count; i++)
  {
    auto node = nodes.p[i];
    for (auto p = node; p != layer; p = (CNode*)p->parent)
    {
      if (!(p->flags & NODE_FL_SELECT)) continue;
      node->flags |= NODE_FL_INSEL; break;
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
    return 0;
  }
  case CDX_CMD_GETBOX:
  case CDX_CMD_GETBOXSEL:
  {
    auto ma = XMLoadFloat4x3((XMFLOAT4X3*)data);
    auto& nodes = scene.p->nodes;
    XMVECTOR box[4]; box[1] = box[3] = -(box[0] = box[2] = g_XMFltMax);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto node = nodes.p[i]; if (!node->ib.p) continue; 
      if (cmd == CDX_CMD_GETBOXSEL && !(node->flags & NODE_FL_INSEL)) continue;
      auto& pts = *node->gethull();
      auto wm = node->gettrans(scene.p) * ma;
      for (UINT i = 0; i < pts.n; i++)
      {
        auto p = XMVector3Transform(XMLoadFloat3(&pts.p[i]), wm);
        box[0] = XMVectorMin(box[0], p);
        box[1] = XMVectorMax(box[1], p);
      }
    }
    XMStoreFloat4(((XMFLOAT4*)data) + 0, box[0]);
    XMStoreFloat4(((XMFLOAT4*)data) + 1, box[1]);
    return 0;
  }
  case CDX_CMD_SETPLANE:
  {
    mm[MM_PLANE] = XMLoadFloat4x3((XMFLOAT4X3*)data) * mm[MM_PLANE];
    return 0;
  }
  case CDX_CMD_PICKPLANE:
  {
    auto p = (XMFLOAT2*)data;
    if (isnan(p->x))
    {
      POINT cp; GetCursorPos(&cp); ScreenToClient(hwnd, &cp);
      p->x = (float)cp.x;
      p->y = (float)cp.y;
    }
    auto& r = mm[MM_PLANE].r;
    auto t0 = (XMLoadFloat2(p) * 2 / XMLoadFloat4((XMFLOAT4*)&viewport.Width) + g_XMNegativeOne) * g_XMNegateY;
    auto t1 = t0 * XMVectorSplatW(r[0]) - r[0];
    auto t2 = t0 * XMVectorSplatW(r[1]) - r[1];
    auto t3 = t0 * XMVectorSplatW(r[3]) - r[3]; t3 = -t3;
    auto f1 = _mm_shuffle_ps(t1, t3, _MM_SHUFFLE(1, 0, 1, 0)); f1 = _mm_shuffle_ps(f1, f1, _MM_SHUFFLE(0, 0, 0, 2));
    auto f2 = _mm_shuffle_ps(t2, t3, _MM_SHUFFLE(1, 0, 1, 0)); f2 = _mm_shuffle_ps(f2, f2, _MM_SHUFFLE(0, 1, 3, 1));
    auto f3 = _mm_shuffle_ps(t2, t3, _MM_SHUFFLE(1, 0, 1, 0)); f3 = _mm_shuffle_ps(f3, f3, _MM_SHUFFLE(0, 0, 2, 0));
    auto f4 = _mm_shuffle_ps(t1, t3, _MM_SHUFFLE(1, 0, 1, 0)); f4 = _mm_shuffle_ps(f4, f4, _MM_SHUFFLE(0, 1, 1, 3));
    auto f5 = f1 * f2 - f3 * f4;
    XMStoreFloat2(p, f5 / XMVectorSplatZ(f5));
    return 0;
  }
  case CDX_CMD_SELECT:
  {
    auto keys = (UINT)(size_t)data; auto ctrl = (keys & 0x20000) == 0x20000;
    void* layer = scene.p;
    auto& nodes = scene.p->nodes;
    auto  main = iover != -1 ? nodes[iover] : 0;
    for (; main && main->parent != layer; main = main->getparent());
    auto sel = ctrl && main && (main->flags & NODE_FL_SELECT) == 0;
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto p = nodes.p[i]; auto is = p->ispart(main); if (ctrl && !is) continue;
      if (ctrl ? sel : is)
      {
        if (p->flags & NODE_FL_STATIC && (keys & 0x70000) != 0x70000) continue; //ctrl+shift+alt for unlock
        p->flags |= p == main ? NODE_FL_SELECT | NODE_FL_INSEL : NODE_FL_INSEL;
      }
      else p->flags &= ~(NODE_FL_SELECT | NODE_FL_INSEL);
    }
    return 0;
  }
  case CDX_CMD_SELECTRECT:
    selectrect(*this, data);
    return 0;
  }
  return E_FAIL;
}

