#include "pch.h"
#include "Factory.h"
#include "view.h"
#include "font.h"

#include "ShaderInc\VSMain.h"
#include "ShaderInc\VSWorld.h"
#include "ShaderInc\PSMain.h"
#include "ShaderInc\PSTexture.h"
#include "ShaderInc\PSFont.h"
#include "ShaderInc\PSMain3D.h"
#include "ShaderInc\PSTexture3D.h"
#include "ShaderInc\PSSpec3D.h"
#include "ShaderInc\GSShadows.h"
#include "ShaderInc\GSOutline3D.h"

const D3D11_INPUT_ELEMENT_DESC layout[] =
{
  { "POSITION",  0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0,  D3D11_INPUT_PER_VERTEX_DATA, 0 },
  { "NORMAL",    0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 },
  { "TEXCOORD",  0, DXGI_FORMAT_R32G32_FLOAT,    0, 24, D3D11_INPUT_PER_VERTEX_DATA, 0 },
};
struct CB_VS_PER_FRAME
{
  XMMATRIX g_mViewProjection;
  XMVECTOR g_fAmbient;
  XMVECTOR g_vLightDir;
};
struct CB_VS_PER_OBJECT
{
  XMMATRIX g_mWorld;
};
struct CB_PS_PER_OBJECT
{
  XMVECTOR g_vDiffuse;
};
struct shaders { const BYTE* p; UINT n; };
static shaders _vsshaders[MO_VSSHADER_COUNT] =
{
  { _VSMain,			sizeof(_VSMain)			  },
  { _VSWorld,			sizeof(_VSWorld)		  },
};
static shaders _psshaders[MO_PSSHADER_COUNT] =
{
  { _PSMain,			sizeof(_PSMain)				}, //INV_VV_DIFFUSE
  { _PSTexture,		sizeof(_PSTexture)		}, //INV_VV_DIFFUSE | INV_TT_DIFFUSE
  { _PSFont,			sizeof(_PSFont)				}, //INV_VV_DIFFUSE | INV_TT_DIFFUSE
  { _PSMain3D,		sizeof(_PSMain3D)			},
  { _PSTexture3D,	sizeof(_PSTexture3D)	},
  //{ _PSSpec3D,	  sizeof(_PSSpec3D)	    },
};
static shaders _gsshaders[MO_GSSHADER_COUNT] =
{
  { _GSShadows,		sizeof(_GSShadows)		},
  { _GSOutline3D,	sizeof(_GSOutline3D)	},
};

static void CreateDepthStencilState(ID3D11Device* device, UINT m, ID3D11DepthStencilState** p)
{
  D3D11_DEPTH_STENCIL_DESC desc;
  desc.DepthEnable = 1;
  desc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ZERO;
  desc.DepthFunc = D3D11_COMPARISON_LESS_EQUAL;
  desc.StencilEnable = 1;
  desc.StencilReadMask = D3D11_DEFAULT_STENCIL_READ_MASK;
  desc.StencilWriteMask = D3D11_DEFAULT_STENCIL_WRITE_MASK;
  desc.FrontFace.StencilFunc = D3D11_COMPARISON_EQUAL;
  desc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
  desc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
  desc.FrontFace.StencilFailOp = D3D11_STENCIL_OP_KEEP; desc.BackFace = desc.FrontFace;
  switch (m & MO_DEPTHSTENCIL_MASK)
  {
  case MO_DEPTHSTENCIL_ZWRITE:
    desc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
    break;
  case MO_DEPTHSTENCIL_STEINC:
    desc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_INCR;
    desc.BackFace.StencilPassOp = D3D11_STENCIL_OP_INCR;
    break;
  case MO_DEPTHSTENCIL_STEDEC:
    desc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_DECR;
    desc.BackFace.StencilPassOp = D3D11_STENCIL_OP_DECR;
    break;
  case MO_DEPTHSTENCIL_CLEARZ:
    desc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
    desc.DepthFunc = D3D11_COMPARISON_ALWAYS;
    desc.FrontFace.StencilFunc = D3D11_COMPARISON_LESS_EQUAL;
    desc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_REPLACE;
    break;
  case MO_DEPTHSTENCIL_TWOSID:
    desc.DepthFunc = D3D11_COMPARISON_LESS;
    desc.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
    desc.BackFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
    desc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_DECR;
    desc.BackFace.StencilDepthFailOp = D3D11_STENCIL_OP_INCR;
    break;
  case MO_DEPTHSTENCIL_CLEARS:
    desc.DepthEnable = 0;
    desc.FrontFace.StencilFunc = D3D11_COMPARISON_LESS;
    desc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_REPLACE;
    break;
  }
  auto hr = device->CreateDepthStencilState(&desc, p); XMASSERT(!hr); //todo: sink.p->OnMessage
}
static void CreateBlendState(ID3D11Device* device, UINT m, ID3D11BlendState** p)
{
  D3D11_BLEND_DESC desc;
  desc.AlphaToCoverageEnable = 0;
  desc.IndependentBlendEnable = 0;
  desc.RenderTarget[0].BlendEnable = 0;
  desc.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
  desc.RenderTarget[0].DestBlend = D3D11_BLEND_ZERO;
  desc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
  desc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
  desc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
  desc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
  desc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
  if ((m & MO_BLENDSTATE_MASK) == MO_BLENDSTATE_ALPHA)
  {
    desc.RenderTarget[0].BlendEnable = 1;
    desc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_ALPHA;
    desc.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
    desc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_SRC_ALPHA;
    desc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
  }
  if ((m & MO_BLENDSTATE_MASK) == MO_BLENDSTATE_ALPHAADD)
  {
    desc.RenderTarget[0].BlendEnable = 1;
    desc.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
    desc.RenderTarget[0].DestBlend = D3D11_BLEND_ONE;
  }
  auto hr = device->CreateBlendState(&desc, p); XMASSERT(!hr);
}
static void CreateRasterizerState(ID3D11Device* device, UINT m, UINT rh, ID3D11RasterizerState** p)
{
  D3D11_RASTERIZER_DESC desc;
  desc.FillMode = (m & MO_RASTERIZER_MASK) == MO_RASTERIZER_WIRE ? D3D11_FILL_WIREFRAME : D3D11_FILL_SOLID;
  desc.CullMode = (m & MO_RASTERIZER_MASK) == MO_RASTERIZER_NOCULL ? D3D11_CULL_NONE : (m & MO_RASTERIZER_MASK) == MO_RASTERIZER_FRCULL ? D3D11_CULL_FRONT : D3D11_CULL_BACK;
  desc.FrontCounterClockwise = rh;
  desc.DepthBias = 0;
  desc.DepthBiasClamp = 0;
  desc.SlopeScaledDepthBias = 0;
  desc.DepthClipEnable = 1;
  desc.ScissorEnable = 0;
  desc.MultisampleEnable = 1;
  desc.AntialiasedLineEnable = 0;
  auto hr = device->CreateRasterizerState(&desc, p); XMASSERT(!hr);
}
static void CreateSamplerState(ID3D11Device* device, UINT m, ID3D11SamplerState** p)
{
  D3D11_SAMPLER_DESC desc;
  desc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
  desc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
  desc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
  desc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
  desc.MipLODBias = 0.0f;
  desc.MaxAnisotropy = 1;
  desc.ComparisonFunc = D3D11_COMPARISON_ALWAYS;
  desc.BorderColor[0] = desc.BorderColor[1] = desc.BorderColor[2] = desc.BorderColor[3] = 0;//0x00ffffff;
  desc.MinLOD = 0;
  desc.MaxLOD = D3D11_FLOAT32_MAX;
  switch (m & MO_SAMPLERSTATE_MASK)
  {
  case MO_SAMPLERSTATE_VBORDER:
    desc.AddressV = D3D11_TEXTURE_ADDRESS_BORDER;
    break;
  case MO_SAMPLERSTATE_FONT:
    desc.AddressU = D3D11_TEXTURE_ADDRESS_BORDER;
    desc.AddressV = D3D11_TEXTURE_ADDRESS_BORDER;
    desc.MipLODBias = -0.5f;
    break;
  case MO_SAMPLERSTATE_IMAGE:
    //desc.BorderColor[0] = desc.BorderColor[1] = desc.BorderColor[2] = desc.BorderColor[3] = 0x00ffffff;
    desc.AddressU = D3D11_TEXTURE_ADDRESS_BORDER;
    desc.AddressV = D3D11_TEXTURE_ADDRESS_BORDER;
    break;
  }
  auto hr = device->CreateSamplerState(&desc, p); XMASSERT(!hr); //todo: sink.p->OnMessage
}

