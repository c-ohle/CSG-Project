#pragma warning disable 0649 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static csg3mf.Viewer.D3DView;

namespace csg3mf.Viewer
{
  public unsafe partial class D3DView : UserControl, D3DView.IDisplay, D3DView.ISelector
  {
    static long drvsettings;
    static D3DView()
    {
      Application.ApplicationExit += reset;
      StackPtr = baseptr = (byte*)VirtualAlloc(null, (void*)maxstack, 0x00001000 | 0x00002000, 0x04); //MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE   
      cb1 = (cbPerObject*)StackPtr; StackPtr += sizeof(cbPerObject) << 1;
      cb2 = (cbPerFrame*)StackPtr; StackPtr += sizeof(cbPerFrame) << 1;
      cb3 = (cbPsPerObject*)StackPtr; StackPtr += sizeof(cbPsPerObject) << 1;
      currentvp = (VIEWPORT*)StackPtr; StackPtr += sizeof(VIEWPORT);
      StackPtr += 8; Assert(StackPtr - baseptr == fixstack);
      memset(baseptr, 0, (void*)fixstack); //0x180
      var drv = Application.UserAppDataRegistry.GetValue("drv"); if (drv is long) drvsettings = (long)drv;
      CreateDriver((uint)(drvsettings & 0xffffffff));
      DpiScale = GetDeviceCaps(hdcnull = GetDC(null), 88) * (1f / 96); //LOGPIXELSX 
      font = GetFont("Segoe UI", 9);
    }

    static void CreateDriver(uint id)
    {
      var adapter = (IAdapter)null;
      if (id != 0)
      {
        CreateDXGIFactory(typeof(IFactory).GUID, out object unk); var factory = (IFactory)unk;
        for (int i = 0; factory.EnumAdapters(i, out adapter) == 0 && adapter.Desc.DeviceId != id; i++) Marshal.ReleaseComObject(adapter);
      }
      //if (adapter != null) {for (int i = 0; adapter.EnumOutputs(i, out var output) == 0; i++) { var desc = output.Desc; Marshal.ReleaseComObject(output); }}
      FEATURE_LEVEL level; var levels = FEATURE_LEVEL._11_0;// stackalloc FEATURE_LEVEL[] { FEATURE_LEVEL._12_1, FEATURE_LEVEL._12_0, FEATURE_LEVEL._11_1, FEATURE_LEVEL._11_0, FEATURE_LEVEL._10_1, FEATURE_LEVEL._10_0 };
      int hr = D3D11CreateDevice(adapter, adapter != null ? D3D_DRIVER_TYPE.Unknown : D3D_DRIVER_TYPE.Hardware, null,
        CREATE_DEVICE_FLAG.SingleThreaded | CREATE_DEVICE_FLAG.BGRA_Support,
        &levels, 1, SDK_VERSION.Current, out device, &level, out context);
      if (adapter != null) Marshal.ReleaseComObject(adapter);
      //adapter = ((IDXGIDevice)device).Adapter;
      //for (int i = 0; adapter.EnumOutputs(i, out var output) == 0; i++) { var desc = output.Desc; Marshal.ReleaseComObject(output); }
      //Marshal.ReleaseComObject(adapter);
      if (hr < 0) { if (id == 0) throw new Exception("D3D11CreateDevice failed!"); CreateDriver(0); return; }
      cbperobject = CreateConstBuffer(sizeof(cbPerObject));
      cbperframe = CreateConstBuffer(sizeof(cbPerFrame));
      cbpsperobject = CreateConstBuffer(sizeof(cbPsPerObject));
      //cbClipBox = CreateConstBuffer(6 * sizeof(float4));
      context.VSSetConstantBuffers(0, 1, ref cbperframe);
      context.GSSetConstantBuffers(0, 1, ref cbperframe);
      context.VSSetConstantBuffers(1, 1, ref cbperobject);
      context.PSSetConstantBuffers(1, 1, ref cbpsperobject);
      //context.PSSetConstantBuffers(2, 1, ref cbClipBox);
      cb1->World._11 = cb1->World._22 = cb1->World._33 = cb1->World._44 = cb3->Diffuse.w = 1;

      //var planes = (float4*)context.Map(cbClipBox, 0, MAP.WRITE_DISCARD, 0).pData;
      //planes[0] = new float4(0.1f, 0.0f, 0.0f, 0.0f);
      //planes[1] = new float4(0.0f, 0.0f, -1.0f, 0.5f);
      //context.Unmap(cbClipBox, 0);
    }
    static void reset(object sender, EventArgs e)
    {
      release(ref vertexshader); release(ref geoshader); release(ref pixelshader); release(ref depthstencil); release(ref blend); release(ref rasterizer); release(ref sampler);
      release(ref vertexlayout); release(ref ringbuffer); release(ref cbperobject); release(ref cbperframe); release(ref cbpsperobject); //release(ref cbClipBox);
      release(ref rtvtex1); release(ref dsvtex1); release(ref rtvcpu1); release(ref dsvcpu1); release(ref rtv1); release(ref dsv1);
      if (sender == null) return;
      ReleaseDC(null, hdcnull); hdcnull = null;
    }

    const string res = "csg3mf.Viewer.Shader.";
    const int maxstack = 100_000_000; //100 MB
    const int fixstack = 0x180;
    public static byte* StackPtr;
    static byte* baseptr, ReadPtr;
    static void* hdcnull;

    public Action<IDisplay> Render;
    public Func<int, ISelector, int> Dispatch;
    public Action Timer;

    Action<int, object> tool; //Action<int, object> droptool;
    protected virtual void OnRender(IDisplay dc)
    {
      Render?.Invoke(dc);
    }
    protected virtual int OnDispatch(int id, ISelector dc)
    {
      return Dispatch != null ? Dispatch(id, dc) : 0;
    }
    protected virtual void OnTimer()
    {
      Timer?.Invoke();
    }

    public static float DpiScale;
    IDisplay dc; VIEWPORT viewport; int counter; bool inval;
    ISwapChain swapchain; IntPtr rtv, dsv; //IRenderTargetView, IDepthStencilView

    void sizebuffers()
    {
      var cs = this.ClientSize;
      viewport.Width = cs.Width = Math.Max(cs.Width, 1);
      viewport.Height = cs.Height = Math.Max(cs.Height, 1); viewport.MaxDepth = 1;
      SWAP_CHAIN_DESC desc;
      if (swapchain == null)
      {
        var factory = (IFactory)((IDXGIDevice)device).Adapter.GetParent(typeof(IFactory).GUID);
        desc.BufferDesc.Width = cs.Width;
        desc.BufferDesc.Height = cs.Height;
        //desc.BufferDesc.RefreshRate.Numerator = 60;
        //desc.BufferDesc.RefreshRate.Denominator = 1;
        desc.BufferDesc.Format = FORMAT.B8G8R8A8_UNORM;
        desc.SampleDesc = CheckMultisample(device, desc.BufferDesc.Format, Math.Max(1, (int)(drvsettings >> 32)));
        desc.BufferUsage = BUFFERUSAGE.RENDER_TARGET_OUTPUT;
        desc.BufferCount = 1;
        desc.OutputWindow = this.Handle;
        desc.Windowed = 1; //desc.Flags = 2; DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH = 2,
        swapchain = factory.CreateSwapChain(device, &desc);
        factory.MakeWindowAssociation(desc.OutputWindow, MWA_NO.ALT_ENTER | MWA_NO.WINDOW_CHANGES);
      }
      else
      {
        releasebuffers(false); desc = swapchain.Desc;
        swapchain.ResizeBuffers(desc.BufferCount, cs.Width, cs.Height, desc.BufferDesc.Format, 0);
      }

      var backbuffer = swapchain.GetBuffer(0, typeof(ITexture2D).GUID);
      RENDER_TARGET_VIEW_DESC renderDesc;
      renderDesc.Format = FORMAT.B8G8R8A8_UNORM;
      renderDesc.ViewDimension = desc.SampleDesc.Count > 1 ? RTV_DIMENSION.TEXTURE2DMS : RTV_DIMENSION.TEXTURE2D;
      this.rtv = device.CreateRenderTargetView(backbuffer, &renderDesc);
      Marshal.Release(backbuffer);

      TEXTURE2D_DESC descDepth;
      descDepth.Width = cs.Width;
      descDepth.Height = cs.Height;
      descDepth.MipLevels = 1;
      descDepth.ArraySize = 1;
      descDepth.Format = FORMAT.D24_UNORM_S8_UINT;
      descDepth.SampleDesc.Count = desc.SampleDesc.Count;
      descDepth.SampleDesc.Quality = desc.SampleDesc.Quality;
      descDepth.Usage = USAGE.DEFAULT;
      descDepth.BindFlags = BIND.DEPTH_STENCIL;
      descDepth.CPUAccessFlags = 0;
      descDepth.MiscFlags = 0;
      var tds = device.CreateTexture2D(&descDepth);

      DEPTH_STENCIL_VIEW_DESC descDSV;
      descDSV.Format = descDepth.Format;
      descDSV.Flags = 0;
      descDSV.ViewDimension = descDepth.SampleDesc.Count > 1 ? DSV_DIMENSION.TEXTURE2DMS : DSV_DIMENSION.TEXTURE2D;
      descDSV.Texture2D.MipSlice = 0;
      this.dsv = device.CreateDepthStencilView(tds, &descDSV);
      Marshal.Release(tds);
    }
    void releasebuffers(bool swp)
    {
      release(ref this.rtv);
      release(ref this.dsv);
      if (swp && swapchain != null) { Marshal.ReleaseComObject(swapchain); swapchain = null; }
    }

    public override void Refresh()
    {
      OnTimer(); if (!inval) return;
      if (rtv == IntPtr.Zero) sizebuffers();
      long t1; QueryPerformanceCounter(&t1);
      Begin(rtv, dsv, viewport, (uint)BackColor.ToArgb()); OnRender(dc);
      var hr = swapchain.Present(0, 0); Debug.WriteLineIf(hr != 0, $"Present 0x{hr:X8}");
      //if(hr == unchecked((int)0x8876017C)) hr = swapchain.Present(0, 0); //DDERR_OUTOFVIDEOMEMORY windows 10 1803 nvidia bug
      long t2; QueryPerformanceCounter(&t2); counter = (int)(t2 - t1);
      inval = false; Assert(StackPtr - baseptr == fixstack); //StackPtr = baseptr + fixstack;
    }
    public new void Invalidate() { inval = true; }
    public long GetFPS()
    {
      long fr; QueryPerformanceFrequency(&fr);
      return counter != 0 ? fr / counter : 0;
    }
    public string Adapter
    {
      get { return ((IDXGIDevice)device).Adapter.Desc.Description; }
    }
    public int OnDriver(object test)
    {
      if (test is ToolStripMenuItem item)
      {
        CreateDXGIFactory(typeof(IFactory).GUID, out object unk);
        var factory = (IFactory)unk; var current = ((IDXGIDevice)device).Adapter.Desc.DeviceId;
        for (int t = 0; factory.EnumAdapters(t, out IAdapter adapter) == 0; t++)
        {
          var desc = adapter.Desc;
          if (item.DropDownItems.Cast<ToolStripMenuItem>().Any(p => (uint)p.Tag == adapter.Desc.DeviceId)) continue;
          item.DropDownItems.Add(new ToolStripMenuItem(desc.Description) { Tag = desc.DeviceId, Checked = desc.DeviceId == current });
        }
        return 0;
      }
      if (((IDXGIDevice)device).Adapter.Desc.DeviceId == (uint)test) return 0; Cursor.Current = Cursors.WaitCursor;
      var tmp = Buffer.cache.Values.Select(p => p.Target).OfType<Buffer>().Select(p => new { buffer = p, data = p.Reset() }).ToArray();
      releasebuffers(true); reset(null, null);
      memset(baseptr, 0, (void*)fixstack); rbindex = rbcount = cbsok = 0; drvmode = 0x7fffffff;
      CreateDriver((uint)test); foreach (var p in tmp) p.buffer.Reset(p.data); Invalidate();
      drvsettings = (drvsettings >> 32 << 32) | (uint)test; Application.UserAppDataRegistry.SetValue("drv", drvsettings, Microsoft.Win32.RegistryValueKind.QWord);
      return 0;
    }
    public int OnSamples(object test)
    {
      var current = swapchain.Desc.SampleDesc.Count;
      if (test is ToolStripMenuItem item)
      {
        SAMPLE_DESC desc; desc.Count = 1; desc.Quality = 0;
        for (int i = 1, q; i <= 16; i++)
          if (device.CheckMultisampleQualityLevels(FORMAT.B8G8R8A8_UNORM, i, out q) == 0 && q > 0)
            item.DropDownItems.Add(new ToolStripMenuItem($"{i} Samples") { Tag = i, Checked = current == i });
        return 0;
      }
      drvsettings = (drvsettings & 0xffffffff) | ((long)(int)test << 32);
      releasebuffers(true); Invalidate(); Application.UserAppDataRegistry.SetValue("drv", drvsettings, Microsoft.Win32.RegistryValueKind.QWord);
      return 0;
    }

    protected override void WndProc(ref Message m)
    {
      switch (m.Msg)
      {
        case 0x0113: Refresh(); return;//WM_TIMER  
        case 0x0014: m.Result = (IntPtr)1; return; //WM_ERASEBKGND
        case 0x000F: ValidateRect(m.HWnd, null); inval = true; Refresh(); return; //WM_PAINT
        case 0x0201: //WM_LBUTTONDOWN           
        case 0x0204: //WM_RBUTTONDOWN
        case 0x0207: //WM_MBUTTONDOWN
          Capture = true; //Focus();
          Pick(new System.Drawing.Point((int)m.LParam)); if (OnDispatch(m.Msg, this) != 0) return;
          break;
        case 0x0200: //WM_MOUSEMOVE
          if (tool != null) { point = new System.Drawing.Point((int)m.LParam); tool(0, this); Refresh(); return; }
          Pick(new System.Drawing.Point((int)m.LParam)); SetCursor(LoadCursor(null, (void*)Math.Max(32512, OnDispatch(m.Msg, this))));
          return;
        case 0x0202: //WM_LBUTTONUP
        case 0x0205: //WM_RBUTTONUP
        case 0x0208: //WM_MBUTTONUP
          Capture = false;
          if (tool != null) { tool(1, this); tool = null; }
          if (OnDispatch(m.Msg, this) != 0) return;
          break;
        case 0x020A: //WM_MOUSEWHEEL  
        case 0x020E: //WM_MOUSEHWHEEL 
          if (tool == null) { Pick(PointToClient(Cursor.Position)); OnDispatch(m.Msg | ((int)m.WParam.ToInt64() >> 16 << 16), this); }
          return;
        case 0x0203: //WM_LBUTTONDBLCLK  
          OnDispatch(m.Msg, this);
          return;
        case 0x02A3: //WM_MOUSELEAVE
          if (tool != null && droptool == null) { tool(1, this); tool = null; }
          OnDispatch(m.Msg, this); pickdata = pickview = null; pickid = 0;
          return;
        case 0x007B: //WM_CONTEXTMENU 
          if ((Tag = ContextMenuStrip?.Items) != null) { id = OnDispatch(m.Msg, this); Tag = null; if (id != 0) return; }
          break;
        case 0x0005: releasebuffers(false); Invalidate(); base.Invalidate(); break; //WM_SIZE
        case 0x0020: m.Result = (IntPtr)1; return; // WM_SETCURSOR
        case 0x0100: //WM_KEYDOWN  
        case 0x0102: //WM_CHAR   
          //case 0x0104: //WM_SYSKEYDOWN
          if (OnDispatch(m.Msg | ((int)m.WParam << 16), null) != 0) { Refresh(); return; }
          break;
        case 0x0007:  //WM_SETFOCUS
        case 0x0008:  //WM_KILLFOCUS
          OnDispatch(m.Msg, this);
          return;
        case 0x0001: //WM_CREATE    
          SetTimer(Handle, (void*)0, 30, null); dc = this; break;
        case 0x0002: //WM_DESTROY    
          releasebuffers(true);
          break;
      }
      //Debug.WriteLine(m.Msg.ToString("X4"));
      base.WndProc(ref m);
    }

    protected override bool IsInputKey(Keys keyData)
    {
      return (keyData & (Keys.Control | Keys.Alt)) == 0;
    }

    #region Display

    static IDevice device;
    static IDeviceContext context;
    static readonly VIEWPORT* currentvp; static float pixelscale;
    static IntPtr currentdsv; //IDepthStencilView
    static Texture texture; static IntPtr currentsrv; //IShaderResourceView
    static Font font;
    static IntPtr cbperobject, cbperframe, cbpsperobject; //, cbClipBox; //IBuffer
    static readonly cbPerObject* cb1; static readonly cbPerFrame* cb2; static readonly cbPsPerObject* cb3; static int cbsok;
    struct cbPerFrame
    {
      internal float4x4 ViewProjection;
      internal float4 Ambient;
      internal float4 LightDir;
    }
    struct cbPerObject
    {
      internal float4x4 World;
    }
    struct cbPsPerObject
    {
      internal float4 Diffuse;
    }

    static bool cmpcpy(void* d, void* s, int n)
    {
      Assert(n >> 2 << 2 == n);
      int i = 0; for (n >>= 2; i < n && ((int*)d)[i] == ((int*)s)[i]; i++) ; if (i == n) return false;
      for (; i < n; i++) ((int*)d)[i] = ((int*)s)[i]; return true;
    }
    static void Begin(IntPtr/*IRenderTargetView*/ rtv, IntPtr/*IDepthStencilView*/ dsv, VIEWPORT viewport, uint bkcolor)
    {
      if (cmpcpy(currentvp, &viewport, sizeof(VIEWPORT)))
      {
        context.RSSetViewports(1, &viewport); pixelscale = 0;
        cb2->ViewProjection = float4x4.OrthoCenter(0, viewport.Width / DpiScale, viewport.Height / DpiScale, 0, 0, 1); cbsok &= ~1;
      }
      context.OMSetRenderTargets(1, ref rtv, currentdsv = dsv); //RECT r; r.right = 1000; r.bottom = 1000; context.RSSetScissorRects(1, &r);
      context.ClearRenderTargetView(rtv, (float4)bkcolor);
      context.ClearDepthStencilView(dsv, CLEAR.DEPTH | CLEAR.STENCIL, 1, 0);
    }

    static int mode, drvmode = 0x7fffffff, stencilref;
    static float4 blenfactors; static uint samplemask;
    static IntPtr vertexlayout; //IInputLayout
    static IntPtr[] vertexshader = new IntPtr[4]; //IVertexShader
    static IntPtr[] geoshader = new IntPtr[3]; //IGeometryShader
    static IntPtr[] pixelshader = new IntPtr[6]; //IPixelShader
    static IntPtr[] depthstencil = new IntPtr[8]; //IDepthStencilState
    static IntPtr[] blend = new IntPtr[3]; //IBlendState
    static IntPtr[] rasterizer = new IntPtr[4]; //IRasterizerState
    static IntPtr[] sampler = new IntPtr[3]; //ISamplerState

    static IntPtr CreateDepthStencilState(DepthStencil id) //IDepthStencilState
    {
      DEPTH_STENCIL_DESC ds;
      ds.DepthEnable = 1;
      ds.DepthWriteMask = DEPTH_WRITE_MASK.ZERO;
      ds.DepthFunc = COMPARISON.LESS_EQUAL;
      ds.StencilEnable = 1;
      ds.StencilReadMask = 0xff;//3D11_DEFAULT_STENCIL_READ_MASK;
      ds.StencilWriteMask = 0xff;//D3D11_DEFAULT_STENCIL_WRITE_MASK;
      ds.FrontFace.StencilFunc = COMPARISON.EQUAL;
      ds.FrontFace.StencilDepthFailOp = STENCIL_OP.KEEP;
      ds.FrontFace.StencilPassOp = STENCIL_OP.KEEP;
      ds.FrontFace.StencilFailOp = STENCIL_OP.KEEP;
      ds.BackFace = ds.FrontFace;
      switch (id)
      {
        case DepthStencil.ZWrite:
          ds.DepthWriteMask = DEPTH_WRITE_MASK.ALL;
          break;
        case DepthStencil.StencilInc:
          ds.FrontFace.StencilPassOp = STENCIL_OP.INCR;
          ds.BackFace.StencilPassOp = STENCIL_OP.INCR; // Rasterizer = Rasterizer.CullNone
          break;
        case DepthStencil.StencilDec:
          ds.FrontFace.StencilPassOp = STENCIL_OP.DECR;
          ds.BackFace.StencilPassOp = STENCIL_OP.DECR; // Rasterizer = Rasterizer.CullNone
          break;
        case DepthStencil.ClearZ:
          ds.DepthWriteMask = DEPTH_WRITE_MASK.ALL;
          ds.DepthFunc = COMPARISON.ALWAYS;
          //ds.BackFace.StencilFunc = COMPARISON.LESS_EQUAL;
          //ds.BackFace.StencilPassOp = STENCIL_OP.REPLACE;
          break;
        case DepthStencil.TwoSide:
          ds.DepthFunc = COMPARISON.LESS;
          ds.FrontFace.StencilFunc = COMPARISON.ALWAYS;
          ds.BackFace.StencilFunc = COMPARISON.ALWAYS;
          ds.FrontFace.StencilDepthFailOp = STENCIL_OP.DECR;
          ds.BackFace.StencilDepthFailOp = STENCIL_OP.INCR;
          break;
        case DepthStencil.TwoSideRH:
          ds.DepthFunc = COMPARISON.LESS;
          ds.FrontFace.StencilFunc = COMPARISON.ALWAYS;
          ds.BackFace.StencilFunc = COMPARISON.ALWAYS;
          ds.FrontFace.StencilDepthFailOp = STENCIL_OP.INCR;
          ds.BackFace.StencilDepthFailOp = STENCIL_OP.DECR;
          break;
        case DepthStencil.ClearStencil:
          ds.DepthEnable = 0;
          ds.FrontFace.StencilFunc = COMPARISON.LESS;
          ds.FrontFace.StencilPassOp = STENCIL_OP.REPLACE;
          ds.BackFace = ds.FrontFace;
          break;
      }
      return device.CreateDepthStencilState(&ds);
    }
    static IntPtr CreateBlendState(BlendState id) //IBlendState
    {
      var bd = new BLEND_DESC { AlphaToCoverageEnable = 0, IndependentBlendEnable = 0 };
      bd.RenderTarget0.BlendEnable = 0;
      bd.RenderTarget0.SrcBlend = BLEND.ONE;
      bd.RenderTarget0.DestBlend = BLEND.ZERO;
      bd.RenderTarget0.BlendOp = BLEND_OP.ADD;
      bd.RenderTarget0.SrcBlendAlpha = BLEND.ONE;
      bd.RenderTarget0.DestBlendAlpha = BLEND.ZERO;
      bd.RenderTarget0.BlendOpAlpha = BLEND_OP.ADD;
      bd.RenderTarget0.RenderTargetWriteMask = COLOR_WRITE_ENABLE.ALL;
      switch (id)
      {
        case BlendState.Alpha:
          bd.RenderTarget0.BlendEnable = 1;
          bd.RenderTarget0.SrcBlend = BLEND.SRC_ALPHA;
          bd.RenderTarget0.DestBlend = BLEND.INV_SRC_ALPHA;
          bd.RenderTarget0.SrcBlendAlpha = BLEND.SRC_ALPHA;
          bd.RenderTarget0.DestBlendAlpha = BLEND.INV_SRC_ALPHA;
          break;
        case BlendState.AlphaAdd:
          bd.RenderTarget0.BlendEnable = 1;
          bd.RenderTarget0.SrcBlend = BLEND.ONE;
          bd.RenderTarget0.DestBlend = BLEND.ONE;
          break;
      }
      return device.CreateBlendState(&bd);
    }
    static IntPtr CreateRasterizerState(Rasterizer id) //IRasterizerState
    {
      RASTERIZER_DESC rd;
      rd.CullMode = id == Rasterizer.CullBack ? CULL_MODE.BACK : id == Rasterizer.CullFront ? CULL_MODE.FRONT : CULL_MODE.NONE;// NONE = 1, FRONT = 2, BACK = 3
      rd.FillMode = id == Rasterizer.Wireframe ? FILL_MODE.WIREFRAME : FILL_MODE.SOLID;
      rd.DepthClipEnable = 1;
      rd.MultisampleEnable = 1;
      //rd.FrontCounterClockwise = 0; //righthand
      //rd.DepthBias = 0;
      //rd.DepthBiasClamp = 0;
      //rd.SlopeScaledDepthBias = 0;
      //rd.ScissorEnable = 0;
      //rd.AntialiasedLineEnable = 0;
      return device.CreateRasterizerState(&rd);
    }
    static IntPtr CreateSamplerState(Sampler id) //ISamplerState
    {
      SAMPLER_DESC sd;
      sd.Filter = FILTER.MIN_MAG_MIP_LINEAR;
      sd.AddressU = TEXTURE_ADDRESS_MODE.WRAP;
      sd.AddressV = TEXTURE_ADDRESS_MODE.WRAP;
      sd.AddressW = TEXTURE_ADDRESS_MODE.WRAP;
      sd.MaxAnisotropy = 1;
      sd.ComparisonFunc = COMPARISON.ALWAYS;
      sd.MaxLOD = float.MaxValue;
      switch (id)
      {
        case Sampler.Font:
          sd.Filter = FILTER.MIN_MAG_MIP_POINT;
          sd.AddressU = TEXTURE_ADDRESS_MODE.BORDER;
          sd.AddressV = TEXTURE_ADDRESS_MODE.BORDER;
          break;
        case Sampler.Line:
          sd.AddressV = TEXTURE_ADDRESS_MODE.BORDER;
          break;
      }
      return device.CreateSamplerState(&sd);
    }
    static IntPtr CreateConstBuffer(int size) //IBuffer
    {
      BUFFER_DESC desc;
      desc.Usage = USAGE.DYNAMIC;
      desc.BindFlags = BIND.CONSTANT_BUFFER;
      desc.CPUAccessFlags = CPU_ACCESS_FLAG.WRITE;
      desc.MiscFlags = 0; desc.ByteWidth = size;
      return device.CreateBuffer(&desc);
    }
    static IntPtr CreateBuffer(void* p, int n, BIND bind) //IBuffer
    {
      BUFFER_DESC bd; SUBRESOURCE_DATA id;
      bd.Usage = USAGE.IMMUTABLE;// USAGE.DEFAULT;
      bd.ByteWidth = n;
      bd.BindFlags = bind;
      id.pSysMem = p;
      return device.CreateBuffer(&bd, &id);
    }

    static void apply()
    {
      var mask = drvmode ^ mode; drvmode = mode;
      for (int i = 0; mask != 0; i++, mask >>= 4)
      {
        if ((mask & 0xf) == 0) continue;
        var id = (mode >> (i << 2)) & 0xf;
        switch (i)
        {
          case 0: context.IASetPrimitiveTopology((PRIMITIVE_TOPOLOGY)id); continue;
          case 1:
            if (id-- == 0) { context.PSSetShader(IntPtr.Zero); continue; } //PixelShader.Null = 0, Color = 1, Texture = 2, AlphaTexture = 3, Font = 4, Color3D = 5, Mask = 6 }
            if (pixelshader[id] == IntPtr.Zero)
            {
              var s = id == 0 ? "ps_main_0" : id == 1 ? "ps_main_1" : id == 2 ? "ps_main_2" : id == 3 ? "ps_font" : id == 4 ? "ps_main_a" : "ps_main_3";
              using (var str = (UnmanagedMemoryStream)typeof(D3DView).Assembly.GetManifestResourceStream(res + s))
                pixelshader[id] = device.CreatePixelShader(str.PositionPointer, (UIntPtr)str.Length);
            }
            context.PSSetShader(pixelshader[id]);
            continue;
          case 2:
            if (depthstencil[id] == IntPtr.Zero) depthstencil[id] = CreateDepthStencilState((DepthStencil)id);
            context.OMSetDepthStencilState(depthstencil[id], stencilref);
            continue;
          case 3:
            if (blend[id] == IntPtr.Zero) blend[id] = CreateBlendState((BlendState)id);
            context.OMSetBlendState(blend[id], ref blenfactors, ~samplemask);
            continue;
          case 4:
            if (rasterizer[id] == IntPtr.Zero) rasterizer[id] = CreateRasterizerState((Rasterizer)id);
            context.RSSetState(rasterizer[id]);
            continue;
          case 5:
            if (sampler[id] == IntPtr.Zero) sampler[id] = CreateSamplerState((Sampler)id);
            context.PSSetSamplers(0, 1, ref sampler[id]);
            continue;
          case 6:
            if (id-- == 0) { context.GSSetShader(IntPtr.Zero); continue; }
            if (geoshader[id] == IntPtr.Zero)
            {
              var s = id == 0 ? "gs_shadows" : id == 1 ? "gs_outl3d" : "gs_line";// "gs_font";// ;
              using (var str = (UnmanagedMemoryStream)typeof(D3DView).Assembly.GetManifestResourceStream(res + s))
                geoshader[id] = device.CreateGeometryShader(str.PositionPointer, (UIntPtr)str.Length);
            }
            context.GSSetShader(geoshader[id]);
            continue;
          case 7:
            if (vertexshader[id] == IntPtr.Zero)
            {
              var s = id == 0 ? "vs_main_1" : id == 1 ? "vs_main" : id == 2 ? "vs_world" : "vs_main_2";
              using (var str = (UnmanagedMemoryStream)typeof(D3DView).Assembly.GetManifestResourceStream(res + s))
              {
                vertexshader[id] = device.CreateVertexShader(str.PositionPointer, (UIntPtr)str.Length);
                if (vertexlayout == IntPtr.Zero)
                {
                  var layout = new INPUT_ELEMENT_DESC[]
                  {
                      new INPUT_ELEMENT_DESC { SemanticName = "POSITION", Format = FORMAT.R32G32B32_FLOAT, AlignedByteOffset = 0, InputSlotClass =  INPUT_CLASSIFICATION.PER_VERTEX_DATA },
                      new INPUT_ELEMENT_DESC { SemanticName = "NORMAL", Format = FORMAT.R32G32B32_FLOAT, AlignedByteOffset = 12, InputSlotClass =  INPUT_CLASSIFICATION.PER_VERTEX_DATA },
                      new INPUT_ELEMENT_DESC { SemanticName = "TEXCOORD", Format = FORMAT.R32G32_FLOAT, AlignedByteOffset = 24, InputSlotClass =  INPUT_CLASSIFICATION.PER_VERTEX_DATA },
                  };
                  context.IASetInputLayout(vertexlayout = device.CreateInputLayout(layout, layout.Length, str.PositionPointer, (UIntPtr)str.Length));
                }
              }
            }
            context.VSSetShader(vertexshader[id]);
            continue;
        }
      }

      if (texture != null) SetTexture(texture.srv);

      if (cbsok == (1 | 2 | 4)) return;
      if ((cbsok & 1) == 0 && cmpcpy(&cb2[1], &cb2[0], sizeof(cbPerFrame)))
      {
        *(cbPerFrame*)context.Map(cbperframe, 0, MAP.WRITE_DISCARD, 0).pData = cb2[0];
        context.Unmap(cbperframe, 0);
      }
      if ((cbsok & 2) == 0 && cmpcpy(&cb1[1], &cb1[0], sizeof(cbPerObject)))
      {
        *(cbPerObject*)context.Map(cbperobject, 0, MAP.WRITE_DISCARD, 0).pData = cb1[0];
        context.Unmap(cbperobject, 0);
      }
      if ((cbsok & 4) == 0 && cmpcpy(&cb3[1], &cb3[0], sizeof(cbPsPerObject)))
      {
        *(cbPsPerObject*)context.Map(cbpsperobject, 0, MAP.WRITE_DISCARD, 0).pData = cb3[0];
        context.Unmap(cbpsperobject, 0);
      }
      cbsok = (1 | 2 | 4);
    }

