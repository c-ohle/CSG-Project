#include "pch.h"
#include "Factory.h"
#include "Scene.h"
#include "View.h"

#include <gdiplus.h>
#include <gdiplusflat.h>
#pragma comment(lib, "gdiplus.lib")
using namespace Gdiplus;
using namespace Gdiplus::DllExports;

extern CComPtr<ID3D11Device> device;
extern CComPtr<ID3D11DeviceContext> context;
DXGI_SAMPLE_DESC chksmp(ID3D11Device* device, UINT samples);

HRESULT CreateTexture(UINT dx, UINT dy, UINT pitch, DXGI_FORMAT fmt, UINT mips, void* p, ID3D11ShaderResourceView** srv)
{
  if (pitch == 0) pitch = fmt == DXGI_FORMAT_A8_UNORM ? dx : dx << 2;
  D3D11_TEXTURE2D_DESC td = { 0 }; D3D11_SHADER_RESOURCE_VIEW_DESC rv; memset(&rv, 0, sizeof(rv));
  td.Width = dx;
  td.Height = dy;
  td.ArraySize = td.SampleDesc.Count = 1;// = td.MipLevels = 1;
  td.BindFlags = D3D11_BIND_SHADER_RESOURCE;
  td.Format = fmt;
  if (mips != 0)
  {
    td.BindFlags |= D3D11_BIND_RENDER_TARGET;
    td.MiscFlags = D3D11_RESOURCE_MISC_GENERATE_MIPS;
  }
  else td.MipLevels = 1;
  CComPtr<ID3D11Texture2D> tex;
  CHR(device->CreateTexture2D(&td, 0, &tex.p));
  context->UpdateSubresource(tex, 0, 0, p, pitch, 0);
  tex->GetDesc(&td);
  rv.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
  rv.Format = td.Format;
  rv.Texture2D.MipLevels = td.MipLevels;
  CHR(device->CreateShaderResourceView(tex, &rv, srv));
  if (mips != 0) context->GenerateMips(*srv);
  return 0;
}

void CTexture::init()
{
  if (fl & 1) return; fl |= 1;
  INT64 x = 0; str.p->Seek(*(LARGE_INTEGER*)&x, 0, 0);
  GpBitmap* bmp = 0; GdipCreateBitmapFromStream(str, &bmp); if (!bmp) return;
  UINT dx; GdipGetImageWidth(bmp, &dx);
  UINT dy; GdipGetImageHeight(bmp, &dy);
  PixelFormat pf; GdipGetImagePixelFormat(bmp, &pf);
  DXGI_FORMAT fmt;
  switch (pf)
  {
  case PixelFormat32bppRGB: fmt = DXGI_FORMAT_B8G8R8X8_UNORM; break;
  case PixelFormat32bppARGB: fmt = DXGI_FORMAT_B8G8R8A8_UNORM; { REAL res; GdipGetImageHorizontalResolution(bmp, &res); if (res <= 1) fmt = DXGI_FORMAT_A8_UNORM; } break;
  case PixelFormat24bppRGB: fmt = DXGI_FORMAT_B8G8R8X8_UNORM; pf = PixelFormat32bppRGB; break;
  case PixelFormat16bppRGB565: fmt = DXGI_FORMAT_B5G6R5_UNORM; break;
  case PixelFormat16bppRGB555: fmt = DXGI_FORMAT_B5G6R5_UNORM; pf = PixelFormat16bppRGB565; break;
  default: fmt = DXGI_FORMAT_B8G8R8X8_UNORM; pf = PixelFormat32bppRGB; break;
  }
  BitmapData data; GpRect r(0, 0, dx, dy);
  GdipBitmapLockBits(bmp, &r, 1, pf, &data);
  auto ptr = (BYTE*)data.Scan0; auto stride = data.Stride;
  if (fmt == DXGI_FORMAT_A8_UNORM)
  {
    auto p = ptr; fl |= 2;
    for (UINT y = 0; y < dx; y++, p += stride)
      for (UINT x = 0; x < dy; x++) p[x] = p[(x << 2) + 3];
  }
  auto hr = CreateTexture(dx, dy, stride, fmt, 1, ptr, &this->p);
  GdipBitmapUnlockBits(bmp, &data);
  GdipDisposeImage(bmp); if (hr >= 0) fl &= ~1;
}

static bool equals(IStream* a, IStream* b)
{
  if (a == b) return true;
  STATSTG sa; a->Stat(&sa, STATFLAG_NONAME);
  STATSTG sb; b->Stat(&sb, STATFLAG_NONAME);
  if (sa.cbSize.LowPart != sb.cbSize.LowPart) return false;
  INT64 x = 0; BYTE pa[4096], pb[4096];
  a->Seek(*(LARGE_INTEGER*)&x, 0, 0);
  b->Seek(*(LARGE_INTEGER*)&x, 0, 0);
  for (;;)
  {
    ULONG na; a->Read(pa, sizeof(pa), &na);
    ULONG nb; b->Read(pb, sizeof(pb), &nb);
    if (memcmp(pa, pb, na)) return false;
    if (na < sizeof(pa)) break;
  }
  return true;
}