UINT                              adapterid, currentmode, stencilref, invstate;
CComPtr<ID3D11Device>             device;
CComPtr<ID3D11DeviceContext>      context;
CComPtr<ID3D11DepthStencilState>	depthstencilstates[MO_DEPTHSTENCIL_COUNT];
CComPtr<ID3D11BlendState>					blendstates[MO_BLENDSTATE_COUNT];
CComPtr<ID3D11RasterizerState>		rasterizerstates[MO_RASTERIZER_COUNT];
CComPtr<ID3D11SamplerState>				samplerstates[MO_SAMPLERSTATE_COUNT];
CComPtr<ID3D11VertexShader>				vertexshader[MO_VSSHADER_COUNT];
CComPtr<ID3D11GeometryShader>			geometryshader[MO_GSSHADER_COUNT];
CComPtr<ID3D11PixelShader>				pixelshader[MO_PSSHADER_COUNT];
CComPtr<ID3D11InputLayout>        vertexlayout;
CComPtr<ID3D11Buffer>             cbbuffer[3];
void* currentbuffer[3];

CComPtr<CFont>                    d_font; UINT d_blend;

static void SetMode(UINT mode)
{
  if (mode == currentmode) return;
  auto mask = mode ^ currentmode; currentmode = mode;
  if (mask)
  {
    if (mask & MO_DEPTHSTENCIL_MASK)
    {
      UINT i = (mode & MO_DEPTHSTENCIL_MASK) >> MO_DEPTHSTENCIL_SHIFT;
      if (i < MO_DEPTHSTENCIL_COUNT && !depthstencilstates[i].p) CreateDepthStencilState(device, mode, &depthstencilstates[i].p);
      context->OMSetDepthStencilState(i < MO_DEPTHSTENCIL_COUNT ? depthstencilstates[i].p : 0, stencilref);
    }
    if (mask & MO_BLENDSTATE_MASK)
    {
      UINT i = (mode & MO_BLENDSTATE_MASK) >> MO_BLENDSTATE_SHIFT;
      if (i < MO_BLENDSTATE_COUNT && !blendstates[i].p) CreateBlendState(device, mode, &blendstates[i].p);
      float ffff[4] = { 0 };
      context->OMSetBlendState(i < MO_BLENDSTATE_COUNT ? blendstates[i].p : 0, ffff, -1);
    }
    if (mask & MO_RASTERIZER_MASK)
    {
      UINT i = (mode & MO_RASTERIZER_MASK) >> MO_RASTERIZER_SHIFT;
      if (i < MO_RASTERIZER_COUNT && !rasterizerstates[i].p) CreateRasterizerState(device, currentmode, /*moderh*/1, &rasterizerstates[i].p);
      context->RSSetState(i < MO_RASTERIZER_COUNT ? rasterizerstates[i].p : 0);
    }
    if (mask & MO_SAMPLERSTATE_MASK)
    {
      UINT i = (mode & MO_SAMPLERSTATE_MASK) >> MO_SAMPLERSTATE_SHIFT;
      if (i < MO_SAMPLERSTATE_COUNT && !samplerstates[i].p) CreateSamplerState(device, mode, &samplerstates[i].p);
      context->PSSetSamplers(0, 1, &samplerstates[i].p);
    }
    if (mask & MO_VSSHADER_MASK)
    {
      UINT i = (mode & MO_VSSHADER_MASK) >> MO_VSSHADER_SHIFT;
      if (!vertexshader[i].p) {
        auto hr = device->CreateVertexShader(_vsshaders[i].p, _vsshaders[i].n, 0, &vertexshader[i].p); XMASSERT(!hr);
      }
      context->VSSetShader(vertexshader[i].p, 0, 0);
    }
    if (mask & MO_PSSHADER_MASK)
    {
      UINT i = (mode & MO_PSSHADER_MASK) >> MO_PSSHADER_SHIFT;
      if (i < MO_PSSHADER_COUNT && !pixelshader[i].p) { auto hr = device->CreatePixelShader(_psshaders[i].p, _psshaders[i].n, 0, &pixelshader[i].p); XMASSERT(!hr); }
      context->PSSetShader(i < MO_PSSHADER_COUNT ? pixelshader[i].p : 0, 0, 0);
    }
    if (mask & MO_GSSHADER_MASK)
    {
      UINT i = ((mode & MO_GSSHADER_MASK) >> MO_GSSHADER_SHIFT) - 1;
      if (i < MO_GSSHADER_COUNT && !geometryshader[i].p) { auto hr = device->CreateGeometryShader(_gsshaders[i].p, _gsshaders[i].n, 0, &geometryshader[i].p); XMASSERT(!hr); }
      context->GSSetShader(i < MO_GSSHADER_COUNT ? geometryshader[i].p : 0, 0, 0);
    }
    if (mask & MO_TOPO_MASK)
    {
      UINT i = (mode & MO_TOPO_MASK) >> MO_TOPO_SHIFT;
      XMASSERT(i > D3D_PRIMITIVE_TOPOLOGY_UNDEFINED && i <= D3D_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP_ADJ);
      context->IASetPrimitiveTopology((D3D_PRIMITIVE_TOPOLOGY)i);
    }
  }
}
static void SetVertexBuffer(ID3D11Buffer* p)
{
  if (currentbuffer[0] == p) return; currentbuffer[0] = p;
  const UINT stride = 32, offs = 0;
  context->IASetVertexBuffers(0, 1, &p, &stride, &offs);
}
static void SetIndexBuffer(ID3D11Buffer* p)
{
  if (currentbuffer[1] == p) return; currentbuffer[1] = p;
  context->IASetIndexBuffer(p, DXGI_FORMAT_R16_UINT, 0);
}
void SetTexture(ID3D11ShaderResourceView* p)
{
  if (currentbuffer[2] == p) return; currentbuffer[2] = p;
  context->PSSetShaderResources(0, 1, &p);
}
void CView::SetBuffers()
{
  if (!(invstate & (INV_CB_VS_PER_FRAME | INV_CB_VS_PER_OBJECT | INV_CB_PS_PER_OBJECT | INV_TT_DIFFUSE))) return;
  if (invstate & INV_CB_VS_PER_FRAME)
  {
    invstate &= ~INV_CB_VS_PER_FRAME;
    auto& cb = cbbuffer[0].p; D3D11_MAPPED_SUBRESOURCE map;
    context->Map(cb, 0, D3D11_MAP_WRITE_DISCARD, 0, &map);
    auto p = (CB_VS_PER_FRAME*)map.pData;
    p->g_mViewProjection = mm[MM_VIEWPROJ];
    p->g_fAmbient = vv[VV_AMBIENT];
    p->g_vLightDir = vv[VV_LIGHTDIR];
    context->Unmap(cb, 0);
    context->VSSetConstantBuffers(0, 1, &cb);
    context->GSSetConstantBuffers(0, 1, &cb); //todo: mask out !!!
  }
  if (invstate & INV_CB_VS_PER_OBJECT)
  {
    invstate &= ~INV_CB_VS_PER_OBJECT;
    auto& cb = cbbuffer[1].p; D3D11_MAPPED_SUBRESOURCE map;
    context->Map(cb, 0, D3D11_MAP_WRITE_DISCARD, 0, &map);
    auto p = (CB_VS_PER_OBJECT*)map.pData;
    p->g_mWorld = mm[MM_WORLD];
    context->Unmap(cb, 0);
    context->VSSetConstantBuffers(1, 1, &cb);
  }
  if (invstate & INV_CB_PS_PER_OBJECT)
  {
    invstate &= ~INV_CB_PS_PER_OBJECT;
    auto& cb = cbbuffer[2].p; D3D11_MAPPED_SUBRESOURCE map;
    context->Map(cb, 0, D3D11_MAP_WRITE_DISCARD, 0, &map);
    auto p = (CB_PS_PER_OBJECT*)map.pData;
    p->g_vDiffuse = vv[VV_DIFFUSE];
    context->Unmap(cb, 0);
    context->PSSetConstantBuffers(1, 1, &cb);
  }
  if (invstate & INV_TT_DIFFUSE)
  {
    invstate &= ~INV_TT_DIFFUSE;
    //ID3D11ShaderResourceView* srv = 0;
    //if (tt[TT_DIFFUSE].p) { auto t = (CTexture*)tt[TT_DIFFUSE].p; if (!t->srv11.p) t->init11(device, context); srv = t->srv11.p; }
    //context->PSSetShaderResources(0, 1, &srv);
  }
}
void CView::SetColor(UINT i, UINT v)
{
  auto p = XMLoadColor((const XMCOLOR*)&v);
  if (XMComparisonAllTrue(XMVector4EqualR(vv[i], p))) return;
  vv[i] = p; invstate |= (0x000001 << i);
}
void CView::SetVector(UINT i, const XMVECTOR& p)
{
  if (XMComparisonAllTrue(XMVector4EqualR(vv[i], p))) return;
  vv[i] = p; invstate |= (0x000001 << i);
}
void CView::SetMatrix(UINT i, const XMMATRIX& p)
{
  //if(!XMComparisonAllTrue(XMVector4EqualR(vv[i], p))) return;
  mm[i] = p; invstate |= (0x000100 << i);
}

