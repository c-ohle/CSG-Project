

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for csg.idl:
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

#ifndef __csg_i_h__
#define __csg_i_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __ITesselator_FWD_DEFINED__
#define __ITesselator_FWD_DEFINED__
typedef interface ITesselator ITesselator;

#endif 	/* __ITesselator_FWD_DEFINED__ */


#ifndef __Tesselator_FWD_DEFINED__
#define __Tesselator_FWD_DEFINED__

#ifdef __cplusplus
typedef class Tesselator Tesselator;
#else
typedef struct Tesselator Tesselator;
#endif /* __cplusplus */

#endif 	/* __Tesselator_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"
#include "shobjidl.h"

#ifdef __cplusplus
extern "C"{
#endif 


/* interface __MIDL_itf_csg_0000_0000 */
/* [local] */ 

typedef 
enum tagMode
    {
        EvenOdd	= 0x1,
        NonZero	= 0x2,
        Positive	= 0x4,
        Negative	= 0x8,
        AbsGeqTwo	= 0x10,
        GeqThree	= 0x20,
        Fill	= 0x100,
        FillFast	= 0x200,
        IndexOnly	= 0x800,
        Outline	= 0x1000,
        OutlinePrecise	= 0x2000,
        NoTrim	= 0x4000,
        NormX	= 0x10000,
        NormY	= 0x20000,
        NormZ	= 0x40000,
        NormNeg	= 0x80000
    } 	Mode;

typedef struct tagVertex
    {
    double x;
    double y;
    double z;
    } 	Vertex;



extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0000_v0_0_s_ifspec;

#ifndef __ITesselator_INTERFACE_DEFINED__
#define __ITesselator_INTERFACE_DEFINED__

/* interface ITesselator */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ITesselator;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("d210bdc1-65a3-43f7-a296-bf8d4bb7b962")
    ITesselator : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Version( 
            /* [retval][out] */ LONG *pVal) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Mode( 
            /* [retval][out] */ Mode *pVal) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Mode( 
            /* [in] */ Mode newVal) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetNormal( 
            /* [in] */ Vertex *v) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE BeginPolygon( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE BeginContour( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddVertex( 
            /* [in] */ Vertex *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE EndContour( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE EndPolygon( void) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_VertexCount( 
            /* [retval][out] */ LONG *pVal) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE VertexAt( 
            /* [in] */ LONG i,
            /* [retval][out] */ Vertex *pVal) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IndexCount( 
            /* [retval][out] */ LONG *pVal) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE IndexAt( 
            /* [in] */ LONG i,
            /* [retval][out] */ LONG *pVal) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_OutlineCount( 
            /* [retval][out] */ LONG *pVal) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE OutlineAt( 
            /* [in] */ LONG i,
            /* [retval][out] */ LONG *pVal) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ITesselatorVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ITesselator * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ITesselator * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ITesselator * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Version )( 
            ITesselator * This,
            /* [retval][out] */ LONG *pVal);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Mode )( 
            ITesselator * This,
            /* [retval][out] */ Mode *pVal);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Mode )( 
            ITesselator * This,
            /* [in] */ Mode newVal);
        
        HRESULT ( STDMETHODCALLTYPE *SetNormal )( 
            ITesselator * This,
            /* [in] */ Vertex *v);
        
        HRESULT ( STDMETHODCALLTYPE *BeginPolygon )( 
            ITesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *BeginContour )( 
            ITesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *AddVertex )( 
            ITesselator * This,
            /* [in] */ Vertex *p);
        
        HRESULT ( STDMETHODCALLTYPE *EndContour )( 
            ITesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *EndPolygon )( 
            ITesselator * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_VertexCount )( 
            ITesselator * This,
            /* [retval][out] */ LONG *pVal);
        
        HRESULT ( STDMETHODCALLTYPE *VertexAt )( 
            ITesselator * This,
            /* [in] */ LONG i,
            /* [retval][out] */ Vertex *pVal);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IndexCount )( 
            ITesselator * This,
            /* [retval][out] */ LONG *pVal);
        
        HRESULT ( STDMETHODCALLTYPE *IndexAt )( 
            ITesselator * This,
            /* [in] */ LONG i,
            /* [retval][out] */ LONG *pVal);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_OutlineCount )( 
            ITesselator * This,
            /* [retval][out] */ LONG *pVal);
        
        HRESULT ( STDMETHODCALLTYPE *OutlineAt )( 
            ITesselator * This,
            /* [in] */ LONG i,
            /* [retval][out] */ LONG *pVal);
        
        END_INTERFACE
    } ITesselatorVtbl;

    interface ITesselator
    {
        CONST_VTBL struct ITesselatorVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ITesselator_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ITesselator_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ITesselator_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ITesselator_get_Version(This,pVal)	\
    ( (This)->lpVtbl -> get_Version(This,pVal) ) 

#define ITesselator_get_Mode(This,pVal)	\
    ( (This)->lpVtbl -> get_Mode(This,pVal) ) 

#define ITesselator_put_Mode(This,newVal)	\
    ( (This)->lpVtbl -> put_Mode(This,newVal) ) 

#define ITesselator_SetNormal(This,v)	\
    ( (This)->lpVtbl -> SetNormal(This,v) ) 

#define ITesselator_BeginPolygon(This)	\
    ( (This)->lpVtbl -> BeginPolygon(This) ) 

#define ITesselator_BeginContour(This)	\
    ( (This)->lpVtbl -> BeginContour(This) ) 

#define ITesselator_AddVertex(This,p)	\
    ( (This)->lpVtbl -> AddVertex(This,p) ) 

#define ITesselator_EndContour(This)	\
    ( (This)->lpVtbl -> EndContour(This) ) 

#define ITesselator_EndPolygon(This)	\
    ( (This)->lpVtbl -> EndPolygon(This) ) 

#define ITesselator_get_VertexCount(This,pVal)	\
    ( (This)->lpVtbl -> get_VertexCount(This,pVal) ) 

#define ITesselator_VertexAt(This,i,pVal)	\
    ( (This)->lpVtbl -> VertexAt(This,i,pVal) ) 

#define ITesselator_get_IndexCount(This,pVal)	\
    ( (This)->lpVtbl -> get_IndexCount(This,pVal) ) 

#define ITesselator_IndexAt(This,i,pVal)	\
    ( (This)->lpVtbl -> IndexAt(This,i,pVal) ) 

#define ITesselator_get_OutlineCount(This,pVal)	\
    ( (This)->lpVtbl -> get_OutlineCount(This,pVal) ) 

#define ITesselator_OutlineAt(This,i,pVal)	\
    ( (This)->lpVtbl -> OutlineAt(This,i,pVal) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ITesselator_INTERFACE_DEFINED__ */



#ifndef __csgLib_LIBRARY_DEFINED__
#define __csgLib_LIBRARY_DEFINED__

/* library csgLib */
/* [version][uuid] */ 


EXTERN_C const IID LIBID_csgLib;

EXTERN_C const CLSID CLSID_Tesselator;

#ifdef __cplusplus

class DECLSPEC_UUID("96b75f96-e933-4eff-a6a2-ac2aceb1b824")
Tesselator;
#endif
#endif /* __csgLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