    static IntPtr ringbuffer, currentvb, currentib; //IBuffer
    static int rbindex, rbcount;
    static void rballoc(int nv)
    {
      release(ref ringbuffer);
      BUFFER_DESC bd;
      bd.Usage = USAGE.DYNAMIC;
      bd.ByteWidth = (rbcount = (((nv >> 11) + 1) << 11)) << 5; //64kb 2kv
      bd.BindFlags = BIND.VERTEX_BUFFER;
      bd.CPUAccessFlags = CPU_ACCESS_FLAG.WRITE;
      ringbuffer = device.CreateBuffer(&bd);
    }

    static void SetVertexBuffer(IntPtr vb) //IBuffer
    {
      if (currentvb == vb) return;
      int stride = 32, offs = 0; context.IASetVertexBuffers(0, 1, currentvb = vb, &stride, &offs);
    }
    static void SetIndexBuffer(IntPtr ib) //IBuffer
    {
      if (currentib == ib) return;
      context.IASetIndexBuffer(currentib = ib, FORMAT.R16_UINT, 0);
    }
    static void SetTexture(IntPtr srv) //IShaderResourceView
    {
      if (currentsrv == srv) return;
      context.PSSetShaderResources(0, 1, currentsrv = srv);
    }

    void IDisplay.Clear(CLEAR fl)
    {
      if (inpick && (fl & CLEAR.DEPTH) != 0) testpick();
      context.ClearDepthStencilView(currentdsv, fl, 1, 0);
    }
    float2 IDisplay.Viewport
    {
      get { return *(float2*)&currentvp->Width / DpiScale; }
    }
    float4x4 IDisplay.Projection
    {
      get { return cb2->ViewProjection; }
      //set
      //{
      //  cb2->ViewProjection = value; cbsok &= ~1; pixelscale = 0;
      //  if (inpick2 && pickid == -1 && id2 == -1) { pickid = -2; pickplane = cb2->ViewProjection; } //projection on background
      //}
    }
    void IDisplay.SetProjection(in float4x4 m)
    {
      cb2->ViewProjection = m; cbsok &= ~1; pixelscale = 0;
      if (inpick2 && pickid == -1 && id2 == -1) { pickid = -2; pickplane = cb2->ViewProjection; } //projection on background
    }
    float IDisplay.PixelScale
    {
      get
      {
        if (pixelscale == 0)
        {
          var p = &cb2->ViewProjection;
          pixelscale = (float)Math.Sqrt(p->_11 * p->_11 + p->_21 * p->_21 + p->_31 * p->_31) * currentvp->Width * 0.5f;
        }
        return pixelscale;
      }
    }
    float3 IDisplay.Light
    {
      get { return *(float3*)&cb2->LightDir; }
      set { *(float3*)&cb2->LightDir = value; cbsok &= ~1; }
    }
    float IDisplay.LightZero
    {
      get { return cb2->LightDir.w; }
      set { cb2->LightDir.w = value; cbsok &= ~1; }
    }
    uint IDisplay.Ambient
    {
      get { return (uint)cb2->Ambient; }
      set { cb2->Ambient = (float4)value; cbsok &= ~1; }
    }
    float3x4 IDisplay.Transform
    {
      get { return (float3x4)cb1->World; }
      //set { cb1->World = value; cbsok &= ~2; }
    }
    void IDisplay.SetTransform(in float3x4 m)
    {
      cb1->World = m; cbsok &= ~2;
    }
    Font IDisplay.Font
    {
      get { return font; }
      set { font = value; }
    }
    Texture IDisplay.Texture
    {
      get { return texture; }
      set { texture = value; }
    }
    uint IDisplay.Color
    {
      get { return (uint)cb3->Diffuse; }
      set { cb3->Diffuse = (float4)value; cbsok &= ~4; }
    }
    float IDisplay.Alpha //todo: modulate
    {
      get { return cb3->Diffuse.w; }
      set
      {
        //_cbPsPerObject->Diffuse.w = value;
        //BlendState = value < 1 ? BlendState.Alpha : BlendState.Default;
      }
    }
    int IDisplay.State
    {
      get { return mode; }
      set
      {
        if (((value ^ mode) & 0x00000f00) != 0)
          switch ((DepthStencil)((mode & 0x00000f00) >> 8))//dc.DepthStencil)
          {
            case DepthStencil.StencilInc: stencilref++; drvmode |= 0x00000f00; break;
            case DepthStencil.StencilDec: stencilref--; drvmode |= 0x00000f00; break;
          }
        mode = value;
      }
    }
    Topology IDisplay.Topology
    {
      get { return (Topology)(mode & 0x0000000f); }
      set { mode = (mode & ~0x0000000f) | (int)value; }
    }
    PixelShader IDisplay.PixelShader
    {
      get { return (PixelShader)((mode & 0x000000f0) >> 4); }
      set { mode = (mode & ~0x000000f0) | ((int)value << 4); }
    }
    GeometryShader IDisplay.GeometryShader
    {
      get { return (GeometryShader)((mode & 0x0f000000) >> 24); }
      set { mode = (mode & ~0x0f000000) | ((int)value << 24); }
    }
    VertexShader IDisplay.VertexShader
    {
      get { return (VertexShader)((mode & 0x70000000) >> 28); }
      set { mode = (mode & ~0x70000000) | ((int)value << 28); }
    }
    DepthStencil IDisplay.DepthStencil
    {
      get { return (DepthStencil)((mode & 0x00000f00) >> 8); }
      set { mode = (mode & ~0x00000f00) | ((int)value << 8); }
    }
    BlendState IDisplay.BlendState
    {
      get { return (BlendState)((mode & 0x0000f000) >> 12); }
      set { mode = (mode & ~0x0000f000) | ((int)value << 12); }
    }
    float4 BlendFactors
    {
      get { return blenfactors; }
      set { if (!blenfactors.Equals(value)) { blenfactors = value; drvmode |= 0x0000f000; } }
    }
    uint BlendMask
    {
      get { return samplemask; }
      set { if (samplemask != value) { samplemask = value; drvmode |= 0x0000f000; } }
    }
    Rasterizer IDisplay.Rasterizer
    {
      get { return (Rasterizer)((mode & 0x000f0000) >> 16); }
      set { mode = (mode & ~0x000f0000) | ((int)value << 16); }
    }
    Sampler IDisplay.Sampler
    {
      get { return (Sampler)((mode & 0x00f00000) >> 20); }
      set { mode = (mode & ~0x00f00000) | ((int)value << 20); }
    }
    bool IDisplay.IsPicking { get { return inpick; } }

    void IDisplay.Operator(int code, void* p)
    {
      for (; code != 0; code >>= 4)
        switch (code & 0xf)
        {
          case 0x1: *(float4x4*)StackPtr = cb2->ViewProjection; StackPtr += sizeof(float4x4); continue; //push vp
          case 0x2: *(float4x4*)StackPtr = cb1->World; StackPtr += sizeof(float4x4); continue; //push wm
          case 0x3: StackPtr -= sizeof(float4x4); cb2->ViewProjection = *(float4x4*)StackPtr; cbsok &= ~1; pixelscale = 0; continue; //pop vp
          case 0x4: StackPtr -= sizeof(float4x4); cb1->World = *(float4x4*)StackPtr; cbsok &= ~2; continue; //pop wm
          case 0x5: *(float4x4*)p = cb1->World * cb2->ViewProjection; continue; //wm * vp
          case 0x6: //p = vm
          case 0x7: //p = p * vm
            {
              var t = (float4x4*)((code & 0xf) == 6 ? p : StackPtr); *t = new float4x4();
              t->_41 = +(t->_11 = currentvp->Width / DpiScale * +0.5f);
              t->_42 = -(t->_22 = currentvp->Height / DpiScale * -0.5f);
              t->_33 = t->_44 = 1; if (p != t) *(float4x4*)p = *(float4x4*)p * *t;
            }
            continue;
          case 0x8: //wm = p * wm
            cb1->World = *(float3x4*)p * (float3x4)cb1->World; cbsok &= ~2;
            continue;
          case 0x9: //conv float2
            { var t = (float3x4*)StackPtr + 2; *t = 1; *(float2*)&t->_41 = *(float2*)p; p = t; }
            continue;
        }
    }
    vertex* IDisplay.BeginVertices(int nv)
    {
      if (rbindex + nv > rbcount) { if (nv > rbcount) rballoc(nv); rbindex = 0; }
      var map = context.Map(ringbuffer, 0, rbindex != 0 ? MAP.WRITE_NO_OVERWRITE : MAP.WRITE_DISCARD, 0);
      var vv = (vertex*)map.pData + rbindex; memset(vv, 0, (void*)(nv << 5)); return vv;
    }
    void IDisplay.EndVertices(int nv, Topology topo)
    {
      context.Unmap(ringbuffer, 0);
      if (inpick) { pick(ringbuffer, IntPtr.Zero, nv, ref rbindex, topo); return; }
      if (topo != 0)
      {
        if (dc.PixelShader == PixelShader.Color) dc.BlendState = cb3->Diffuse.w < 1 ? BlendState.Alpha : BlendState.Opaque;
        dc.Topology = topo; apply();
        SetVertexBuffer(ringbuffer);
      }
      context.Draw(nv, rbindex); rbindex += nv;
    }
    void IDisplay.DrawMesh(VertexBuffer vertices, IndexBuffer indices, int i, int n)
    {
      int nv = n != 0 ? n << 1 : indices.count << 1; i <<= 1;
      if (inpick) { pick(vertices.buffer, indices.buffer, nv, ref i, 0); return; }
      SetVertexBuffer(vertices.buffer); SetIndexBuffer(indices.buffer); apply();
      context.DrawIndexed(nv, i, 0);
    }
    void IDisplay.Select(object data, int id)
    {
      D3DView.data = data; D3DView.id = id;
    }
    Action<int, object> IDisplay.Tool { get { return this.tool; } }
    #endregion

    #region Pick
    static IntPtr rtvtex1, dsvtex1, rtvcpu1, dsvcpu1; //ITexture2D
    static IntPtr rtv1, dsv1; //IRenderTargetView rtv1; IDepthStencilView dsv1;
    static bool inpick, inpick2;
    static object data; static int id, id1, id2, prim;

    System.Drawing.Point point;
    object pickdata, pickview; int pickid, pickprim, pickz;
    float4x4 pickplane; float3x4 picktrans, viewtrans; float3 pickp;

    static void initpixel()
    {
      TEXTURE2D_DESC td;
      td.Width = td.Height = td.ArraySize = td.MipLevels = td.SampleDesc.Count = 1;
      td.BindFlags = BIND.RENDER_TARGET | BIND.SHADER_RESOURCE;
      td.Format = FORMAT.B8G8R8A8_UNORM; //td.CPUAccessFlags = CPU_ACCESS_FLAG.READ;
      rtvtex1 = device.CreateTexture2D(&td);

      RENDER_TARGET_VIEW_DESC rdesc;
      rdesc.Format = td.Format;
      rdesc.ViewDimension = RTV_DIMENSION.TEXTURE2D;
      rtv1 = device.CreateRenderTargetView(rtvtex1, &rdesc);

      td.BindFlags = BIND.DEPTH_STENCIL;
      td.Format = FORMAT.D24_UNORM_S8_UINT;
      dsvtex1 = device.CreateTexture2D(&td);

      DEPTH_STENCIL_VIEW_DESC ddesc;
      ddesc.Format = td.Format;
      ddesc.ViewDimension = DSV_DIMENSION.TEXTURE2D;
      dsv1 = device.CreateDepthStencilView(dsvtex1, &ddesc);

      td.BindFlags = 0;
      td.CPUAccessFlags = CPU_ACCESS_FLAG.READ | CPU_ACCESS_FLAG.WRITE;
      td.Usage = USAGE.STAGING;
      td.Format = FORMAT.B8G8R8A8_UNORM;
      rtvcpu1 = device.CreateTexture2D(&td, null);

      td.Format = FORMAT.D24_UNORM_S8_UINT;
      dsvcpu1 = device.CreateTexture2D(&td, null);
    }
    void pick(IntPtr vb, IntPtr ib, int nv, ref int sv, Topology topo)
    {
      if (data == null) return; id1++;
      if (inpick2)
      {
        if (id2 < 0) switch (dc.DepthStencil) { case DepthStencil.StencilInc: id2--; break; case DepthStencil.StencilDec: if (++id2 == 0) { pickview = data; viewtrans = ((IDisplay)this).Transform; } break; }
        if (topo != 0) prim = id1; if (id1 == id2) capture(); return;
      }
      if (topo != 0) dc.Topology = topo;
      var t1 = mode; var t2 = dc.Color;
      if (dc.PixelShader == PixelShader.AlphaTexture) dc.PixelShader = PixelShader.Mask;
      else if (dc.PixelShader != PixelShader.Null) dc.PixelShader = PixelShader.Color;
      else if (dc.DepthStencil == DepthStencil.ClearZ) testpick(); //{ testpick(); float4 v; context.ClearRenderTargetView(rtv1, &v); }
      dc.BlendState = BlendState.Opaque; dc.Color = unchecked((uint)id1);
      SetVertexBuffer(vb); apply();
      if (ib == IntPtr.Zero) { context.Draw(nv, sv); sv += nv; }
      else { SetIndexBuffer(ib); context.DrawIndexed(nv, sv, 0); }
      mode = t1; dc.Color = t2;
    }
    void testpick()
    {
      context.CopyResource(rtvcpu1, rtvtex1);
      var pc = *(int*)context.Map(rtvcpu1, 0, MAP.READ, 0).pData; context.Unmap(rtvcpu1, 0);
      if (pc <= id2) return; id2 = pc;
      context.CopyResource(dsvcpu1, dsvtex1); //float4 v; context.ClearRenderTargetView(rtv1, &v);
      pickz = *(int*)context.Map(dsvcpu1, 0, MAP.READ, 0).pData; context.Unmap(dsvcpu1, 0);
    }
    void capture()
    {
      pickdata = data; pickid = id; pickprim = id1 - prim;
      pickplane = cb2->ViewProjection; picktrans = dc.Transform; id2 = -1;
    }

    void Pick(System.Drawing.Point p)
    {
      if (swapchain == null) return;
      if (rtv1 == IntPtr.Zero) initpixel(); point = p;
      var vp = viewport; vp.TopLeftX = -point.X; vp.TopLeftY = -point.Y;
      Begin(rtv1, dsv1, vp, 0); pickplane = cb2->ViewProjection; picktrans = 1;
      inpick = true; pickdata = pickview = null; id1 = id2 = pickid = pickprim = 0;
      OnRender(dc); testpick();
      pickp.x = +((point.X * 2) / viewport.Width - 1);
      pickp.y = -((point.Y * 2) / viewport.Height - 1);
      pickp.z = (pickz & 0xffffff) * (1.0f / 0xffffff);
      inpick2 = true; id1 = 0; OnRender(dc);
      inpick = inpick2 = false;
    }

    float4x4 ISelector.Plane
    {
      get { return pickplane; }
      set { pickplane = value; }
    }
    float3x4 ISelector.Transform
    {
      get { return picktrans; }
    }
    object ISelector.View
    {
      get { return pickview; }
    }
    object ISelector.Hover
    {
      get { return pickdata; }
      set
      {
        if (value == pickview)
        {
          pickdata = value;
          float4x4 m; ((IDisplay)this).Operator(0x06, &m); m = !m;// inv(&m, &m);
          pickplane = viewtrans * m; picktrans = 1;
        }
      }
    }
    int ISelector.Id
    {
      get { return pickid; }
      set { pickid = value; }
    }
    int ISelector.Primitive
    {
      get { return pickprim; }
    }
    float3 ISelector.Point
    {
      get { return pickp * !(picktrans * pickplane); }
    }
    void ISelector.SetPlane(in float3x4 m)
    {
      pickplane = m * pickplane;
    }

    float2 ISelector.Pick()
    {
      float2 p;
      p.x = +((point.X * 2) / currentvp->Width - 1);
      p.y = -((point.Y * 2) / currentvp->Height - 1);
      var m = pickplane;
      var a1 = p.x * m._14 - m._11;
      var b1 = p.y * m._14 - m._12;
      var a2 = p.x * m._24 - m._21;
      var b2 = p.y * m._24 - m._22;
      var de = 1 / (a1 * b2 - a2 * b1);
      var c1 = m._41 - p.x * m._44;
      var c2 = m._42 - p.y * m._44;
      p.x = (c1 * b2 - a2 * c2) * de;
      p.y = (a1 * c2 - c1 * b1) * de;
      return p;
    }
    void ISelector.SetTool(Action<int, object> tool) { this.tool = tool; }
    #endregion

    #region Drop
    Action<int, object> droptool;
    protected override void OnDragEnter(DragEventArgs e)
    {
      object view = this; tool = null;
      droptool = (id, p) =>
      {
        if (id == 0)
        {
          var plane = pickplane; Pick(point);
          if (view == pickview || view == pickdata) { if (tool != null) { pickplane = plane; tool(0, p); } return; }
          if (tool != null) { tool(2, p); tool = null; }
          view = pickview; var t = Tag; Tag = e.Data;
          try { OnDispatch(0x0233, this); } //WM_DROPFILES
          catch (Exception ex) { Debug.WriteLine(ex.Message); }
          Tag = t; return;
        }
        if (tool != null) { tool(id, p); tool = null; }
      };
      point.X = -1; OnDragOver(e);
    }
    protected override void OnDragOver(DragEventArgs e)
    {
      var p = PointToClient(Cursor.Position);
      if (p != point) { point = p; droptool(0, this); Refresh(); }
      e.Effect = tool != null ? DragDropEffects.Copy : DragDropEffects.None;
    }
    protected override void OnDragDrop(DragEventArgs e)
    {
      e.Effect = tool != null ? DragDropEffects.Copy : DragDropEffects.None;
      droptool(1, this); droptool = null;
    }
    protected override void OnDragLeave(EventArgs e)
    {
      droptool(2, this); droptool = null;
    }
    #endregion

    #region Native