CComPtr<ID3D11Texture2D> rtvtex1, dsvtex1, rtvcpu1, dsvcpu1;
CComPtr<ID3D11RenderTargetView> rtv1;
CComPtr<ID3D11DepthStencilView> dsv1;

static void initpixel()
{
  D3D11_TEXTURE2D_DESC td = { 0 };
  td.Width = td.Height = td.ArraySize = td.MipLevels = td.SampleDesc.Count = 1;
  td.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
  td.Format = DXGI_FORMAT_B8G8R8A8_UNORM; //td.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
  device->CreateTexture2D(&td, 0, &rtvtex1);

  D3D11_RENDER_TARGET_VIEW_DESC rdesc; memset(&rdesc, 0, sizeof(rdesc));
  rdesc.Format = td.Format;
  rdesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
  device->CreateRenderTargetView(rtvtex1.p, &rdesc, &rtv1);

  td.BindFlags = D3D11_BIND_DEPTH_STENCIL;
  td.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
  device->CreateTexture2D(&td, 0, &dsvtex1);

  D3D11_DEPTH_STENCIL_VIEW_DESC ddesc; memset(&ddesc, 0, sizeof(ddesc));
  ddesc.Format = td.Format;
  ddesc.ViewDimension = D3D11_DSV_DIMENSION_TEXTURE2D;
  device->CreateDepthStencilView(dsvtex1, &ddesc, &dsv1);

  td.BindFlags = 0;
  td.CPUAccessFlags = D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_WRITE;
  td.Usage = D3D11_USAGE_STAGING;
  td.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
  device->CreateTexture2D(&td, 0, &rtvcpu1);

  td.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
  device->CreateTexture2D(&td, 0, &dsvcpu1);
}

