

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for cdx.idl:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0622 
    protocol : all , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef __cdx_i_h__
#define __cdx_i_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __ICDXView_FWD_DEFINED__
#define __ICDXView_FWD_DEFINED__
typedef interface ICDXView ICDXView;

#endif 	/* __ICDXView_FWD_DEFINED__ */


#ifndef __ICDXSink_FWD_DEFINED__
#define __ICDXSink_FWD_DEFINED__
typedef interface ICDXSink ICDXSink;

#endif 	/* __ICDXSink_FWD_DEFINED__ */


#ifndef __ICDXScene_FWD_DEFINED__
#define __ICDXScene_FWD_DEFINED__
typedef interface ICDXScene ICDXScene;

#endif 	/* __ICDXScene_FWD_DEFINED__ */


#ifndef __ICDXMaterial_FWD_DEFINED__
#define __ICDXMaterial_FWD_DEFINED__
typedef interface ICDXMaterial ICDXMaterial;

#endif 	/* __ICDXMaterial_FWD_DEFINED__ */


#ifndef __ICDXNode_FWD_DEFINED__
#define __ICDXNode_FWD_DEFINED__
typedef interface ICDXNode ICDXNode;

#endif 	/* __ICDXNode_FWD_DEFINED__ */


#ifndef __ICDXFont_FWD_DEFINED__
#define __ICDXFont_FWD_DEFINED__
typedef interface ICDXFont ICDXFont;

#endif 	/* __ICDXFont_FWD_DEFINED__ */


#ifndef __ICDXFactory_FWD_DEFINED__
#define __ICDXFactory_FWD_DEFINED__
typedef interface ICDXFactory ICDXFactory;

#endif 	/* __ICDXFactory_FWD_DEFINED__ */


#ifndef __Factory_FWD_DEFINED__
#define __Factory_FWD_DEFINED__

#ifdef __cplusplus
typedef class Factory Factory;
#else
typedef struct Factory Factory;
#endif /* __cplusplus */

#endif 	/* __Factory_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"
#include "shobjidl.h"