CTexture* CTexture::GetTexture(IStream* str)
{
  { Critical crit; for (auto t = CTexture::first; t; t = t->next) if (equals(str, t->str.p)) { t->AddRef(); return t; } }
  auto t = new CTexture(); t->str = str; return t;
}

HRESULT CFactory::GetTexture(IStream* str, ICDXTexture** p)
{
  *p = CTexture::GetTexture(str);
  return 0;
}

struct __declspec(uuid("557cf406-1a04-11d3-9a73-0000f81ef32e")) _PNG {};

HRESULT CView::Thumbnail(UINT dx, UINT dy, UINT samples, UINT bkcolor, IStream* str)
{
  D3D11_TEXTURE2D_DESC td = { 0 };
  td.Width = dx; td.Height = dy;
  td.ArraySize = td.MipLevels = 1;
  td.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
  td.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
  td.SampleDesc = chksmp(device, max(1, samples));
  CComPtr<ID3D11Texture2D> tex; CHR(device->CreateTexture2D(&td, 0, &tex.p));
  D3D11_RENDER_TARGET_VIEW_DESC rdesc; memset(&rdesc, 0, sizeof(rdesc));
  rdesc.Format = td.Format;
  rdesc.ViewDimension = td.SampleDesc.Count > 1 ? D3D11_RTV_DIMENSION_TEXTURE2DMS : D3D11_RTV_DIMENSION_TEXTURE2D;
  CComPtr<ID3D11RenderTargetView> rtv; CHR(device->CreateRenderTargetView(tex, &rdesc, &rtv.p));
  td.BindFlags = D3D11_BIND_DEPTH_STENCIL;
  td.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
  CComPtr<ID3D11Texture2D> ds; CHR(device->CreateTexture2D(&td, 0, &ds.p));
  D3D11_DEPTH_STENCIL_VIEW_DESC ddesc; memset(&ddesc, 0, sizeof(ddesc));
  ddesc.Format = td.Format;
  ddesc.ViewDimension = td.SampleDesc.Count > 1 ? D3D11_DSV_DIMENSION_TEXTURE2DMS : D3D11_DSV_DIMENSION_TEXTURE2D;
  CComPtr<ID3D11DepthStencilView>  dsv; CHR(device->CreateDepthStencilView(ds, &ddesc, &dsv.p));
  D3D11_VIEWPORT viewport = { 0 }; viewport.Width = (float)dx; viewport.Height = (float)dy; viewport.MaxDepth = 1;

  context->RSSetViewports(1, &viewport); //pixelscale = 0;
  context->OMSetRenderTargets(1, &rtv.p, dsv.p); auto bk = XMLoadColor((const XMCOLOR*)&bkcolor);
  context->ClearRenderTargetView(rtv.p, bk.m128_f32);
  context->ClearDepthStencilView(dsv.p, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1, 0);

  auto t1 = flags; flags = (CDX_RENDER)(flags & CDX_RENDER_SHADOWS);
  auto t2 = camera.p->matrix; auto t3 = znear; auto t4 = zfar; auto t5 = minwz; auto t6 = this->viewport; auto t7 = rcclient;
  if (bkcolor == 0x00fffffe)
  {
    this->viewport = viewport; rcclient.right = dx; rcclient.bottom = dy;
    float rand = 0; Command(CDX_CMD_CENTER, (UINT*)&rand);
  }
  else
  {
    auto f = (this->viewport.Width + this->viewport.Height) * 0.5f;
    this->viewport.Width = f;
    this->viewport.Height = f;
  }
  setproject(); RenderScene();
  flags = t1; camera.p->matrix = t2; znear = t3; zfar = t4; minwz = t5; this->viewport = t6; rcclient = t7;

  dsv.Release(); ds.Release(); rtv.Release();
  context->OMSetRenderTargets(1, &rtv.p, dsv.p);
  td.BindFlags = 0;
  td.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
  if (td.SampleDesc.Count > 1)
  {
    td.SampleDesc.Count = 1;
    td.SampleDesc.Quality = 0;
    td.Usage = D3D11_USAGE_DEFAULT;
    CComPtr<ID3D11Texture2D> t1; CHR(device->CreateTexture2D(&td, 0, &t1.p));
    context->ResolveSubresource(t1, 0, tex, 0, td.Format); tex = t1;
  }
  td.Usage = D3D11_USAGE_STAGING;
  td.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
  CComPtr<ID3D11Texture2D> tex2; CHR(device->CreateTexture2D(&td, 0, &tex2.p));
  context->CopyResource(tex2, tex); tex = tex2;
  D3D11_MAPPED_SUBRESOURCE map; CHR(context->Map(tex, 0, D3D11_MAP_READ, 0, &map));
  GpBitmap* bmp = 0;
  auto st = GdipCreateBitmapFromScan0(dx, dy, map.RowPitch, PixelFormat32bppARGB, (BYTE*)map.pData, &bmp);
  if (!st) { st = GdipSaveImageToStream(bmp, str, &__uuidof(_PNG), 0); GdipDisposeImage(bmp); }
  context->Unmap(tex, 0);
  return st == 0 ? 0 : E_FAIL;
}