CComPtr<ID3D11Buffer> ringbuffer;
UINT rbindex, rbcount;
static void rballoc(UINT nv)
{
  ringbuffer.Release();
  D3D11_BUFFER_DESC bd;
  bd.Usage = D3D11_USAGE_DYNAMIC;
  bd.ByteWidth = (rbcount = (((nv >> 11) + 1) << 11)) << 5; //64kb 2kv
  bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
  bd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
  bd.MiscFlags = 0; bd.StructureByteStride = 0;
  device->CreateBuffer(&bd, 0, &ringbuffer);
}

VERTEX* CView::BeginVertices(UINT nv)
{
  if (rbindex + nv > rbcount) { if (nv > rbcount) rballoc(nv); rbindex = 0; }
  D3D11_MAPPED_SUBRESOURCE map;
  context->Map(ringbuffer, 0, rbindex != 0 ? D3D11_MAP_WRITE_NO_OVERWRITE : D3D11_MAP_WRITE_DISCARD, 0, &map);
  auto vv = (VERTEX*)map.pData + rbindex; memset(vv, 0, nv * sizeof(VERTEX)); return vv;
}
void CView::EndVertices(UINT nv, UINT mode)
{
  context->Unmap(ringbuffer, 0);
  if (mode != 0) { SetVertexBuffer(ringbuffer.p); SetMode(mode); SetBuffers(); }
  context->Draw(nv, rbindex); rbindex += nv;
}

void CView::Pick(const short* pt)
{
  if (!swapchain.p) return;
  XMFLOAT2 pc(pt[0], pt[1]); iover = -1;
  if (!rtvtex1.p) initpixel();
  auto vp = viewport; vp.TopLeftX = -pc.x; vp.TopLeftY = -pc.y;
  context->RSSetViewports(1, &vp); //pixelscale = 0;
  context->OMSetRenderTargets(1, &rtv1.p, dsv1.p); float bk[] = { 0,0,0,0 };
  context->ClearRenderTargetView(rtv1.p, bk);
  context->ClearDepthStencilView(dsv1.p, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1, 0);
  if (scene.p)
  {
    setproject();
    auto& nodes = scene.p->nodes; const UINT stride = 32, offs = 0;
    SetMode(MO_TOPO_TRIANGLELISTADJ | MO_PSSHADER_COLOR | MO_DEPTHSTENCIL_ZWRITE);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      SetMatrix(MM_WORLD, node.gettrans(scene.p));
      SetColor(VV_DIFFUSE, i + 1); SetBuffers();
      for (UINT k = 0; k < node.materials.n; k++)
      {
        auto& ma = node.materials.p[k];
        SetVertexBuffer(node.vb.p->p.p); SetIndexBuffer(node.ib.p->p.p);
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
      }
    }
  }
  context->CopyResource(rtvcpu1, rtvtex1);
  D3D11_MAPPED_SUBRESOURCE map;
  context->Map(rtvcpu1, 0, D3D11_MAP_READ, 0, &map);
  auto ppc = *(UINT*)map.pData; context->Unmap(rtvcpu1, 0);
  context->CopyResource(dsvcpu1, dsvtex1);
  context->Map(dsvcpu1, 0, D3D11_MAP_READ, 0, &map);
  auto ppz = *(UINT*)map.pData; context->Unmap(dsvcpu1, 0);

  XMVECTOR pickp;
  pickp.m128_f32[0] = +((pc.x * 2) / viewport.Width - 1);
  pickp.m128_f32[1] = -((pc.y * 2) / viewport.Height - 1);
  pickp.m128_f32[2] = (ppz & 0xffffff) / (float)0xffffff;
  pickp.m128_f32[3] = 0;

  if (ppc)
  {
    auto& nodes = scene.p->nodes;
    auto m = XMMatrixInverse(0, nodes.p[iover = ppc - 1]->gettrans(scene.p) * mm[MM_PLANE]);
    vv[VV_OVERPOS] = XMVector3TransformCoord(pickp, m);
    mm[MM_PLANE] = mm[MM_VIEWPROJ];
  }

  if (isnan(vv[VV_OVERPOS].m128_f32[0]))
  {
    iover = -1; //todo: check
  }
}

void CView::setproject()
{
  auto vpx = viewport.Width * znear * vscale; //var vp = size * (vscale * z1);
  auto vpy = viewport.Height * znear * vscale;
  SetMatrix(MM_VIEWPROJ, XMMatrixInverse(0,
    camera.p->gettrans(camera.p->parent ? scene.p : 0)) *
    XMMatrixPerspectiveOffCenterLH(vpx, -vpx, -vpy, vpy, znear, zfar));
}