    static void release(ref IntPtr p) { if (p != IntPtr.Zero) { var c = Marshal.Release(p); p = IntPtr.Zero; } }
    static void release(ref IntPtr[] a) { if (a != null) { for (int i = 0; i < a.Length; i++) release(ref a[i]); } }

    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* VirtualAlloc(void* p, void* size, int type, int protect);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool VirtualFree(void* p, void* size, int type);

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    public static extern void* memset(void* p, int v, void* n);
    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    public static extern int memcmp(void* a, void* b, void* n);
    [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    public static extern void* memcpy(void* d, void* s, void* n);
    static void memcpy(byte[] d, void* s) { fixed (byte* p = d) memcpy(p, s, (void*)d.Length); }
    static void memcpy(void* d, byte[] s) { fixed (byte* p = s) memcpy(d, p, (void*)s.Length); }

    //[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
    //static extern double wcstod(char* s, char** e);

    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool ValidateRect(IntPtr wnd, RECT* p);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool QueryPerformanceCounter(long* p);
    [DllImport("Kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool QueryPerformanceFrequency(long* p);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* SetTimer(IntPtr wnd, void* id, int dt, void* func);

    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* LoadCursor(void* hinst, void* s);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* SetCursor(void* p);

    [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
    static extern void* CreateFileW(char* path, uint access, FileShare share, void* security, FileMode mode, int attris, void* pt);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int GetFileSize(void* h, int* ph);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int SetFilePointer(void* h, int lo, int* hi, int flags);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int ReadFile(void* h, void* p, int n, int* pn, void* ov);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int WriteFile(void* h, void* p, int n, int* pn, void* ov);
    [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool CloseHandle(void* h);

    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* GetDC(void* wnd);
    [DllImport("user32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int ReleaseDC(void* wnd, void* dc);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int GetDeviceCaps(void* dc, int i);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* SelectObject(void* dc, void* p);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool DeleteObject(void* p);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* CreateFontW(int height, int width, int esc, int ori, int weight, int italic, int underline, int strike, int charset, int prec, int clip, int quality, int pitch, char* name);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int GetTextMetrics(void* dc, void* p);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int GetKerningPairsW(void* dc, int n, void* p);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int GetGlyphOutlineW(void* dc, int c, uint f, void* gm, int np, void* pp, void* mat);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool TextOutW(void* dc, int x, int y, char* s, int n);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern uint SetTextColor(void* dc, uint color);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int SetTextAlign(void* dc, int v);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern int SetBkMode(void* dc, int v);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* CreateCompatibleDC(void* dc);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern bool DeleteDC(void* dc);
    [DllImport("gdi32.dll"), SuppressUnmanagedCodeSecurity]
    static extern void* CreateDIBSection(void* dc, void* pbi, int usage, void* pp, void* sec, int offs);
    #endregion
  }

  unsafe partial class D3DView
  {
    public interface IDisplay
    {
      float2 Viewport { get; }
      float4x4 Projection { get; /*set;*/ }
      float3x4 Transform { get; /*set;*/ }
      void SetProjection(in float4x4 v);
      void SetTransform(in float3x4 v);
      float3 Light { get; set; }
      float LightZero { get; set; }
      uint Ambient { get; set; }
      uint Color { get; set; }
      float Alpha { get; set; }
      float PixelScale { get; }
      Font Font { get; set; }
      Texture Texture { get; set; }
      int State { get; set; }
      Topology Topology { get; set; }
      VertexShader VertexShader { get; set; }
      GeometryShader GeometryShader { get; set; }
      PixelShader PixelShader { get; set; }
      BlendState BlendState { get; set; }
      Rasterizer Rasterizer { get; set; }
      Sampler Sampler { get; set; }
      DepthStencil DepthStencil { get; set; }
      void Operator(int code, void* p); //finally the only way for high performance
      void Clear(CLEAR fl);
      vertex* BeginVertices(int nv);
      void EndVertices(int nv, Topology topo);
      void DrawMesh(VertexBuffer vertices, IndexBuffer indices, int i = 0, int n = 0);
      bool IsPicking { get; }
      void Select(object data = null, int id = 0);
      Action<int, object> Tool { get; }
    }

    public interface ISelector
    {
      float4x4 Plane { get; set; }
      float3x4 Transform { get; }
      object View { get; }
      object Hover { get; set; }
      object Tag { get; set; }
      int Id { get; set; }
      int Primitive { get; }
      float3 Point { get; }
      void SetTool(Action<int, object> tool);
      void SetPlane(in float3x4 m);
      float2 Pick();
      void Invalidate();
    }

    public enum Topology { Points = 1, LineList = 2, LineStrip = 3, TriangleList = 4, TriangleStrip = 5, LineListAdj = 10, LineStripAdj = 11, TriangleListAdj = 12, TriangleStripAdj = 13, }
    public enum VertexShader { Default = 0, Lighting = 1, World = 2, Tex = 3 }
    public enum GeometryShader { Null = 0, Shadows = 1, Outline3D = 2, Line = 3 }
    public enum PixelShader { Null = 0, Color = 1, Texture = 2, AlphaTexture = 3, Font = 4, Color3D = 5, Mask = 6 }
    public enum BlendState { Opaque = 0, Alpha = 1, AlphaAdd = 2 }
    public enum Rasterizer { CullNone = 0, CullFront = 1, CullBack = 2, Wireframe = 3 }
    public enum Sampler { Default = 0, Font = 1, Line = 2 }
    public enum DepthStencil { Default = 0, ZWrite = 1, StencilInc = 2, StencilDec = 3, ClearZ = 4, TwoSide = 5, TwoSideRH = 6, ClearStencil = 7 }
    public enum SysCursor { ARROW = 32512, IBEAM = 32513, WAIT = 32514, Cross = 32515, UpArrow = 32516, SIZE = 32640, ICON = 32641, SIZENWSE = 32642, SIZENESW = 32643, SIZEWE = 32644, SIZENS = 32645, SIZEALL = 32646, NO = 32648, HAND = 32649, APPSTARTING = 32650, HELP = 32651 }
    public enum Winding { EvenOdd, NonZero, Positive, Negative, AbsGeqTwo }

    public interface IArchive
    {
      uint Version { get; set; }
      bool IsStoring { get; }
      bool Compress { get; set; }
      void Serialize<T>(ref T value);
    }
    public interface IExchange
    {
      bool Group(string name);
      bool Exchange<T>(string name, ref T value, object fmt = null, int flags = 0); //1:readonly, 2:noundo, 4:bold, 8:edit
    }

    [Conditional("DEBUG")]
    public static void Assert(bool ok) { if (!ok) throw new Exception(); }
  }

  unsafe partial class D3DView
  {
    public static float dot(float2 a, float2 b) => a & b;
    public static float ccw(float2 a, float2 b) => a ^ b;
    public static float dot(float2 a)
    {
      return a.x * a.x + a.y * a.y;
    }
    public static float length(float2 v)
    {
      return (float)Math.Sqrt(v.x * v.x + v.y * v.y);
    }
    public static float dist(float2 a, float2 b)
    {
      return (float)Math.Sqrt((a.x -= b.x) * a.x + (a.y -= b.y) * a.y);
    }
    public static double atan2(float2 v)
    {
      return Math.Atan2(v.y, v.x);
    }
    public static float2 normalize(float2 v)
    {
      var l = Math.Sqrt(v.x * v.x + v.y * v.y); if (l != 0) { v.x = (float)(v.x / l); v.y = (float)(v.y / l); }
      return v;
    }
    public static float2 min(float2 a, float2 b)
    {
      if (a.x > b.x) a.x = b.x;
      if (a.y > b.y) a.y = b.y; return a;
    }
    public static float2 max(float2 a, float2 b)
    {
      if (a.x < b.x) a.x = b.x;
      if (a.y < b.y) a.y = b.y; return a;
    }
    public static float2 sincos(float a)
    {
      float2 v; v.x = (float)Math.Sin(a); v.y = (float)Math.Cos(a); return v;
    }
    public static void inline(float2 a, float2 b, ref float2 p)
    {
      var v = normalize(b - a); p = a + v * ccw(p - a, ~v);
    }

    public static float dot(float3 v)
    {
      return v.x * v.x + v.y * v.y + v.z * v.z;
    }
    public static float length(float3 v)
    {
      return (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    }
    public static float dot(float3 a, float3 b) => a & b;
    public static float3 ccw(float3 a, float3 b) => a ^ b;
    public static float3 normalize(float3 v)
    {
      var l = 1 / Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
      v.x = (float)(v.x * l);
      v.y = (float)(v.y * l);
      v.z = (float)(v.z * l);
      return v;
    }
    public static float3 min(float3 a, float3 b)
    {
      if (a.x > b.x) a.x = b.x;
      if (a.y > b.y) a.y = b.y;
      if (a.z > b.z) a.z = b.z;
      return a;
    }
    public static float3 max(float3 a, float3 b)
    {
      if (a.x < b.x) a.x = b.x;
      if (a.y < b.y) a.y = b.y;
      if (a.z < b.z) a.z = b.z; return a;
    }

    public static double length(double3* v)
    {
      return Math.Sqrt(dot(v));
    }
    public static double dot(double3* v)
    {
      return v->x * v->x + v->y * v->y + v->z * v->z;
    }
    public static double dot(double3* a, double3* b)
    {
      return a->x * b->x + a->y * b->y + a->z * b->z;
    }
    public static double distsq(double3* a, double3* b)
    {
      double x = a->x - b->x, y = a->y - b->y, z = a->z - b->z;
      return x * x + y * y + z * z;
    }
    public static void ccw(double3* a, double3* b, double3* r)
    {
      r->x = a->y * b->z - a->z * b->y;
      r->y = a->z * b->x - a->x * b->z;
      r->z = a->x * b->y - a->y * b->x;
    }
    public static void normalize(double3* v)
    {
      var l = length(v); v->x /= l; v->y /= l; v->z /= l;
    }
    public static void cross(double3* a, double3* b, double3* c, double3* r)
    {
      double3 u; u.x = b->x - a->x; u.y = b->y - a->y; u.z = b->z - a->z;
      double3 v; v.x = c->x - a->x; v.y = c->y - a->y; v.z = c->z - a->z; ccw(&u, &v, r);
    }
    public static double3 cross(double3* a, double3* b, double3* c)
    {
      double3 v; cross(a, b, c, &v); return v;
    }
    public static double dot(double3* a, double3* b, double3* c)
    {
      double3 v; cross(a, b, c, &v); return dot(&v);
    }
    public static double inline(double3* a, double3* b, double3* c)
    {
      double3 u; u.x = b->x - a->x; u.y = b->y - a->y; u.z = b->z - a->z; var s = dot(&u); if (s == 0) return 0;
      double3 v; v.x = c->x - b->x; v.y = c->y - b->y; v.z = c->z - b->z; var t = dot(&v); if (t == 0) return 0;
      var d = u.x * v.x + u.y * v.y + u.z * v.z; if (d == 0) return 1;
      var w = 1 - Math.Abs(d) / (Math.Sqrt(s) * Math.Sqrt(t)); return Math.Abs(w);
    }

    public static double4 plane(double3 p, double3 n) //todo: * symmetry
    {
      double4 e; *(double3*)&e = n; e.w = -(p.x * n.x + p.y * n.y + p.z * n.z);
      var l = length((double3*)&e); e.x /= l; e.y /= l; e.z /= l; e.w /= l; return e;
    }
    public static bool plane(double3* a, double3* b, double3* c, double4* e)
    {
      cross(a, b, c, (double3*)e); var l = length((double3*)e); if (l == 0) return false;
      e->x /= l; e->y /= l; e->z /= l; e->w = -(a->x * e->x + a->y * e->y + a->z * e->z); return true;
    }
    public static double distsq(double4* a, double4* b)
    {
      double x = a->x - b->x, y = a->y - b->y, z = a->z - b->z, w = a->w - b->w;
      return x * x + y * y + z * z + w * w;
    }
    public static double dotcoord(double4* e, double3* p)
    {
      return e->x * p->x + e->y * p->y + e->z * p->z + e->w;
    }
    public static void intersect(double4* e, double3* a, double3* b, double3* s)
    {
      var u = e->x * a->x + e->y * a->y + e->z * a->z;
      var v = e->x * b->x + e->y * b->y + e->z * b->z;
      var w = (u + e->w) / (u - v); //if (w < 1e-10) { } if (w > 1 - 1e-10) { }
      s->x = a->x + (b->x - a->x) * w;
      s->y = a->y + (b->y - a->y) * w;
      s->z = a->z + (b->z - a->z) * w;
    }

    public static float determiante(float3x4* m)
    {
      return m->_11 * (m->_22 * m->_33 - m->_23 * m->_32) -
             m->_12 * (m->_21 * m->_33 - m->_23 * m->_31) +
             m->_13 * (m->_21 * m->_32 - m->_22 * m->_31);
    }

    public static void mul(float3* p, float3x4* m, float2* r)
    {
      r->x = m->_11 * p->x + m->_21 * p->y + m->_31 * p->z + m->_41;
      r->y = m->_12 * p->x + m->_22 * p->y + m->_32 * p->z + m->_42;
    }

    public static float3x4 move(float x, float y, float z)
    {
      float3x4 m; (&m)->_11 = m._22 = m._33 = 1; m._41 = x; m._42 = y; m._43 = z; return m;
    }
    public static float3x4 scale(float x, float y, float z)
    {
      float3x4 m; (&m)->_11 = x; m._22 = y; m._33 = z; return m;
    }
    public static void rot(float3x4* m, int x, double a)
    {
      var s = (float)Math.Round(Math.Sin(a), 15);
      var c = (float)Math.Round(Math.Cos(a), 15);
      if (x == 0) { m->_11 = 1; m->_22 = m->_33 = c; m->_32 = -(m->_23 = s); return; }
      if (x == 1) { m->_22 = 1; m->_11 = m->_33 = c; m->_13 = -(m->_31 = s); return; }
      if (x == 2) { m->_33 = 1; m->_11 = m->_22 = c; m->_21 = -(m->_12 = s); return; }
    }
    public static float3x4 rotx(double a)
    {
      float3x4 m; rot(&m, 0, a); return m;
    }
    public static float3x4 roty(double a)
    {
      float3x4 m; rot(&m, 1, a); return m;
    }
    public static float3x4 rotz(double a)
    {
      float3x4 m; rot(&m, 2, a); return m;
    }
    public static float3x4 rotaxis(float3 v, float a)
    {
      var m = new float3x4(); float s = (float)Math.Sin(a), c = (float)Math.Cos(a), cc = 1 - c;
      m._11 = cc * v.x * v.x + c;
      m._21 = cc * v.x * v.y - s * v.z;
      m._31 = cc * v.x * v.z + s * v.y;
      m._12 = cc * v.y * v.x + s * v.z;
      m._22 = cc * v.y * v.y + c;
      m._32 = cc * v.y * v.z - s * v.x;
      m._13 = cc * v.z * v.x - s * v.y;
      m._23 = cc * v.z * v.y + s * v.x;
      m._33 = cc * v.z * v.z + c;
      return m;
    }

    public static float euler(float3x4* m, double3* e)
    {
      var s = length(*(float3*)&m->_11); var t = m->_13 / s;
      e->x = +Math.Atan2(m->_23, m->_33);
      e->y = -Math.Asin(t);
      e->z = t == 1 || t == -1 ? -Math.Atan2(m->_21, m->_22) : Math.Atan2(m->_12, m->_11); return s;
    }
    public static void euler(double3* e, float3x4* m)
    {
      float3x4 x; rot(&x, 0, e->x);
      float3x4 y; rot(&y, 1, e->y); x = x * y;
      float3x4 z; rot(&z, 2, e->z); x = x * z;
      memcpy(m, &x, (void*)(9 * sizeof(float)));
    }
    public static void euler(float4* q, double3* e)
    {
      e->x = Math.Atan2(2.0 * (q->y * q->z + q->w * q->x), q->w * q->w - q->x * q->x - q->y * q->y + q->z * q->z);
      e->y = Math.Asin(-2.0 * (q->x * q->z - q->w * q->y));
      e->z = Math.Atan2(2.0 * (q->x * q->y + q->w * q->z), q->w * q->w + q->x * q->x - q->y * q->y - q->z * q->z);
    }
    public static void euler(float3* e, float4* q)
    {
      var sx = Math.Sin(e->x * 0.5f); var cx = Math.Cos(e->x * 0.5f);
      var sy = Math.Sin(e->y * 0.5f); var cy = Math.Cos(e->y * 0.5f);
      var sz = Math.Sin(e->z * 0.5f); var cz = Math.Cos(e->z * 0.5f);
      q->x = (float)(cz * cy * sx - sz * sy * cx);
      q->y = (float)(cz * sy * cx + sz * cy * sx);
      q->z = (float)(sz * cy * cx - cz * sy * sx);
      q->w = (float)(cz * cy * cx + sz * sy * sx);
    }

    public static void quaternion(float3x4* m, float4* q)
    {
      var scale = m->_11 + m->_22 + m->_33;
      if (scale > 0.0f)
      {
        var s = (float)Math.Sqrt(scale + 1.0f);
        q->w = s * 0.5f; s = 0.5f / s;
        q->x = (m->_23 - m->_32) * s;
        q->y = (m->_31 - m->_13) * s;
        q->z = (m->_12 - m->_21) * s;
      }
      else if (m->_11 >= m->_22 && m->_11 >= m->_33)
      {
        var s = (float)Math.Sqrt(1.0f + m->_11 - m->_22 - m->_33);
        var h = 0.5f / s;
        q->x = 0.5f * s;
        q->y = (m->_12 + m->_21) * h;
        q->z = (m->_13 + m->_31) * h;
        q->w = (m->_23 - m->_32) * h;
      }
      else if (m->_22 > m->_33)
      {
        var s = (float)Math.Sqrt(1.0f + m->_22 - m->_11 - m->_33);
        var h = 0.5f / s;
        q->x = (m->_21 + m->_12) * h;
        q->y = 0.5f * s;
        q->z = (m->_32 + m->_23) * h;
        q->w = (m->_31 - m->_13) * h;
      }
      else
      {
        var s = (float)Math.Sqrt(1.0f + m->_33 - m->_11 - m->_22);
        var h = 0.5f / s;
        q->x = (m->_31 + m->_13) * h;
        q->y = (m->_32 + m->_23) * h;
        q->z = 0.5f * s;
        q->w = (m->_12 - m->_21) * h;
      }
    }
    public static void quaternion(float4* q, float3x4* m)
    {
      var xx = q->x * q->x;
      var yy = q->y * q->y;
      var zz = q->z * q->z;
      var xy = q->x * q->y;
      var zw = q->z * q->w;
      var zx = q->z * q->x;
      var yw = q->y * q->w;
      var yz = q->y * q->z;
      var xw = q->x * q->w;
      m->_11 = 1.0f - (2.0f * (yy + zz));
      m->_12 = 2.0f * (xy + zw);
      m->_13 = 2.0f * (zx - yw);
      m->_21 = 2.0f * (xy - zw);
      m->_22 = 1.0f - (2.0f * (zz + xx));
      m->_23 = 2.0f * (yz + xw);
      m->_31 = 2.0f * (zx + yw);
      m->_32 = 2.0f * (yz - xw);
      m->_33 = 1.0f - (2.0f * (yy + xx));
    }

    public static bool decompose(float3x4* m, float3* s, float3x4* r)
    {
      const float eps = 0.0001f; *r = 1;// identity(r); //if (m->_12 == 0 && m->_13 == 0 && m->_21 == 0 && m->_23 == 0 && m->_31 == 0 && m->_32 == 0) { s->x = m->_11; s->y = m->_22; s->z = m->_33; return true; }
      float3x4 t2; var pc = (float3*)&t2;
      float2x3 t3; var pv = (float3**)&t3;
      pc[0].x = pc[1].y = pc[2].z = 1;
      pv[0] = (float3*)&r->_11; *pv[0] = *(float3*)&m->_11;
      pv[1] = (float3*)&r->_21; *pv[1] = *(float3*)&m->_21;
      pv[2] = (float3*)&r->_31; *pv[2] = *(float3*)&m->_31;
      s->x = length(*pv[0]);
      s->y = length(*pv[1]);
      s->z = length(*pv[2]);
      float* ss = (float*)s;
      float x = ss[0], y = ss[1], z = ss[2]; uint a, b, c;
      if (x < y) { if (y < z) { a = 2; b = 1; c = 0; } else { a = 1; if (x < z) { b = 2; c = 0; } else { b = 0; c = 2; } } }
      else { if (x < z) { a = 2; b = 0; c = 1; } else { a = 0; if (y < z) { b = 2; c = 1; } else { b = 1; c = 2; } } }
      if (ss[a] < eps) *pv[a] = pc[a];
      *pv[a] = normalize(*pv[a]);
      if (ss[b] < eps)
      {
        int cc; float ax = Math.Abs(pv[a]->x), ay = Math.Abs(pv[a]->y), az = Math.Abs(pv[a]->z);
        if (ax < ay) { if (ay < az) cc = 0; else { if (ax < az) cc = 0; else cc = 2; } }
        else { if (ax < az) cc = 1; else { if (ay < az) cc = 1; else cc = 2; } }
        *pv[b] = *pv[a] ^ *pc + cc;
      }
      *pv[b] = normalize(*pv[b]);
      if (ss[c] < eps) *pv[c] = *pv[a] ^ *pv[b];
      *pv[c] = normalize(*pv[c]);
      var det = determiante(r);
      if (det < 0.0f) { ss[a] = -ss[a]; *pv[a] = -*pv[a]; det = -det; }
      det -= 1.0f; det *= det;
      return eps > det;
    }

    public static float3x4 saturate(float3x4 m)
    {
      for (int i = 0; i < 3; i++)
      {
        var p = &m._11 + i * 3; var l = dot(*(float3*)p); if (l < 0.1f) break;
        for (int k = 0; k < 3; k++)
        {
          var v = p[k]; if (v * v < l - 1e-5) continue;
          l = (float)(Math.Round(Math.Sqrt(l), 6)); //if (l != (int)l) { }
          p[0] = p[1] = p[2] = 0; p[k] = v > 0 ? +l : -l; break;
        }
      }
      return m;
    }

    public static float3x4 LookAt(float3 eye, float3 pos, float3 up)
    {
      var R2 = normalize(pos - eye);
      var R0 = normalize(up ^ R2);
      var R1 = R2 ^ R0;
      eye = -eye;
      var D0 = R0 & eye;
      var D1 = R1 & eye;
      var D2 = R2 & eye;
      float3x4 m;
      m._11 = R0.x; m._12 = R1.x; m._13 = R2.x;
      m._21 = R0.y; m._22 = R1.y; m._23 = R2.y;
      m._31 = R0.z; m._32 = R1.z; m._33 = R2.z;
      m._41 = D0; m._42 = D1; m._43 = D2;
      return m;
    }

    public static bool intersect(float2 p, float2* box)
    {
      return box[0].x <= p.x && box[1].x >= p.x && box[0].y <= p.y && box[1].y >= p.y;
    }
    public static bool intersect(float2 p1, float2 p2, float2* box)
    {
      var y1 = Math.Min(p1.y, p2.y); if (y1 > box[1].y) return false;
      var y2 = Math.Max(p1.y, p2.y); if (y2 < box[0].y) return false;
      var x1 = Math.Min(p1.x, p2.x); if (x1 > box[1].x) return false;
      var x2 = Math.Max(p1.x, p2.x); if (x2 < box[0].x) return false;
      if (y1 == y2) return true;
      var f = (p2.x - p1.x) / (p2.y - p1.y);
      x1 = p1.x + (Math.Max(y1, box[0].y) - p1.y) * f;
      x2 = p1.x + (Math.Min(y2, box[1].y) - p1.y) * f;
      if (x1 < box[0].x && x2 < box[0].x) return false;
      if (x1 > box[1].x && x2 > box[1].x) return false;
      return true;
    }
    public static bool intersect(float2 p1, float2 p2, float2 p3, float2 pt)
    {
      float2 v0 = p2 - p1, v1 = p3 - p1, v2 = pt - p1;
      float d00 = v0 & v0, d01 = v0 & v1, d02 = v0 & v2, d11 = v1 & v1, d12 = v1 & v2;
      float d = 1 / (d00 * d11 - d01 * d01);
      float u = (d11 * d02 - d01 * d12) * d;
      float v = (d00 * d12 - d01 * d02) * d;
      return u >= 0 && v >= 0 && u + v < 1;
    }
    public static bool intersect(float2 p1, float2 p2, float2 p3, float2* box)
    {
      if (Math.Min(Math.Min(p1.y, p2.y), p3.y) > box[1].y) return false;
      if (Math.Max(Math.Max(p1.y, p2.y), p3.y) < box[0].y) return false;
      if (Math.Min(Math.Min(p1.x, p2.x), p3.x) > box[1].x) return false;
      if (Math.Max(Math.Max(p1.x, p2.x), p3.x) < box[0].x) return false;
      if (intersect(p1, p2, box)) return true;
      if (intersect(p2, p3, box)) return true;
      if (intersect(p3, p1, box)) return true;
      if (intersect(p1, p2, p3, box[0])) return true;
      return false;
    }

    public static void boxempty(float3* box)
    {
      box[1].x = box[1].y = box[1].z = -(box[0].x = box[0].y = box[0].z = float.MaxValue);
    }
    public static void boxadd(float3* p, float3* box)
    {
      if (box[0].x > p->x) box[0].x = p->x;
      if (box[0].y > p->y) box[0].y = p->y;
      if (box[0].z > p->z) box[0].z = p->z;
      if (box[1].x < p->x) box[1].x = p->x;
      if (box[1].y < p->y) box[1].y = p->y;
      if (box[1].z < p->z) box[1].z = p->z;
    }
    public static void boxadd(float3* p, float3x4* m, float3* box)
    {
      var r = p[0] * m[0]; boxadd(&r, box);
    }
    public static float3 boxcor(float3* box, int f)
    {
      float3 p;
      p.x = (f & 0x001) != 0 ? box[1].x : box[0].x;
      p.y = (f & 0x002) != 0 ? box[1].y : box[0].y;
      p.z = (f & 0x004) != 0 ? box[1].z : box[0].z;
      return p;
    }

    public static double sin(double v) { if (Math.Abs(v = Math.Sin(v)) < 1e-15) v = 0; return v; }
    public static double cos(double v) { if (Math.Abs(v = Math.Cos(v)) < 1e-15) v = 0; return v; }

    public static void swap<T>(ref T a, ref T b) { var t = a; a = b; b = t; }

    public struct float2 : IEquatable<float2>
    {
      public float x, y;
      public override string ToString()
      {
        return $"{x:R}; {y:R}";
      }
      public static float2 Parse(string s)
      {
        var ss = s.Split(';'); return new float2(float.Parse(ss[0]), float.Parse(ss[1]));
      }
      public float2(float x, float y)
      {
        this.x = x; this.y = y;
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        h2 = ((h2 << 7) | (h1 >> 25)) ^ h1;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(float2 v)
      {
        return x == v.x && y == v.y;
      }
      public override bool Equals(object obj)
      {
        return obj is float2 && Equals((float2)obj);
      }
      public static implicit operator float2((float x, float y) p)
      {
        float2 v; v.x = p.x; v.y = p.y; return v;
      }
      public static implicit operator float2(System.Drawing.Size p)
      {
        float2 v; v.x = p.Width; v.y = p.Height; return v;
      }
      public static implicit operator float2(System.Drawing.Point p)
      {
        float2 v; v.x = p.X; v.y = p.Y; return v;
      }
      public static bool operator ==(float2 a, float2 b) { return a.x == b.x && a.y == b.y; }
      public static bool operator !=(float2 a, float2 b) { return a.x != b.x || a.y != b.y; }
      public static float2 operator -(float2 v) { v.x = -v.x; v.y = -v.y; return v; }
      public static float2 operator *(float2 v, float f)
      {
        v.x *= f; v.y *= f; return v;
      }
      public static float2 operator /(float2 v, float f)
      {
        v.x /= f; v.y /= f; return v;
      }
      public static float2 operator /(float2 a, float2 b)
      {
        a.x /= b.x; a.y /= b.y; return a;
      }
      public static float2 operator /(float f, float2 v)
      {
        v.x = f / v.x; v.y = f / v.y; return v;
      }
      public static float2 operator +(float2 a, float2 b) { a.x = a.x + b.x; a.y = a.y + b.y; return a; }
      public static float2 operator -(float2 a, float2 b) { a.x = a.x - b.x; a.y = a.y - b.y; return a; }
      public static float2 operator *(float2 a, float2 b) { a.x = a.x * b.x; a.y = a.y * b.y; return a; }
      public static float2 operator ~(float2 v) { float2 b; b.x = -v.y; b.y = v.x; return b; }
      public static float operator ^(float2 a, float2 b) => a.x * b.y - a.y * b.x;
      public static float operator &(float2 a, float2 b) => a.x * b.x + a.y * b.y;
    }

    public struct float3 : IEquatable<float3>
    {
      public float x, y, z;
      public override string ToString()
      {
        return $"{x:R}; {y:R}; {z:R}";
      }
      public static float3 Parse(string s)
      {
        var ss = s.Split(';'); return new float3(float.Parse(ss[0]), float.Parse(ss[1]), float.Parse(ss[2]));
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(float3 v)
      {
        return x == v.x && y == v.y && z == v.z;
      }
      public override bool Equals(object obj)
      {
        return obj is float3 && Equals((float3)obj);
      }
      public float3(float x, float y, float z)
      {
        this.x = x; this.y = y; this.z = z;
      }
      public float2 xy { get { float2 p; p.x = x; p.y = y; return p; } }
      public static implicit operator float3(float p)
      {
        float3 b; b.x = p; b.y = b.z = 0; return b;
      }
      public static implicit operator float3(float2 p)
      {
        float3 b; b.x = p.x; b.y = p.y; b.z = 0; return b;
      }
      public static explicit operator float2(float3 p)
      {
        float2 b; b.x = p.x; b.y = p.y; return b;
      }
      public static bool operator ==(float3 a, float3 b)
      {
        return a.x == b.x && a.y == b.y && a.z == b.z;
      }
      public static bool operator !=(float3 a, float3 b)
      {
        return a.x != b.x || a.y != b.y || a.z != b.z;
      }
      public static float3 operator -(float3 v)
      {
        v.x = -v.x; v.y = -v.y; v.z = -v.z; return v;
      }
      public static float3 operator +(float3 a, float3 b)
      {
        a.x += b.x; a.y += b.y; a.z += b.z; return a;
      }
      public static float3 operator -(float3 a, float3 b)
      {
        a.x -= b.x; a.y -= b.y; a.z -= b.z; return a;
      }
      public static float3 operator *(float3 v, float f)
      {
        v.x *= f; v.y *= f; v.z *= f; return v;
      }
      public static float3 operator /(float3 v, float f)
      {
        v.x /= f; v.y /= f; v.z /= f; return v;
      }
      public static float3 operator /(float3 a, float3 b)
      {
        a.x /= b.x; a.y /= b.y; a.z /= b.z; return a;
      }
      public static float3 operator ^(float3 a, float3 b)
      {
        float3 c;
        c.x = a.y * b.z - a.z * b.y;
        c.y = a.z * b.x - a.x * b.z;
        c.z = a.x * b.y - a.y * b.x;
        return c;
      }
      public static float operator &(float3 a, float3 b)
      {
        return a.x * b.x + a.y * b.y + a.z * b.z;
      }
    }

    public struct float4 : IEquatable<float4>
    {
      public float x, y, z, w;
      public float4(float x, float y, float z, float w)
      {
        this.x = x; this.y = y; this.z = z; this.w = w;
      }
      public static float4 Parse(string s)
      {
        var ss = s.Split(';'); return new float4(float.Parse(ss[0]), float.Parse(ss[1]), float.Parse(ss[2]), float.Parse(ss[3]));
      }
      public override string ToString()
      {
        return $"{x:R}; {y:R}; {z:R}; {w:R}";
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        var h4 = (uint)w.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2 ^ h4;
        return (int)h1;
      }
      public bool Equals(float4 v)
      {
        return this == v;
      }
      public override bool Equals(object obj)
      {
        return obj is float4 && Equals((float4)obj);
      }
      public static bool operator ==(in float4 a, in float4 b)
      {
        return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
      }
      public static bool operator !=(float4 a, float4 b)
      {
        return !(a == b);
      }
      public static float4 operator *(float4 v, float f)
      {
        v.x *= f; v.y *= f; v.z *= f; v.w *= f; return v;
      }
      public static explicit operator uint(in float4 p)
      {
        uint d;
        ((byte*)&d)[0] = (byte)(p.z * 255);
        ((byte*)&d)[1] = (byte)(p.y * 255);
        ((byte*)&d)[2] = (byte)(p.x * 255);
        ((byte*)&d)[3] = (byte)(p.w * 255); return d;
      }
      public static explicit operator float4(uint p)
      {
        float4 d;
        d.z = ((byte*)&p)[0] * (1.0f / 255);
        d.y = ((byte*)&p)[1] * (1.0f / 255);
        d.x = ((byte*)&p)[2] * (1.0f / 255);
        d.w = ((byte*)&p)[3] * (1.0f / 255); return d;
      }
      public float3 xyz { get { float3 p; p.x = x; p.y = y; p.z = z; return p; } }
    }

    public struct float3box { public float3 min, max; }

    public struct float2x3
    {
      public float _11, _12;
      public float _21, _22;
      public float _41, _42;
    }

    public struct float3x4
    {
      public float _11, _12, _13;
      public float _21, _22, _23;
      public float _31, _32, _33;
      public float _41, _42, _43;
      public override int GetHashCode()
      {
        return base.GetHashCode();
      }
      public override bool Equals(object p)
      {
        return p is float3x4 && !((float3x4)p != this);
      }
      public static bool operator ==(in float3x4 a, in float3x4 b)
      {
        return !(a != b);
      }
      public static bool operator !=(in float3x4 a, in float3x4 b)
      {
        return a._11 != b._11 || a._12 != b._12 || a._13 != b._13 || //a._14 != b._14 ||
               a._21 != b._21 || a._22 != b._22 || a._23 != b._23 || //a._24 != b._24 || 
               a._31 != b._31 || a._32 != b._32 || a._33 != b._33 || //a._34 != b._34 ||  
               a._41 != b._41 || a._42 != b._42 || a._43 != b._43;//|| a._44 != b._44;
        //for (int i = 0; i < 12; i++) if ((&a._11)[i] != (&b._11)[i]) return true; return false;
      }

      public static implicit operator float3x4(float s)
      {
        return new float3x4() { _11 = s, _22 = s, _33 = s };
      }
      public static implicit operator float3x4(float2 p)
      {
        float3x4 m; *(float*)&m = m._22 = m._33 = 1; *(float2*)&m._41 = p; return m;
      }
      public static implicit operator float3x4(float3 p)
      {
        float3x4 m; *(float*)&m = m._22 = m._33 = 1; *(float3*)&m._41 = p; return m;
      }

      public static float3x4 operator !(in float3x4 p)
      {
        //inv(&v, &v); return v;
        var b0 = p._31 * p._42 - p._32 * p._41;
        var b1 = p._31 * p._43 - p._33 * p._41;
        var b3 = p._32 * p._43 - p._33 * p._42;
        var d1 = p._22 * p._33 + p._23 * -p._32;
        var d2 = p._21 * p._33 + p._23 * -p._31;
        var d3 = p._21 * p._32 + p._22 * -p._31;
        var d4 = p._21 * b3 + p._22 * -b1 + p._23 * b0;
        var de = p._11 * d1 - p._12 * d2 + p._13 * d3; de = 1f / de; //if (det == 0) throw new Exception();
        var a0 = p._11 * p._22 - p._12 * p._21;
        var a1 = p._11 * p._23 - p._13 * p._21;
        var a3 = p._12 * p._23 - p._13 * p._22;
        var d5 = p._12 * p._33 + p._13 * -p._32;
        var d6 = p._11 * p._33 + p._13 * -p._31;
        var d7 = p._11 * p._32 + p._12 * -p._31;
        var d8 = p._11 * b3 + p._12 * -b1 + p._13 * b0;
        var d9 = p._41 * a3 + p._42 * -a1 + p._43 * a0; float3x4 r;
        r._11 = +d1 * de; r._12 = -d5 * de;
        r._13 = +a3 * de;
        r._21 = -d2 * de; r._22 = +d6 * de;
        r._23 = -a1 * de;
        r._31 = +d3 * de; r._32 = -d7 * de;
        r._33 = +a0 * de;
        r._41 = -d4 * de; r._42 = +d8 * de;
        r._43 = -d9 * de; return r;
      }
      public static float3x4 operator *(in float3x4 a, in float3x4 b)
      {
        //float3x4 c; mul(&a, &b, &c); return c;
        float x = a._11, y = a._12, z = a._13; float3x4 r;
        r._11 = b._11 * x + b._21 * y + b._31 * z;
        r._12 = b._12 * x + b._22 * y + b._32 * z;
        r._13 = b._13 * x + b._23 * y + b._33 * z; x = a._21; y = a._22; z = a._23;
        r._21 = b._11 * x + b._21 * y + b._31 * z;
        r._22 = b._12 * x + b._22 * y + b._32 * z;
        r._23 = b._13 * x + b._23 * y + b._33 * z; x = a._31; y = a._32; z = a._33;
        r._31 = b._11 * x + b._21 * y + b._31 * z;
        r._32 = b._12 * x + b._22 * y + b._32 * z;
        r._33 = b._13 * x + b._23 * y + b._33 * z; x = a._41; y = a._42; z = a._43;
        r._41 = b._11 * x + b._21 * y + b._31 * z + b._41;
        r._42 = b._12 * x + b._22 * y + b._32 * z + b._42;
        r._43 = b._13 * x + b._23 * y + b._33 * z + b._43; return r;
      }
      public static float3x4 operator *(float a, in float3x4 b)
      {
        return (float3x4)a * b;
      }
      public static float3 operator *(float3 a, in float3x4 b)
      {
        float3 c;
        c.x = b._11 * a.x + b._21 * a.y + b._31 * a.z + b._41;
        c.y = b._12 * a.x + b._22 * a.y + b._32 * a.z + b._42;
        c.z = b._13 * a.x + b._23 * a.y + b._33 * a.z + b._43;
        return c;
      }

      public static float3x4 operator +(float2 a, in float3x4 b)
      {
        return new float3x4 { _11 = 1, _22 = 1, _33 = 1, _41 = a.x, _42 = a.y } * b;
      }
      public static float3x4 operator +(float3 a, in float3x4 b)
      {
        return new float3x4 { _11 = 1, _22 = 1, _33 = 1, _41 = a.x, _42 = a.y, _43 = a.z } * b;
      }

      public static float3x4 operator ^(float a, in float3x4 b)
      {
        return new float3x4 { _11 = a, _22 = a, _33 = a } * b;
      }
      public static float3x4 operator ^(float2 a, in float3x4 b)
      {
        return new float3x4 { _11 = a.x, _22 = a.y, _33 = 1 } * b;
      }
      public static float3x4 operator ^(float3 a, in float3x4 b)
      {
        return new float3x4 { _11 = a.x, _22 = a.y, _33 = a.z } * b;
      }
      public static float3 operator &(float3 a, in float3x4 b)
      {
        float3 c;
        c.x = b._11 * a.x + b._21 * a.y + b._31 * a.z;
        c.y = b._12 * a.x + b._22 * a.y + b._32 * a.z;
        c.z = b._13 * a.x + b._23 * a.y + b._33 * a.z;
        return c;
      }

      public float3 this[int i]
      {
        get { var t = this; return ((float3*)&t)[i]; }
        set { var t = this; ((float3*)&t)[i] = value; this = t; }
      }
    }

    public struct float4x4
    {
      public float _11, _12, _13, _14;
      public float _21, _22, _23, _24;
      public float _31, _32, _33, _34;
      public float _41, _42, _43, _44;
      public static float4x4 operator *(in float4x4 a, in float4x4 b)
      {
        float x = a._11, y = a._12, z = a._13, w = a._14; float4x4 r;
        r._11 = b._11 * x + b._21 * y + b._31 * z + b._41 * w;
        r._12 = b._12 * x + b._22 * y + b._32 * z + b._42 * w;
        r._13 = b._13 * x + b._23 * y + b._33 * z + b._43 * w;
        r._14 = b._14 * x + b._24 * y + b._34 * z + b._44 * w; x = a._21; y = a._22; z = a._23; w = a._24;
        r._21 = b._11 * x + b._21 * y + b._31 * z + b._41 * w;
        r._22 = b._12 * x + b._22 * y + b._32 * z + b._42 * w;
        r._23 = b._13 * x + b._23 * y + b._33 * z + b._43 * w;
        r._24 = b._14 * x + b._24 * y + b._34 * z + b._44 * w; x = a._31; y = a._32; z = a._33; w = a._34;
        r._31 = b._11 * x + b._21 * y + b._31 * z + b._41 * w;
        r._32 = b._12 * x + b._22 * y + b._32 * z + b._42 * w;
        r._33 = b._13 * x + b._23 * y + b._33 * z + b._43 * w;
        r._34 = b._14 * x + b._24 * y + b._34 * z + b._44 * w; x = a._41; y = a._42; z = a._43; w = a._44;
        r._41 = b._11 * x + b._21 * y + b._31 * z + b._41 * w;
        r._42 = b._12 * x + b._22 * y + b._32 * z + b._42 * w;
        r._43 = b._13 * x + b._23 * y + b._33 * z + b._43 * w;
        r._44 = b._14 * x + b._24 * y + b._34 * z + b._44 * w; return r;
      }
      public static float4x4 operator !(in float4x4 m)
      {
        var b0 = m._31 * m._42 - m._32 * m._41;
        var b1 = m._31 * m._43 - m._33 * m._41;
        var b2 = m._34 * m._41 - m._31 * m._44;
        var b3 = m._32 * m._43 - m._33 * m._42;
        var b4 = m._34 * m._42 - m._32 * m._44;
        var b5 = m._33 * m._44 - m._34 * m._43;
        var d11 = m._22 * b5 + m._23 * b4 + m._24 * b3;
        var d12 = m._21 * b5 + m._23 * b2 + m._24 * b1;
        var d13 = m._21 * -b4 + m._22 * b2 + m._24 * b0;
        var d14 = m._21 * b3 + m._22 * -b1 + m._23 * b0;
        var det = m._11 * d11 - m._12 * d12 + m._13 * d13 - m._14 * d14;
        var dei = 1.0f / det;
        var a0 = m._11 * m._22 - m._12 * m._21;
        var a1 = m._11 * m._23 - m._13 * m._21;
        var a2 = m._14 * m._21 - m._11 * m._24;
        var a3 = m._12 * m._23 - m._13 * m._22;
        var a4 = m._14 * m._22 - m._12 * m._24;
        var a5 = m._13 * m._24 - m._14 * m._23;
        var d21 = m._12 * b5 + m._13 * b4 + m._14 * b3;
        var d22 = m._11 * b5 + m._13 * b2 + m._14 * b1;
        var d23 = m._11 * -b4 + m._12 * b2 + m._14 * b0;
        var d24 = m._11 * b3 + m._12 * -b1 + m._13 * b0;
        var d31 = m._42 * a5 + m._43 * a4 + m._44 * a3;
        var d32 = m._41 * a5 + m._43 * a2 + m._44 * a1;
        var d33 = m._41 * -a4 + m._42 * a2 + m._44 * a0;
        var d34 = m._41 * a3 + m._42 * -a1 + m._43 * a0;
        var d41 = m._32 * a5 + m._33 * a4 + m._34 * a3;
        var d42 = m._31 * a5 + m._33 * a2 + m._34 * a1;
        var d43 = m._31 * -a4 + m._32 * a2 + m._34 * a0;
        var d44 = m._31 * a3 + m._32 * -a1 + m._33 * a0; float4x4 r;
        r._11 = +d11 * dei; r._12 = -d21 * dei;
        r._13 = +d31 * dei; r._14 = -d41 * dei;
        r._21 = -d12 * dei; r._22 = +d22 * dei;
        r._23 = -d32 * dei; r._24 = +d42 * dei;
        r._31 = +d13 * dei; r._32 = -d23 * dei;
        r._33 = +d33 * dei; r._34 = -d43 * dei;
        r._41 = -d14 * dei; r._42 = +d24 * dei;
        r._43 = -d34 * dei; r._44 = +d44 * dei; return r;
      }
      public static implicit operator float4x4(in float3x4 a)
      {
        float4x4 b;
        b._11 = a._11; b._12 = a._12; b._13 = a._13; b._14 = 0;
        b._21 = a._21; b._22 = a._22; b._23 = a._23; b._24 = 0;
        b._31 = a._31; b._32 = a._32; b._33 = a._33; b._34 = 0;
        b._41 = a._41; b._42 = a._42; b._43 = a._43; b._44 = 1; return b;
      }
      public static explicit operator float3x4(in float4x4 a)
      {
        float3x4 b;
        b._11 = a._11; b._12 = a._12; b._13 = a._13;
        b._21 = a._21; b._22 = a._22; b._23 = a._23;
        b._31 = a._31; b._32 = a._32; b._33 = a._33;
        b._41 = a._41; b._42 = a._42; b._43 = a._43; return b;
      }
      public static float3 operator *(float3 a, in float4x4 b)
      {
        var x = b._11 * a.x + b._21 * a.y + b._31 * a.z + b._41;
        var y = b._12 * a.x + b._22 * a.y + b._32 * a.z + b._42;
        var z = b._13 * a.x + b._23 * a.y + b._33 * a.z + b._43;
        var w = b._14 * a.x + b._24 * a.y + b._34 * a.z + b._44;
        float3 r; r.x = x * (w = 1 / w); r.y = y * w; r.z = z * w; return r;
      }
      public static float4x4 PerspectiveFov(float fov, float aspect, float z1, float z2)
      {
        float4x4 m; var a = fov * 0.5f; var s = (float)Math.Sin(a); var c = (float)Math.Cos(a);
        m._11 = (m._22 = c / s) / aspect; m._33 = z2 / (z2 - z1); m._34 = 1; m._43 = -m._33 * z1; return *(&m);
      }
      public static float4x4 OrthoCenter(float x1, float x2, float y2, float y1, float z1, float z2)
      {
        var x = 1.0f / (x2 - x1);
        var y = 1.0f / (y1 - y2);
        float4x4 m; *(float*)&m =
        m._11 = x + x;
        m._22 = y + y;
        m._33 = 1.0f / (z2 - z1);
        m._41 = -(x1 + x2) * x;
        m._42 = -(y1 + y2) * y;
        m._43 = -m._33 * z1;
        m._44 = 1.0f;
        return m;
      }
      public static float4x4 PerspectiveOffCenter(float x1, float x2, float y2, float y1, float z1, float z2)
      {
        float4x4 m; *(float*)&m =
        m._11 = 2.0f * z1 / (x2 - x1);
        m._22 = 2.0f * z1 / (y1 - y2);
        m._31 = (x1 + x2) / (x1 - x2);
        m._32 = (y1 + y2) / (y2 - y1);
        m._33 = z2 / (z2 - z1);
        m._34 = 1.0f;
        m._43 = -m._33 * z1;
        return m;
      }
    }

    public struct vertex
    {
      public float3 p, n; public float2 t;
    }

    public struct double2 : IEquatable<double2>
    {
      public double x, y;
      public override string ToString() => $"{x:R}; {y:R}";
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        h2 = ((h2 << 7) | (h1 >> 25)) ^ h1;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(double2 v)
      {
        return x == v.x && y == v.y;
      }
      public override bool Equals(object obj)
      {
        return obj is double2 && Equals((double2)obj);
      }
      public double2(double x, double y) { this.x = x; this.y = y; }
      public double2(double a)
      {
        x = Math.Cos(a); if (Math.Abs(x) == 1) { y = 0; return; }
        y = Math.Sin(a); if (Math.Abs(y) == 1) { x = 0; return; }
      }
      public static bool operator ==(in double2 a, in double2 b) { return a.x == b.x && a.y == b.y; }
      public static bool operator !=(in double2 a, in double2 b) { return a.x != b.x || a.y != b.y; }
      public static explicit operator float2(in double2 p)
      {
        float2 t; t.x = (float)p.x; t.y = (float)p.y; return t;
      }
      public static double2 operator -(double2 v) { v.x = -v.x; v.y = -v.y; return v; }
      public static double2 operator ~(double2 v) { double2 b; b.x = -v.y; b.y = v.x; return b; }
      public static double2 operator -(double2 a, double2 b) { a.x = a.x - b.x; a.y = a.y - b.y; return a; }
      public static double2 operator *(double2 v, double f) { v.x *= f; v.y *= f; return v; }
      public static double2 operator /(double2 v, double f) { v.x /= f; v.y /= f; return v; }
      public double Length => Math.Sqrt(x * x + y * y);
    }

    public struct double3 : IEquatable<double3>
    {
      public double x, y, z;
      public override string ToString()
      {
        return string.Format("{0}; {1}; {2}", x.ToString("R"), y.ToString("R"), z.ToString("R"));
      }
      public static double3 Parse(string s)
      {
        var ss = s.Split(';'); return new double3(double.Parse(ss[0]), double.Parse(ss[1]), double.Parse(ss[2]));
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2;
        return (int)h1;
      }
      public bool Equals(double3 v)
      {
        return x == v.x && y == v.y && z == v.z;
      }
      public override bool Equals(object obj)
      {
        return obj is double3 && Equals((double3)obj);
      }
      public double3(double x, double y, double z)
      {
        this.x = x; this.y = y; this.z = z;
      }
      public static implicit operator double3(float3 p)
      {
        double3 t; t.x = p.x; t.y = p.y; t.z = p.z; return t;
      }
      public static explicit operator float3(in double3 p)
      {
        float3 t; t.x = (float)p.x; t.y = (float)p.y; t.z = (float)p.z; return t;
      }
      public static bool operator ==(in double3 a, in double3 b)
      {
        return a.x == b.x && a.y == b.y && a.z == b.z;
      }
      public static bool operator !=(in double3 a, in double3 b)
      {
        return a.x != b.x || a.y != b.y || a.z != b.z;
      }
      public static double3 operator -(in double3 v)
      {
        return new double3 { x = -v.x, y = -v.y, z = -v.z };
      }
      public static double3 operator +(in double3 a, in double3 b)
      {
        return new double3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };
      }
      public static double3 operator -(in double3 a, in double3 b)
      {
        return new double3 { x = a.x - b.x, y = a.y - b.y, z = a.z - b.z };
      }
      public static double3 operator *(in double3 v, double f)
      {
        return new double3 { x = v.x * f, y = v.y * f, z = v.z * f };
      }
      public static double3 operator /(in double3 v, double f)
      {
        return new double3 { x = v.x / f, y = v.y / f, z = v.z / f };
      }
      public double Length => Math.Sqrt(x * x + y * y + z * z);
    }

    public struct double4 : IEquatable<double4>
    {
      public double x, y, z, w;
      public override string ToString()
      {
        return $"{x:R}; {y:R}; {z:R}; {w:R}";
      }
      public override int GetHashCode()
      {
        var h1 = (uint)x.GetHashCode();
        var h2 = (uint)y.GetHashCode();
        var h3 = (uint)z.GetHashCode();
        var h4 = (uint)w.GetHashCode();
        h2 = ((h2 << 7) | (h3 >> 25)) ^ h3;
        h1 = ((h1 << 7) | (h2 >> 25)) ^ h2 ^ h4;
        return (int)h1;
      }
      public bool Equals(double4 v)
      {
        return x == v.x && y == v.y && z == v.z && w == v.w;
      }
      public override bool Equals(object obj)
      {
        return obj is double4 && Equals((double4)obj);
      }
    }

    public struct double3x4
    {
      public double _11, _12, _13;
      public double _21, _22, _23;
      public double _31, _32, _33;
      public double _41, _42, _43;
      public static implicit operator double3x4(in float3x4 p)
      {
        double3x4 d;
        d._11 = p._11; d._12 = p._12; d._13 = p._13;
        d._21 = p._21; d._22 = p._22; d._23 = p._23;
        d._31 = p._31; d._32 = p._32; d._33 = p._33;
        d._41 = p._41; d._42 = p._42; d._43 = p._43;
        return d;
      }
      public static explicit operator float3x4(in double3x4 p)
      {
        float3x4 d;
        d._11 = (float)p._11; d._12 = (float)p._12; d._13 = (float)p._13;
        d._21 = (float)p._21; d._22 = (float)p._22; d._23 = (float)p._23;
        d._31 = (float)p._31; d._32 = (float)p._32; d._33 = (float)p._33;
        d._41 = (float)p._41; d._42 = (float)p._42; d._43 = (float)p._43;
        return d;
      }
    }
    public static void round(double* p, int n, int digits) { for (int i = 0; i < n; i++) p[i] = Math.Round(p[i], digits); }
    public static void rot(double3x4* m, int i, double a)
    {
      var v = (float2)new double2(a);
      if (i == 0) { m->_11 = 1; m->_22 = m->_33 = v.x; m->_32 = -(m->_23 = v.y); return; }
      if (i == 1) { m->_22 = 1; m->_11 = m->_33 = v.x; m->_13 = -(m->_31 = v.y); return; }
      if (i == 2) { m->_33 = 1; m->_11 = m->_22 = v.x; m->_21 = -(m->_12 = v.y); return; }
    }
    public static void mul(double3* p, double3x4* pm)
    {
      double x = p->x, y = p->y, z = p->z;
      p->x = pm->_11 * x + pm->_21 * y + pm->_31 * z + pm->_41;
      p->y = pm->_12 * x + pm->_22 * y + pm->_32 * z + pm->_42;
      p->z = pm->_13 * x + pm->_23 * y + pm->_33 * z + pm->_43;
    }
    public static void mul(double3x4* a, double3x4* b, double3x4* r)
    {
      double x = a->_11, y = a->_12, z = a->_13;
      r->_11 = b->_11 * x + b->_21 * y + b->_31 * z;
      r->_12 = b->_12 * x + b->_22 * y + b->_32 * z;
      r->_13 = b->_13 * x + b->_23 * y + b->_33 * z; x = a->_21; y = a->_22; z = a->_23;
      r->_21 = b->_11 * x + b->_21 * y + b->_31 * z;
      r->_22 = b->_12 * x + b->_22 * y + b->_32 * z;
      r->_23 = b->_13 * x + b->_23 * y + b->_33 * z; x = a->_31; y = a->_32; z = a->_33;
      r->_31 = b->_11 * x + b->_21 * y + b->_31 * z;
      r->_32 = b->_12 * x + b->_22 * y + b->_32 * z;
      r->_33 = b->_13 * x + b->_23 * y + b->_33 * z; x = a->_41; y = a->_42; z = a->_43;
      r->_41 = b->_11 * x + b->_21 * y + b->_31 * z + b->_41;
      r->_42 = b->_12 * x + b->_22 * y + b->_32 * z + b->_42;
      r->_43 = b->_13 * x + b->_23 * y + b->_33 * z + b->_43;
    }
    public static int msb(int v)
    {
      int i = 0;
      if ((v & 0xFFFF0000) != 0) { i |= 0x10; v >>= 0x10; }
      if ((v & 0x0000FF00) != 0) { i |= 0x08; v >>= 0x08; }
      if ((v & 0x000000F0) != 0) { i |= 0x04; v >>= 0x04; }
      if ((v & 0x0000000C) != 0) { i |= 0x02; v >>= 0x02; }
      if ((v & 0x00000002) != 0) { i |= 0x01; }
      return i;
    }

  }

  unsafe partial class D3DView
  {
    public abstract class Buffer
    {
      uint refcount, id; internal static Dictionary<uint, GCHandle> cache = new Dictionary<uint, GCHandle>(); static bool gc;
      public static Buffer GetBuffer(Type type, void* p, int n, Buffer buff = null) //todo: internal
      {
        if (gc) { gc = false; compact(); }
        uint idfree = 0, id = 17; for (int t = 0, l = n >> 2; t < l; t++) id = id * 31 + ((uint*)p)[t];
        for (; ; id++)
        {
          if (!cache.TryGetValue(id, out GCHandle h)) break;
          if (!(h.Target is Buffer test)) { if (idfree == 0) idfree = id; continue; }
          if (test.GetType() != type || !test.Equals(p, n)) continue;
          test.AddRef(); buff?.Release(); return test;
        }
        if (buff == null || !buff.Release()) buff = (Buffer)Activator.CreateInstance(type); buff.Init(p, n);
        if (idfree != 0) { var h = cache[buff.id = idfree]; h.Target = buff; }
        else cache.Add(buff.id = id, GCHandle.Alloc(buff, GCHandleType.Weak));
        buff.AddRef(); return buff;
      }
      static void compact()
      {
        var a = cache.Keys.ToList(); a.Sort();
        for (int i = a.Count - 1; i >= 0; i--) if (cache[a[i]].Target == null && !cache.ContainsKey(a[i] + 1)) cache.Remove(a[i]);
      }
      public void AddRef() { refcount++; }
      public bool Release()
      {
        if (--refcount != 0) return false;
        var h = cache[id];
        if (cache.ContainsKey(id + 1)) h.Target = null; //keep the line
        else { h.Free(); cache.Remove(id); }
        id = 0; Dispose(); return true;
      }
      ~Buffer() { if (id != 0) gc = true; Dispose(); }

      protected abstract bool Equals(void* p, int n);
      protected abstract void Init(void* p, int n);
      protected abstract void Dispose();
      public abstract int GetData(void* p);

      internal byte[] Reset() { var n = GetData(StackPtr); var a = new byte[n]; memcpy(a, StackPtr); Dispose(); return a; }
      internal void Reset(byte[] a) { fixed (void* t = a) Init(t, a.Length); }
    }

    static void copy(void* p, int n, IntPtr buffer)
    {
      BUFFER_DESC bd; bd.ByteWidth = n;
      bd.Usage = USAGE.STAGING; bd.CPUAccessFlags = CPU_ACCESS_FLAG.READ;
      var tmp = device.CreateBuffer(&bd);
      context.CopyResource(tmp, buffer);
      var map = context.Map(tmp, 0, MAP.READ, 0);
      memcpy(p, map.pData, (void*)bd.ByteWidth);
      context.Unmap(tmp, 0); Marshal.Release(tmp);
    }
    static bool equals(IntPtr buffer, void* p, int n)
    {
      BUFFER_DESC bd; bd.ByteWidth = n;
      bd.Usage = USAGE.STAGING; bd.CPUAccessFlags = CPU_ACCESS_FLAG.READ;
      var tmp = device.CreateBuffer(&bd);
      context.CopyResource(tmp, buffer);
      var map = context.Map(tmp, 0, MAP.READ, 0);
      n = memcmp(p, map.pData, (void*)n);
      context.Unmap(tmp, 0); Marshal.Release(tmp);
      return n == 0;
    }

    public abstract class Buffer<T> : Buffer where T : unmanaged
    {
      internal IntPtr buffer; //IBuffer 
      internal int count; //protected WeakReference tmp;
      protected override bool Equals(void* p, int n)
      {
        if (count == 0) return n == 0;
        if (n != count * sizeof(T)) return false;
        //if (tmp != null) if (tmp.Target is byte[] a) { fixed (byte* t = a) if (memcmp(t, p, (void*)n) == 0) return true; return false; } else tmp = null;
        if (!equals(buffer, p, n)) return false;
        //var b = new byte[n]; memcpy(b, p); tmp = new WeakReference(b);
        return true;
      }
      public override int GetData(void* p)
      {
        if (p != null && count != 0) copy(p, count * sizeof(T), buffer);
        return count * sizeof(T);
      }
      protected override void Dispose()
      {
        release(ref buffer); //tmp = null;
      }
      public T[] ToArray()
      {
        var a = new T[count]; fixed (T* p = a) GetData(p); return a;
      }
    }

    public class VertexBuffer : Buffer<vertex>
    {
      protected override void Init(void* p, int n)
      {
        Assert(buffer == IntPtr.Zero);
        if (n == 0) { n = 1; p = (byte*)&n + 2; }
        count = n / sizeof(vertex);
        buffer = CreateBuffer(p, n, BIND.VERTEX_BUFFER);
      }
      float3[] points;
      public float3[] GetPoints()
      {
        if (points == null)
        {
          var vv = (vertex*)StackPtr; var nv = GetData(vv) / sizeof(vertex);
          points = Enumerable.Range(0, nv).Select(i => vv[i].p).Distinct().ToArray();
        }
        return points;
      }
      protected override void Dispose()
      {
        base.Dispose(); points = null;
      }
    }

    public class IndexBuffer : Buffer<int>
    {
      protected override void Init(void* p, int n)
      {
        Assert(buffer == IntPtr.Zero);
        if (n == 0) { n = 1; p = (byte*)&n + 2; }
        count = n / sizeof(int);
        buffer = CreateBuffer(p, n, BIND.INDEX_BUFFER);
      }
    }

    public abstract class MemBuffer : Buffer
    {
      protected byte[] data;
      protected override void Dispose() { }
      protected override bool Equals(void* p, int n)
      {
        if (data.Length != n) return false;
        fixed (byte* t = data) return memcmp(t, p, (void*)n) == 0;
      }
      public override int GetData(void* p)
      {
        if (p != null) memcpy(p, data); return data.Length;
      }
      protected override void Init(void* p, int n)
      {
        memcpy(data != null && data.Length == n ? data : data = new byte[n], p);
      }
    }

    public class Texture : MemBuffer
    {
      internal IntPtr/*IShaderResourceView*/ srv; int info;
      //IShaderResourceView Srv { get { return (IShaderResourceView)Marshal.GetObjectForIUnknown(srv); } }
      public float2 Size
      {
        get { return new float2(info & 0xffff, info >> 16); }
      }
      public override string ToString()
      {
        if (data[0] == 0) fixed (byte* p = data) { ReadPtr = p + 1; return ReadString(); }
        return $"{info & 0xffff} x {info >> 16}"; // {Srv.Desc.Format}";
      }
      protected override void Init(void* p, int n)
      {
        base.Init(p, n);
        if (*(byte*)p == 0)
        {
          var t1 = ReadPtr; ReadPtr = (byte*)p + 1; var s = ReadString();
          if (s.Contains(':'))
            try { var a = s.StartsWith("data") ? Convert.FromBase64String(s.Substring(s.IndexOf(',') + 1)) : DownloadData(s); memcpy(StackPtr, a); n = a.Length; }
            catch (Exception e) { n = 0; Debug.WriteLine(e.Message + " " + s); }
          else n = ZipArc.GetDefault().Load(s);
          p = StackPtr; ReadPtr = t1; if (n == 0) return;
        }
        srv = CreateTexture((byte*)p, n, out info);
      }
      protected override void Dispose()
      {
        release(ref srv);
      }
      //public Bitmap GetBitmap() //todo: dds + native
      //{
      //  var p = StackPtr; var n = GetData(p);
      //  if (*p == 0) { var t1 = ReadPtr; ReadPtr = p + 1; n = ZipArc.GetDefault().Load(ReadString()); p = StackPtr; ReadPtr = t1; }
      //  using (var str = new UnmanagedMemoryStream(p, n)) return (Bitmap)Image.FromStream(str);
      //}
      public byte[] ToArray()
      {
        var x = StackPtr; var p = StackPtr; var n = GetData(p);
        if (*p == 0) { var t1 = ReadPtr; ReadPtr = p + 1; n = ZipArc.GetDefault().Load(ReadString()); p = StackPtr; ReadPtr = t1; }
        var a = new byte[n]; memcpy(a, p); Debug.Assert(x == StackPtr); return a;
      }
    }
    public static Texture GetTexture((string s, int i, int n) path)
    {
      var t1 = StackPtr; WriteCount(0); WriteString(path); var n = (int)(StackPtr - t1); StackPtr = t1;
      return (Texture)Buffer.GetBuffer(typeof(Texture), StackPtr, n);
    }
    public static Texture GetTexture(string path)
    {
      if (Path.IsPathRooted(path)) return (Texture)Buffer.GetBuffer(typeof(Texture), StackPtr, ReadFile(path));
      return GetTexture((path, 0, path.Length));
      //var t1 = StackPtr; WriteCount(0); WriteString(path); var n = (int)(StackPtr - t1); StackPtr = t1;
      //return (Texture)Buffer.GetBuffer(typeof(Texture), StackPtr, n);
    }
    public static Texture GetTexture(byte[] data)
    {
      fixed (byte* p = data) return (Texture)Buffer.GetBuffer(typeof(Texture), p, data.Length);
    }
    public static Texture GetTexture(Bitmap bmp)
    {
      var str = new UnmanagedMemoryStream(StackPtr, int.MaxValue, int.MaxValue, FileAccess.ReadWrite);
      bmp.Save(str, ImageFormat.Png);
      return (Texture)Buffer.GetBuffer(typeof(Texture), StackPtr, (int)str.Position);
    }
    public static Texture GetTexture(int dx, int dy, Func<Graphics, FORMAT> draw)
    {
      using (var bmp = new Bitmap(dx, dy, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
      {
        FORMAT fmt; using (var gr = Graphics.FromImage(bmp))
        {
          gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
          gr.SmoothingMode = SmoothingMode.AntiAlias;
          fmt = draw(gr);
        }
        if (fmt != 0)
        {
          var data = bmp.LockBits(new Rectangle(0, 0, dx, dy), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
          var ptr = (byte*)data.Scan0.ToPointer(); Assert(data.Stride == dx << 2);
          var n = data.Stride * dy;
          if (fmt == FORMAT.A8_UNORM) { for (int i = 3; i < n; i += 4) ptr[i >> 2] = ptr[i]; n >>= 2; }
          var i1 = StackPtr; var ii = (int*)i1; ii[0] = (int)fmt << 16; ii[1] = dx | (dy << 16);
          StackPtr = i1 + 8; StackPtr += Zip.Compress(ptr, n); bmp.UnlockBits(data);
          var tex = (Texture)Buffer.GetBuffer(typeof(Texture), ii, (int)(StackPtr - i1));
          StackPtr = i1; return tex;
        }
        return GetTexture(bmp);
      }
    }

    struct DDS_PIXELFORMAT
    {
      internal int Size, Flags, FourCC, RGBBitCount;
      internal uint RBitMask, GBitMask, BBitMask, ABitMask;
    };
    struct DDS_HEADER
    {
      internal int Size, Flags, Height, Width, PitchOrLinearSize, Depth, MipMapCount;
      internal fixed int Reserved1[11];
      internal DDS_PIXELFORMAT pf;
      internal int Caps, Caps2, Caps3, Caps4, Reserved2;
    }
    static IntPtr CreateTexture(byte* p, int n, out int info) //IShaderResourceView
    {
      if (*(ushort*)p == 0)
      {
        info = ((int*)p)[1]; ReadPtr = p + 8; Zip.Decompress();
        return CreateTexture(((ushort*)p)[2], ((ushort*)p)[3], 0, (FORMAT)((ushort*)p)[1], 1, StackPtr);
      }
      if (*(int*)p == 0x20534444) //DDS, only the compressed formats 
      {
        var dh = (DDS_HEADER*)(p + 4);
        info = dh->Width | (dh->Height << 16);
        int bpl = 0; FORMAT fmt = 0;
        switch (dh->pf.FourCC)
        {
          case 0x31545844: //DXT1
            fmt = FORMAT.BC1_UNORM; bpl = 8; break;
          case 0x32545844: //DXT2 pre-multiplied alpha 
          case 0x33545844: //DXT3
            fmt = FORMAT.BC2_UNORM; bpl = 16; break;
          case 0x34545844: //DXT4 pre-multiplied alpha 
          case 0x35545844: //DXT5
            fmt = FORMAT.BC3_UNORM; bpl = 16; break;
          case 0x42475247: //GRGB
            fmt = FORMAT.G8R8_G8B8_UNORM; bpl = 4; break;
          case 0x31495441: //ATI1
            fmt = FORMAT.BC4_UNORM; bpl = 8; break;
          case 0x32495441: //ATI2
            fmt = FORMAT.BC5_UNORM; bpl = 16; break;
          case 0x42475852: //RXGB
            fmt = FORMAT.BC5_UNORM; bpl = 16; break;
          //todo: BC6H
          //todo: BC7
          default: { var s = new string((sbyte*)&dh->pf.FourCC, 0, 4); throw new Exception($"{"FourCC"} {s}"); }
        }
        bpl = bpl == 4 ? ((dh->Width + 1) >> 1) * bpl : Math.Max(1, (dh->Width + 3) >> 2) * bpl;
        var ff = device.CheckFormatSupport(fmt);
        if ((ff & FORMAT_SUPPORT.TEXTURE2D) == 0) throw new Exception($"{"Hardware restriction"} {fmt}");
        return CreateTexture(dh->Width, dh->Height, bpl, fmt, 0, dh + 1);
      }

      using (var str = new UnmanagedMemoryStream(p, n))
      using (var bmp = Image.FromStream(str))
      {
        info = bmp.Width | (bmp.Height << 16);
        return CreateTexture((Bitmap)bmp);
      }
    }
    static IntPtr CreateTexture(Bitmap bmp, int flags = 0) //IShaderResourceView
    {
      int dx = bmp.Width, dy = bmp.Height;
      var pf = bmp.PixelFormat; FORMAT fmt;
      switch (pf)
      {
        case System.Drawing.Imaging.PixelFormat.Format32bppRgb: fmt = FORMAT.B8G8R8X8_UNORM; break;
        case System.Drawing.Imaging.PixelFormat.Format32bppArgb: fmt = FORMAT.B8G8R8A8_UNORM; if ((flags & 1) != 0) fmt = FORMAT.A8_UNORM; break;
        case System.Drawing.Imaging.PixelFormat.Format24bppRgb: fmt = FORMAT.B8G8R8X8_UNORM; pf = System.Drawing.Imaging.PixelFormat.Format32bppRgb; break;
        case System.Drawing.Imaging.PixelFormat.Format16bppRgb565: fmt = FORMAT.B5G6R5_UNORM; break;
        case System.Drawing.Imaging.PixelFormat.Format16bppRgb555: fmt = FORMAT.B5G6R5_UNORM; pf = System.Drawing.Imaging.PixelFormat.Format16bppRgb565; break;
        default: fmt = FORMAT.B8G8R8X8_UNORM; pf = System.Drawing.Imaging.PixelFormat.Format32bppRgb; break;
      }
      var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, dx, dy), System.Drawing.Imaging.ImageLockMode.ReadOnly, pf);
      var ptr = (byte*)data.Scan0.ToPointer(); var stride = data.Stride;
      if (fmt == FORMAT.A8_UNORM)
      {
        var p = ptr;
        for (int y = 0; y < dx; y++, p += stride)
          for (int x = 0; x < dy; x++) p[x] = p[(x << 2) + 3];
      }
      var srv = CreateTexture(dx, dy, stride, fmt, 1, ptr);
      bmp.UnlockBits(data); return srv;
    }
    static IntPtr CreateTexture(int dx, int dy, int pitch, FORMAT fmt, int mips, void* p) //IShaderResourceView
    {
      if (pitch == 0) pitch = fmt == FORMAT.A8_UNORM ? dx : dx << 2;
      TEXTURE2D_DESC td; SHADER_RESOURCE_VIEW_DESC rv;
      td.Width = dx;
      td.Height = dy;
      td.ArraySize = td.SampleDesc.Count = 1;// = td.MipLevels = 1;
      td.BindFlags = BIND.SHADER_RESOURCE;
      td.Format = fmt;
      if (mips != 0)
      {
        td.BindFlags |= BIND.RENDER_TARGET;
        td.MiscFlags = RESOURCE_MISC.GENERATE_MIPS;
      }
      else td.MipLevels = 1;
      var tex = device.CreateTexture2D(&td);
      context.UpdateSubresource(tex, 0, null, p, pitch, 0);
      td = ((ITexture2D)Marshal.GetObjectForIUnknown(tex)).Desc;
      rv.ViewDimension = SRV_DIMENSION.TEXTURE2D;
      rv.Format = td.Format;
      rv.Texture2D.MipLevels = td.MipLevels;
      var srv = device.CreateShaderResourceView(tex, &rv);
      release(ref tex);
      if (mips != 0) context.GenerateMips(srv);
      return srv;
    }

    public new class Font : MemBuffer
    {
      public string Name => name;
      public float Size
      {
        get { return size; }
        set { size = value; }
      }
      public FontStyle Style => style;
      public float Ascent => ascent * size;
      public float Descent => descent * size;
      public float Height => (ascent + descent) * size;
      public static float ExtraSpace;
      //public void PreInit(string s) { fixed (char* p = s) create(p, s.Length); }
      public int Measure(char* s, int n, ref float cx)
      {
        float x = 0, dx; int i = 0;
        for (; i < n; i++)
        {
          var c = s[i]; if (!dict.TryGetValue(c, out int j)) { create(s + i, n - i); j = dict[c]; }
          dx = glyphs[j].incx * size;
          if (i != 0) { if (kern.TryGetValue((((uint)c << 16) | s[i - 1]), out float k)) dx += k * size; }
          if (x + dx > cx) break; x += dx; if (c == ' ') x += ExtraSpace;
        }
        cx = x; return i;
      }
      public float Measure(char* s, int n)
      {
        float dx = float.MaxValue; Measure(s, n, ref dx); return dx;
      }
      public void Draw(IDisplay dc, float x, float y, char* s, int n)
      {
        if (inpick) return;
        var t1 = texture; texture = null;
        var topo = Topology.TriangleStrip;
        var f = 1 / size; x *= f; y *= f;
        for (int a = 0, b = 0, j, bsrv = -1, csrv = -1, nc = 0; ; b++)
        {
          if (b < n)
          {
            if (!dict.TryGetValue(s[b], out j)) { create(s + b, n - b); j = dict[s[b]]; }
            bsrv = glyphs[j].srv;
          }
          if (b == n || (bsrv != csrv && bsrv != -1) || nc == 64) //256 * sizeof(vertex) 8k blocks
          {
            if (nc != 0)
            {
              SetTexture(srvs[csrv]); //dc.TextureSrv = srvs[csrv];
              var vv = dc.BeginVertices(nc << 2);
              for (; a < b; a++)
              {
                if (a != 0) { if (kern.TryGetValue((((uint)s[a] << 16) | s[a - 1]), out float k)) x += k; }
                var pg = glyphs + dict[s[a]];
                if (pg->srv != -1)
                {
                  vv[0].p.x = vv[2].p.x = (x + pg->orgx) * size;
                  vv[0].p.y = vv[1].p.y = (y - pg->orgy) * size;
                  vv[1].p.x = vv[3].p.x = vv[0].p.x + pg->boxx * size;
                  vv[3].p.y = vv[2].p.y = vv[0].p.y + pg->boxy * size;
                  vv[0].t.x = vv[2].t.x = pg->x1;
                  vv[1].t.x = vv[3].t.x = pg->x2;
                  vv[2].t.y = vv[3].t.y = 1; vv += 4;
                }
                x += pg->incx; if (ExtraSpace != 0 && s[a] == ' ') x += ExtraSpace * f;
              }
              dc.EndVertices(nc << 2, topo); topo = 0;
            }
            if (b == n) break;
            if (bsrv != -1) csrv = bsrv; nc = 0;
          }
          if (bsrv != -1) nc++;
        }
        texture = t1;
      }

      string name; FontStyle style; float size, ascent, descent, scale; void* hfont;
      Dictionary<char, int> dict; Dictionary<uint, float> kern;
      struct Glyph { internal float boxx, boxy, orgx, orgy, incx, x1, x2; internal int srv; }
      Glyph* glyphs; int glyphn, glypha;
      IntPtr[] srvs; int srvn; /*D3D11.IShaderResourceView*/

      protected override void Dispose()
      {
        Marshal.FreeCoTaskMem((IntPtr)glyphs); glyphs = null;
        if (hfont != null) { DeleteObject(hfont); hfont = null; }
        release(ref srvs); srvs = null; dict = null;
      }
      protected override void Init(void* p, int n)
      {
        base.Init(p, n); var t1 = ReadPtr; ReadPtr = (byte*)p;
        name = ReadString(); float size; Read(&size, 4); style = (FontStyle)ReadCount(); ReadPtr = t1;
        this.size = size; size *= DpiScale; scale = 1f / size;
        fixed (char* s = name) hfont = CreateFontW(-(int)Math.Round(size * (96f / 72)), 0, 0, 0,
            (style & FontStyle.Bold) != 0 ? 700 : 400,
            (style & FontStyle.Italic) != 0 ? 1 : 0,
            (style & FontStyle.Underline) != 0 ? 1 : 0,
            (style & FontStyle.Strikeout) != 0 ? 1 : 0,
            1, //DEFAULT_CHARSET
            4, //OUT_TT_PRECIS
            0,
            5, //CLEARTYPE_QUALITY
            0, s);
        var po = SelectObject(hdcnull, hfont);
        int* textmetric = (int*)StackPtr;
        GetTextMetrics(hdcnull, textmetric);
        //height = textmetric[0] * scale;
        ascent = textmetric[1] * scale;
        descent = textmetric[2] * scale;
        //intlead = textmetric[3] * scale;
        //extlead = textmetric[4] * scale;
        var nk = GetKerningPairsW(hdcnull, 0, null) << 1;
        var kk = (uint*)StackPtr;
        var kr = GetKerningPairsW(hdcnull, nk >> 1, kk);
        kern = new Dictionary<uint, float>(); for (int i = 0, v; i < nk; i += 2) if ((v = *(int*)&kk[i + 1]) != 0) kern[kk[i]] = v * scale;
        SelectObject(hdcnull, po);
        dict = new Dictionary<char, int>(128);
      }
      void create(char* s, int n)
      {
        const int ex = 4;

        int nc = 0; var cc = (char*)StackPtr;
        for (int i = 0; i < n; i++) { var c = s[i]; if (!dict.ContainsKey(c)) { dict[c] = -1; cc[nc++] = c; } }
        if (nc == 0) return; var space = ' '; if ((style & (FontStyle.Underline | FontStyle.Strikeout)) != 0) { cc[nc++] = '|'; space--; }
        //for (int i = 0; i < nc; i++) Debug.Write(cc[i] + " "); Debug.WriteLine("");
        if (glyphs == null || glyphn + nc > glypha)
        {
          var cb = (glypha = (((glyphn + nc) >> 5) + 1) << 5) * sizeof(Glyph);
          glyphs = (Glyph*)Marshal.ReAllocCoTaskMem((IntPtr)glyphs, cb).ToPointer();// Native.realloc(glyphs, cb);
        }
        float4 ma; var m2 = (int*)&ma; m2[0] = m2[3] = 0x10000;
        var po = SelectObject(hdcnull, hfont);

        for (int i1 = 0, i2 = 0, gi = glyphn; i1 < nc; i1 = i2)
        {
          int mx = ex, my = 0;
          for (int i = i1; i < nc; i++, i2++)
          {
            var gm = (int*)&glyphs[gi + i]; GetGlyphOutlineW(hdcnull, cc[i], 0, gm, 0, null, m2); if (cc[i] <= space) continue;
            if (cc[i] == ' ') gm[0] = gm[4] & 0xffff; //dx = cellincx
            if (mx + gm[0] + (ex << 1) > 4096 && n > 1) break; //D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION (16384)
            mx += gm[0] + (ex << 1); my = Math.Max(my, gm[1] + (ex << 1));
          }

          if (my != 0)
          {
            if (srvs == null || srvn == srvs.Length) Array.Resize(ref srvs, srvn == 0 ? 4 : srvn << 1);

            byte* pp; float3x4 m12; var bi = (int*)&m12; bi[0] = 40; bi[1] = mx; bi[2] = -my; bi[3] = 1 | (32 << 16);
            var dib = CreateDIBSection(null, bi, 0, &pp, null, 0);
            var ddc = CreateCompatibleDC(hdcnull);
            var obmp = SelectObject(ddc, dib);
            var ofont = SelectObject(ddc, hfont);
            //int old = SetMapMode(ddc, 1); //MM_TEXT
            SetTextColor(ddc, 0x00ffffff); SetBkMode(ddc, 1); SetTextAlign(ddc, 24);
            for (int i = i1, x = ex; i < i2; i++)
            {
              var c = cc[i]; if (c <= space) continue; var gm = (int*)&glyphs[gi + i];
              TextOutW(ddc, x - gm[2], ex + gm[3], &c, 1); x += gm[0] + (ex << 1);
            }
            SelectObject(ddc, ofont);
            SelectObject(ddc, obmp);
            DeleteDC(ddc);

            //if(Height > 80) 50px
            //using (var bmp = new Bitmap(mx, my, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            //{
            //  var data = bmp.LockBits(new Rectangle(0, 0, mx, my), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            //  memcpy(data.Scan0.ToPointer(), pp, (void*)(mx * my * 4));
            //  bmp.UnlockBits(data);
            //  bmp.Save("C:\\Users\\cohle\\Desktop\\xxxx.png",System.Drawing.Imaging.ImageFormat.Png);
            //}

            for (int k = 0, nk = mx * my; k < nk; k++)
            {
              var t = (pp[(k << 2)] * 0x4c + pp[(k << 2) + 1] * 0x95 + pp[(k << 2) + 2] * 0x1f) >> 8; t = (t * t) >> 8; // t = (t * t) >> 8;
              pp[k] = (byte)t; //pp[k] = (byte)((pp[(k << 2)] * 0x4c + pp[(k << 2) + 1] * 0x95 + pp[(k << 2) + 2] * 0x1f) >> 8); //0x1e -> 0x1f
            }

            srvs[srvn++] = CreateTexture(mx, my, mx, FORMAT.A8_UNORM, 1, pp);
            DeleteObject(dib);
          }

          for (int i = i1, x = ex; i < i2; i++)
          {
            var c = cc[i]; dict[c] = glyphn;
            var gl = &glyphs[glyphn++]; var gm = (int*)gl;
            if (c > space)
            {
              gl->srv = srvn - 1;
              gl->x1 = (x - ex) / (float)mx;
              gl->x2 = (x + gm[0] + ex) / (float)mx;
              x += gm[0] + (ex << 1);
            }
            else gl->srv = -1;
            gl->boxx = (gm[0] + (ex << 1)) * scale;
            gl->boxy = my * scale;
            gl->orgx = (gm[2] - ex) * scale;
            gl->orgy = (gm[3] + ex) * scale;
            gl->incx = (gm[4] & 0xffff) * scale; if (c < ' ') gl->incx *= 0.5f;
          }
        }
        SelectObject(hdcnull, po);
      }
      //internal void glyphrun(Tess64 tess, string text, float flat)
      //{
      //  tess.SetNormal(); tess.SetWinding(Winding.Negative); tess.BeginBoundary(); tess.BeginPolygon();
      //  var po = SelectObject(hdcnull, hfont); const float f = (1.0f / 0x10000);
      //  float4 ma; var m2 = (int*)&ma; m2[0] = m2[3] = 0x10000; Glyph gm; float x = 0;
      //  for (int i = 0; i < text.Length; i++)
      //  {
      //    if (i != 0) { if (kern.TryGetValue((((uint)text[i] << 16) | text[i - 1]), out float k)) x += k * size; }
      //    var nc = GetGlyphOutlineW(hdcnull, text[i], 2, &gm, 1 << 20, StackPtr, m2); //GGO_NATIVE
      //    var vv = (float2*)(StackPtr + nc);
      //    for (var ph = StackPtr; ph - StackPtr < nc;) //TTPOLYGONHEADER 
      //    {
      //      var cb = ((int*)ph)[0]; //var dwType = ((int*)ph)[1]; //24
      //      tess.BeginContour();
      //      vv[0].x = x + ((int*)ph)[2] * f; vv[0].y = ((int*)ph)[3] * f; tess.AddVertex(vv[0]);
      //      for (var pc = ph + 16; pc - ph < cb;) //TTPOLYCURVE
      //      {
      //        var wType = ((ushort*)pc)[0]; //TT_PRIM_LINE 1, TT_PRIM_QSPLINE 2, TT_PRIM_CSPLINE 3
      //        var cpfx = ((ushort*)pc)[1]; var pp = (int*)(pc + 4);
      //        for (int t = 0; t < cpfx; t++) { vv[t + 1].x = x + pp[t << 1] * f; vv[t + 1].y = pp[(t << 1) + 1] * f; }
      //        if (wType == 2) tess.AddQSpline(vv, cpfx + 1, flat);
      //        else if (wType == 3) tess.AddCSpline(vv, cpfx + 1, flat);
      //        else for (int t = 0; t < cpfx; t++) tess.AddVertex(vv[t + 1]);
      //        vv[0] = vv[cpfx]; pc += 4 + (cpfx << 3);
      //      }
      //      tess.EndContour(); ph += cb;
      //    }
      //    x += *(int*)&gm.incx;
      //  }
      //  SelectObject(hdcnull, po);
      //  tess.EndPolygon(); tess.SetWinding(Winding.Positive); tess.EndBoundary(); tess.Compact();
      //}
    }

    public static Font GetFont((string s, int i, int n) name, float size, FontStyle style)
    {
      var t = StackPtr; WriteString(name); Write(&size, 4); WriteCount((int)style);
      var n = (int)(StackPtr - t); StackPtr = t;
      return (Font)Buffer.GetBuffer(typeof(Font), StackPtr, n);
    }
    public static Font GetFont(string name, float size, FontStyle style = 0)
    {
      return GetFont((name, 0, name.Length), size, style);
    }

    public static void GetMesh(ref VertexBuffer vertices, ref IndexBuffer indices, double3* pp, int np, ushort* ii, int ni, float smooth = 0, void* tex = null, int fl = 0)
    {
      var kk = (int*)StackPtr; var tt = kk + ni;
      var e = msb(ni); e = 1 << (e + 1); var w = e - 1; //if(e > 15) e = 15 // 64k?
      var dict = tt; memset(dict, 0, (void*)(e << 2));
      for (int i = ni - 1, m = 0b010010; i >= 0; i--, m = (m >> 1) | ((m & 1) << 5))
      {
        int j = i - (m & 3), k = j + ((m >> 2) & 3), v = j + ((m >> 1) & 3), h;
        dict[e] = k = ii[i] | (ii[k] << 16); dict[e + 1] = v;
        dict[e + 2] = dict[h = (k ^ ((k >> 16) * 31)) & w]; dict[h] = e; e += 3;
      }
      for (int i = 0, m = 0b100100; i < ni; i++, m = (m << 1) & 0b111111 | (m >> 5))
      {
        int j = i - (m & 3), k = (ii[j + ((m >> 2) & 3)]) | (ii[i] << 16), h = (k ^ ((k >> 16) * 31)) & w, t;
        for (t = dict[h]; t != 0; t = dict[t + 2]) if (dict[t] == k) { dict[t] = -1; break; }
        kk[i] = ii[t != 0 ? dict[t + 1] : j + ((m >> 1) & 3)];
      }
      var vv = (vertex*)tt;
      for (int i = 0; i < np; i++) { vv[i].p = (float3)pp[i]; vv[i].t.x = vv[i].t.y = 0; }
      /////////////////
      for (int i = 0; i < ni; i += 3)
      {
        var p1 = vv[ii[i + 0]].p;
        var p2 = vv[ii[i + 1]].p;
        var p3 = vv[ii[i + 2]].p;
        var no = normalize(p2 - p1 ^ p3 - p1); if (float.IsNaN(no.x)) no = 0.1f;
        for (int k = 0, j; k < 3; k++)
        {
          for (j = ii[i + k]; ;)
          {
            var c = *(int*)&vv[j].t.x; if (c == 0) { vv[j].n = no; *(int*)&vv[j].t.x = 1; break; }
            var nt = vv[j].n;
            if (dot((c == 1 ? nt : normalize(nt)) - no) <= smooth) { vv[j].n = no + nt; *(int*)&vv[j].t.x = c + 1; break; }
            var l = *(int*)&vv[j].t.y; if (l != 0) { j = l - 1; continue; }
            *(int*)&vv[j].t.y = np + 1; vv[np].p = vv[j].p; vv[np].t.x = vv[np].t.y = 0; j = np++;
          }
          kk[i + k] = (kk[i + k] << 16) | j;
        }
      }

      for (int i = 0; i < np; i++) { vv[i].n = normalize(vv[i].n); if ((fl & 1) != 0) mul(&vv[i].p, (float3x4*)tex, &vv[i].t); else *(long*)&vv[i].t = 0; }

      if ((fl & 2) != 0)
      {
        for (int i = 0, j; i < ni; i++)
        {
          var p = ((float2*)tex)[i];
          if (vv[j = kk[i] & 0xffff].t == p) continue;
          if (*(long*)&vv[j].t != 0)
          {
            vv[np] = vv[j];
            for (int t = i; t < ni; t++)
              if ((kk[t] & 0xffff) == j && ((float2*)tex)[t] == p)
                *(ushort*)&kk[t] = (ushort)(np & 0xffff); //14162
            j = np++;
          }
          vv[j].t = p;
        }
      }

      indices = (IndexBuffer)Buffer.GetBuffer(typeof(IndexBuffer), kk, ni * sizeof(int), indices);
      vertices = (VertexBuffer)Buffer.GetBuffer(typeof(VertexBuffer), vv, np * sizeof(vertex), vertices);
    }

    static class Zip
    {
      internal static byte[] Compress(byte[] a)
      {
        int n; fixed (byte* p = a) n = Compress(p, a.Length);
        memcpy(a = new byte[n], StackPtr); return a;
      }
      internal static byte[] Decompress(byte[] a)
      {
        var t = StackPtr; memcpy(t, a); StackPtr = t + a.Length;
        ReadPtr = t; int n = Decompress();
        memcpy(a = new byte[n], StackPtr); StackPtr = t; return a;
      }
      internal static int Compress(void* p, int n)
      {
        zstr str; int hr = deflateInit2_(&str, -1, 8, -15, 8, 0, ref zver, sizeof(zstr)); Assert(hr == 0);
        str.nextIn = p; str.availIn = n;
        str.nextOut = StackPtr; str.availOut = int.MaxValue;
        hr = deflate(&str, 4); Assert(hr == 1);
        hr = deflateEnd(&str); Assert(hr == 0);
        return str.totalOut;
      }
      internal static int Decompress()
      {
        zstr str; int hr = inflateInit2_(&str, -15, ref zver, sizeof(zstr)); Assert(hr == 0);
        str.nextIn = ReadPtr; str.availIn = int.MaxValue;
        str.nextOut = StackPtr; str.availOut = int.MaxValue;
        hr = inflate(&str, 0); if (hr != 1) throw new InvalidDataException();
        hr = inflateEnd(&str); ReadPtr += str.totalIn; return str.totalOut;
      }
      static long zver = 0x332e322e31;
      struct zstr
      {
        internal void* nextIn;
        internal int availIn, totalIn;
        internal void* nextOut;
        internal int availOut, totalOut;
        internal void* msg, state, zalloc, zfree, opaque;
        internal int dataType, adler, reserved;
      }
      //return: Ok, StreamEnd, NeedDictionary, ErrorNo = -1, StreamError = -2, DataError = -3, MemError = -4, BufError = -5, VersionError = -6
      //strategy: Filtered = 1, HuffmanOnly = 2, Rle = 3, Fixed = 4, Default = 0
      //level:  NoCompression = 0,  BestSpeed = 1, BestCompression = 9, Default = -1
      //flush: NoFlush = 0, PartialFlush = 1, SyncFlush = 2, FullFlush = 3, Finish = 4, Block = 5
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int deflateInit2_(zstr* p, int level, int method, int windowBits, int memLevel, int strategy, [In] ref long version, int streamSize);
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int deflate(zstr* p, int flush);
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int deflateEnd(zstr* p);
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int inflateInit2_(zstr* p, int windowBits, [In] ref long version, int streamSize);
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int inflate(zstr* p, int flush);
      [DllImport("clrcompression.dll", CallingConvention = CallingConvention.StdCall), SuppressUnmanagedCodeSecurity]
      extern static int inflateEnd(zstr* p);
      [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurity]
      static extern void* LoadLibrary(string s);
      static void* dll = LoadLibrary(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "clrcompression.dll"));
    }

    public class ZipArc : IDisposable
    {
      static WeakReference db = new WeakReference(null);
      public static ZipArc GetDefault()
      {
        var p = db.Target as ZipArc;
        if (p == null) db.Target = p = new ZipArc(Path.ChangeExtension(Application.ExecutablePath, "zip"));
        return p;
      }

      public ZipArc(string path)
      {
        //Debug.WriteLine("ZipArc " + path);
        fixed (char* p = path) file = CreateFileW(p, 0x80000000, FileShare.Read, null, FileMode.Open, 0x80, null);
        if (file == (void*)-1) throw new Exception($"{new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}\n{path}");
        int nh, nl = GetFileSize(file, &nh); len = nl;
        mmf = CreateFileMappingW(file, null, 0x2, 0, len, null); if (mmf == null) throw new Exception();
        ptr = MapViewOfFile(mmf, 0x4, 0, 0, 0); if (ptr == null) throw new Exception();
      }

      //Tuple<string> entry(ref int x)
      //{
      //  if (ptr == null) return null;
      //  int nc = *(ushort*)(ptr + len - 12); if (x >= nc) return null;
      //  int ic = *(int*)(ptr + len - 6);
      //  var rec = ptr + ic;
      //  for (int i = 0;; i++)
      //  {
      //    var ns = *(ushort*)(rec + 0x001C);
      //    if(i == x)
      //    {
      //      var ss = new string((sbyte*)(rec + 0x002E), 0, ns, encoding);
      //      return new Tuple<string>(ss);
      //    }
      //    rec += 0x002E + ns + *(ushort*)(rec + 0x001E) + *(ushort*)(rec + 0x0020);
      //  }
      //}
      Tuple<string> find(ref int it)
      {
        if (it == 0) it = *(int*)(ptr + len - 6); if (it >= len - 22) return null;
        var rec = ptr + it; var ns = *(ushort*)(rec + 0x001C);
        it += 0x002E + ns + *(ushort*)(rec + 0x001E) + *(ushort*)(rec + 0x0020);
        return new Tuple<string>(new string((sbyte*)(rec + 0x002E), 0, ns, encoding));
      }

      public IEnumerable<Tuple<string>> Entries
      {
        get { for (int i = 0; ;) { var p = find(ref i); if (p == null) break; yield return p; } }
      }

      public int Load(string name)
      {
        int nn; fixed (char* ps = name) nn = encoding.GetBytes(ps, name.Length, StackPtr, 1024);
        int nc = *(ushort*)(ptr + len - 12);
        int ic = *(int*)(ptr + len - 6);
        var rec = ptr + ic;
        for (int i = 0; i < nc; i++)
        {
          var ns = *(ushort*)(rec + 0x001C); //var ss = new string((sbyte*)(rec + 0x002E), 0, ns);
          var ok = ns == name.Length;
          if (ns == nn && memcmp((sbyte*)(rec + 0x002E), StackPtr, (void*)ns) == 0)
          {
            var loc = *(uint*)(rec + 0x002A);
            var dat = ptr + loc;
            var cmp = *(ushort*)(dat + 0x0008); var l1 = *(uint*)(dat + 0x0012); var l2 = *(uint*)(dat + 0x0016);
            dat += 0x001E + *(ushort*)(dat + 0x001A) + *(ushort*)(dat + 0x001C);
            if (cmp == 0) { memcpy(StackPtr, dat, (void*)l2); return (int)l2; }
            else if (cmp == 8) { ReadPtr = dat; return Zip.Decompress(); }
            else return -1;
          }
          rec += 0x002E + ns + *(ushort*)(rec + 0x001E) + *(ushort*)(rec + 0x0020);
        }
        throw new FileNotFoundException(name);
      }
      public void Dispose()
      {
        //Debug.WriteLine("ZipArc " + "Dispose");
        if (ptr != null) { var ok = UnmapViewOfFile(ptr); ptr = null; }
        if (mmf != null) { var ok = CloseHandle(mmf); mmf = null; }
        if (file != (void*)-1) { var ok = CloseHandle(file); file = (void*)-1; }
      }
      ~ZipArc() { Dispose(); }
      void* file, mmf; byte* ptr; int len;
      [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
      static extern void* CreateFileMappingW(void* hFile, void* lpFileMappingAttributes, int flProtect, int dwMaximumSizeHigh, int dwMaximumSizeLow, void* lpName);
      [DllImport("Kernel32", SetLastError = true), SuppressUnmanagedCodeSecurity]
      static extern byte* MapViewOfFile(void* hFileMapping, int dwDesiredAccess, int dwFileOffsetHigh, int dwFileOffsetLow, int dwNumberOfBytesToMap);
      [DllImport("kernel32"), SuppressUnmanagedCodeSecurity]
      static extern bool UnmapViewOfFile(void* p);
      static System.Text.Encoding encoding = System.Text.Encoding.GetEncoding(437);
    }

    static void Read(void* p, int n)
    {
      memcpy(p, ReadPtr, (void*)n); ReadPtr += n;
    }
    static void Write(void* p, int n)
    {
      memcpy(StackPtr, p, (void*)n); StackPtr += n;
    }
    static void WriteCount(int c)
    {
      for (; c >= 0x80; c >>= 7) *StackPtr++ = (byte)(c | 0x80); *StackPtr++ = (byte)c;
    }
    static int ReadCount()
    {
      int c = 0; for (int shift = 0; ; shift += 7) { var b = *ReadPtr++; c |= (b & 0x7F) << shift; if ((b & 0x80) == 0) break; }
      return c;
    }
    static void WriteString(string s)
    {
      if (s == null) { WriteCount(0); return; }
      var n = s.Length; WriteCount(n + 1); for (int i = 0; i < n; i++) WriteCount(s[i]);
    }
    static void WriteString((string s, int i, int n) s)
    {
      WriteCount(s.n + 1); for (int i = 0; i < s.n; i++) WriteCount(s.s[s.i + i]);
    }
    static string ReadString()
    {
      int n = ReadCount() - 1;
      if (n == -1) return null;
      if (n == 0) return string.Empty;
      var s = new string((char)0, n);
      fixed (char* t = s) for (int i = 0; i < n; i++) t[i] = (char)ReadCount();
      return s;
    }
    static int ReadFile(string path)
    {
      void* h; fixed (char* p = path) h = CreateFileW(p, 0x80000000, FileShare.Read, null, FileMode.Open, 0x80, null);
      if (h == (void*)-1) throw new Exception($"{new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}\n{path}");
      int nh, nl = GetFileSize(h, &nh); if (nh != 0 || nl < 0) { CloseHandle(h); throw new Exception(); }
      if (nl > maxstack - (StackPtr - baseptr)) throw new OutOfMemoryException();
      int nr = ReadFile(h, StackPtr, nl, null, null);
      CloseHandle(h); if (nr != 1) throw new Exception(); return nl;
    }
    static void WriteFile(string path, void* pp, int np)
    {
      void* h; fixed (char* p = path) h = CreateFileW(p, 0x40000000, FileShare.None, null, FileMode.Create, 0x80, null);
      if (h == (void*)-1) throw new Exception($"{new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}\n{path}");
      int nr = WriteFile(h, pp, np, null, null);
      CloseHandle(h); if (nr != 1) throw new Exception($"{new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}\n{path}");
    }
    public static void WriteFile(string path, Action<IArchive> ar)
    {
      var p = StackPtr; try
      {
        if (path == null) StackPtr += 4; var a = new Archive(true); ar(a); a.Compress = false; var c = (int)(StackPtr - p);
        if (path == null) *(int*)p = c - 4; else WriteFile(path, p, c);
      }
      finally { StackPtr = p; }
    }
    public static void ReadFile(string path, Action<IArchive> ar)
    {
      var p = StackPtr; try
      {
        byte* endptr;
        if (path == null) { endptr = StackPtr += 4 + *(int*)StackPtr; ReadPtr = p + 4; }
        else { endptr = StackPtr += Path.IsPathRooted(path) ? ReadFile(path) : ZipArc.GetDefault().Load(path); ReadPtr = p; }
        var a = new Archive(false); ar(a); a.Compress = false;
        Debug.WriteLineIf(ReadPtr - endptr < -3 || ReadPtr - endptr > 0, $"StackPtr - ReadPtr {ReadPtr - endptr}");
      }
      finally { StackPtr = p; }
    }

    class Archive : IArchive
    {
      internal Archive(bool storing)
      {
        if (this.storing = storing)
          o2i = new Dictionary<object, int>(512);
        else
          i2o = new List<object>(512);
      }
      bool storing; Dictionary<object, int> o2i; List<object> i2o;
      byte* p1, p2; static Type[] types;

      public uint Version { get; set; }
      public bool IsStoring
      {
        get { return storing; }
      }
      public void Serialize<T>(ref T value)
      {
        if (storing) type<T>.Serialize(value, this);
        else value = type<T>.Serialize(value, this);
      }
      public bool Compress
      {
        get { return p1 != null; }
        set
        {
          if (Compress == value) return;
          if (storing)
          {
            if (value) p1 = StackPtr;
            else { var c = (int)(StackPtr - p1); StackPtr = p1; StackPtr += Zip.Compress(p1, c); Debug.WriteLine($"compress: {c} -> {StackPtr - p1}"); p1 = null; }
          }
          else
          {
            if (value) { p2 = StackPtr; StackPtr += Zip.Decompress(); p1 = ReadPtr; ReadPtr = p2; }
            else { ReadPtr = p1; StackPtr = p2; p1 = null; }
          }
        }
      }

      static int SizeOf(Type t)
      {
        if (!t.IsValueType) return -1;
        if (t.IsEnum) return SizeOf(t.GetEnumUnderlyingType());
        if (!t.IsLayoutSequential) return -1; //{ if (t == typeof(DateTime)) return sizeof(long); return -1; }
        if (!IsPrimitive(t)) return -1;
        return Marshal.SizeOf(t);
      }
      static bool IsPrimitive(Type type)
      {
        if (type.IsPrimitive) return true; if (!type.IsValueType) return false; //if (!type.IsLayoutSequential) return false;
        var a = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < a.Length; i++) if (!IsPrimitive(a[i].FieldType)) return false; return true;
      }

      static Func<T[], Archive, T[]> make<T>()
      {
        return (v, ar) =>
        {
          int n; if (ar.storing) WriteCount((n = (v != null) ? v.Length : -1) + 1);
          else v = (n = ReadCount() - 1) >= 0 ? new T[n] : null; if (n <= 0) return v;
          for (int i = 0; i < n; i++) ar.Serialize(ref v[i]);
          return v;
        };
      }

      static Delegate f3(Type type)
      {
        //works for readonly struct too
        var dm = new DynamicMethod(String.Empty, type, new Type[] { type, typeof(Archive) }, typeof(Archive).Module, true);
        var il = dm.GetILGenerator(); MethodInfo me = null;
        var ff = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < ff.Length; i++)
        {
          il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, ff[i]);
          il.Emit(OpCodes.Call, i != 0 && ff[i - 1].FieldType == ff[i].FieldType ? me :
            (me = typeof(Archive).GetMethod(nameof(Serialize)).MakeGenericMethod(ff[i].FieldType)));
        }
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ret);
        return dm.CreateDelegate(Expression.GetFuncType(type, typeof(Archive), type));

        //var ff = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        //var p1 = Expression.Parameter(typeof(Archive)); var p2 = Expression.Parameter(type);
        //var t2 = Expression.Block(ff.Select(fi => Expression.Call(p1, typeof(Archive).GetMethod(nameof(Serialize)).MakeGenericMethod(new Type[] { fi.FieldType }), Expression.Field(p2, fi))));
        //var t4 = Expression.Lambda(Expression.GetFuncType(type, typeof(Archive), type), Expression.Block(t2, p2), p2, p1);
        //return t4.Compile();
      }

      static class type<T>
      {
        internal static readonly Func<T, Archive, T> Serialize;
        static type()
        {
          var t = typeof(T); var size = SizeOf(t);
          if (size != -1) { Serialize = (v, ar) => { var r = __makeref(v); var p = *((void**)&r); if (ar.storing) Write(p, size); else Read(p, size); return v; }; return; }
          if (t.IsValueType) { Serialize = (Func<T, Archive, T>)f3(t); return; }
          if (t.IsArray)
          {
            var e = t.GetElementType(); size = SizeOf(e);
            if (size != -1)
            {
              Serialize = (v, ar) =>
              {
                int n; if (ar.storing) WriteCount((n = (v != null) ? (v as Array).Length : -1) + 1);
                else v = (n = ReadCount() - 1) >= 0 ? (T)(object)Array.CreateInstance(e, n) : default(T); if (n <= 0) return v;
                var h = GCHandle.Alloc(v, GCHandleType.Pinned); var p = (byte*)h.AddrOfPinnedObject(); n *= size;
                if (ar.storing) Write(p, n); else Read(p, n); h.Free(); return v;
              };
              return;
            }
            Serialize = (Func<T, Archive, T>)typeof(Archive).GetMethod(nameof(make), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(e).Invoke(null, null);
            return;
          }
          if (t == typeof(string)) { Serialize = (v, ar) => { if (ar.storing) WriteString((string)(object)v); else v = (T)(object)ReadString(); return v; }; return; }
          if (typeof(Buffer).IsAssignableFrom(t))
          {
            Serialize = (value, ar) =>
            {
              if (ar.storing)
              {
                if (!(value is Buffer v)) { WriteCount(0); return value; }
                if (ar.o2i.TryGetValue(v, out int id)) { WriteCount(2 + id); return value; }
                WriteCount(1); ar.o2i.Add(v, ar.o2i.Count); WriteCount(v.GetData(null)); StackPtr += v.GetData(StackPtr); //if (v is AppBuffer b) b.Serialize(ar);
              }
              else
              {
                var i = ReadCount(); if (i == 0) return default;
                if (i >= 2) { value = (T)ar.i2o[i - 2]; (value as Buffer).AddRef(); return value; }
                var n = ReadCount(); value = (T)(object)Buffer.GetBuffer(typeof(T), ReadPtr, n); ReadPtr += n; ar.i2o.Add(value); //if ((object)value is AppBuffer b) b.Serialize(ar);
              }
              return value;
            };
            return;
          }
          if (t == typeof(object))
          {
            Serialize = (v, ar) =>
            {
              if (ar.storing)
              {
                { if (v is Font p) { WriteCount(1); ar.Serialize(ref p); return v; } }
                { if (v is Texture p) { WriteCount(2); ar.Serialize(ref p); return v; } }
                Debug.Assert(false);
              }
              else
                switch (ReadCount())
                {
                  case 1: { Font p = null; ar.Serialize(ref p); return (T)(object)p; }
                  case 2: { Texture p = null; ar.Serialize(ref p); return (T)(object)p; }
                }
              return v;
            };
            return;
          }
          {
            if (types == null)
            {
              var r = t; for (; r.BaseType != typeof(object); r = r.BaseType) ;
              types = (Type[])r.GetMethod("GetTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
            }
            var inner = (Action<T, Archive>)Delegate.CreateDelegate(typeof(Action<T, Archive>), t.GetMethod("Serialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Serialize = (v, ar) =>
            {
              if (ar.storing)
              {
                if (v == null) { WriteCount(0); return v; }
                if (ar.o2i.TryGetValue(v, out int id)) { WriteCount(2 + id); return v; }
                WriteCount(1); WriteCount(id = Array.IndexOf(types, v.GetType())); Assert(id >= 0);
                ar.o2i.Add(v, ar.o2i.Count);
              }
              else
              {
                var i = ReadCount();
                if (i == 0) return default(T);
                if (i >= 2) return (T)ar.i2o[i - 2];
                ar.i2o.Add(v = (T)Activator.CreateInstance(types[ReadCount()]));
              }
              inner(v, ar); return v;
            };
          }
        }
      }
    }

    static WeakReference webc;
    public static byte[] DownloadData(string s)
    {
      var wc = webc != null ? webc.Target as System.Net.WebClient : null;
      if (wc == null) webc = new WeakReference(wc = new System.Net.WebClient());
      return wc.DownloadData(s);
    }
  }

  unsafe partial class D3DView
  {
    [DllImport("d3d11.dll"), SuppressUnmanagedCodeSecurity]
    static extern int D3D11CreateDevice(IAdapter Adapter, D3D_DRIVER_TYPE DriverType, void* Software, CREATE_DEVICE_FLAG Flags, FEATURE_LEVEL* pFeatureLevels, int FeatureLevels, SDK_VERSION SDKVersion, out IDevice Device, FEATURE_LEVEL* Level, out IDeviceContext ImmediateContext);
    [DllImport("dxgi.dll"), SuppressUnmanagedCodeSecurity]
    static extern int CreateDXGIFactory([In, MarshalAs(UnmanagedType.LPStruct)] Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object unk);

    [ComImport, Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IDevice //: IDXGIDevice
    {
      IntPtr CreateBuffer(BUFFER_DESC* Desc, SUBRESOURCE_DATA* pInitialData = null); //IBuffer
      void dummy2();
      //virtual HRESULT STDMETHODCALLTYPE CreateTexture1D( 
      //    /* [annotation] */
      //    __in  const D3D11_TEXTURE1D_DESC *pDesc,
      //    /* [annotation] */ 
      //    __in_xcount_opt(pDesc->MipLevels * pDesc->ArraySize)  const D3D11_SUBRESOURCE_DATA *pInitialData,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Texture1D **ppTexture1D) = 0;
      IntPtr CreateTexture2D(TEXTURE2D_DESC* Desc, SUBRESOURCE_DATA* pInitialData = null); //ITexture2D
      void dummy4();
      //virtual HRESULT STDMETHODCALLTYPE CreateTexture3D( 
      //    /* [annotation] */ 
      //    __in  const D3D11_TEXTURE3D_DESC *pDesc,
      //    /* [annotation] */ 
      //    __in_xcount_opt(pDesc->MipLevels)  const D3D11_SUBRESOURCE_DATA *pInitialData,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Texture3D **ppTexture3D) = 0;
      IntPtr CreateShaderResourceView(/*IResource*/IntPtr Resource, SHADER_RESOURCE_VIEW_DESC* Desc); //IShaderResourceView
      IntPtr CreateUnorderedAccessView(IResource pResource, UNORDERED_ACCESS_VIEW_DESC* Desc); //IUnorderedAccessView
      IntPtr CreateRenderTargetView(/*IResource*/IntPtr pResource, RENDER_TARGET_VIEW_DESC* Desc); //IRenderTargetView
      IntPtr CreateDepthStencilView(/*IResource*/IntPtr pResource, DEPTH_STENCIL_VIEW_DESC* Desc); //IDepthStencilView
      IntPtr CreateInputLayout([MarshalAs(UnmanagedType.LPArray)] INPUT_ELEMENT_DESC[] pInputElementDescs, int NumElements, void* ShaderBytecodeWithInputSignature, UIntPtr BytecodeLength); //IInputLayout
      IntPtr CreateVertexShader(void* pShaderBytecode, UIntPtr BytecodeLength, void*/*IClassLinkage*/ ClassLinkage = null); //IVertexShader
      IntPtr CreateGeometryShader(void* pShaderBytecode, UIntPtr BytecodeLength, void*/*IClassLinkage*/ ClassLinkage = null); //IGeometryShader
      void dummy12();
      //virtual HRESULT STDMETHODCALLTYPE CreateGeometryShaderWithStreamOutput( 
      //    /* [annotation] */ 
      //    __in  const void *pShaderBytecode,
      //    /* [annotation] */ 
      //    __in  SIZE_T BytecodeLength,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumEntries)  const D3D11_SO_DECLARATION_ENTRY *pSODeclaration,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SO_STREAM_COUNT * D3D11_SO_OUTPUT_COMPONENT_COUNT )  UINT NumEntries,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumStrides)  const UINT *pBufferStrides,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SO_BUFFER_SLOT_COUNT )  UINT NumStrides,
      //    /* [annotation] */ 
      //    __in  UINT RasterizedStream,
      //    /* [annotation] */ 
      //    __in_opt  ID3D11ClassLinkage *pClassLinkage,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11GeometryShader **ppGeometryShader) = 0;
      IntPtr CreatePixelShader(void* ShaderBytecode, UIntPtr BytecodeLength, /*IClassLinkage*/void* ClassLinkage = null); //IPixelShader
      void dummy14();
      //virtual HRESULT STDMETHODCALLTYPE CreateHullShader( 
      //    /* [annotation] */ 
      //    __in  const void *pShaderBytecode,
      //    /* [annotation] */ 
      //    __in  SIZE_T BytecodeLength,
      //    /* [annotation] */ 
      //    __in_opt  ID3D11ClassLinkage *pClassLinkage,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11HullShader **ppHullShader) = 0;
      void dummy15();
      //virtual HRESULT STDMETHODCALLTYPE CreateDomainShader( 
      //    /* [annotation] */ 
      //    __in  const void *pShaderBytecode,
      //    /* [annotation] */ 
      //    __in  SIZE_T BytecodeLength,
      //    /* [annotation] */ 
      //    __in_opt  ID3D11ClassLinkage *pClassLinkage,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11DomainShader **ppDomainShader) = 0;
      IntPtr CreateComputeShader(void* ShaderBytecode, UIntPtr BytecodeLength, /*IClassLinkage*/void* ClassLinkage = null); //IComputeShader
      void dummy17();
      //virtual HRESULT STDMETHODCALLTYPE CreateClassLinkage( 
      //    /* [annotation] */ 
      //    __out  ID3D11ClassLinkage **ppLinkage) = 0;
      IntPtr CreateBlendState(BLEND_DESC* BlendStateDesc); //IBlendState
      IntPtr CreateDepthStencilState(DEPTH_STENCIL_DESC* DepthStencilDesc); //IDepthStencilState
      IntPtr CreateRasterizerState(RASTERIZER_DESC* RasterizerDesc); //IRasterizerState
      IntPtr CreateSamplerState(SAMPLER_DESC* SamplerDesc); //ISamplerState
      void dummy22();
      //virtual HRESULT STDMETHODCALLTYPE CreateQuery( 
      //    /* [annotation] */ 
      //    __in  const D3D11_QUERY_DESC *pQueryDesc,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Query **ppQuery) = 0;
      void dummy23();
      //virtual HRESULT STDMETHODCALLTYPE CreatePredicate( 
      //    /* [annotation] */ 
      //    __in  const D3D11_QUERY_DESC *pPredicateDesc,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Predicate **ppPredicate) = 0;
      void dummy24();
      //virtual HRESULT STDMETHODCALLTYPE CreateCounter( 
      //    /* [annotation] */ 
      //    __in  const D3D11_COUNTER_DESC *pCounterDesc,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Counter **ppCounter) = 0;
      IDeviceContext CreateDeferredContext(int ContextFlags); //not SingleThreaded
      void dummy26();
      //virtual HRESULT STDMETHODCALLTYPE OpenSharedResource( 
      //    /* [annotation] */ 
      //    __in  HANDLE hResource,
      //    /* [annotation] */ 
      //    __in  REFIID ReturnedInterface,
      //    /* [annotation] */ 
      //    __out_opt  void **ppResource) = 0;
      FORMAT_SUPPORT CheckFormatSupport(FORMAT Format);
      [PreserveSig]
      int CheckMultisampleQualityLevels(FORMAT Format, int SampleCount, out int NumQualityLevels);
      void dummy29();
      //virtual void STDMETHODCALLTYPE CheckCounterInfo( 
      //    /* [annotation] */ 
      //    __out  D3D11_COUNTER_INFO *pCounterInfo) = 0;
      void dummy30();
      //virtual HRESULT STDMETHODCALLTYPE CheckCounter( 
      //    /* [annotation] */ 
      //    __in  const D3D11_COUNTER_DESC *pDesc,
      //    /* [annotation] */ 
      //    __out  D3D11_COUNTER_TYPE *pType,
      //    /* [annotation] */ 
      //    __out  UINT *pActiveCounters,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNameLength)  LPSTR szName,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNameLength,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pUnitsLength)  LPSTR szUnits,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pUnitsLength,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pDescriptionLength)  LPSTR szDescription,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pDescriptionLength) = 0;
      void dummy31();
      //virtual HRESULT STDMETHODCALLTYPE CheckFeatureSupport( 
      //    D3D11_FEATURE Feature,
      //    /* [annotation] */ 
      //    __out_bcount(FeatureSupportDataSize)  void *pFeatureSupportData,
      //    UINT FeatureSupportDataSize) = 0;
      void dummy32();
      //virtual HRESULT STDMETHODCALLTYPE GetPrivateData( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __inout  UINT *pDataSize,
      //    /* [annotation] */ 
      //    __out_bcount_opt(*pDataSize)  void *pData) = 0;
      void dummy33();
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateData( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __in  UINT DataSize,
      //    /* [annotation] */ 
      //    __in_bcount_opt(DataSize)  const void *pData) = 0;
      void dummy34();
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateDataInterface( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __in_opt  const IUnknown *pData) = 0;
      FEATURE_LEVEL FeatureLevel { [PreserveSig] get; }
      uint CreationFlags { [PreserveSig] get; }
      [PreserveSig]
      int GetDeviceRemovedReason();
      void dummy38();
      //virtual void STDMETHODCALLTYPE GetImmediateContext( 
      //    /* [annotation] */ 
      //    __out  ID3D11DeviceContext **ppImmediateContext) = 0;
      uint ExceptionMode { set; [PreserveSig] get; }
    }

    enum DEPTH_WRITE_MASK { ZERO = 0, ALL = 1 }
    enum COMPARISON { NEVER = 1, LESS = 2, EQUAL = 3, LESS_EQUAL = 4, GREATER = 5, NOT_EQUAL = 6, GREATER_EQUAL = 7, ALWAYS = 8 }
    enum STENCIL_OP { KEEP = 1, ZERO = 2, REPLACE = 3, INCR_SAT = 4, DECR_SAT = 5, INVERT = 6, INCR = 7, DECR = 8 }

    struct DEPTH_STENCILOP_DESC
    {
      public STENCIL_OP StencilFailOp;
      public STENCIL_OP StencilDepthFailOp;
      public STENCIL_OP StencilPassOp;
      public COMPARISON StencilFunc;
    }
    struct DEPTH_STENCIL_DESC
    {
      public int DepthEnable;
      public DEPTH_WRITE_MASK DepthWriteMask;
      public COMPARISON DepthFunc;
      public int StencilEnable;
      public byte StencilReadMask;
      public byte StencilWriteMask;
      public DEPTH_STENCILOP_DESC FrontFace;
      public DEPTH_STENCILOP_DESC BackFace;
    }
    struct SUBRESOURCE_DATA
    {
      public void* pSysMem;
      public int SysMemPitch;
      public int SysMemSlicePitch;
    }

    enum USAGE { DEFAULT = 0, IMMUTABLE = 1, DYNAMIC = 2, STAGING = 3 }
    enum BIND
    {
      VERTEX_BUFFER = 0x1,
      INDEX_BUFFER = 0x2,
      CONSTANT_BUFFER = 0x4,
      SHADER_RESOURCE = 0x8,
      STREAM_OUTPUT = 0x10,
      RENDER_TARGET = 0x20,
      DEPTH_STENCIL = 0x40,
      UNORDERED_ACCESS = 0x80
    }
    enum CPU_ACCESS_FLAG { WRITE = 0x10000, READ = 0x20000 }

    struct BUFFER_DESC
    {
      public int ByteWidth;
      public USAGE Usage;
      public BIND BindFlags;
      public CPU_ACCESS_FLAG CPUAccessFlags;
      public RESOURCE_MISC MiscFlags;
      public int StructureByteStride;
    }

    [ComImport, Guid("1841e5c8-16b0-489b-bcc8-44cfb0d5deae"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IDeviceChild
    {
      IDevice Device { get; }
      //virtual HRESULT STDMETHODCALLTYPE GetPrivateData( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __inout  UINT *pDataSize,
      //    /* [annotation] */ 
      //    __out_bcount_opt( *pDataSize )  void *pData) = 0;
      //
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateData( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __in  UINT DataSize,
      //    /* [annotation] */ 
      //    __in_bcount_opt( DataSize )  const void *pData) = 0;
      //
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateDataInterface( 
      //    /* [annotation] */ 
      //    __in  REFGUID guid,
      //    /* [annotation] */ 
      //    __in_opt  const IUnknown *pData) = 0;     
    }

    //[ComImport, Guid("b0e06fe0-8192-4e1a-b1ca-36d7414710b2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //interface IShaderResourceView : IView { void _VtblGap1_5(); SHADER_RESOURCE_VIEW_DESC Desc { get; } }
    //[ComImport, Guid("3b301d64-d678-4289-8897-22f8928b72f3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IVertexShader : IDeviceChild { }
    //[ComImport, Guid("ea82e40d-51dc-4f33-93d4-db7c9125ae8c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IPixelShader : IDeviceChild { }
    //[ComImport, Guid("38325b96-effb-4022-ba02-2e795b70275c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IGeometryShader : IDeviceChild { }
    //[ComImport, Guid("4f5b196e-c2bd-495e-bd01-1fded38e4969"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IComputeShader : IDeviceChild { }
    //[ComImport, Guid("e4819ddc-4cf0-4025-bd26-5de82a3e07b7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IInputLayout : IDeviceChild { }//void _VtblGap1_4(); }
    //[ComImport, Guid("48570b85-d1ee-4fcd-a250-eb350722b037"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IBuffer : IResource { void _VtblGap1_7(); BUFFER_DESC Desc { get; } }
    //[ComImport, Guid("ddf57cba-9543-46e4-a12b-f207a0fe7fed"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IClassLinkage : IDeviceChild { void _VtblGap1_4(); }
    //[ComImport, Guid("75b68faa-347d-4159-8f45-a0640f01cd9a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IBlendState : IDeviceChild { void _VtblGap1_4(); BLEND_DESC Desc { get; } }
    //[ComImport, Guid("da6fea51-564c-4487-9810-f0d0f9b4e3a5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface ISamplerState : IDeviceChild { void _VtblGap1_4(); SAMPLER_DESC Desc { get; } } 
    //[ComImport, Guid("03823efb-8d8f-4e1c-9aa2-f64bb2cbfdf1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IDepthStencilState : IDeviceChild { void _VtblGap1_4(); DEPTH_STENCIL_DESC Desc { get; } }
    //[ComImport, Guid("9bb4ab81-ab1a-4d8f-b506-fc04200b6ee7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IRasterizerState : IDeviceChild { void _VtblGap1_4(); RASTERIZER_DESC Desc { get; } }
    //[ComImport, Guid("dfdba067-0b8d-4865-875b-d7b4516cc164"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IRenderTargetView : IView { void _VtblGap1_5(); RENDER_TARGET_VIEW_DESC Desc { get; } }
    //[ComImport, Guid("9fdac92a-1876-48c3-afad-25b94f84a9b6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IDepthStencilView : IView { void _VtblGap1_5(); DEPTH_STENCIL_VIEW_DESC Desc { get; } }
    //[ComImport, Guid("28acf509-7f5c-48f6-8611-f316010a6380"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    //public interface IUnorderedAccessView : IView { };

    enum INPUT_CLASSIFICATION
    {
      PER_VERTEX_DATA = 0,
      PER_INSTANCE_DATA = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT_ELEMENT_DESC
    {
      [MarshalAs(UnmanagedType.LPStr)]
      public string SemanticName;
      public int SemanticIndex;
      public FORMAT Format;
      public int InputSlot;
      public int AlignedByteOffset;
      public INPUT_CLASSIFICATION InputSlotClass;
      public int InstanceDataStepRate;
    }

    struct VIEWPORT
    {
      public float TopLeftX, TopLeftY, Width, Height, MinDepth, MaxDepth;
    }

    [ComImport, Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IDeviceContext : IDeviceChild
    {
      void _VtblGap1_4();
      [PreserveSig]
      void VSSetConstantBuffers(int StartSlot, int NumBuffers, [In] ref IntPtr buffer); //IBuffer
      [PreserveSig]
      void PSSetShaderResources(int StartSlot, int NumViews, [In] ref IntPtr ShaderResourceViews); //IShaderResourceView
      [PreserveSig]
      void PSSetShader(/*IPixelShader*/IntPtr PixelShader, void* ppClassInstances = null, int NumClassInstances = 0);
      [PreserveSig]
      void PSSetSamplers(int StartSlot, int NumSamplers, [In] ref /*ISamplerState*/IntPtr Samplers);
      [PreserveSig]
      void VSSetShader(/*IVertexShader*/IntPtr VertexShader, void** ppClassInstances = null, int NumClassInstances = 0);
      [PreserveSig]
      void DrawIndexed(int IndexCount, int StartIndexLocation, int BaseVertexLocation);
      [PreserveSig]
      void Draw(int VertexCount, int StartVertex);
      MAPPED_SUBRESOURCE Map(/*IResource*/IntPtr Resource, int Subresource, MAP MapType, int MapFlags);
      [PreserveSig]
      void Unmap(/*IResource*/IntPtr Resource, int Subresource);
      [PreserveSig]
      void PSSetConstantBuffers(int StartSlot, int NumBuffers, [In] ref IntPtr buffer); //IBuffer
      [PreserveSig]
      void IASetInputLayout(IntPtr pInputLayout); //IInputLayout
      [PreserveSig]
      void IASetVertexBuffers(int StartSlot, int NumBuffers, [In] ref /*IBuffer*/IntPtr VertexBuffers, [In] int* Strides, [In] int* Offsets);
      [PreserveSig]
      void IASetIndexBuffer( /*IBuffer*/IntPtr IndexBuffer, FORMAT Format, int Offset);
      void dummy14();
      //virtual void STDMETHODCALLTYPE DrawIndexedInstanced( 
      //    /* [annotation] */ 
      //    __in  UINT IndexCountPerInstance,
      //    /* [annotation] */ 
      //    __in  UINT InstanceCount,
      //    /* [annotation] */ 
      //    __in  UINT StartIndexLocation,
      //    /* [annotation] */ 
      //    __in  INT BaseVertexLocation,
      //    /* [annotation] */ 
      //    __in  UINT StartInstanceLocation) = 0;
      void dummy15();
      //virtual void STDMETHODCALLTYPE DrawInstanced( 
      //    /* [annotation] */ 
      //    __in  UINT VertexCountPerInstance,
      //    /* [annotation] */ 
      //    __in  UINT InstanceCount,
      //    /* [annotation] */ 
      //    __in  UINT StartVertex,
      //    /* [annotation] */ 
      //    __in  UINT StartInstanceLocation) = 0;
      [PreserveSig]
      void GSSetConstantBuffers(int StartSlot, int NumBuffers, [In] ref IntPtr buffer); //IBuffer
      [PreserveSig]
      void GSSetShader(/*IGeometryShader*/IntPtr Shader, void** ppClassInstances = null, int NumClassInstances = 0);
      [PreserveSig]
      void IASetPrimitiveTopology(PRIMITIVE_TOPOLOGY Topology);
      [PreserveSig]
      void VSSetShaderResources(int StartSlot, int NumViews, [In] ref /*IShaderResourceView*/IntPtr ShaderResourceViews);
      void dummy20();
      //virtual void STDMETHODCALLTYPE VSSetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __in_ecount(NumSamplers)  ID3D11SamplerState *const *ppSamplers) = 0;
      void dummy21();
      //virtual void STDMETHODCALLTYPE Begin( 
      //    /* [annotation] */ 
      //    __in  ID3D11Asynchronous *pAsync) = 0;
      void dummy22();
      //virtual void STDMETHODCALLTYPE End( 
      //    /* [annotation] */ 
      //    __in  ID3D11Asynchronous *pAsync) = 0;
      void dummy23();
      //virtual HRESULT STDMETHODCALLTYPE GetData( 
      //    /* [annotation] */ 
      //    __in  ID3D11Asynchronous *pAsync,
      //    /* [annotation] */ 
      //    __out_bcount_opt( DataSize )  void *pData,
      //    /* [annotation] */ 
      //    __in  UINT DataSize,
      //    /* [annotation] */ 
      //    __in  UINT GetDataFlags) = 0;
      void dummy24();
      //virtual void STDMETHODCALLTYPE SetPredication( 
      //    /* [annotation] */ 
      //    __in_opt  ID3D11Predicate *pPredicate,
      //    /* [annotation] */ 
      //    __in  BOOL PredicateValue) = 0;
      [PreserveSig]
      void GSSetShaderResources(int StartSlot, int NumViews, [In] ref /*IShaderResourceView*/IntPtr ShaderResourceViews);
      void dummy26();
      //virtual void STDMETHODCALLTYPE GSSetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __in_ecount(NumSamplers)  ID3D11SamplerState *const *ppSamplers) = 0;
      [PreserveSig]
      void OMSetRenderTargets(int NumViews, [In] ref IntPtr/*IRenderTargetView*/ RenderTargetViews, IntPtr/*IDepthStencilView*/ DepthStencilView);
      void dummy28();
      //virtual void STDMETHODCALLTYPE OMSetRenderTargetsAndUnorderedAccessViews( 
      //    /* [annotation] */ 
      //    __in  UINT NumRTVs,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumRTVs)  ID3D11RenderTargetView *const *ppRenderTargetViews,
      //    /* [annotation] */ 
      //    __in_opt  ID3D11DepthStencilView *pDepthStencilView,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_PS_CS_UAV_REGISTER_COUNT - 1 )  UINT UAVStartSlot,
      //    /* [annotation] */ 
      //    __in  UINT NumUAVs,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumUAVs)  ID3D11UnorderedAccessView *const *ppUnorderedAccessViews,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumUAVs)  const UINT *pUAVInitialCounts) = 0;
      [PreserveSig]
      void OMSetBlendState(/*IBlendState*/IntPtr BlendState, [In] ref float4 BlendFactors, uint SampleMask);
      [PreserveSig]
      void OMSetDepthStencilState(/*IDepthStencilState*/IntPtr DepthStencilState, int StencilRef);
      void dummy31();
      //virtual void STDMETHODCALLTYPE SOSetTargets( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SO_BUFFER_SLOT_COUNT)  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumBuffers)  ID3D11Buffer *const *ppSOTargets,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumBuffers)  const UINT *pOffsets) = 0;
      [PreserveSig]
      void DrawAuto();
      void dummy33();
      //virtual void STDMETHODCALLTYPE DrawIndexedInstancedIndirect( 
      //    /* [annotation] */ 
      //    __in  ID3D11Buffer *pBufferForArgs,
      //    /* [annotation] */ 
      //    __in  UINT AlignedByteOffsetForArgs) = 0;
      void dummy34();
      //virtual void STDMETHODCALLTYPE DrawInstancedIndirect( 
      //    /* [annotation] */ 
      //    __in  ID3D11Buffer *pBufferForArgs,
      //    /* [annotation] */ 
      //    __in  UINT AlignedByteOffsetForArgs) = 0;
      [PreserveSig]
      void Dispatch(int x, int y, int z);
      [PreserveSig]
      void DispatchIndirect(/*IBuffer*/IntPtr pBufferForArgs, int AlignedByteOffsetForArgs);
      [PreserveSig]
      void RSSetState(IntPtr RasterizerState); //IRasterizerState
      [PreserveSig]
      void RSSetViewports(int NumViewports, VIEWPORT* Viewports);
      [PreserveSig]
      void RSSetScissorRects(int NumRects, RECT* pRects);
      [PreserveSig]
      void CopySubresourceRegion(IResource dst, int DstSubresource, int DstX, int DstY, int DstZ, IResource Src, int SrcSubresource, BOX* pSrcBox); //pSrcBox opt
      [PreserveSig]
      void CopyResource(/*IResource*/IntPtr dst, /*IResource*/IntPtr src);
      [PreserveSig]
      void UpdateSubresource(/*IResource*/IntPtr DstResource, int DstSubresource, BOX* DstBox, void* pSrcData, int SrcRowPitch, int SrcDepthPitch);
      void dummy43();
      //virtual void STDMETHODCALLTYPE CopyStructureCount( 
      //    /* [annotation] */ 
      //    __in  ID3D11Buffer *pDstBuffer,
      //    /* [annotation] */ 
      //    __in  UINT DstAlignedByteOffset,
      //    /* [annotation] */ 
      //    __in  ID3D11UnorderedAccessView *pSrcView) = 0;
      [PreserveSig]
      void ClearRenderTargetView(IntPtr/*IRenderTargetView*/ rtv, in float4 rgba);
      void dummy45();
      //virtual void STDMETHODCALLTYPE ClearUnorderedAccessViewUint( 
      //    /* [annotation] */ 
      //    __in  ID3D11UnorderedAccessView *pUnorderedAccessView,
      //    /* [annotation] */ 
      //    __in  const UINT Values[ 4 ]) = 0;
      void dummy46();
      //virtual void STDMETHODCALLTYPE ClearUnorderedAccessViewFloat( 
      //    /* [annotation] */ 
      //    __in  ID3D11UnorderedAccessView *pUnorderedAccessView,
      //    /* [annotation] */ 
      //    __in  const FLOAT Values[ 4 ]) = 0;
      [PreserveSig]
      void ClearDepthStencilView(IntPtr/*IDepthStencilView*/ DepthStencilView, CLEAR ClearFlags, float Depth, byte Stencil);
      [PreserveSig]
      void GenerateMips(IntPtr srv); //IShaderResourceView
      void dummy49();
      //virtual void STDMETHODCALLTYPE SetResourceMinLOD( 
      //    /* [annotation] */ 
      //    __in  ID3D11Resource *pResource,
      //    FLOAT MinLOD) = 0;
      void dummy50();
      //virtual FLOAT STDMETHODCALLTYPE GetResourceMinLOD( 
      //    /* [annotation] */ 
      //    __in  ID3D11Resource *pResource) = 0;
      [PreserveSig]
      void ResolveSubresource(IntPtr /*ID3D11Resource*/ dst, int dstsubres, IntPtr /*ID3D11Resource*/ src, int srcsubres, FORMAT Format);
      void dummy52();
      //virtual void STDMETHODCALLTYPE ExecuteCommandList( 
      //    /* [annotation] */ 
      //    __in  ID3D11CommandList *pCommandList,
      //    BOOL RestoreContextState) = 0;
      [PreserveSig]
      void HSSetShaderResources(int StartSlot, int NumViews, [In] ref /*IShaderResourceView*/IntPtr ShaderResourceViews);
      void dummy54();
      //virtual void STDMETHODCALLTYPE HSSetShader( 
      //    /* [annotation] */ 
      //    __in_opt  ID3D11HullShader *pHullShader,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumClassInstances)  ID3D11ClassInstance *const *ppClassInstances,
      //    UINT NumClassInstances) = 0;
      void dummy55();
      //virtual void STDMETHODCALLTYPE HSSetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __in_ecount(NumSamplers)  ID3D11SamplerState *const *ppSamplers) = 0;
      void dummy56();
      //virtual void STDMETHODCALLTYPE HSSetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __in_ecount(NumBuffers)  ID3D11Buffer *const *ppConstantBuffers) = 0;
      [PreserveSig]
      void DSSetShaderResources(int StartSlot, int NumViews, [In] ref /*IShaderResourceView*/IntPtr ShaderResourceViews);
      void dummy58();
      //virtual void STDMETHODCALLTYPE DSSetShader( 
      //    /* [annotation] */ 
      //    __in_opt  ID3D11DomainShader *pDomainShader,
      //    /* [annotation] */ 
      //    __in_ecount_opt(NumClassInstances)  ID3D11ClassInstance *const *ppClassInstances,
      //    UINT NumClassInstances) = 0;
      void dummy59();
      //virtual void STDMETHODCALLTYPE DSSetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __in_ecount(NumSamplers)  ID3D11SamplerState *const *ppSamplers) = 0;
      void dummy60();
      //virtual void STDMETHODCALLTYPE DSSetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __in_ecount(NumBuffers)  ID3D11Buffer *const *ppConstantBuffers) = 0;
      [PreserveSig]
      void CSSetShaderResources(int StartSlot, int NumViews, [In] ref IntPtr/*IShaderResourceView*/ ShaderResourceViews);
      [PreserveSig]
      void CSSetUnorderedAccessViews(int StartSlot, int NumUAVs, [In] ref IntPtr/*IUnorderedAccessView*/ ppUnorderedAccessViews, int* pUAVInitialCounts);
      [PreserveSig]
      void CSSetShader(/*IComputeShader*/IntPtr cs, void* ppClassInstances, int numClassInstances);
      void dummy64();
      //virtual void STDMETHODCALLTYPE CSSetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __in_ecount(NumSamplers)  ID3D11SamplerState *const *ppSamplers) = 0;
      [PreserveSig]
      void CSSetConstantBuffers(int StartSlot, int NumBuffers, [In] ref /*IBuffer*/IntPtr ppConstantBuffers);
      void dummy66();
      //virtual void STDMETHODCALLTYPE VSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      void dummy67();
      //virtual void STDMETHODCALLTYPE PSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      void dummy68();
      //virtual void STDMETHODCALLTYPE PSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11PixelShader **ppPixelShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      void dummy69();
      //virtual void STDMETHODCALLTYPE PSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      void dummy70();
      //virtual void STDMETHODCALLTYPE VSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11VertexShader **ppVertexShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      void dummy71();
      //virtual void STDMETHODCALLTYPE PSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      void dummy72();
      //virtual void STDMETHODCALLTYPE IAGetInputLayout( 
      //    /* [annotation] */ 
      //    __out  ID3D11InputLayout **ppInputLayout) = 0;
      void dummy73();
      //virtual void STDMETHODCALLTYPE IAGetVertexBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_IA_VERTEX_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumBuffers)  ID3D11Buffer **ppVertexBuffers,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumBuffers)  UINT *pStrides,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumBuffers)  UINT *pOffsets) = 0;
      void dummy74();
      //virtual void STDMETHODCALLTYPE IAGetIndexBuffer( 
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Buffer **pIndexBuffer,
      //    /* [annotation] */ 
      //    __out_opt  DXGI_FORMAT *Format,
      //    /* [annotation] */ 
      //    __out_opt  UINT *Offset) = 0;
      //
      //virtual void STDMETHODCALLTYPE GSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      //
      //virtual void STDMETHODCALLTYPE GSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11GeometryShader **ppGeometryShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      //
      //virtual void STDMETHODCALLTYPE IAGetPrimitiveTopology( 
      //    /* [annotation] */ 
      //    __out  D3D11_PRIMITIVE_TOPOLOGY *pTopology) = 0;
      //
      //virtual void STDMETHODCALLTYPE VSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE VSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      //
      //virtual void STDMETHODCALLTYPE GetPredication( 
      //    /* [annotation] */ 
      //    __out_opt  ID3D11Predicate **ppPredicate,
      //    /* [annotation] */ 
      //    __out_opt  BOOL *pPredicateValue) = 0;
      //
      //virtual void STDMETHODCALLTYPE GSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE GSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      //
      //virtual void STDMETHODCALLTYPE OMGetRenderTargets( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumViews)  ID3D11RenderTargetView **ppRenderTargetViews,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11DepthStencilView **ppDepthStencilView) = 0;
      //
      //virtual void STDMETHODCALLTYPE OMGetRenderTargetsAndUnorderedAccessViews( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT )  UINT NumRTVs,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumRTVs)  ID3D11RenderTargetView **ppRenderTargetViews,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11DepthStencilView **ppDepthStencilView,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_PS_CS_UAV_REGISTER_COUNT - 1 )  UINT UAVStartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_PS_CS_UAV_REGISTER_COUNT - UAVStartSlot )  UINT NumUAVs,
      //    /* [annotation] */ 
      //    __out_ecount_opt(NumUAVs)  ID3D11UnorderedAccessView **ppUnorderedAccessViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE OMGetBlendState( 
      //    /* [annotation] */ 
      //    __out_opt  ID3D11BlendState **ppBlendState,
      //    /* [annotation] */ 
      //    __out_opt  FLOAT BlendFactor[ 4 ],
      //    /* [annotation] */ 
      //    __out_opt  UINT *pSampleMask) = 0;
      //
      //virtual void STDMETHODCALLTYPE OMGetDepthStencilState( 
      //    /* [annotation] */ 
      //    __out_opt  ID3D11DepthStencilState **ppDepthStencilState,
      //    /* [annotation] */ 
      //    __out_opt  UINT *pStencilRef) = 0;
      //
      //virtual void STDMETHODCALLTYPE SOGetTargets( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_SO_BUFFER_SLOT_COUNT )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppSOTargets) = 0;
      //
      //virtual void STDMETHODCALLTYPE RSGetState( 
      //    /* [annotation] */ 
      //    __out  ID3D11RasterizerState **ppRasterizerState) = 0;
      //
      //virtual void STDMETHODCALLTYPE RSGetViewports( 
      //    /* [annotation] */ 
      //    __inout /*_range(0, D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE )*/   UINT *pNumViewports,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumViewports)  D3D11_VIEWPORT *pViewports) = 0;
      //
      //virtual void STDMETHODCALLTYPE RSGetScissorRects( 
      //    /* [annotation] */ 
      //    __inout /*_range(0, D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE )*/   UINT *pNumRects,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumRects)  D3D11_RECT *pRects) = 0;
      //
      //virtual void STDMETHODCALLTYPE HSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE HSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11HullShader **ppHullShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      //
      //virtual void STDMETHODCALLTYPE HSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      //
      //virtual void STDMETHODCALLTYPE HSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      //
      //virtual void STDMETHODCALLTYPE DSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE DSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11DomainShader **ppDomainShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      //
      //virtual void STDMETHODCALLTYPE DSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      //
      //virtual void STDMETHODCALLTYPE DSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      //
      //virtual void STDMETHODCALLTYPE CSGetShaderResources( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT - StartSlot )  UINT NumViews,
      //    /* [annotation] */ 
      //    __out_ecount(NumViews)  ID3D11ShaderResourceView **ppShaderResourceViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE CSGetUnorderedAccessViews( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_PS_CS_UAV_REGISTER_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_PS_CS_UAV_REGISTER_COUNT - StartSlot )  UINT NumUAVs,
      //    /* [annotation] */ 
      //    __out_ecount(NumUAVs)  ID3D11UnorderedAccessView **ppUnorderedAccessViews) = 0;
      //
      //virtual void STDMETHODCALLTYPE CSGetShader( 
      //    /* [annotation] */ 
      //    __out  ID3D11ComputeShader **ppComputeShader,
      //    /* [annotation] */ 
      //    __out_ecount_opt(*pNumClassInstances)  ID3D11ClassInstance **ppClassInstances,
      //    /* [annotation] */ 
      //    __inout_opt  UINT *pNumClassInstances) = 0;
      //
      //virtual void STDMETHODCALLTYPE CSGetSamplers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT - StartSlot )  UINT NumSamplers,
      //    /* [annotation] */ 
      //    __out_ecount(NumSamplers)  ID3D11SamplerState **ppSamplers) = 0;
      //
      //virtual void STDMETHODCALLTYPE CSGetConstantBuffers( 
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - 1 )  UINT StartSlot,
      //    /* [annotation] */ 
      //    __in_range( 0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT - StartSlot )  UINT NumBuffers,
      //    /* [annotation] */ 
      //    __out_ecount(NumBuffers)  ID3D11Buffer **ppConstantBuffers) = 0;
      //
      //virtual void STDMETHODCALLTYPE ClearState( void) = 0;
      //
      //virtual void STDMETHODCALLTYPE Flush( void) = 0;
      //
      //virtual D3D11_DEVICE_CONTEXT_TYPE STDMETHODCALLTYPE GetType( void) = 0;
      //
      //virtual UINT STDMETHODCALLTYPE GetContextFlags( void) = 0;
      //
      //virtual HRESULT STDMETHODCALLTYPE FinishCommandList( 
      //    BOOL RestoreDeferredContextState,
      //    /* [annotation] */ 
      //    __out_opt  ID3D11CommandList **ppCommandList) = 0;
    }

    struct BOX { public int left, top, front, right, bottom, back; }
    enum FILTER
    {
      MIN_MAG_MIP_POINT = 0,
      MIN_MAG_POINT_MIP_LINEAR = 0x1,
      MIN_POINT_MAG_LINEAR_MIP_POINT = 0x4,
      MIN_POINT_MAG_MIP_LINEAR = 0x5,
      MIN_LINEAR_MAG_MIP_POINT = 0x10,
      MIN_LINEAR_MAG_POINT_MIP_LINEAR = 0x11,
      MIN_MAG_LINEAR_MIP_POINT = 0x14,
      MIN_MAG_MIP_LINEAR = 0x15,
      ANISOTROPIC = 0x55,
      COMPARISON_MIN_MAG_MIP_POINT = 0x80,
      COMPARISON_MIN_MAG_POINT_MIP_LINEAR = 0x81,
      COMPARISON_MIN_POINT_MAG_LINEAR_MIP_POINT = 0x84,
      COMPARISON_MIN_POINT_MAG_MIP_LINEAR = 0x85,
      COMPARISON_MIN_LINEAR_MAG_MIP_POINT = 0x90,
      COMPARISON_MIN_LINEAR_MAG_POINT_MIP_LINEAR = 0x91,
      COMPARISON_MIN_MAG_LINEAR_MIP_POINT = 0x94,
      COMPARISON_MIN_MAG_MIP_LINEAR = 0x95,
      COMPARISON_ANISOTROPIC = 0xd5
    }
    enum TEXTURE_ADDRESS_MODE
    {
      WRAP = 1,
      MIRROR = 2,
      CLAMP = 3,
      BORDER = 4,
      MIRROR_ONCE = 5
    }
    struct SAMPLER_DESC
    {
      public FILTER Filter;
      public TEXTURE_ADDRESS_MODE AddressU;
      public TEXTURE_ADDRESS_MODE AddressV;
      public TEXTURE_ADDRESS_MODE AddressW;
      public float MipLODBias;
      public int MaxAnisotropy;
      public COMPARISON ComparisonFunc;
      public fixed float BorderColor[4];
      public float MinLOD;
      public float MaxLOD;
    }
    enum FILL_MODE { WIREFRAME = 2, SOLID = 3 }
    enum CULL_MODE { NONE = 1, FRONT = 2, BACK = 3 }
    struct RASTERIZER_DESC
    {
      public FILL_MODE FillMode;
      public CULL_MODE CullMode;
      public int FrontCounterClockwise;
      public int DepthBias;
      public float DepthBiasClamp;
      public float SlopeScaledDepthBias;
      public int DepthClipEnable;
      public int ScissorEnable;
      public int MultisampleEnable;
      public int AntialiasedLineEnable;
    }
    enum BLEND { ZERO = 1, ONE = 2, SRC_COLOR = 3, INV_SRC_COLOR = 4, SRC_ALPHA = 5, INV_SRC_ALPHA = 6, DEST_ALPHA = 7, INV_DEST_ALPHA = 8, DEST_COLOR = 9, INV_DEST_COLOR = 10, SRC_ALPHA_SAT = 11, BLEND_FACTOR = 14, INV_BLEND_FACTOR = 15, SRC1_COLOR = 16, INV_SRC1_COLOR = 17, SRC1_ALPHA = 18, INV_SRC1_ALPHA = 19 }
    enum BLEND_OP { ADD = 1, SUBTRACT = 2, REV_SUBTRACT = 3, MIN = 4, MAX = 5 }
    enum COLOR_WRITE_ENABLE : byte { RED = 1, GREEN = 2, BLUE = 4, ALPHA = 8, ALL = (((RED | GREEN) | BLUE) | ALPHA) }
    struct RENDER_TARGET_BLEND
    {
      public int BlendEnable;
      public BLEND SrcBlend;
      public BLEND DestBlend;
      public BLEND_OP BlendOp;
      public BLEND SrcBlendAlpha;
      public BLEND DestBlendAlpha;
      public BLEND_OP BlendOpAlpha;
      public COLOR_WRITE_ENABLE RenderTargetWriteMask;
    }
    struct BLEND_DESC
    {
      public int AlphaToCoverageEnable;
      public int IndependentBlendEnable;
      public RENDER_TARGET_BLEND RenderTarget0;
      public RENDER_TARGET_BLEND RenderTarget1;
      public RENDER_TARGET_BLEND RenderTarget2;
      public RENDER_TARGET_BLEND RenderTarget3;
      public RENDER_TARGET_BLEND RenderTarget4;
      public RENDER_TARGET_BLEND RenderTarget5;
      public RENDER_TARGET_BLEND RenderTarget6;
      public RENDER_TARGET_BLEND RenderTarget7;
    }
    enum RESOURCE_DIMENSION { UNKNOWN = 0, BUFFER = 1, TEXTURE1D = 2, TEXTURE2D = 3, TEXTURE3D = 4 }

    [ComImport, Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IResource : IDeviceChild
    {
      void _VtblGap1_4();
      RESOURCE_DIMENSION Type { get; }
      //virtual void STDMETHODCALLTYPE SetEvictionPriority( 
      //    /* [annotation] */ 
      //    __in  UINT EvictionPriority) = 0;
      //
      //virtual UINT STDMETHODCALLTYPE GetEvictionPriority( void) = 0;
    }

    [ComImport, Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface ITexture2D : IResource
    {
      void _VtblGap1_7();
      TEXTURE2D_DESC Desc { get; }
    }

    struct RECT
    {
      public int left, top, right, bottom;
    }

    enum RESOURCE_MISC
    {
      GENERATE_MIPS = 0x1,
      SHARED = 0x2,
      TEXTURECUBE = 0x4,
      DRAWINDIRECT_ARGS = 0x10,
      BUFFER_ALLOW_RAW_VIEWS = 0x20,
      BUFFER_STRUCTURED = 0x40,
      RESOURCE_CLAMP = 0x80,
      SHARED_KEYEDMUTEX = 0x100,
      GDI_COMPATIBLE = 0x200
    }

    struct TEXTURE2D_DESC
    {
      public int Width;
      public int Height;
      public int MipLevels;
      public int ArraySize;
      public FORMAT Format;
      public SAMPLE_DESC SampleDesc;
      public USAGE Usage;
      public BIND BindFlags;
      public CPU_ACCESS_FLAG CPUAccessFlags;
      public RESOURCE_MISC MiscFlags;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct BUFFER_SRV
    {
      [FieldOffset(0)]
      public int FirstElement;
      [FieldOffset(4)]
      public int ElementOffset;
      [FieldOffset(0)]
      public int NumElements;
      [FieldOffset(4)]
      public int ElementWidth;
    }
    struct BUFFEREX_SRV
    {
      public int FirstElement;
      public int NumElements;
      public int Flags; //D3D11_BUFFEREX_SRV_FLAG_RAW	= 0x1
    }
    struct TEX1D_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
    }
    struct TEX1D_ARRAY_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX2D_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
    }
    struct TEX2D_ARRAY_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX3D_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
    }
    struct TEXCUBE_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
    }
    struct TEXCUBE_ARRAY_SRV
    {
      public int MostDetailedMip;
      public int MipLevels;
      public int First2DArrayFace;
      public int NumCubes;
    }
    struct TEX2DMS_SRV
    {
      public int UnusedField_NothingToDefine;
    }
    struct TEX2DMS_ARRAY_SRV
    {
      public int FirstArraySlice;
      public int ArraySize;
    }

    enum UAV_DIMENSION { UNKNOWN = 0, BUFFER = 1, TEXTURE1D = 2, TEXTURE1DARRAY = 3, TEXTURE2D = 4, TEXTURE2DARRAY = 5, TEXTURE3D = 8 }
    enum BUFFER_UAV_FLAG { RAW = 0x1, APPEND = 0x2, COUNTER = 0x4 }
    struct BUFFER_UAV { public int FirstElement, NumElements; public BUFFER_UAV_FLAG Flags; }
    [StructLayout(LayoutKind.Explicit)]
    struct UNORDERED_ACCESS_VIEW_DESC
    {
      [FieldOffset(0)]
      public FORMAT Format;
      [FieldOffset(4)]
      public UAV_DIMENSION ViewDimension;
      [FieldOffset(8)]
      public BUFFER_UAV Buffer;
      //union 
      //  {
      //  D3D11_BUFFER_UAV Buffer;
      //  D3D11_TEX1D_UAV Texture1D;
      //  D3D11_TEX1D_ARRAY_UAV Texture1DArray;
      //  D3D11_TEX2D_UAV Texture2D;
      //  D3D11_TEX2D_ARRAY_UAV Texture2DArray;
      //  D3D11_TEX3D_UAV Texture3D;
      //  } 	;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct SHADER_RESOURCE_VIEW_DESC
    {
      [FieldOffset(0)]
      public FORMAT Format;
      [FieldOffset(4)]
      public SRV_DIMENSION ViewDimension;
      [FieldOffset(8)]
      public BUFFER_SRV Buffer;
      [FieldOffset(8)]
      public TEX1D_SRV Texture1D;
      [FieldOffset(8)]
      public TEX1D_ARRAY_SRV Texture1DArray;
      [FieldOffset(8)]
      public TEX2D_SRV Texture2D;
      [FieldOffset(8)]
      public TEX2D_ARRAY_SRV Texture2DArray;
      [FieldOffset(8)]
      public TEX2DMS_SRV Texture2DMS;
      [FieldOffset(8)]
      public TEX2DMS_ARRAY_SRV Texture2DMSArray;
      [FieldOffset(8)]
      public TEX3D_SRV Texture3D;
      [FieldOffset(8)]
      public TEXCUBE_SRV TextureCube;
      [FieldOffset(8)]
      public TEXCUBE_ARRAY_SRV TextureCubeArray;
      [FieldOffset(8)]
      public BUFFEREX_SRV BufferEx;
    }


    [StructLayout(LayoutKind.Explicit)]
    struct BUFFER_RTV
    {
      [FieldOffset(0)]
      public int FirstElement;
      [FieldOffset(4)]
      public int ElementOffset;
      [FieldOffset(0)]
      public int NumElements;
      [FieldOffset(4)]
      public int ElementWidth;
    }
    struct TEX1D_RTV
    {
      public int MipSlice;
    }
    struct TEX1D_ARRAY_RTV
    {
      public int MipSlice;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX2D_RTV
    {
      public int MipSlice;
    }
    struct TEX2DMS_RTV
    {
      public int UnusedField_NothingToDefine;
    }
    struct TEX2D_ARRAY_RTV
    {
      public int MipSlice;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX2DMS_ARRAY_RTV
    {
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX3D_RTV
    {
      public int MipSlice;
      public int FirstWSlice;
      public int WSize;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct RENDER_TARGET_VIEW_DESC
    {
      [FieldOffset(0)]
      public FORMAT Format;
      [FieldOffset(4)]
      public RTV_DIMENSION ViewDimension;
      [FieldOffset(8)]
      public BUFFER_RTV Buffer;
      [FieldOffset(8)]
      public TEX1D_RTV Texture1D;
      [FieldOffset(8)]
      public TEX1D_ARRAY_RTV Texture1DArray;
      [FieldOffset(8)]
      public TEX2D_RTV Texture2D;
      [FieldOffset(8)]
      public TEX2D_ARRAY_RTV Texture2DArray;
      [FieldOffset(8)]
      public TEX2DMS_RTV Texture2DMS;
      [FieldOffset(8)]
      public TEX2DMS_ARRAY_RTV Texture2DMSArray;
      [FieldOffset(8)]
      public TEX3D_RTV Texture3D;
    }

    enum DSV_DIMENSION { UNKNOWN = 0, TEXTURE1D = 1, TEXTURE1DARRAY = 2, TEXTURE2D = 3, TEXTURE2DARRAY = 4, TEXTURE2DMS = 5, TEXTURE2DMSARRAY = 6 }

    struct TEX1D_DSV
    {
      public int MipSlice;
    }
    struct TEX1D_ARRAY_DSV
    {
      public int MipSlice;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX2D_DSV
    {
      public int MipSlice;
    }
    struct TEX2D_ARRAY_DSV
    {
      public int MipSlice;
      public int FirstArraySlice;
      public int ArraySize;
    }
    struct TEX2DMS_DSV
    {
      public int UnusedField_NothingToDefine;
    }
    struct TEX2DMS_ARRAY_DSV
    {
      public int FirstArraySlice;
      public int ArraySize;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct DEPTH_STENCIL_VIEW_DESC
    {
      [FieldOffset(0)]
      public FORMAT Format;
      [FieldOffset(4)]
      public DSV_DIMENSION ViewDimension;
      [FieldOffset(8)]
      public int Flags;
      [FieldOffset(12)]
      public TEX1D_DSV Texture1D;
      [FieldOffset(12)]
      public TEX1D_ARRAY_DSV Texture1DArray;
      [FieldOffset(12)]
      public TEX2D_DSV Texture2D;
      [FieldOffset(12)]
      public TEX2D_ARRAY_DSV Texture2DArray;
      [FieldOffset(12)]
      public TEX2DMS_DSV Texture2DMS;
      [FieldOffset(12)]
      public TEX2DMS_ARRAY_DSV Texture2DMSArray;
    }

    struct MAPPED_SUBRESOURCE
    {
      public void* pData;
      public int RowPitch;
      public int DepthPitch;
    }
    enum MAP { READ = 1, WRITE = 2, READ_WRITE = 3, WRITE_DISCARD = 4, WRITE_NO_OVERWRITE = 5 }

    [ComImport, Guid("839d1216-bb2e-412b-b7f4-a9dbebe08ed1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IView : IDeviceChild
    {
      void _VtblGap1_4();
      IResource GetResource();
    };

    enum SDK_VERSION { Current = 7 }
    enum FEATURE_LEVEL
    {
      //_9_1 = 0x9100, _9_2 = 0x9200, _9_3 = 0x9300, 
      _10_0 = 0xa000, _10_1 = 0xa100, _11_0 = 0xb000, _11_1 = 0xb100, _12_0 = 0xc000, _12_1 = 0xc100,
    }
    enum SRV_DIMENSION { UNKNOWN = 0, BUFFER = 1, TEXTURE1D = 2, TEXTURE1DARRAY = 3, TEXTURE2D = 4, TEXTURE2DARRAY = 5, TEXTURE2DMS = 6, TEXTURE2DMSARRAY = 7, TEXTURE3D = 8, TEXTURECUBE = 9, TEXTURECUBEARRAY = 10, BUFFEREX = 11, }
    enum RTV_DIMENSION { UNKNOWN = 0, BUFFER = 1, TEXTURE1D = 2, TEXTURE1DARRAY = 3, TEXTURE2D = 4, TEXTURE2DARRAY = 5, TEXTURE2DMS = 6, TEXTURE2DMSARRAY = 7, TEXTURE3D = 8 }
    enum D3D_DRIVER_TYPE { Unknown = 0, Hardware = 1, Reference = 2, Null = 3, Software = 4, Warp = 5 }
    enum CREATE_DEVICE_FLAG { SingleThreaded = 0x1, Debug = 0x2, Switch_To_Ref = 0x4, Prevent_Internal_Threading_Optimizations = 0x8, BGRA_Support = 0x20 }
    public enum CLEAR { DEPTH = 0x1, STENCIL = 0x2 }
    enum PRIMITIVE_TOPOLOGY { UNDEFINED = 0, POINTLIST = 1, LINELIST = 2, LINESTRIP = 3, TRIANGLELIST = 4, TRIANGLESTRIP = 5, LINELIST_ADJ = 10, LINESTRIP_ADJ = 11, TRIANGLELIST_ADJ = 12, TRIANGLESTRIP_ADJ = 13, }
    [Flags]
    enum FORMAT_SUPPORT
    {
      BUFFER = 0x1,
      IA_VERTEX_BUFFER = 0x2,
      IA_INDEX_BUFFER = 0x4,
      SO_BUFFER = 0x8,
      TEXTURE1D = 0x10,
      TEXTURE2D = 0x20,
      TEXTURE3D = 0x40,
      TEXTURECUBE = 0x80,
      SHADER_LOAD = 0x100,
      SHADER_SAMPLE = 0x200,
      SHADER_SAMPLE_COMPARISON = 0x400,
      SHADER_SAMPLE_MONO_TEXT = 0x800,
      MIP = 0x1000,
      MIP_AUTOGEN = 0x2000,
      RENDER_TARGET = 0x4000,
      BLENDABLE = 0x8000,
      DEPTH_STENCIL = 0x10000,
      CPU_LOCKABLE = 0x20000,
      MULTISAMPLE_RESOLVE = 0x40000,
      DISPLAY = 0x80000,
      CAST_WITHIN_BIT_LAYOUT = 0x100000,
      MULTISAMPLE_RENDERTARGET = 0x200000,
      MULTISAMPLE_LOAD = 0x400000,
      SHADER_GATHER = 0x800000,
      BACK_BUFFER_CAST = 0x1000000,
      TYPED_UNORDERED_ACCESS_VIEW = 0x2000000,
      SHADER_GATHER_COMPARISON = 0x4000000,
      DECODER_OUTPUT = 0x8000000,
      VIDEO_PROCESSOR_OUTPUT = 0x10000000,
      VIDEO_PROCESSOR_INPUT = 0x20000000,
      VIDEO_ENCODER = 0x40000000
    }

    static SAMPLE_DESC CheckMultisample(IDevice device, FORMAT fmt, int samples)
    {
      SAMPLE_DESC desc; desc.Count = 1; desc.Quality = 0;
      for (int i = samples, q; i > 0; i--)
        if (device.CheckMultisampleQualityLevels(fmt, i, out q) == 0 && q > 0) { desc.Count = i; desc.Quality = q - 1; break; }
      return desc;
    }

    [ComImport, Guid("aec22fb8-76f3-4639-9be0-28eb43a67a2e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IObject
    {
      void dummy1();
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateData( 
      //    /* [annotation][in] */ 
      //    __in  REFGUID Name,
      //    /* [in] */ UINT DataSize,
      //    /* [annotation][in] */ 
      //    __in_bcount(DataSize)  const void *pData) = 0;
      void dummy2();
      //virtual HRESULT STDMETHODCALLTYPE SetPrivateDataInterface( 
      //    /* [annotation][in] */ 
      //    __in  REFGUID Name,
      //    /* [annotation][in] */ 
      //    __in  const IUnknown *pUnknown) = 0;
      void dummy3();
      //virtual HRESULT STDMETHODCALLTYPE GetPrivateData( 
      //    /* [annotation][in] */ 
      //    __in  REFGUID Name,
      //    /* [annotation][out][in] */ 
      //    __inout  UINT *pDataSize,
      //    /* [annotation][out] */ 
      //    __out_bcount(*pDataSize)  void *pData) = 0;
      //
      [return: MarshalAs(UnmanagedType.IUnknown)]
      object GetParent([In] ref Guid riid);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ADAPTER_DESC
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string Description;
      public uint VendorId;
      public uint DeviceId;
      public uint SubSysId;
      public uint Revision;
      public UIntPtr DedicatedVideoMemory;
      public UIntPtr DedicatedSystemMemory;
      public UIntPtr SharedSystemMemory;
      public long AdapterLuid;
    }

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IAdapter : IObject //IDXGIAdapter
    {
      void _VtblGap1_4();
      [PreserveSig]
      int EnumOutputs(int i, out IOutput p);
      ADAPTER_DESC Desc { get; }
      //virtual HRESULT STDMETHODCALLTYPE GetDesc( 
      //    /* [annotation][out] */ 
      //    __out  DXGI_ADAPTER_DESC *pDesc) = 0;
      new void dummy3();
      //virtual HRESULT STDMETHODCALLTYPE CheckInterfaceSupport( 
      //    /* [annotation][in] */ 
      //    __in  REFGUID InterfaceName,
      //    /* [annotation][out] */ 
      //    __out  LARGE_INTEGER *pUMDVersion) = 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OUTPUT_DESC
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string DeviceName;
      RECT DesktopCoordinates;
      bool AttachedToDesktop;
      int Rotation; //DXGI_MODE_ROTATION_UNSPECIFIED = 0, DXGI_MODE_ROTATION_IDENTITY = 1, DXGI_MODE_ROTATION_ROTATE90 = 2, DXGI_MODE_ROTATION_ROTATE180 = 3, DXGI_MODE_ROTATION_ROTATE270 = 4
      IntPtr Monitor; //HMONITOR
    }

    [ComImport, Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IOutput : IObject // IDXGIOutput 
    {
      void _VtblGap1_4();
      OUTPUT_DESC Desc { get; } //virtual HRESULT STDMETHODCALLTYPE GetDesc(_Out_ DXGI_OUTPUT_DESC *pDesc) = 0;
      //virtual HRESULT STDMETHODCALLTYPE GetDisplayModeList(
      //    /* [in] */ DXGI_FORMAT EnumFormat,
      //    /* [in] */ UINT Flags,
      //    /* [annotation][out][in] */
      //    _Inout_ UINT *pNumModes,
      //    /* [annotation][out] */
      //    _Out_writes_to_opt_(* pNumModes,* pNumModes) DXGI_MODE_DESC *pDesc) = 0;   
      //virtual HRESULT STDMETHODCALLTYPE FindClosestMatchingMode(
      //    /* [annotation][in] */
      //    _In_  const DXGI_MODE_DESC* pModeToMatch,
      //    /* [annotation][out] */
      //    _Out_  DXGI_MODE_DESC* pClosestMatch,
      //    /* [annotation][in] */
      //    _In_opt_  IUnknown* pConcernedDevice) = 0;
      //virtual HRESULT STDMETHODCALLTYPE WaitForVBlank(void) = 0;   
      //virtual HRESULT STDMETHODCALLTYPE TakeOwnership(
      //    /* [annotation][in] */
      //    _In_ IUnknown *pDevice,
      //    BOOL Exclusive) = 0; 
      //virtual void STDMETHODCALLTYPE ReleaseOwnership(void) = 0;   
      //virtual HRESULT STDMETHODCALLTYPE GetGammaControlCapabilities(
      //    /* [annotation][out] */
      //    _Out_ DXGI_GAMMA_CONTROL_CAPABILITIES *pGammaCaps) = 0;   
      //virtual HRESULT STDMETHODCALLTYPE SetGammaControl(
      //    /* [annotation][in] */
      //    _In_  const DXGI_GAMMA_CONTROL* pArray) = 0;
      //virtual HRESULT STDMETHODCALLTYPE GetGammaControl(
      //  /* [annotation][out] */
      //  _Out_ DXGI_GAMMA_CONTROL *pArray) = 0;   
      //virtual HRESULT STDMETHODCALLTYPE SetDisplaySurface(
      //    /* [annotation][in] */
      //    _In_ IDXGISurface *pScanoutSurface) = 0;  
      //virtual HRESULT STDMETHODCALLTYPE GetDisplaySurfaceData(
      //    /* [annotation][in] */
      //    _In_ IDXGISurface *pDestination) = 0; 
      //virtual HRESULT STDMETHODCALLTYPE GetFrameStatistics(
      //    /* [annotation][out] */
      //    _Out_ DXGI_FRAME_STATISTICS *pStats) = 0;
    }

    [ComImport, Guid("3d3e0379-f9de-4d58-bb6c-18d62992f1a6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IDeviceSubObject : IObject
    {
      void _VtblGap1_4(); //IObject
      //virtual HRESULT STDMETHODCALLTYPE GetDevice( 
      //    /* [annotation][in] */ 
      //    __in  REFIID riid,
      //    /* [annotation][retval][out] */ 
      //    __out  void **ppDevice) = 0;
    };

    [ComImport, Guid("310d36a0-d2e7-4c0a-aa04-6a9d23b8886a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface ISwapChain : IDeviceSubObject
    {
      void _VtblGap1_5();
      [PreserveSig]
      int Present(int SyncInterval, int Flags);
      IntPtr GetBuffer(int Buffer, [In] ref Guid riid);
      new void dummy2();
      //virtual HRESULT STDMETHODCALLTYPE SetFullscreenState( 
      //    /* [in] */ BOOL Fullscreen,
      //    /* [annotation][in] */ 
      //    __in_opt  IDXGIOutput *pTarget) = 0;
      new void dummy3();
      //virtual HRESULT STDMETHODCALLTYPE GetFullscreenState( 
      //    /* [annotation][out] */ 
      //    __out  BOOL *pFullscreen,
      //    /* [annotation][out] */ 
      //    __out  IDXGIOutput **ppTarget) = 0;
      SWAP_CHAIN_DESC Desc { get; }
      void ResizeBuffers(int BufferCount, int Width, int Height, FORMAT NewFormat, int SwapChainFlags);
      void ResizeTarget([In] MODE_DESC* Desc);
      void dummy7();
      //virtual HRESULT STDMETHODCALLTYPE GetContainingOutput( 
      //    /* [annotation][out] */ 
      //    __out  IDXGIOutput **ppOutput) = 0;
      void GetFrameStatistics(out FRAME_STATISTICS LastPresentCount); // hr not supported
      uint LastPresentCount { get; }
    }

    [ComImport, Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IDXGIDevice : IObject
    {
      void _VtblGap1_4();
      IAdapter Adapter { get; }
      new void dummy1();
      //virtual HRESULT STDMETHODCALLTYPE CreateSurface( 
      //    /* [annotation][in] */ 
      //    __in  const DXGI_SURFACE_DESC *pDesc,
      //    /* [in] */ UINT NumSurfaces,
      //    /* [in] */ DXGI_USAGE Usage,
      //    /* [annotation][in] */ 
      //    __in_opt  const DXGI_SHARED_RESOURCE *pSharedResource,
      //    /* [annotation][out] */ 
      //    __out  IDXGISurface **ppSurface) = 0;
      new void dummy2();
      //virtual HRESULT STDMETHODCALLTYPE QueryResourceResidency( 
      //    /* [annotation][size_is][in] */ 
      //    __in_ecount(NumResources)  IUnknown *const *ppResources,
      //    /* [annotation][size_is][out] */ 
      //    __out_ecount(NumResources)  DXGI_RESIDENCY *pResidencyStatus,
      //    /* [in] */ UINT NumResources) = 0;
      int GPUThreadPriority { set; get; }
    }

    [ComImport, Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    interface IFactory : IObject
    {
      void _VtblGap1_4();
      [PreserveSig]
      int EnumAdapters(int i, out IAdapter p);
      void MakeWindowAssociation(IntPtr WindowHandle, MWA_NO Flags);
      new void dummy3();
      //virtual HRESULT STDMETHODCALLTYPE GetWindowAssociation( 
      //    /* [annotation][out] */ 
      //    __out  HWND *pWindowHandle) = 0;
      ISwapChain CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] object Device, SWAP_CHAIN_DESC* Desc);
      void dummy5();
      //virtual HRESULT STDMETHODCALLTYPE CreateSoftwareAdapter( 
      //    /* [in] */ HMODULE Module,
      //    /* [annotation][out] */ 
      //    __out  IDXGIAdapter **ppAdapter) = 0;
    }

    public enum FORMAT
    {
      UNKNOWN = 0,
      R32G32B32A32_TYPELESS = 1,
      R32G32B32A32_FLOAT = 2,
      R32G32B32A32_UINT = 3,
      R32G32B32A32_SINT = 4,
      R32G32B32_TYPELESS = 5,
      R32G32B32_FLOAT = 6,
      R32G32B32_UINT = 7,
      R32G32B32_SINT = 8,
      R16G16B16A16_TYPELESS = 9,
      R16G16B16A16_FLOAT = 10,
      R16G16B16A16_UNORM = 11,
      R16G16B16A16_UINT = 12,
      R16G16B16A16_SNORM = 13,
      R16G16B16A16_SINT = 14,
      R32G32_TYPELESS = 15,
      R32G32_FLOAT = 16,
      R32G32_UINT = 17,
      R32G32_SINT = 18,
      R32G8X24_TYPELESS = 19,
      D32_FLOAT_S8X24_UINT = 20,
      R32_FLOAT_X8X24_TYPELESS = 21,
      X32_TYPELESS_G8X24_UINT = 22,
      R10G10B10A2_TYPELESS = 23,
      R10G10B10A2_UNORM = 24,
      R10G10B10A2_UINT = 25,
      R11G11B10_FLOAT = 26,
      R8G8B8A8_TYPELESS = 27,
      R8G8B8A8_UNORM = 28,
      R8G8B8A8_UNORM_SRGB = 29,
      R8G8B8A8_UINT = 30,
      R8G8B8A8_SNORM = 31,
      R8G8B8A8_SINT = 32,
      R16G16_TYPELESS = 33,
      R16G16_FLOAT = 34,
      R16G16_UNORM = 35,
      R16G16_UINT = 36,
      R16G16_SNORM = 37,
      R16G16_SINT = 38,
      R32_TYPELESS = 39,
      D32_FLOAT = 40,
      R32_FLOAT = 41,
      R32_UINT = 42,
      R32_SINT = 43,
      R24G8_TYPELESS = 44,
      D24_UNORM_S8_UINT = 45,
      R24_UNORM_X8_TYPELESS = 46,
      X24_TYPELESS_G8_UINT = 47,
      R8G8_TYPELESS = 48,
      R8G8_UNORM = 49,
      R8G8_UINT = 50,
      R8G8_SNORM = 51,
      R8G8_SINT = 52,
      R16_TYPELESS = 53,
      R16_FLOAT = 54,
      D16_UNORM = 55,
      R16_UNORM = 56,
      R16_UINT = 57,
      R16_SNORM = 58,
      R16_SINT = 59,
      R8_TYPELESS = 60,
      R8_UNORM = 61,
      R8_UINT = 62,
      R8_SNORM = 63,
      R8_SINT = 64,
      A8_UNORM = 65,
      R1_UNORM = 66,
      R9G9B9E5_SHAREDEXP = 67,
      R8G8_B8G8_UNORM = 68,
      G8R8_G8B8_UNORM = 69,
      BC1_TYPELESS = 70,
      BC1_UNORM = 71,
      BC1_UNORM_SRGB = 72,
      BC2_TYPELESS = 73,
      BC2_UNORM = 74,
      BC2_UNORM_SRGB = 75,
      BC3_TYPELESS = 76,
      BC3_UNORM = 77,
      BC3_UNORM_SRGB = 78,
      BC4_TYPELESS = 79,
      BC4_UNORM = 80,
      BC4_SNORM = 81,
      BC5_TYPELESS = 82,
      BC5_UNORM = 83,
      BC5_SNORM = 84,
      B5G6R5_UNORM = 85,
      B5G5R5A1_UNORM = 86,
      B8G8R8A8_UNORM = 87,
      B8G8R8X8_UNORM = 88,
      R10G10B10_XR_BIAS_A2_UNORM = 89,
      B8G8R8A8_TYPELESS = 90,
      B8G8R8A8_UNORM_SRGB = 91,
      B8G8R8X8_TYPELESS = 92,
      B8G8R8X8_UNORM_SRGB = 93,
      BC6H_TYPELESS = 94,
      BC6H_UF16 = 95,
      BC6H_SF16 = 96,
      BC7_TYPELESS = 97,
      BC7_UNORM = 98,
      BC7_UNORM_SRGB = 99,
    }

    enum MODE_SCANLINE_ORDER { UNSPECIFIED = 0, PROGRESSIVE = 1, UPPER_FIELD_FIRST = 2, LOWER_FIELD_FIRST = 3 }
    enum MODE_SCALING { UNSPECIFIED = 0, CENTERED = 1, STRETCHED = 2 }
    enum BUFFERUSAGE { SHADER_INPUT = (1 << (0 + 4)), RENDER_TARGET_OUTPUT = (1 << (1 + 4)), BACK_BUFFER = (1 << (2 + 4)), SHARED = (1 << (3 + 4)), READ_ONLY = (1 << (4 + 4)), DISCARD_ON_PRESENT = (1 << (5 + 4)), UNORDERED_ACCESS = (1 << (6 + 4)), }
    enum SWAP_EFFECT { DISCARD = 0, SEQUENTIAL = 1 }
    enum MWA_NO { WINDOW_CHANGES = (1 << 0), ALT_ENTER = (1 << 1), PRINT_SCREEN = (1 << 2) }

    struct RATIONAL
    {
      public int Numerator;
      public int Denominator;
    }
    struct MODE_DESC
    {
      public int Width;
      public int Height;
      public RATIONAL RefreshRate;
      public FORMAT Format;
      public MODE_SCANLINE_ORDER ScanlineOrdering;
      public MODE_SCALING Scaling;
    }
    struct SAMPLE_DESC
    {
      public int Count;
      public int Quality;
    }
    struct SWAP_CHAIN_DESC //DXGI_SWAP_CHAIN_DESC
    {
      public MODE_DESC BufferDesc;
      public SAMPLE_DESC SampleDesc;
      public BUFFERUSAGE BufferUsage;
      public int BufferCount;
      public IntPtr OutputWindow;
      public int Windowed;
      public SWAP_EFFECT SwapEffect;
      public int Flags;
    }
    struct FRAME_STATISTICS
    {
      public uint PresentCount;
      public uint PresentRefreshCount;
      public uint SyncRefreshCount;
      public long SyncQPCTime;
      public long SyncGPUTime;
    }

  }

  unsafe partial class D3DView
  {
    public static Bitmap Print(int dx, int dy, int samples, uint bkcolor, Action<IDisplay> print)
    {
      TEXTURE2D_DESC td;
      td.Width = dx; td.Height = dy;
      td.ArraySize = td.MipLevels = 1;
      td.BindFlags = BIND.RENDER_TARGET | BIND.SHADER_RESOURCE;
      td.Format = FORMAT.B8G8R8A8_UNORM;
      td.SampleDesc = CheckMultisample(device, td.Format, Math.Max(1, samples));
      var tex = device.CreateTexture2D(&td);
      RENDER_TARGET_VIEW_DESC rdesc;
      rdesc.Format = td.Format;
      rdesc.ViewDimension = td.SampleDesc.Count > 1 ? RTV_DIMENSION.TEXTURE2DMS : RTV_DIMENSION.TEXTURE2D;
      var rtv = device.CreateRenderTargetView(tex, &rdesc);
      td.BindFlags = BIND.DEPTH_STENCIL;
      td.Format = FORMAT.D24_UNORM_S8_UINT;
      var ds = device.CreateTexture2D(&td);
      DEPTH_STENCIL_VIEW_DESC ddesc;
      ddesc.Format = td.Format;
      ddesc.ViewDimension = td.SampleDesc.Count > 1 ? DSV_DIMENSION.TEXTURE2DMS : DSV_DIMENSION.TEXTURE2D;
      var dsv = device.CreateDepthStencilView(ds, &ddesc);
      VIEWPORT viewport; (&viewport)->Width = dx; viewport.Height = dy; viewport.MaxDepth = 1;
      Begin(rtv, dsv, viewport, bkcolor);
      print(new D3DView());
      var zero = IntPtr.Zero; context.OMSetRenderTargets(1, ref zero, zero);
      Marshal.Release(dsv); Marshal.Release(ds); Marshal.Release(rtv);
      td.BindFlags = 0;
      td.Format = FORMAT.B8G8R8A8_UNORM;
      if (td.SampleDesc.Count > 1)
      {
        td.SampleDesc.Count = 1;
        td.SampleDesc.Quality = 0;
        td.Usage = USAGE.DEFAULT;
        var t1 = device.CreateTexture2D(&td);
        context.ResolveSubresource(t1, 0, tex, 0, td.Format);
        Marshal.Release(tex); tex = t1;
      }
      td.Usage = USAGE.STAGING;
      td.CPUAccessFlags = CPU_ACCESS_FLAG.READ;
      var t2 = device.CreateTexture2D(&td);
      context.CopyResource(t2, tex);
      Marshal.Release(tex); tex = t2;
      var map = context.Map(tex, 0, MAP.READ, 0);
      var a = new Bitmap(dx, dy, map.RowPitch, System.Drawing.Imaging.PixelFormat.Format32bppArgb, new IntPtr(map.pData));
      var b = new Bitmap(a); a.Dispose();
      context.Unmap(tex, 0); Marshal.Release(tex);
      return b;
    }
  }

  public static unsafe class Extensions
  {
    public static void PushTransform(this IDisplay dc, float3x4 m)
    {
      dc.Operator(0x82, &m); //push wm, wm = m * wm
    }
    public static void PushTransform(this IDisplay dc, float2 trans)
    {
      dc.Operator(0x892, &trans); //push wm, conv float2, wm = m * wm
    }
    public static void PopTransform(this IDisplay dc)
    {
      dc.Operator(0x4, null); //pop wm 
    }

    static Texture gettex(int id)
    {
      return GetTexture(32, 32, gr =>
      {
        gr.FillEllipse(System.Drawing.Brushes.Black, 1, 1, 30, 30);
        gr.FillEllipse(System.Drawing.Brushes.White, 4, 4, 30 - 6, 30 - 6);
        return 0;
      });
    }
    static Texture texpt32;

    public static void DrawPoint(this IDisplay dc, float3 p, float radius)
    {
      dc.DrawPoints(&p, 1, radius);
    }
    public static void DrawPoints(this IDisplay dc, float3* pp, int np, float radius)
    {
      var t2 = dc.Texture; dc.Texture = texpt32 ?? (texpt32 = gettex(0));
      var t0 = dc.State;
      dc.PixelShader = PixelShader.AlphaTexture;
      dc.BlendState = BlendState.Alpha;
      dc.Rasterizer = Rasterizer.CullNone;
      dc.DepthStencil = DepthStencil.ZWrite;
      float4x4 m1, m2; dc.Operator(0x75, &m1); m2 = !m1; //wm * vp * vm 
      float2 t1; t1.x = -radius; t1.y = +radius;
      for (int i = 0; i < np; i++)
      {
        float3 mp = pp[i] * m1;
        var v = dc.BeginVertices(4);
        v[0].t.x = v[2].t.x = 0; v[1].t.x = v[3].t.x = 1;
        v[0].t.y = v[1].t.y = 0; v[2].t.y = v[3].t.y = 1;
        for (int t = 0; t < 4; t++, v++)
        {
          v->p.x = mp.x + (&t1.x)[(t >> 1) & 1];
          v->p.y = mp.y + (&t1.x)[t & 1];
          v->p.z = mp.z; v->p = v->p * m2;
        }
        dc.EndVertices(4, i == 0 ? Topology.TriangleStrip : 0);
      }
      dc.State = t0; dc.Texture = t2;
    }
    public static void DrawPoints(this IDisplay dc, IEnumerable<float3> pp, float radius)
    {
      var a = (float3*)StackPtr; int n = 0; for (var e = pp.GetEnumerator(); e.MoveNext(); a[n++] = e.Current) ;
      StackPtr = (byte*)(a + n); DrawPoints(dc, a, n, radius); StackPtr = (byte*)a;
    }
    public static void DrawPoints(this IDisplay dc, float3 box, float radius)
    {
      var pp = stackalloc float3[8];
      pp[2].x = pp[3].x = pp[4].x = box.x;
      pp[4].y = pp[5].y = pp[6].y = box.y;
      pp[1].x = pp[5].x = box.x * .5f;
      pp[3].y = pp[7].y = box.y * .5f; for (int i = 0; i < 8; i++) pp[i].z = box.z;
      dc.DrawPoints(pp, 8, 5);
    }
    public static void DrawLines(this IDisplay dc, float3* pp, int np)
    {
      var vv = dc.BeginVertices(np); D3DView.Assert((np & 1) == 0);
      for (int i = 0; i < np; i++) vv[i].p = pp[i];
      dc.EndVertices(np, Topology.LineList);
    }
    public static void DrawPolyline(this IDisplay dc, float3* pp, int np)
    {
      var vv = dc.BeginVertices(np);
      for (int i = 0; i < np; i++) vv[i].p = pp[i];
      dc.EndVertices(np, Topology.LineStrip);
    }
    public static void DrawPolygon(this IDisplay dc, float3* pp, int np)
    {
      var vv = dc.BeginVertices(++np);
      for (int i = 0, k = np - 2; i < np; k = i++) vv[i].p = pp[k];
      dc.EndVertices(np, Topology.LineStrip);
    }
    public static void DrawPolygon(this IDisplay dc, float3* pp, int np, float radius)
    {
      bool open; if (open = np < 0) np = -np;
      float4x4 m1, m2; dc.Operator(0x75, &m1); m2 = !m1; //wm * vp * vm 
      var tt = (float3*)StackPtr;
      for (int i = 0; i < np; i++) tt[i] = pp[i] * m1;
      if (!open) tt[np++] = tt[0];
      var nu = 0; var uu = tt + np;
      for (int i = 0; i < np - 1; i++)
      {
        var r = ~normalize(*(float2*)&tt[i + 1] - *(float2*)&tt[i + 0]) * radius;
        uu[nu++] = tt[i + 0] - r; uu[nu++] = tt[i + 0] + r;
        uu[nu++] = tt[i + 1] - r; uu[nu++] = tt[i + 1] + r;
      }
      for (int i = 0; i < nu; i++) uu[i] = uu[i] * m2; //if (!open) { uu[nu++] = uu[0]; uu[nu++] = uu[1]; }
      var step = dc.IsPicking ? 4 : nu;
      for (int i = 0; i < nu; i += step)
      {
        var v = dc.BeginVertices(step); for (int t = 0; t < step; t++) v[t].p = uu[i + t];
        dc.EndVertices(step, i == 0 ? Topology.TriangleStrip : 0);
      }
    }
    public static void DrawPolygon(this IDisplay dc, IEnumerable<float3> pp, float radius, bool closed = true)
    {
      var a = (float3*)StackPtr; int n = 0; for (var e = pp.GetEnumerator(); e.MoveNext(); a[n++] = e.Current) ;
      StackPtr = (byte*)(a + n); DrawPolygon(dc, a, closed ? n : -n, radius); StackPtr = (byte*)a;
    }

    public static void DrawLine(this IDisplay dc, float2 a, float2 b)
    {
      float2x3 m; var p = (float3*)&m; *(float2*)&p[0] = a; *(float2*)&p[1] = b; dc.DrawPolyline(p, 2);
    }
    public static void DrawLine(this IDisplay dc, float3 a, float3 b)
    {
      float2x3 m; var p = (float3*)&m; p[0] = a; p[1] = b; dc.DrawPolyline(p, 2);
    }
    public static void DrawLine(this IDisplay dc, float3 a, float3 b, float radius)
    {
      float2x3 m; var p = (float3*)&m; p[0] = a; p[1] = b; dc.DrawPolygon(p, -2, radius);
    }
    public static void DrawRect(this IDisplay dc, float x, float y, float dx, float dy)
    {
      float3x4 m; var p = (float3*)&m;
      p[0].x = p[3].x = x;
      p[0].y = p[1].y = y;
      p[1].x = p[2].x = x + dx;
      p[2].y = p[3].y = y + dy;
      dc.DrawPolygon(p, 4);
    }
    public static void DrawEllipse(this IDisplay dc, float x, float y, float dx, float dy)
    {
      x += (dx *= 0.5f); y += (dy *= 0.5f);
      int nv = csegs(dc, dx, dy) + 1;
      var fa = (float)(2 * Math.PI) / (nv - 1);
      var vv = dc.BeginVertices(nv);
      for (int i = 0; i < nv; i++)
      {
        vv[i].p.x = x + (float)Math.Sin(i * fa) * dx;
        vv[i].p.y = y + (float)Math.Cos(i * fa) * dy;
      }
      dc.EndVertices(nv, Topology.LineStrip);
    }
    public static void DrawPath(this IDisplay dc, float2* pp, int np, bool closed)
    {
      var nt = closed ? np + 1 : np; var vv = dc.BeginVertices(nt);
      for (int i = 0; i < np; i++) *(float2*)&vv[i].p = pp[i]; if (closed) *(float2*)&vv[np].p = pp[0];
      dc.EndVertices(nt, Topology.LineStrip);
    }
    public static void DrawImage(this IDisplay dc, Texture tex, float x, float y, float dx, float dy)
    {
      var t1 = dc.Texture; dc.Texture = tex;
      var t2 = dc.State; dc.PixelShader = PixelShader.AlphaTexture;
      var vv = dc.BeginVertices(4);
      vv[0].p.x = vv[2].p.x = x;
      vv[1].p.x = vv[3].p.x = x + dx;
      vv[0].p.y = vv[1].p.y = y;
      vv[2].p.y = vv[3].p.y = y + dy;
      vv[1].t.x = vv[3].t.x = vv[2].t.y = vv[3].t.y = 1;
      dc.EndVertices(4, Topology.TriangleStrip); dc.Texture = t1; dc.State = t2;
    }

    static float3 boxpt(float3* box, int f) { return new float3(box[f & 1].x, box[(f >> 1) & 1].y, box[(f >> 2) & 1].z); }

    public static void DrawBox(this IDisplay dc, float3* box)
    {
      long f1 = 0x4c8948990, f2 = 0xdecddabd9;
      for (int t = 0; t < 12; t++, f1 >>= 3, f2 >>= 3)
        dc.DrawLine(boxpt(box, (int)f1), boxpt(box, (int)f2));
    }
    public static void FillRect(this IDisplay dc, float x, float y, float dx, float dy)
    {
      var vv = dc.BeginVertices(4);
      vv[0].p.x = vv[2].p.x = x;
      vv[1].p.x = vv[3].p.x = x + dx;
      vv[0].p.y = vv[1].p.y = y;
      vv[2].p.y = vv[3].p.y = y + dy;
      dc.EndVertices(4, Topology.TriangleStrip);
    }
    public static void FillEllipse(this IDisplay dc, float x, float y, float dx, float dy)
    {
      x += (dx *= 0.5f); y += (dy *= 0.5f);
      var se = csegs(dc, dx, dy);
      var fa = (2 * (float)Math.PI) / se;
      var nv = se + 2;
      var vv = dc.BeginVertices(nv);
      for (int i = 0, j = 0; j < nv; i++)
      {
        var u = (float)Math.Sin(i * fa) * dx;
        var v = (float)Math.Cos(i * fa) * dy;
        vv[j].p.x = x + u; vv[j++].p.y = y + v;
        vv[j].p.x = x - u; vv[j++].p.y = y + v;
      }
      dc.EndVertices(nv, Topology.TriangleStrip);
    }
    public static void FillRoundRect(this IDisplay dc, float x, float y, float dx, float dy, float ra)
    {
      float2 pm, po;
      po.x = x + (pm.x = dx * 0.5f);
      po.y = y + (pm.y = dy * 0.5f);
      var se = csegs(dc, ra, ra);
      var fa = (2 * (float)Math.PI) / (se - 2);
      var nv = se + 4;
      var vv = dc.BeginVertices(nv);
      var ddy = (pm.y - ra) * 2;
      for (int i = 0, j = 0, im = se >> 2; j < nv; i++)
      {
        var p = sincos(i * fa) * ra;
        p.x += pm.x - ra;
        p.y += pm.y - ra; if (i > im) p.y -= ddy;
        *(float2*)&vv[j].p = po - p; j++; p.x = -p.x;
        *(float2*)&vv[j].p = po - p; j++;
        if (i != im) continue; p.y -= ddy; p.x = -p.x;
        *(float2*)&vv[j].p = po - p; j++; p.x = -p.x;
        *(float2*)&vv[j].p = po - p; j++;
      }
      dc.EndVertices(nv, Topology.TriangleStrip);
    }

    public static void FillFrame(this IDisplay dc,
      float x1, float x2,
      float y1, float y2,
      float u1, float u2,
      float v1, float v2)
    {
      var vv = dc.BeginVertices(4);
      vv[0].p.x = vv[2].p.x = x1;
      vv[0].p.y = vv[1].p.y = y1;
      vv[1].p.x = vv[3].p.x = x2;
      vv[2].p.y = vv[3].p.y = y2;
      vv[0].t.x = vv[2].t.x = u1;
      vv[0].t.y = vv[1].t.y = v1;
      vv[1].t.x = vv[3].t.x = u2;
      vv[2].t.y = vv[3].t.y = v2;
      dc.EndVertices(4, Topology.TriangleStrip);
    }

    public static void FillFrame(this IDisplay dc,
      float x1, float x2, float x3, float x4,
      float y1, float y2, float y3, float y4,
      float u1, float u2, float u3, float u4,
      float v1, float v2, float v3, float v4)
    {
      var vv = dc.BeginVertices(24);
      vv[00].p.x = vv[17].p.x = vv[19].p.x = vv[21].p.x = vv[23].p.x = x1;
      vv[00].p.y = vv[01].p.y = vv[03].p.y = vv[05].p.y = vv[23].p.y = y1;
      vv[01].p.x = vv[02].p.x = vv[15].p.x = vv[16].p.x = vv[18].p.x = vv[20].p.x = vv[22].p.x = x2;
      vv[04].p.y = vv[02].p.y = vv[06].p.y = vv[08].p.y = vv[07].p.y = vv[21].p.y = vv[22].p.y = y2;
      vv[03].p.x = vv[04].p.x = vv[06].p.x = vv[08].p.x = vv[10].p.x = vv[12].p.x = vv[13].p.x = vv[14].p.x = x3;
      vv[09].p.y = vv[10].p.y = vv[12].p.y = vv[14].p.y = vv[16].p.y = vv[18].p.y = vv[19].p.y = vv[20].p.y = y3;
      vv[05].p.x = vv[07].p.x = vv[09].p.x = vv[11].p.x = x4;
      vv[11].p.y = vv[13].p.y = vv[15].p.y = vv[17].p.y = y4;
      vv[00].t.x = vv[17].t.x = vv[19].t.x = vv[21].t.x = vv[23].t.x = u1;
      vv[00].t.y = vv[01].t.y = vv[03].t.y = vv[05].t.y = vv[23].t.y = v1;
      vv[01].t.x = vv[02].t.x = vv[15].t.x = vv[16].t.x = vv[18].t.x = vv[20].t.x = vv[22].t.x = u2;
      vv[04].t.y = vv[02].t.y = vv[06].t.y = vv[08].t.y = vv[07].t.y = vv[21].t.y = vv[22].t.y = v2;
      vv[03].t.x = vv[04].t.x = vv[06].t.x = vv[08].t.x = vv[10].t.x = vv[12].t.x = vv[13].t.x = vv[14].t.x = u3;
      vv[09].t.y = vv[10].t.y = vv[12].t.y = vv[14].t.y = vv[16].t.y = vv[18].t.y = vv[19].t.y = vv[20].t.y = v3;
      vv[05].t.x = vv[07].t.x = vv[09].t.x = vv[11].t.x = u4;
      vv[11].t.y = vv[13].t.y = vv[15].t.y = vv[17].t.y = v4;
      dc.EndVertices(24, Topology.TriangleStrip);
    }

    public static void FillPolygon(this IDisplay dc, float2* pp, int np)
    {
      if (np >= 0)//todo: concave
      {
        //Tess.BeginPolygon();
        //Tess.BeginContour();
        //for (int i = 0; i < np; i++) Tess.Add(pp[i]);
        //Tess.EndContour();
        //Tess.EndPolygon();
        //Tess.Draw(dc);
        return;
      }
      var nv = ((np = -np - 1) - 1) * 3;
      var vv = dc.BeginVertices(nv);
      for (int i = 1, j = 0; i < np; i++) { *(float2*)&vv[j++].p = pp[0]; *(float2*)&vv[j++].p = pp[i]; *(float2*)&vv[j++].p = pp[i + 1]; }
      dc.EndVertices(nv, Topology.TriangleStrip);
    }

    public static void DrawScreen(this IDisplay dc, float3 p, int fl, Action<IDisplay> draw) //fl 1: keep z
    {
      float4x4 tm; dc.Operator(0x7521, &tm); //push vp, push wm, wm*vp*vm 
      p *= tm; p.x = (int)p.x; p.y = (int)p.y; if ((fl & 1) == 0) p.z = 0;
      dc.Operator(0x6, &tm); tm = !tm;//vm
      dc.SetProjection(tm); dc.SetTransform(p); draw(dc);
      dc.Operator(0x34, null); //pop wm, pop vp 

      //float4x4 t0; dc.GetTransform(0, &t0); 
      //var t1 = dc.Projection; var t2 = dc.Transform;
      //p *= t2 * t1 * t0; //todo: optimize
      //p.x = (int)p.x; p.y = (int)p.y; if ((fl & 1) == 0) p.z = 0;
      //dc.Projection = !t0; dc.Transform = p; draw(dc);
      //dc.Projection = t1; dc.Transform = t2;
    }
    public static void DrawArrow(this IDisplay dc, float3 p, float3 v, float r, int s = 10)
    {
      var t1 = dc.State;// SetMode(Mode.Color3dNoCull);
      dc.VertexShader = VertexShader.Lighting;
      dc.PixelShader = PixelShader.Color3D;// | (DepthStencil.ZWrite << 8) | (Rasterizer.CullNone << 16),
      dc.Rasterizer = Rasterizer.CullNone;
      var fa = (float)(2 * Math.PI) / s++;
      var rl = 1 / length(v);
      var r1 = new float3(v.z, v.x, v.y) * rl;
      var r2 = new float3(v.y, v.z, v.x) * rl;
      var vv = dc.BeginVertices(s << 1);
      for (int i = 0; i < s; i++)
      {
        var n = r1 * (float)Math.Sin(rl = i * fa) + r2 * (float)Math.Cos(rl);
        vv[(i << 1) + 0].p = p + n * r;
        vv[(i << 1) + 1].p = p + v;
        vv[(i << 1) + 0].n = vv[(i << 1) + 1].n = n;
      }
      dc.EndVertices(s << 1, Topology.TriangleStrip);
      dc.State = t1;
    }
    //Text
    public static float Measure(this IDisplay dc, string s, int n = -1)
    {
      var cx = float.MaxValue; fixed (char* p = s) dc.Font.Measure(p, n != -1 ? n : s.Length, ref cx); return cx;
    }
    public static void DrawText(this IDisplay dc, float x, float y, string s)
    {
      var t1 = dc.State; dc.State = 0x00121040;// dc.PixelShader = PixelShader.Font; dc.BlendState = BlendState.Alpha; dc.Sampler = Sampler.Font; dc.Rasterizer = Rasterizer.CullBack; //for draw2d
      fixed (char* p = s) dc.Font.Draw(dc, x, y, p, s.Length);
      dc.State = t1;
    }

    #region private
    //static byte* codecs;
    static int csegs(IDisplay dc, float rx, float ry)
    {
      //return 8;
      var tt = (int)((float)Math.Pow(Math.Max(Math.Abs(rx), Math.Abs(ry)), 0.95f) * dc.PixelScale);
      return Math.Max(8, Math.Min(200, tt)) >> 1 << 1;
    }
    #endregion

  }

}