#ifdef __cplusplus
extern "C"{
#endif 


/* interface __MIDL_itf_cdx_0000_0000 */
/* [local] */ 







#if(0)
typedef IUnknown ICSGMesh;

typedef DECIMAL CSGVAR;

typedef DECIMAL XMFLOAT3;

typedef DECIMAL XMFLOAT4X3;

typedef DECIMAL XMFLOAT4X4;

#endif
typedef 
enum CDX_RENDER
    {
        CDX_RENDER_BOUNDINGBOX	= 0x1,
        CDX_RENDER_COORDINATES	= 0x2,
        CDX_RENDER_NORMALS	= 0x4,
        CDX_RENDER_WIREFRAME	= 0x8,
        CDX_RENDER_OUTLINES	= 0x10,
        CDX_RENDER_SHADOWS	= 0x400
    } 	CDX_RENDER;

typedef 
enum CDX_CMD
    {
        CDX_CMD_CENTER	= 1,
        CDX_CMD_GETBOX	= 2,
        CDX_CMD_GETBOXSEL	= 3,
        CDX_CMD_SETPLANE	= 4,
        CDX_CMD_PICKPLANE	= 5,
        CDX_CMD_SELECTRECT	= 6
    } 	CDX_CMD;

typedef 
enum CDX_DRAW
    {
        CDX_DRAW_ORTHOGRAPHIC	= 0,
        CDX_DRAW_GET_TRANSFORM	= 1,
        CDX_DRAW_SET_TRANSFORM	= 2,
        CDX_DRAW_GET_COLOR	= 3,
        CDX_DRAW_SET_COLOR	= 4,
        CDX_DRAW_GET_FONT	= 5,
        CDX_DRAW_SET_FONT	= 6,
        CDX_DRAW_FILL_RECT	= 7,
        CDX_DRAW_FILL_ELLIPSE	= 8,
        CDX_DRAW_GET_TEXTEXTENT	= 9,
        CDX_DRAW_DRAW_TEXT	= 10,
        CDX_DRAW_DRAW_RECT	= 11
    } 	CDX_DRAW;



extern RPC_IF_HANDLE __MIDL_itf_cdx_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_cdx_0000_0000_v0_0_s_ifspec;

#ifndef __ICDXView_INTERFACE_DEFINED__
#define __ICDXView_INTERFACE_DEFINED__

/* interface ICDXView */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXView;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("4C0EC273-CA2F-48F4-B871-E487E2774492")
    ICDXView : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Samples( 
            /* [retval][out] */ BSTR *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Samples( 
            /* [in] */ BSTR p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_BkColor( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_BkColor( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Render( 
            /* [retval][out] */ CDX_RENDER *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Render( 
            /* [in] */ CDX_RENDER p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Scene( 
            /* [retval][out] */ ICDXScene **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Scene( 
            /* [in] */ ICDXScene *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Camera( 
            /* [retval][out] */ ICDXNode **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Camera( 
            /* [in] */ ICDXNode *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_OverNode( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_OverPoint( 
            /* [retval][out] */ XMFLOAT3 *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Draw( 
            /* [in] */ CDX_DRAW idc,
            /* [in] */ UINT *data) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Command( 
            /* [in] */ CDX_CMD cmd,
            /* [in] */ UINT *data) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Thumbnail( 
            /* [in] */ UINT dx,
            /* [in] */ UINT dy,
            /* [in] */ UINT samples,
            /* [in] */ UINT bkcolor,
            /* [in] */ IStream *str) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXViewVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXView * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXView * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXView * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Samples )( 
            ICDXView * This,
            /* [retval][out] */ BSTR *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Samples )( 
            ICDXView * This,
            /* [in] */ BSTR p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_BkColor )( 
            ICDXView * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_BkColor )( 
            ICDXView * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Render )( 
            ICDXView * This,
            /* [retval][out] */ CDX_RENDER *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Render )( 
            ICDXView * This,
            /* [in] */ CDX_RENDER p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Scene )( 
            ICDXView * This,
            /* [retval][out] */ ICDXScene **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Scene )( 
            ICDXView * This,
            /* [in] */ ICDXScene *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Camera )( 
            ICDXView * This,
            /* [retval][out] */ ICDXNode **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Camera )( 
            ICDXView * This,
            /* [in] */ ICDXNode *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_OverNode )( 
            ICDXView * This,
            /* [retval][out] */ UINT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_OverPoint )( 
            ICDXView * This,
            /* [retval][out] */ XMFLOAT3 *p);
        
        HRESULT ( STDMETHODCALLTYPE *Draw )( 
            ICDXView * This,
            /* [in] */ CDX_DRAW idc,
            /* [in] */ UINT *data);
        
        HRESULT ( STDMETHODCALLTYPE *Command )( 
            ICDXView * This,
            /* [in] */ CDX_CMD cmd,
            /* [in] */ UINT *data);
        
        HRESULT ( STDMETHODCALLTYPE *Thumbnail )( 
            ICDXView * This,
            /* [in] */ UINT dx,
            /* [in] */ UINT dy,
            /* [in] */ UINT samples,
            /* [in] */ UINT bkcolor,
            /* [in] */ IStream *str);
        
        END_INTERFACE
    } ICDXViewVtbl;

    interface ICDXView
    {
        CONST_VTBL struct ICDXViewVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXView_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXView_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXView_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXView_get_Samples(This,p)	\
    ( (This)->lpVtbl -> get_Samples(This,p) ) 

#define ICDXView_put_Samples(This,p)	\
    ( (This)->lpVtbl -> put_Samples(This,p) ) 

#define ICDXView_get_BkColor(This,p)	\
    ( (This)->lpVtbl -> get_BkColor(This,p) ) 

#define ICDXView_put_BkColor(This,p)	\
    ( (This)->lpVtbl -> put_BkColor(This,p) ) 

#define ICDXView_get_Render(This,p)	\
    ( (This)->lpVtbl -> get_Render(This,p) ) 

#define ICDXView_put_Render(This,p)	\
    ( (This)->lpVtbl -> put_Render(This,p) ) 

#define ICDXView_get_Scene(This,p)	\
    ( (This)->lpVtbl -> get_Scene(This,p) ) 

#define ICDXView_put_Scene(This,p)	\
    ( (This)->lpVtbl -> put_Scene(This,p) ) 

#define ICDXView_get_Camera(This,p)	\
    ( (This)->lpVtbl -> get_Camera(This,p) ) 

#define ICDXView_put_Camera(This,p)	\
    ( (This)->lpVtbl -> put_Camera(This,p) ) 

#define ICDXView_get_OverNode(This,p)	\
    ( (This)->lpVtbl -> get_OverNode(This,p) ) 

#define ICDXView_get_OverPoint(This,p)	\
    ( (This)->lpVtbl -> get_OverPoint(This,p) ) 

#define ICDXView_Draw(This,idc,data)	\
    ( (This)->lpVtbl -> Draw(This,idc,data) ) 

#define ICDXView_Command(This,cmd,data)	\
    ( (This)->lpVtbl -> Command(This,cmd,data) ) 

#define ICDXView_Thumbnail(This,dx,dy,samples,bkcolor,str)	\
    ( (This)->lpVtbl -> Thumbnail(This,dx,dy,samples,bkcolor,str) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXView_INTERFACE_DEFINED__ */


#ifndef __ICDXSink_INTERFACE_DEFINED__
#define __ICDXSink_INTERFACE_DEFINED__

/* interface ICDXSink */
/* [local][unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXSink;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("982A1DBA-0C12-4342-8F58-A34D83956F0D")
    ICDXSink : public IUnknown
    {
    public:
        virtual void STDMETHODCALLTYPE Render( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXSinkVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXSink * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXSink * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXSink * This);
        
        void ( STDMETHODCALLTYPE *Render )( 
            ICDXSink * This);
        
        END_INTERFACE
    } ICDXSinkVtbl;

    interface ICDXSink
    {
        CONST_VTBL struct ICDXSinkVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXSink_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXSink_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXSink_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXSink_Render(This)	\
    ( (This)->lpVtbl -> Render(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXSink_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_cdx_0000_0002 */
/* [local] */ 

typedef 
enum CDX_UNIT
    {
        CDX_UNIT_UNDEF	= 0,
        CDX_UNIT_METER	= 1,
        CDX_UNIT_CENTIMETER	= 2,
        CDX_UNIT_MILLIMETER	= 3,
        CDX_UNIT_MICRON	= 4,
        CDX_UNIT_FOOT	= 5,
        CDX_UNIT_INCH	= 6
    } 	CDX_UNIT;



extern RPC_IF_HANDLE __MIDL_itf_cdx_0000_0002_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_cdx_0000_0002_v0_0_s_ifspec;

#ifndef __ICDXScene_INTERFACE_DEFINED__
#define __ICDXScene_INTERFACE_DEFINED__

/* interface ICDXScene */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXScene;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("98068F4F-7768-484B-A2F8-21D4F7B5D811")
    ICDXScene : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Unit( 
            /* [retval][out] */ CDX_UNIT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Unit( 
            /* [in] */ CDX_UNIT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Count( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetNode( 
            /* [in] */ UINT i,
            /* [retval][out] */ ICDXNode **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Select( 
            /* [in] */ UINT i,
            /* [in] */ UINT f,
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddNode( 
            /* [in] */ BSTR name,
            /* [retval][out] */ ICDXNode **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Remove( 
            /* [in] */ UINT i) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Insert( 
            /* [in] */ UINT i,
            /* [in] */ ICDXNode *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Clear( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SaveToStream( 
            /* [in] */ IStream *s) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE LoadFromStream( 
            /* [in] */ IStream *s) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXSceneVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXScene * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXScene * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXScene * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Unit )( 
            ICDXScene * This,
            /* [retval][out] */ CDX_UNIT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Unit )( 
            ICDXScene * This,
            /* [in] */ CDX_UNIT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Count )( 
            ICDXScene * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetNode )( 
            ICDXScene * This,
            /* [in] */ UINT i,
            /* [retval][out] */ ICDXNode **p);
        
        HRESULT ( STDMETHODCALLTYPE *Select )( 
            ICDXScene * This,
            /* [in] */ UINT i,
            /* [in] */ UINT f,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *AddNode )( 
            ICDXScene * This,
            /* [in] */ BSTR name,
            /* [retval][out] */ ICDXNode **p);
        
        HRESULT ( STDMETHODCALLTYPE *Remove )( 
            ICDXScene * This,
            /* [in] */ UINT i);
        
        HRESULT ( STDMETHODCALLTYPE *Insert )( 
            ICDXScene * This,
            /* [in] */ UINT i,
            /* [in] */ ICDXNode *p);
        
        HRESULT ( STDMETHODCALLTYPE *Clear )( 
            ICDXScene * This);
        
        HRESULT ( STDMETHODCALLTYPE *SaveToStream )( 
            ICDXScene * This,
            /* [in] */ IStream *s);
        
        HRESULT ( STDMETHODCALLTYPE *LoadFromStream )( 
            ICDXScene * This,
            /* [in] */ IStream *s);
        
        END_INTERFACE
    } ICDXSceneVtbl;

    interface ICDXScene
    {
        CONST_VTBL struct ICDXSceneVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXScene_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXScene_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXScene_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXScene_get_Unit(This,p)	\
    ( (This)->lpVtbl -> get_Unit(This,p) ) 

#define ICDXScene_put_Unit(This,p)	\
    ( (This)->lpVtbl -> put_Unit(This,p) ) 

#define ICDXScene_get_Count(This,p)	\
    ( (This)->lpVtbl -> get_Count(This,p) ) 

#define ICDXScene_GetNode(This,i,p)	\
    ( (This)->lpVtbl -> GetNode(This,i,p) ) 

#define ICDXScene_Select(This,i,f,p)	\
    ( (This)->lpVtbl -> Select(This,i,f,p) ) 

#define ICDXScene_AddNode(This,name,p)	\
    ( (This)->lpVtbl -> AddNode(This,name,p) ) 

#define ICDXScene_Remove(This,i)	\
    ( (This)->lpVtbl -> Remove(This,i) ) 

#define ICDXScene_Insert(This,i,p)	\
    ( (This)->lpVtbl -> Insert(This,i,p) ) 

#define ICDXScene_Clear(This)	\
    ( (This)->lpVtbl -> Clear(This) ) 

#define ICDXScene_SaveToStream(This,s)	\
    ( (This)->lpVtbl -> SaveToStream(This,s) ) 

#define ICDXScene_LoadFromStream(This,s)	\
    ( (This)->lpVtbl -> LoadFromStream(This,s) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXScene_INTERFACE_DEFINED__ */


#ifndef __ICDXMaterial_INTERFACE_DEFINED__
#define __ICDXMaterial_INTERFACE_DEFINED__

/* interface ICDXMaterial */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXMaterial;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("C0667169-81D3-405E-9C16-E4C22177BBAA")
    ICDXMaterial : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Index( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Index( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Length( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Length( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Color( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Color( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Texture( 
            /* [retval][out] */ IStream **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Texture( 
            /* [in] */ IStream *p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXMaterialVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXMaterial * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXMaterial * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXMaterial * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Index )( 
            ICDXMaterial * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Index )( 
            ICDXMaterial * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Length )( 
            ICDXMaterial * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Length )( 
            ICDXMaterial * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Color )( 
            ICDXMaterial * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Color )( 
            ICDXMaterial * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Texture )( 
            ICDXMaterial * This,
            /* [retval][out] */ IStream **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Texture )( 
            ICDXMaterial * This,
            /* [in] */ IStream *p);
        
        END_INTERFACE
    } ICDXMaterialVtbl;

    interface ICDXMaterial
    {
        CONST_VTBL struct ICDXMaterialVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXMaterial_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXMaterial_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXMaterial_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXMaterial_get_Index(This,p)	\
    ( (This)->lpVtbl -> get_Index(This,p) ) 

#define ICDXMaterial_put_Index(This,p)	\
    ( (This)->lpVtbl -> put_Index(This,p) ) 

#define ICDXMaterial_get_Length(This,p)	\
    ( (This)->lpVtbl -> get_Length(This,p) ) 

#define ICDXMaterial_put_Length(This,p)	\
    ( (This)->lpVtbl -> put_Length(This,p) ) 

#define ICDXMaterial_get_Color(This,p)	\
    ( (This)->lpVtbl -> get_Color(This,p) ) 

#define ICDXMaterial_put_Color(This,p)	\
    ( (This)->lpVtbl -> put_Color(This,p) ) 

#define ICDXMaterial_get_Texture(This,p)	\
    ( (This)->lpVtbl -> get_Texture(This,p) ) 

#define ICDXMaterial_put_Texture(This,p)	\
    ( (This)->lpVtbl -> put_Texture(This,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXMaterial_INTERFACE_DEFINED__ */


#ifndef __ICDXNode_INTERFACE_DEFINED__
#define __ICDXNode_INTERFACE_DEFINED__

/* interface ICDXNode */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXNode;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2BB87169-81D3-405E-9C16-E4C22177BBAA")
    ICDXNode : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Name( 
            /* [retval][out] */ BSTR *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Name( 
            /* [in] */ BSTR p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Parent( 
            /* [retval][out] */ ICDXNode **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Parent( 
            /* [in] */ ICDXNode *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Scene( 
            /* [retval][out] */ ICDXScene **p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Index( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Index( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsSelect( 
            /* [retval][out] */ BOOL *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_IsSelect( 
            /* [in] */ BOOL p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsStatic( 
            /* [retval][out] */ BOOL *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_IsStatic( 
            /* [in] */ BOOL p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Transform( 
            /* [retval][out] */ CSGVAR *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Transform( 
            /* [in] */ CSGVAR p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Mesh( 
            /* [retval][out] */ ICSGMesh **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Mesh( 
            /* [in] */ ICSGMesh *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Color( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Color( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_MaterialCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_MaterialCount( 
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Tag( 
            /* [retval][out] */ IUnknown **p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Tag( 
            /* [in] */ IUnknown *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMaterial( 
            /* [in] */ UINT i,
            /* [out] */ UINT *start,
            /* [out] */ UINT *count,
            /* [out] */ UINT *color,
            /* [out] */ IStream **tex) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetMaterial( 
            /* [in] */ UINT i,
            /* [in] */ UINT start,
            /* [in] */ UINT count,
            /* [in] */ UINT color,
            /* [in] */ IStream *tex) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTexturCoords( 
            /* [out] */ CSGVAR *m) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTexturCoords( 
            /* [in] */ CSGVAR m) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetTransform( 
            /* [out][in] */ CSGVAR *m) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetTransform( 
            /* [in] */ CSGVAR m) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddNode( 
            /* [in] */ BSTR name,
            /* [retval][out] */ ICDXNode **p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXNodeVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXNode * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXNode * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXNode * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Name )( 
            ICDXNode * This,
            /* [retval][out] */ BSTR *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Name )( 
            ICDXNode * This,
            /* [in] */ BSTR p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Parent )( 
            ICDXNode * This,
            /* [retval][out] */ ICDXNode **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Parent )( 
            ICDXNode * This,
            /* [in] */ ICDXNode *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Scene )( 
            ICDXNode * This,
            /* [retval][out] */ ICDXScene **p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Index )( 
            ICDXNode * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Index )( 
            ICDXNode * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsSelect )( 
            ICDXNode * This,
            /* [retval][out] */ BOOL *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_IsSelect )( 
            ICDXNode * This,
            /* [in] */ BOOL p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsStatic )( 
            ICDXNode * This,
            /* [retval][out] */ BOOL *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_IsStatic )( 
            ICDXNode * This,
            /* [in] */ BOOL p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Transform )( 
            ICDXNode * This,
            /* [retval][out] */ CSGVAR *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Transform )( 
            ICDXNode * This,
            /* [in] */ CSGVAR p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Mesh )( 
            ICDXNode * This,
            /* [retval][out] */ ICSGMesh **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Mesh )( 
            ICDXNode * This,
            /* [in] */ ICSGMesh *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Color )( 
            ICDXNode * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Color )( 
            ICDXNode * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_MaterialCount )( 
            ICDXNode * This,
            /* [retval][out] */ UINT *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_MaterialCount )( 
            ICDXNode * This,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Tag )( 
            ICDXNode * This,
            /* [retval][out] */ IUnknown **p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Tag )( 
            ICDXNode * This,
            /* [in] */ IUnknown *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetMaterial )( 
            ICDXNode * This,
            /* [in] */ UINT i,
            /* [out] */ UINT *start,
            /* [out] */ UINT *count,
            /* [out] */ UINT *color,
            /* [out] */ IStream **tex);
        
        HRESULT ( STDMETHODCALLTYPE *SetMaterial )( 
            ICDXNode * This,
            /* [in] */ UINT i,
            /* [in] */ UINT start,
            /* [in] */ UINT count,
            /* [in] */ UINT color,
            /* [in] */ IStream *tex);
        
        HRESULT ( STDMETHODCALLTYPE *GetTexturCoords )( 
            ICDXNode * This,
            /* [out] */ CSGVAR *m);
        
        HRESULT ( STDMETHODCALLTYPE *SetTexturCoords )( 
            ICDXNode * This,
            /* [in] */ CSGVAR m);
        
        HRESULT ( STDMETHODCALLTYPE *GetTransform )( 
            ICDXNode * This,
            /* [out][in] */ CSGVAR *m);
        
        HRESULT ( STDMETHODCALLTYPE *SetTransform )( 
            ICDXNode * This,
            /* [in] */ CSGVAR m);
        
        HRESULT ( STDMETHODCALLTYPE *AddNode )( 
            ICDXNode * This,
            /* [in] */ BSTR name,
            /* [retval][out] */ ICDXNode **p);
        
        END_INTERFACE
    } ICDXNodeVtbl;

    interface ICDXNode
    {
        CONST_VTBL struct ICDXNodeVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXNode_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXNode_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXNode_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXNode_get_Name(This,p)	\
    ( (This)->lpVtbl -> get_Name(This,p) ) 

#define ICDXNode_put_Name(This,p)	\
    ( (This)->lpVtbl -> put_Name(This,p) ) 

#define ICDXNode_get_Parent(This,p)	\
    ( (This)->lpVtbl -> get_Parent(This,p) ) 

#define ICDXNode_put_Parent(This,p)	\
    ( (This)->lpVtbl -> put_Parent(This,p) ) 

#define ICDXNode_get_Scene(This,p)	\
    ( (This)->lpVtbl -> get_Scene(This,p) ) 

#define ICDXNode_get_Index(This,p)	\
    ( (This)->lpVtbl -> get_Index(This,p) ) 

#define ICDXNode_put_Index(This,p)	\
    ( (This)->lpVtbl -> put_Index(This,p) ) 

#define ICDXNode_get_IsSelect(This,p)	\
    ( (This)->lpVtbl -> get_IsSelect(This,p) ) 

#define ICDXNode_put_IsSelect(This,p)	\
    ( (This)->lpVtbl -> put_IsSelect(This,p) ) 

#define ICDXNode_get_IsStatic(This,p)	\
    ( (This)->lpVtbl -> get_IsStatic(This,p) ) 

#define ICDXNode_put_IsStatic(This,p)	\
    ( (This)->lpVtbl -> put_IsStatic(This,p) ) 

#define ICDXNode_get_Transform(This,p)	\
    ( (This)->lpVtbl -> get_Transform(This,p) ) 

#define ICDXNode_put_Transform(This,p)	\
    ( (This)->lpVtbl -> put_Transform(This,p) ) 

#define ICDXNode_get_Mesh(This,p)	\
    ( (This)->lpVtbl -> get_Mesh(This,p) ) 

#define ICDXNode_put_Mesh(This,p)	\
    ( (This)->lpVtbl -> put_Mesh(This,p) ) 

#define ICDXNode_get_Color(This,p)	\
    ( (This)->lpVtbl -> get_Color(This,p) ) 

#define ICDXNode_put_Color(This,p)	\
    ( (This)->lpVtbl -> put_Color(This,p) ) 

#define ICDXNode_get_MaterialCount(This,p)	\
    ( (This)->lpVtbl -> get_MaterialCount(This,p) ) 

#define ICDXNode_put_MaterialCount(This,p)	\
    ( (This)->lpVtbl -> put_MaterialCount(This,p) ) 

#define ICDXNode_get_Tag(This,p)	\
    ( (This)->lpVtbl -> get_Tag(This,p) ) 

#define ICDXNode_put_Tag(This,p)	\
    ( (This)->lpVtbl -> put_Tag(This,p) ) 

#define ICDXNode_GetMaterial(This,i,start,count,color,tex)	\
    ( (This)->lpVtbl -> GetMaterial(This,i,start,count,color,tex) ) 

#define ICDXNode_SetMaterial(This,i,start,count,color,tex)	\
    ( (This)->lpVtbl -> SetMaterial(This,i,start,count,color,tex) ) 

#define ICDXNode_GetTexturCoords(This,m)	\
    ( (This)->lpVtbl -> GetTexturCoords(This,m) ) 

#define ICDXNode_SetTexturCoords(This,m)	\
    ( (This)->lpVtbl -> SetTexturCoords(This,m) ) 

#define ICDXNode_GetTransform(This,m)	\
    ( (This)->lpVtbl -> GetTransform(This,m) ) 

#define ICDXNode_SetTransform(This,m)	\
    ( (This)->lpVtbl -> SetTransform(This,m) ) 

#define ICDXNode_AddNode(This,name,p)	\
    ( (This)->lpVtbl -> AddNode(This,name,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXNode_INTERFACE_DEFINED__ */


#ifndef __ICDXFont_INTERFACE_DEFINED__
#define __ICDXFont_INTERFACE_DEFINED__

/* interface ICDXFont */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXFont;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("F063C32D-59D1-4A0D-B209-323268059C12")
    ICDXFont : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Name( 
            /* [retval][out] */ BSTR *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Size( 
            /* [retval][out] */ FLOAT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Style( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Ascent( 
            /* [retval][out] */ FLOAT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Descent( 
            /* [retval][out] */ FLOAT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Height( 
            /* [retval][out] */ FLOAT *p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXFontVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXFont * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXFont * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXFont * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Name )( 
            ICDXFont * This,
            /* [retval][out] */ BSTR *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Size )( 
            ICDXFont * This,
            /* [retval][out] */ FLOAT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Style )( 
            ICDXFont * This,
            /* [retval][out] */ UINT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Ascent )( 
            ICDXFont * This,
            /* [retval][out] */ FLOAT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Descent )( 
            ICDXFont * This,
            /* [retval][out] */ FLOAT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Height )( 
            ICDXFont * This,
            /* [retval][out] */ FLOAT *p);
        
        END_INTERFACE
    } ICDXFontVtbl;

    interface ICDXFont
    {
        CONST_VTBL struct ICDXFontVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXFont_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXFont_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXFont_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXFont_get_Name(This,p)	\
    ( (This)->lpVtbl -> get_Name(This,p) ) 

#define ICDXFont_get_Size(This,p)	\
    ( (This)->lpVtbl -> get_Size(This,p) ) 

#define ICDXFont_get_Style(This,p)	\
    ( (This)->lpVtbl -> get_Style(This,p) ) 

#define ICDXFont_get_Ascent(This,p)	\
    ( (This)->lpVtbl -> get_Ascent(This,p) ) 

#define ICDXFont_get_Descent(This,p)	\
    ( (This)->lpVtbl -> get_Descent(This,p) ) 

#define ICDXFont_get_Height(This,p)	\
    ( (This)->lpVtbl -> get_Height(This,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXFont_INTERFACE_DEFINED__ */


#ifndef __ICDXFactory_INTERFACE_DEFINED__
#define __ICDXFactory_INTERFACE_DEFINED__

/* interface ICDXFactory */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICDXFactory;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("f0993d73-ea2a-4bf1-b128-826d4a3ba584")
    ICDXFactory : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Version( 
            /* [retval][out] */ UINT *pVal) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Devices( 
            /* [retval][out] */ BSTR *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetDevice( 
            /* [in] */ UINT id) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateView( 
            /* [in] */ HWND hwnd,
            /* [in] */ ICDXSink *sink,
            /* [in] */ UINT sampels,
            /* [retval][out] */ ICDXView **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateScene( 
            /* [in] */ UINT reserve,
            /* [retval][out] */ ICDXScene **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetFont( 
            /* [in] */ BSTR name,
            FLOAT size,
            UINT style,
            /* [retval][out] */ ICDXFont **p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICDXFactoryVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICDXFactory * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICDXFactory * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICDXFactory * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Version )( 
            ICDXFactory * This,
            /* [retval][out] */ UINT *pVal);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Devices )( 
            ICDXFactory * This,
            /* [retval][out] */ BSTR *p);
        
        HRESULT ( STDMETHODCALLTYPE *SetDevice )( 
            ICDXFactory * This,
            /* [in] */ UINT id);
        
        HRESULT ( STDMETHODCALLTYPE *CreateView )( 
            ICDXFactory * This,
            /* [in] */ HWND hwnd,
            /* [in] */ ICDXSink *sink,
            /* [in] */ UINT sampels,
            /* [retval][out] */ ICDXView **p);
        
        HRESULT ( STDMETHODCALLTYPE *CreateScene )( 
            ICDXFactory * This,
            /* [in] */ UINT reserve,
            /* [retval][out] */ ICDXScene **p);
        
        HRESULT ( STDMETHODCALLTYPE *GetFont )( 
            ICDXFactory * This,
            /* [in] */ BSTR name,
            FLOAT size,
            UINT style,
            /* [retval][out] */ ICDXFont **p);
        
        END_INTERFACE
    } ICDXFactoryVtbl;

    interface ICDXFactory
    {
        CONST_VTBL struct ICDXFactoryVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICDXFactory_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICDXFactory_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICDXFactory_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICDXFactory_get_Version(This,pVal)	\
    ( (This)->lpVtbl -> get_Version(This,pVal) ) 

#define ICDXFactory_get_Devices(This,p)	\
    ( (This)->lpVtbl -> get_Devices(This,p) ) 

#define ICDXFactory_SetDevice(This,id)	\
    ( (This)->lpVtbl -> SetDevice(This,id) ) 

#define ICDXFactory_CreateView(This,hwnd,sink,sampels,p)	\
    ( (This)->lpVtbl -> CreateView(This,hwnd,sink,sampels,p) ) 

#define ICDXFactory_CreateScene(This,reserve,p)	\
    ( (This)->lpVtbl -> CreateScene(This,reserve,p) ) 

#define ICDXFactory_GetFont(This,name,size,style,p)	\
    ( (This)->lpVtbl -> GetFont(This,name,size,style,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICDXFactory_INTERFACE_DEFINED__ */



#ifndef __cdxLib_LIBRARY_DEFINED__
#define __cdxLib_LIBRARY_DEFINED__

/* library cdxLib */
/* [version][uuid] */ 


EXTERN_C const IID LIBID_cdxLib;

EXTERN_C const CLSID CLSID_Factory;

#ifdef __cplusplus

class DECLSPEC_UUID("4e957503-5aeb-41f2-975b-3e6ae9f9c75a")
Factory;
#endif
#endif /* __cdxLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  BSTR_UserSize(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  HWND_UserSize(     unsigned long *, unsigned long            , HWND * ); 
unsigned char * __RPC_USER  HWND_UserMarshal(  unsigned long *, unsigned char *, HWND * ); 
unsigned char * __RPC_USER  HWND_UserUnmarshal(unsigned long *, unsigned char *, HWND * ); 
void                      __RPC_USER  HWND_UserFree(     unsigned long *, HWND * ); 

unsigned long             __RPC_USER  BSTR_UserSize64(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal64(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal64(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree64(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  HWND_UserSize64(     unsigned long *, unsigned long            , HWND * ); 
unsigned char * __RPC_USER  HWND_UserMarshal64(  unsigned long *, unsigned char *, HWND * ); 
unsigned char * __RPC_USER  HWND_UserUnmarshal64(unsigned long *, unsigned char *, HWND * ); 
void                      __RPC_USER  HWND_UserFree64(     unsigned long *, HWND * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