void CView::RenderScene()
{
  SetColor(VV_AMBIENT, 0x00404040);
  auto light = XMVector3Normalize(XMVectorSet(1, -1, 2, 0));
  SetVector(VV_LIGHTDIR, flags & CDX_RENDER_SHADOWS ? XMVectorSetW(XMVectorMultiply(light, XMVectorSet(0.3f, 0.3f, 0.3f, 0)), minwz) : light);
  auto& nodes = scene.p->nodes; UINT transp = 0;
  for (UINT i = 0; i < scene.p->count; i++)
  {
    auto& node = *nodes.p[i]; if (!node.mesh.p) continue;
    BOOL modified; node.mesh.p->GetModified(&modified);
    if (modified || !node.ib.p || !node.ib.p->p.p) { node.update(); if (!node.ib.p) continue; }
    SetMatrix(MM_WORLD, node.gettrans(scene.p));
    for (UINT k = 0; k < node.materials.n; k++)
    {
      auto& ma = node.materials.p[k]; if ((ma.color >> 24) != 0xff) { transp++; continue; }
      if (ma.tex.p && !ma.tex.p->p) ma.update(scene.p);
      SetColor(VV_DIFFUSE, ma.color); SetVertexBuffer(node.vb.p->p.p); SetIndexBuffer(node.ib.p->p.p);
      SetTexture(ma.tex.p ? ma.tex.p->p : 0); SetBuffers();
      SetMode(MO_TOPO_TRIANGLELISTADJ | MO_DEPTHSTENCIL_ZWRITE | (ma.tex.p ? MO_PSSHADER_TEXTURE3D : MO_PSSHADER_COLOR3D));
      context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
    }
  }
  if (flags & CDX_RENDER_SHADOWS)
  {
    SetMode(MO_TOPO_TRIANGLELISTADJ | MO_GSSHADER_SHADOW | MO_VSSHADER_WORLD | MO_PSSHADER_NULL | MO_RASTERIZER_NOCULL | MO_DEPTHSTENCIL_TWOSID);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      SetMatrix(MM_WORLD, node.gettrans(scene.p));
      for (UINT k = 0; k < node.materials.n; k++)
      {
        auto& ma = node.materials.p[k]; if ((ma.color >> 24) != 0xff) continue;
        SetVertexBuffer(node.vb.p->p.p); SetIndexBuffer(node.ib.p->p.p); SetBuffers();
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
      }
    }
    SetColor(VV_AMBIENT, 0); SetVector(VV_LIGHTDIR, XMVectorMultiply(light, XMVectorReplicate(0.7f)));
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      SetMatrix(MM_WORLD, node.gettrans(scene.p));
      for (UINT k = 0; k < node.materials.n; k++)
      {
        auto& ma = node.materials.p[k]; if ((ma.color >> 24) != 0xff) continue;
        SetColor(VV_DIFFUSE, ma.color); SetVertexBuffer(node.vb.p->p.p); SetIndexBuffer(node.ib.p->p.p);
        SetTexture(ma.tex.p ? ma.tex.p->p : 0); SetBuffers();
        SetMode(MO_TOPO_TRIANGLELISTADJ | MO_BLENDSTATE_ALPHAADD | (ma.tex.p ? MO_PSSHADER_TEXTURE3D : MO_PSSHADER_COLOR3D));
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
      }
    }
    context->ClearDepthStencilView(dsv.p, D3D11_CLEAR_STENCIL, 1, 0);
  }
  if (flags & CDX_RENDER_WIREFRAME)
  {
    SetColor(VV_DIFFUSE, 0x40000000);
    SetMode(MO_TOPO_TRIANGLELISTADJ | MO_RASTERIZER_WIRE | MO_BLENDSTATE_ALPHA);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto node = nodes.p[i]; if (!(node->flags & NODE_FL_INSEL) || !node->ib.p) continue;
      SetMatrix(MM_WORLD, node->gettrans(scene.p));
      for (UINT k = 0; k < node->materials.n; k++)
      {
        auto& ma = node->materials.p[k];
        SetVertexBuffer(node->vb.p->p.p); SetIndexBuffer(node->ib.p->p.p); SetBuffers();
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
      }
    }
  }
  if (flags & CDX_RENDER_OUTLINES)
  {
    SetColor(VV_DIFFUSE, 0xff000000); SetVector(VV_LIGHTDIR, camera.p->matrix.r[3]);
    SetMode(MO_TOPO_TRIANGLELISTADJ | MO_GSSHADER_OUTL3D | MO_VSSHADER_WORLD);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto node = nodes.p[i]; if (!(node->flags & NODE_FL_INSEL) || !node->ib.p) continue;
      SetMatrix(MM_WORLD, node->gettrans(scene.p));
      for (UINT k = 0; k < node->materials.n; k++)
      {
        auto& ma = node->materials.p[k];
        SetVertexBuffer(node->vb.p->p.p); SetIndexBuffer(node->ib.p->p.p); SetBuffers();
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0);
      }
    }
  }
  if (flags & (CDX_RENDER_COORDINATES | CDX_RENDER_BOUNDINGBOX))
  {
#if(true)
    for (UINT j = 0; j < scene.p->count; j++)
    {
      auto main = nodes.p[j]; if (!(main->flags & NODE_FL_SELECT)) continue;
      XMVECTOR box[2]; box[1] = XMVectorNegate(box[0] = g_XMFltMax);
      for (UINT i = 0; i < scene.p->count; i++)
      {
        auto node = nodes.p[i]; if (!(node->flags & NODE_FL_INSEL) || !node->vb.p || !node->ispart(main)) continue;
        auto& pts = *node->gethull(); XMMATRIX wm; if (node != main) wm = node->gettrans(main);
        for (UINT i = 0; i < pts.n; i++)
        {
          auto p = XMLoadFloat3(&pts.p[i]); if (node != main) p = XMVector3Transform(p, wm);
          box[0] = XMVectorMin(box[0], p);
          box[1] = XMVectorMax(box[1], p);
        }
      }
      if (XMVector3Greater(box[0], box[1])) 
        box[0] = box[1] = XMVectorZero();
      SetMatrix(MM_WORLD, main->gettrans(scene.p));
      if (flags & CDX_RENDER_COORDINATES)
      {
        XMVECTOR ma = box[1] + XMVectorReplicate(0.3f), va = XMVectorReplicate(0.2f), vt;
        SetVector(VV_LIGHTDIR, light);
        SetColor(VV_DIFFUSE, 0xffff0000); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskX)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskX), 0.05f);
        SetColor(VV_DIFFUSE, 0xff00ff00); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskY)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskY), 0.05f);
        SetColor(VV_DIFFUSE, 0xff0000ff); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskZ)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskZ), 0.05f);
      }
      if (flags & CDX_RENDER_BOUNDINGBOX)
      {
        SetColor(VV_DIFFUSE, 0xffffffff); DrawBox(box[0], box[1]);
      }
    }
#else
    XMVECTOR box[2]; box[1] = XMVectorNegate(box[0] = g_XMFltMax);
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      //if(!(node.flags & NODE_FL_SELECT)) continue;
      auto& pts = *node.gethull(); auto wm = node.gettrans(scene.p);
      for (UINT i = 0; i < pts.n; i++)
      {
        auto p = XMVector3Transform(XMLoadFloat3(&pts.p[i]), wm);
        box[0] = XMVectorMin(box[0], p);
        box[1] = XMVectorMax(box[1], p);
      }
    }
    if (XMVector3LessOrEqual(box[0], box[1]))//box[0].m128_f32[0] < box[1].m128_f32[0])
    {
      if (flags & CDX_RENDER_COORDINATES)
      {
        XMVECTOR ma = box[1] + XMVectorReplicate(0.3f), va = XMVectorReplicate(0.2f), vt;
        SetVector(VV_LIGHTDIR, light);
        SetMatrix(MM_WORLD, XMMatrixIdentity());
        SetColor(VV_DIFFUSE, 0xffff0000); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskX)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskX), 0.05f);
        SetColor(VV_DIFFUSE, 0xff00ff00); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskY)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskY), 0.05f);
        SetColor(VV_DIFFUSE, 0xff0000ff); DrawLine(g_XMZero, vt = XMVectorAndInt(ma, g_XMMaskZ)); DrawArrow(vt, XMVectorAndInt(va, g_XMMaskZ), 0.05f);
      }
      if (flags & CDX_RENDER_BOUNDINGBOX)
      {
        SetColor(VV_DIFFUSE, 0xffffffff); DrawBox(box[0], box[1]);
      }
    }
#endif
  }
  if (transp != 0)
  {
    for (UINT i = 0; i < scene.p->count; i++)
    {
      auto& node = *nodes.p[i]; if (!node.ib.p) continue;
      for (UINT k = 0; k < node.materials.n; k++)
      {
        auto& ma = node.materials.p[k]; if ((ma.color >> 24) == 0xff) continue;
        if (ma.tex.p && !ma.tex.p->p.p) ma.update(scene.p);
        SetColor(VV_DIFFUSE, ma.color); SetVertexBuffer(node.vb.p->p.p); SetIndexBuffer(node.ib.p->p.p);
        SetTexture(ma.tex.p ? ma.tex.p->p.p : 0); SetMatrix(MM_WORLD, node.gettrans(scene.p)); SetBuffers();
        SetMode(MO_TOPO_TRIANGLELISTADJ | MO_BLENDSTATE_ALPHA | (ma.tex.p ? MO_PSSHADER_TEXTURE3D : MO_PSSHADER_COLOR3D));
        context->DrawIndexed(ma.n << 1, ma.i << 1, 0); if (--transp == 0) { i = scene.p->count; break; }
      }
    }
  }
}

void CView::Render()
{
  context.p->RSSetViewports(1, &viewport);
  context.p->OMSetRenderTargets(1, &rtv.p, dsv.p);
  context.p->ClearDepthStencilView(dsv.p, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1, 0);
  context.p->ClearRenderTargetView(rtv.p, vv[VV_BKCOLOR].m128_f32);
  if (scene.p) { if (!camera.p) inits(1); setproject(); RenderScene(); }
  if (sink.p) sink.p->Render();
  auto hr = swapchain.p->Present(0, 0);
  XMASSERT(stackptr == baseptr);
}

HRESULT CView::get_Samples(BSTR* p)
{
  CComBSTR ss; WCHAR tt[32];
  DXGI_SWAP_CHAIN_DESC desc; swapchain->GetDesc(&desc); wsprintf(tt, L"%i", desc.SampleDesc.Count); ss += tt;
  for (UINT i = 1, q; i <= 16; i++)
    if (device->CheckMultisampleQualityLevels(DXGI_FORMAT_B8G8R8A8_UNORM, i, &q) == 0 && q > 0) { wsprintf(tt, L"\n%i", i); ss += tt; }
  return ss.CopyTo(p);
}
HRESULT CView::get_BkColor(UINT* p)
{
  XMStoreColor((XMCOLOR*)p, vv[VV_BKCOLOR]);
  return 0;
}
HRESULT CView::put_BkColor(UINT p)
{
  SetColor(VV_BKCOLOR, p); return 0;
}

DXGI_SAMPLE_DESC chksmp(ID3D11Device* device, UINT samples)
{
  DXGI_SAMPLE_DESC desc; desc.Count = 1; desc.Quality = 0;
  for (UINT i = samples, q; i > 0; i--)
    if (device->CheckMultisampleQualityLevels(DXGI_FORMAT_B8G8R8A8_UNORM, i, &q) == 0 && q > 0)
    {
      desc.Count = i; desc.Quality = q - 1; break;
    }
  return desc;
}

HRESULT CView::Resize()
{
  viewport.Width = (float)size.cx;// vv[VV_RCSIZE].m128_f32[0] / dpiscale;
  viewport.Height = (float)size.cy;//vv[VV_RCSIZE].m128_f32[1] / dpiscale;
  viewport.MinDepth = 0;
  viewport.MaxDepth = 1;
  viewport.TopLeftX = 0;
  viewport.TopLeftY = 0;
  DXGI_SWAP_CHAIN_DESC desc;
  if (!swapchain.p)
  {
    CComPtr<IDXGIDevice> pDXGIDevice; device->QueryInterface(__uuidof(IDXGIDevice), (void**)&pDXGIDevice.p);
    CComPtr<IDXGIAdapter> adapter; pDXGIDevice->GetAdapter(&adapter.p); pDXGIDevice.Release();
    CComPtr<IDXGIFactory> factory; adapter->GetParent(__uuidof(IDXGIFactory), (void**)&factory.p); adapter.Release();
    desc.BufferDesc.Width = size.cx;
    desc.BufferDesc.Height = size.cy;
    desc.BufferDesc.RefreshRate.Numerator = 60;
    desc.BufferDesc.RefreshRate.Denominator = 1;
    desc.BufferDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM; // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB
    desc.BufferDesc.ScanlineOrdering = DXGI_MODE_SCANLINE_ORDER_UNSPECIFIED;
    desc.BufferDesc.Scaling = DXGI_MODE_SCALING_UNSPECIFIED;
    desc.SampleDesc = chksmp(device, sampels); sampels = desc.SampleDesc.Count;
    desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount = 1;
    desc.OutputWindow = hwnd;
    desc.Windowed = 1;
    desc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
    desc.Flags = 0;
    CHR(factory->CreateSwapChain(device, &desc, &swapchain.p));
    CHR(factory->MakeWindowAssociation(hwnd, DXGI_MWA_NO_ALT_ENTER | DXGI_MWA_NO_WINDOW_CHANGES));
  }
  else
  {
    swapchain.p->GetDesc(&desc); rtv.Release(); dsv.Release(); //tds.Release(); 
    CHR(swapchain.p->ResizeBuffers(desc.BufferCount, size.cx, size.cy, desc.BufferDesc.Format, 0));
  }
  CComPtr<ID3D11Texture2D> backbuffer;
  CHR(swapchain.p->GetBuffer(0, __uuidof(ID3D11Texture2D), (void**)&backbuffer.p));
  D3D11_TEXTURE2D_DESC texdesc; backbuffer.p->GetDesc(&texdesc);
  D3D11_RENDER_TARGET_VIEW_DESC renderDesc; memset(&renderDesc, 0, sizeof(renderDesc));
  renderDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
  renderDesc.ViewDimension = desc.SampleDesc.Count > 1 ? D3D11_RTV_DIMENSION_TEXTURE2DMS : D3D11_RTV_DIMENSION_TEXTURE2D;
  CHR(device->CreateRenderTargetView(backbuffer.p, &renderDesc, &rtv.p));
  D3D11_TEXTURE2D_DESC descDepth;
  descDepth.Width = texdesc.Width;
  descDepth.Height = texdesc.Height;
  descDepth.MipLevels = 1;
  descDepth.ArraySize = 1;
  descDepth.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
  descDepth.SampleDesc.Count = desc.SampleDesc.Count;
  descDepth.SampleDesc.Quality = desc.SampleDesc.Quality;
  descDepth.Usage = D3D11_USAGE_DEFAULT;
  descDepth.BindFlags = D3D11_BIND_DEPTH_STENCIL;
  descDepth.CPUAccessFlags = 0;
  descDepth.MiscFlags = 0;
  CComPtr<ID3D11Texture2D> tds;
  CHR(device->CreateTexture2D(&descDepth, 0, &tds.p));
  D3D11_DEPTH_STENCIL_VIEW_DESC descDSV;
  descDSV.Format = descDepth.Format;
  descDSV.Flags = 0;
  descDSV.ViewDimension = descDepth.SampleDesc.Count > 1 ? D3D11_DSV_DIMENSION_TEXTURE2DMS : D3D11_DSV_DIMENSION_TEXTURE2D;
  descDSV.Texture2D.MipSlice = 0;
  CHR(device->CreateDepthStencilView(tds.p, &descDSV, &dsv.p));
  return 0;
}

HRESULT CreateDevice()
{
  CComPtr<IDXGIAdapter> adapter;
  if (adapterid)
  {
    CComPtr<IDXGIFactory> factory; CHR(CreateDXGIFactory(__uuidof(IDXGIFactory), (void**)&factory.p));
    for (UINT i = 0; factory->EnumAdapters(i, &adapter) == 0; i++, adapter.Release())
    {
      DXGI_ADAPTER_DESC desc; adapter->GetDesc(&desc);
      if (desc.DeviceId == adapterid) break;
    }
  }
  D3D_FEATURE_LEVEL level, ff[] = { D3D_FEATURE_LEVEL_11_0 };
  CHR(D3D11CreateDevice(adapter, adapter.p ? D3D_DRIVER_TYPE_UNKNOWN : D3D_DRIVER_TYPE_HARDWARE, 0,
    D3D11_CREATE_DEVICE_SINGLETHREADED | D3D11_CREATE_DEVICE_BGRA_SUPPORT | (Debug ? D3D11_CREATE_DEVICE_DEBUG : 0),
    ff, sizeof(ff) / sizeof(ff[0]), D3D11_SDK_VERSION, &device, &level, &context));
  CHR(device->CreateVertexShader(_VSMain, sizeof(_VSMain), 0, &vertexshader[0].p));
  CHR(device->CreateInputLayout(layout, 3, _VSMain, sizeof(_VSMain), &vertexlayout.p));
  D3D11_BUFFER_DESC desc;
  desc.Usage = D3D11_USAGE_DYNAMIC;
  desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
  desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
  desc.MiscFlags = 0;
  desc.ByteWidth = sizeof(CB_VS_PER_FRAME);  CHR(device->CreateBuffer(&desc, 0, &cbbuffer[0].p));
  desc.ByteWidth = sizeof(CB_VS_PER_OBJECT); CHR(device->CreateBuffer(&desc, 0, &cbbuffer[1].p));
  desc.ByteWidth = sizeof(CB_PS_PER_OBJECT); CHR(device->CreateBuffer(&desc, 0, &cbbuffer[2].p));
  context->IASetInputLayout(vertexlayout);
  currentmode = 0x7fffffff; invstate = INV_CB_VS_PER_FRAME | INV_CB_VS_PER_OBJECT | INV_CB_PS_PER_OBJECT | INV_TT_DIFFUSE;
  return 0;
}

void releasedx()
{
  {
    Critical crit;
    for (auto p = CView::first; p; p = p->next) p->relres();
    for (auto p = CTexture::first; p; p = p->next) p->p.Release();
    for (auto p = CVertices::first; p; p = p->next) p->p.Release();
    for (auto p = CIndices::first; p; p = p->next) p->p.Release();
    for (auto p = CFont::first; p; p = p->next) p->relres(0);
  }
  for (UINT i = 0; i < sizeof(cbbuffer) / sizeof(void*); i++) cbbuffer[i].Release();
  for (UINT i = 0; i < sizeof(vertexshader) / sizeof(void*); i++) vertexshader[i].Release();
  for (UINT i = 0; i < sizeof(depthstencilstates) / sizeof(void*); i++) depthstencilstates[i].Release();
  for (UINT i = 0; i < sizeof(blendstates) / sizeof(void*); i++) blendstates[i].Release();
  for (UINT i = 0; i < sizeof(rasterizerstates) / sizeof(void*); i++) rasterizerstates[i].Release();
  for (UINT i = 0; i < sizeof(samplerstates) / sizeof(void*); i++) samplerstates[i].Release();
  for (UINT i = 0; i < sizeof(geometryshader) / sizeof(void*); i++) geometryshader[i].Release();
  for (UINT i = 0; i < sizeof(pixelshader) / sizeof(void*); i++) pixelshader[i].Release();
  for (UINT i = 0; i < sizeof(currentbuffer) / sizeof(void*); i++) currentbuffer[i] = 0;
  ringbuffer.Release(); rbindex = rbcount = 0;
  rtvtex1.Release(); dsvtex1.Release(); rtvcpu1.Release(); dsvcpu1.Release();
  rtv1.Release(); dsv1.Release();
  vertexlayout.Release(); context.Release();
  //CComQIPtr<ID3D11Debug> dbg(device.p); if (dbg.p) dbg.p->ReportLiveDeviceObjects(D3D11_RLDO_SUMMARY);
  int rc = device.p->Release(); device.p = 0;
  XMASSERT(rc == 0);
}

HRESULT __stdcall CFactory::get_Devices(BSTR* p)
{
  if (!device.p) CHR(CreateDevice());
  CComQIPtr<IDXGIDevice> dev(device);
  CComPtr<IDXGIAdapter> adapter; dev->GetAdapter(&adapter.p);
  CComPtr<IDXGIFactory> factory; adapter->GetParent(__uuidof(IDXGIFactory), (void**)&factory);
  DXGI_ADAPTER_DESC desc; adapter->GetDesc(&desc);
  CComBSTR ss; WCHAR tt[32]; wsprintf(tt, L"%i", desc.DeviceId); ss = tt; adapter.Release();
  for (UINT t = 0, last = -1; factory->EnumAdapters(t, &adapter) == 0; adapter.Release(), t++)
  {
    adapter->GetDesc(&desc); if (desc.DeviceId == last) continue;
    wsprintf(tt, L"\n%i\n", last = desc.DeviceId); ss += tt; ss += desc.Description;
  }
  return ss.CopyTo(p);
}

HRESULT __stdcall CFactory::SetDevice(UINT id)
{
  if (id == -1) { if (device.p) { d_font.Release(); releasedx(); } return 0; }
  if (adapterid == id) return 0;
  adapterid = id; if (device.p == 0) return 0;
  releasedx(); CHR(CreateDevice());
  for (auto p = CView::first; p; p = p->next) p->initres();
  return 0;
}
HRESULT __stdcall CFactory::CreateView(HWND hwnd, ICDXSink* sink, UINT samp, ICDXView** p)
{
  if (!device.p) CHR(CreateDevice());
  auto view = new CView(); view->sampels = samp; view->sink = sink; GetClientRect(view->hwnd = hwnd, &view->rcclient);
  auto olddat = SetWindowLongPtr(hwnd, GWLP_USERDATA, (LONG_PTR)view);
  view->proc = (WNDPROC)SetWindowLongPtr(hwnd, GWLP_WNDPROC, (LONG_PTR)CView::WndProc);
  *p = view; return 0;
}
HRESULT __stdcall CFactory::CreateScene(UINT reserve, ICDXScene** p)
{
  *p = new CScene();
  return 0;
}

HRESULT __stdcall CView::Draw(CDX_DRAW id, UINT* data)
{
  switch (id)
  {
  case CDX_DRAW_ORTHOGRAPHIC:
    SetMatrix(MM_VIEWPROJ, XMMatrixOrthographicOffCenterLH(0, viewport.Width, viewport.Height, 0, -1, 1));
    SetMatrix(MM_WORLD, XMMatrixIdentity());
    return 0;
  case CDX_DRAW_GET_TRANSFORM:
    XMStoreFloat4x3((XMFLOAT4X3*)data, mm[MM_WORLD]);
    return 0;
  case CDX_DRAW_SET_TRANSFORM:
    SetMatrix(MM_WORLD, XMLoadFloat4x3((XMFLOAT4X3*)data));
    return 0;
  case CDX_DRAW_GET_COLOR:
    XMStoreColor((XMCOLOR*)data, vv[VV_DIFFUSE]);
    return 0;
  case CDX_DRAW_SET_COLOR:
    SetColor(VV_DIFFUSE, data[0]); d_blend = data[0] >> 24 != 0xff ? MO_BLENDSTATE_ALPHA : 0;
    return 0;
  case CDX_DRAW_GET_FONT:
    return d_font.CopyTo((CFont**)data);
  case CDX_DRAW_SET_FONT:
    d_font = (CFont*)data;
    return 0; 
  case CDX_DRAW_FILL_RECT:
  {
    auto p = BeginVertices(4);
    p[0].p.x = p[2].p.x = ((float*)data)[0];
    p[0].p.y = p[1].p.y = ((float*)data)[1];
    p[1].p.x = p[3].p.x = ((float*)data)[0] + ((float*)data)[2];
    p[2].p.y = p[3].p.y = ((float*)data)[1] + ((float*)data)[3];
    EndVertices(4, MO_TOPO_TRIANGLESTRIP | MO_PSSHADER_COLOR | MO_RASTERIZER_NOCULL | d_blend);
    return 0;
  }
  case CDX_DRAW_FILL_ELLIPSE:
  {
    float dx = ((float*)data)[2] * 0.5f, dy = ((float*)data)[3] * 0.5f, x = dx + ((float*)data)[0], y = dy + ((float*)data)[1];
    auto se = (int)(max(abs(dx), abs(dy)) * 0.25f);
    se = max(8, min(100, se)) >> 1 << 1; //var tt = (int)((float)Math.Pow(Math.Max(Math.Abs(rx), Math.Abs(ry)), 0.95f) * dc.PixelScale);
    auto fa = (2 * XM_PI) / se; auto nv = se + 2;
    auto vv = BeginVertices(nv);
    for (int i = 0, j = 0; j < nv; i++)
    {
      float u, v; XMScalarSinCosEst(&u, &v, i * fa); u *= dx; v *= dy;
      vv[j].p.x = x + u; vv[j++].p.y = y + v;
      vv[j].p.x = x - u; vv[j++].p.y = y + v;
    }
    EndVertices(nv, MO_TOPO_TRIANGLESTRIP | MO_PSSHADER_COLOR | MO_RASTERIZER_NOCULL | d_blend);
    return 0;
  }
  case CDX_DRAW_GET_TEXTEXTENT:
    *(XMFLOAT2*)data = d_font.p->getextent(*(LPCWSTR*)(data + 2), *(UINT*)((LPCWSTR*)(data + 2) + 1));
    return 0;
  case CDX_DRAW_DRAW_TEXT:
    d_font.p->draw(this, *(XMFLOAT2*)data, *(LPCWSTR*)(data + 2), *(UINT*)((LPCWSTR*)(data + 2) + 1));
    return 0;
  case CDX_DRAW_DRAW_RECT:
  {
    auto vv = BeginVertices(5);
    vv[0].p.x = vv[3].p.x = ((float*)data)[0];
    vv[0].p.y = vv[1].p.y = ((float*)data)[1];
    vv[1].p.x = vv[2].p.x = ((float*)data)[0] + ((float*)data)[2];
    vv[2].p.y = vv[3].p.y = ((float*)data)[1] + ((float*)data)[3]; *(INT64*)&vv[4].p = *(INT64*)&vv[0].p;
    EndVertices(5, MO_TOPO_LINESTRIP | MO_PSSHADER_COLOR | MO_RASTERIZER_NOCULL | d_blend);
    return 0;
  }
  //case CDX_DRAW_DRAW_POLYGON:
  //{
  //  auto np = *(UINT*)data; auto pp = *(XMFLOAT2**)((UINT*)data + 1);
  //  auto vv = BeginVertices(++np);
  //  for (int i = 0, k = np - 2; i < np; k = i++) *(XMFLOAT2*)&vv[i].p = pp[k];
  //  EndVertices(np, MO_TOPO_LINESTRIP | MO_PSSHADER_COLOR | MO_RASTERIZER_NOCULL | d_blend);
  //  return 0;
  //}
  }
  return E_FAIL;
}

