

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for csg.idl:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.01.0622 
    protocol : dce , ms_ext, c_ext, robust
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

#ifndef __ICSGTesselator_FWD_DEFINED__
#define __ICSGTesselator_FWD_DEFINED__
typedef interface ICSGTesselator ICSGTesselator;

#endif 	/* __ICSGTesselator_FWD_DEFINED__ */


#ifndef __ICSGVector_FWD_DEFINED__
#define __ICSGVector_FWD_DEFINED__
typedef interface ICSGVector ICSGVector;

#endif 	/* __ICSGVector_FWD_DEFINED__ */


#ifndef __ICSGMesh_FWD_DEFINED__
#define __ICSGMesh_FWD_DEFINED__
typedef interface ICSGMesh ICSGMesh;

#endif 	/* __ICSGMesh_FWD_DEFINED__ */


#ifndef __ICSGFactory_FWD_DEFINED__
#define __ICSGFactory_FWD_DEFINED__
typedef interface ICSGFactory ICSGFactory;

#endif 	/* __ICSGFactory_FWD_DEFINED__ */


#ifndef __CSGFactory_FWD_DEFINED__
#define __CSGFactory_FWD_DEFINED__

#ifdef __cplusplus
typedef class CSGFactory CSGFactory;
#else
typedef struct CSGFactory CSGFactory;
#endif /* __cplusplus */

#endif 	/* __CSGFactory_FWD_DEFINED__ */


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
enum CSG_TESS
    {
        CSG_TESS_EVENODD	= 0x1,
        CSG_TESS_NONZERO	= 0x2,
        CSG_TESS_POSITIVE	= 0x4,
        CSG_TESS_NEGATIVE	= 0x8,
        CSG_TESS_ABSGEQTWO	= 0x10,
        CSG_TESS_GEQTHREE	= 0x20,
        CSG_TESS_FILL	= 0x100,
        CSG_TESS_FILLFAST	= 0x200,
        CSG_TESS_INDEXONLY	= 0x800,
        CSG_TESS_OUTLINE	= 0x1000,
        CSG_TESS_OUTLINEPRECISE	= 0x2000,
        CSG_TESS_NOTRIM	= 0x4000,
        CSG_TESS_NORMX	= 0x10000,
        CSG_TESS_NORMY	= 0x20000,
        CSG_TESS_NORMZ	= 0x40000,
        CSG_TESS_NORMNEG	= 0x80000
    } 	CSG_TESS;

typedef 
enum CSG_JOIN
    {
        CSG_JOIN_UNION	= 0,
        CSG_JOIN_DIFFERENCE	= 1,
        CSG_JOIN_INTERSECTION	= 2
    } 	CSG_JOIN;

typedef 
enum CSG_TYPE
    {
        CSG_TYPE_NULL	= 0,
        CSG_TYPE_INT	= 1,
        CSG_TYPE_FLOAT	= 2,
        CSG_TYPE_DOUBLE	= 3,
        CSG_TYPE_DECIMAL	= 4,
        CSG_TYPE_RATIONAL	= 5,
        CSG_TYPE_STRING	= 6
    } 	CSG_TYPE;

typedef struct CSGVAR
    {
    BYTE vt;
    BYTE count;
    USHORT dummy;
    UINT length;
    ULONGLONG p;
    } 	CSGVAR;



extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0000_v0_0_s_ifspec;

#ifndef __ICSGTesselator_INTERFACE_DEFINED__
#define __ICSGTesselator_INTERFACE_DEFINED__

/* interface ICSGTesselator */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICSGTesselator;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("d210bdc1-65a3-43f7-a296-bf8d4bb7b962")
    ICSGTesselator : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Mode( 
            /* [retval][out] */ CSG_TESS *p) = 0;
        
        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Mode( 
            /* [in] */ CSG_TESS v) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetNormal( 
            /* [in] */ CSGVAR v) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE BeginPolygon( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE BeginContour( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddVertex( 
            /* [in] */ CSGVAR v) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE EndContour( void) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE EndPolygon( void) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_VertexCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetVertex( 
            /* [in] */ UINT i,
            /* [in] */ CSGVAR z) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IndexCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIndex( 
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_OutlineCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetOutline( 
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Update( 
            /* [in] */ ICSGMesh *mesh,
            /* [in] */ CSGVAR v,
            /* [in] */ UINT flags) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Cut( 
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR plane) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Join( 
            /* [in] */ ICSGMesh *a,
            /* [in] */ ICSGMesh *b,
            /* [in] */ CSG_JOIN op) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE AddGlyphContour( 
            /* [in] */ CSGVAR text,
            /* [in] */ HFONT font,
            /* [in] */ int flat) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Stretch( 
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR dir) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Skeleton( 
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR data) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICSGTesselatorVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICSGTesselator * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICSGTesselator * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICSGTesselator * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Mode )( 
            ICSGTesselator * This,
            /* [retval][out] */ CSG_TESS *p);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Mode )( 
            ICSGTesselator * This,
            /* [in] */ CSG_TESS v);
        
        HRESULT ( STDMETHODCALLTYPE *SetNormal )( 
            ICSGTesselator * This,
            /* [in] */ CSGVAR v);
        
        HRESULT ( STDMETHODCALLTYPE *BeginPolygon )( 
            ICSGTesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *BeginContour )( 
            ICSGTesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *AddVertex )( 
            ICSGTesselator * This,
            /* [in] */ CSGVAR v);
        
        HRESULT ( STDMETHODCALLTYPE *EndContour )( 
            ICSGTesselator * This);
        
        HRESULT ( STDMETHODCALLTYPE *EndPolygon )( 
            ICSGTesselator * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_VertexCount )( 
            ICSGTesselator * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetVertex )( 
            ICSGTesselator * This,
            /* [in] */ UINT i,
            /* [in] */ CSGVAR z);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IndexCount )( 
            ICSGTesselator * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetIndex )( 
            ICSGTesselator * This,
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_OutlineCount )( 
            ICSGTesselator * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetOutline )( 
            ICSGTesselator * This,
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *Update )( 
            ICSGTesselator * This,
            /* [in] */ ICSGMesh *mesh,
            /* [in] */ CSGVAR v,
            /* [in] */ UINT flags);
        
        HRESULT ( STDMETHODCALLTYPE *Cut )( 
            ICSGTesselator * This,
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR plane);
        
        HRESULT ( STDMETHODCALLTYPE *Join )( 
            ICSGTesselator * This,
            /* [in] */ ICSGMesh *a,
            /* [in] */ ICSGMesh *b,
            /* [in] */ CSG_JOIN op);
        
        HRESULT ( STDMETHODCALLTYPE *AddGlyphContour )( 
            ICSGTesselator * This,
            /* [in] */ CSGVAR text,
            /* [in] */ HFONT font,
            /* [in] */ int flat);
        
        HRESULT ( STDMETHODCALLTYPE *Stretch )( 
            ICSGTesselator * This,
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR dir);
        
        HRESULT ( STDMETHODCALLTYPE *Skeleton )( 
            ICSGTesselator * This,
            /* [in] */ ICSGMesh *a,
            /* [in] */ CSGVAR data);
        
        END_INTERFACE
    } ICSGTesselatorVtbl;

    interface ICSGTesselator
    {
        CONST_VTBL struct ICSGTesselatorVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICSGTesselator_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICSGTesselator_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICSGTesselator_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICSGTesselator_get_Mode(This,p)	\
    ( (This)->lpVtbl -> get_Mode(This,p) ) 

#define ICSGTesselator_put_Mode(This,v)	\
    ( (This)->lpVtbl -> put_Mode(This,v) ) 

#define ICSGTesselator_SetNormal(This,v)	\
    ( (This)->lpVtbl -> SetNormal(This,v) ) 

#define ICSGTesselator_BeginPolygon(This)	\
    ( (This)->lpVtbl -> BeginPolygon(This) ) 

#define ICSGTesselator_BeginContour(This)	\
    ( (This)->lpVtbl -> BeginContour(This) ) 

#define ICSGTesselator_AddVertex(This,v)	\
    ( (This)->lpVtbl -> AddVertex(This,v) ) 

#define ICSGTesselator_EndContour(This)	\
    ( (This)->lpVtbl -> EndContour(This) ) 

#define ICSGTesselator_EndPolygon(This)	\
    ( (This)->lpVtbl -> EndPolygon(This) ) 

#define ICSGTesselator_get_VertexCount(This,p)	\
    ( (This)->lpVtbl -> get_VertexCount(This,p) ) 

#define ICSGTesselator_GetVertex(This,i,z)	\
    ( (This)->lpVtbl -> GetVertex(This,i,z) ) 

#define ICSGTesselator_get_IndexCount(This,p)	\
    ( (This)->lpVtbl -> get_IndexCount(This,p) ) 

#define ICSGTesselator_GetIndex(This,i,p)	\
    ( (This)->lpVtbl -> GetIndex(This,i,p) ) 

#define ICSGTesselator_get_OutlineCount(This,p)	\
    ( (This)->lpVtbl -> get_OutlineCount(This,p) ) 

#define ICSGTesselator_GetOutline(This,i,p)	\
    ( (This)->lpVtbl -> GetOutline(This,i,p) ) 

#define ICSGTesselator_Update(This,mesh,v,flags)	\
    ( (This)->lpVtbl -> Update(This,mesh,v,flags) ) 

#define ICSGTesselator_Cut(This,a,plane)	\
    ( (This)->lpVtbl -> Cut(This,a,plane) ) 

#define ICSGTesselator_Join(This,a,b,op)	\
    ( (This)->lpVtbl -> Join(This,a,b,op) ) 

#define ICSGTesselator_AddGlyphContour(This,text,font,flat)	\
    ( (This)->lpVtbl -> AddGlyphContour(This,text,font,flat) ) 

#define ICSGTesselator_Stretch(This,a,dir)	\
    ( (This)->lpVtbl -> Stretch(This,a,dir) ) 

#define ICSGTesselator_Skeleton(This,a,data)	\
    ( (This)->lpVtbl -> Skeleton(This,a,data) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICSGTesselator_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_csg_0000_0001 */
/* [local] */ 

typedef 
enum CSG_OP1
    {
        CSG_OP1_COPY	= 0,
        CSG_OP1_NEG	= 1,
        CSG_OP1_TRANSPM	= 2,
        CSG_OP1_INV3X4	= 3,
        CSG_OP1_DOT2	= 4,
        CSG_OP1_DOT3	= 5,
        CSG_OP1_NORM3	= 6,
        CSG_OP1_NUM	= 7,
        CSG_OP1_DEN	= 8,
        CSG_OP1_LSB	= 9,
        CSG_OP1_MSB	= 10,
        CSG_OP1_TRUNC	= 11,
        CSG_OP1_FLOOR	= 12,
        CSG_OP1_CEIL	= 13,
        CSG_OP1_ROUND	= 14,
        CSG_OP1_RND10	= 15,
        CSG_OP1_COMPL	= 16
    } 	CSG_OP1;

typedef 
enum CSG_OP2
    {
        CSG_OP2_ADD	= 0,
        CSG_OP2_SUB	= 1,
        CSG_OP2_MUL	= 2,
        CSG_OP2_DIV	= 3,
        CSG_OP2_MUL3X4	= 4,
        CSG_OP2_PLANEP3	= 5,
        CSG_OP2_PLANEPN	= 6,
        CSG_OP2_POW	= 7
    } 	CSG_OP2;



extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0001_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0001_v0_0_s_ifspec;

#ifndef __ICSGVector_INTERFACE_DEFINED__
#define __ICSGVector_INTERFACE_DEFINED__

/* interface ICSGVector */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICSGVector;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("DB6EBD51-D2FC-4D75-B2AF-543326AEED48")
    ICSGVector : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Length( 
            /* [retval][out] */ UINT *n) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetString( 
            /* [in] */ UINT i,
            /* [in] */ UINT digits,
            /* [in] */ UINT flags,
            /* [retval][out] */ BSTR *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetHashCode( 
            /* [in] */ UINT i,
            /* [in] */ UINT n,
            /* [retval][out] */ UINT *v) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Equals( 
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [in] */ UINT c,
            /* [retval][out] */ BOOL *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CompareTo( 
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [retval][out] */ INT *sign) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Copy( 
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [in] */ UINT c) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetValue( 
            /* [in] */ UINT i,
            /* [out][in] */ CSGVAR *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetValue( 
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Execute1( 
            /* [in] */ CSG_OP1 op,
            /* [in] */ UINT ic,
            /* [in] */ const ICSGVector *pa,
            /* [in] */ UINT ia) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Execute2( 
            /* [in] */ CSG_OP2 op,
            /* [in] */ UINT ic,
            /* [in] */ const ICSGVector *pa,
            /* [in] */ UINT ia,
            /* [in] */ const ICSGVector *pb,
            /* [in] */ UINT ib) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SinCos( 
            /* [in] */ UINT i,
            /* [in] */ DOUBLE angel,
            /* [in] */ UINT prec) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE WriteToStream( 
            /* [in] */ IStream *str,
            /* [in] */ UINT i,
            /* [in] */ UINT n) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ReadFromStream( 
            /* [in] */ IStream *str,
            /* [in] */ UINT i,
            /* [in] */ UINT n) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICSGVectorVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICSGVector * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICSGVector * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICSGVector * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Length )( 
            ICSGVector * This,
            /* [retval][out] */ UINT *n);
        
        HRESULT ( STDMETHODCALLTYPE *GetString )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ UINT digits,
            /* [in] */ UINT flags,
            /* [retval][out] */ BSTR *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetHashCode )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ UINT n,
            /* [retval][out] */ UINT *v);
        
        HRESULT ( STDMETHODCALLTYPE *Equals )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [in] */ UINT c,
            /* [retval][out] */ BOOL *p);
        
        HRESULT ( STDMETHODCALLTYPE *CompareTo )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [retval][out] */ INT *sign);
        
        HRESULT ( STDMETHODCALLTYPE *Copy )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ ICSGVector *pb,
            /* [in] */ UINT ib,
            /* [in] */ UINT c);
        
        HRESULT ( STDMETHODCALLTYPE *GetValue )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [out][in] */ CSGVAR *p);
        
        HRESULT ( STDMETHODCALLTYPE *SetValue )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p);
        
        HRESULT ( STDMETHODCALLTYPE *Execute1 )( 
            ICSGVector * This,
            /* [in] */ CSG_OP1 op,
            /* [in] */ UINT ic,
            /* [in] */ const ICSGVector *pa,
            /* [in] */ UINT ia);
        
        HRESULT ( STDMETHODCALLTYPE *Execute2 )( 
            ICSGVector * This,
            /* [in] */ CSG_OP2 op,
            /* [in] */ UINT ic,
            /* [in] */ const ICSGVector *pa,
            /* [in] */ UINT ia,
            /* [in] */ const ICSGVector *pb,
            /* [in] */ UINT ib);
        
        HRESULT ( STDMETHODCALLTYPE *SinCos )( 
            ICSGVector * This,
            /* [in] */ UINT i,
            /* [in] */ DOUBLE angel,
            /* [in] */ UINT prec);
        
        HRESULT ( STDMETHODCALLTYPE *WriteToStream )( 
            ICSGVector * This,
            /* [in] */ IStream *str,
            /* [in] */ UINT i,
            /* [in] */ UINT n);
        
        HRESULT ( STDMETHODCALLTYPE *ReadFromStream )( 
            ICSGVector * This,
            /* [in] */ IStream *str,
            /* [in] */ UINT i,
            /* [in] */ UINT n);
        
        END_INTERFACE
    } ICSGVectorVtbl;

    interface ICSGVector
    {
        CONST_VTBL struct ICSGVectorVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICSGVector_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICSGVector_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICSGVector_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICSGVector_get_Length(This,n)	\
    ( (This)->lpVtbl -> get_Length(This,n) ) 

#define ICSGVector_GetString(This,i,digits,flags,p)	\
    ( (This)->lpVtbl -> GetString(This,i,digits,flags,p) ) 

#define ICSGVector_GetHashCode(This,i,n,v)	\
    ( (This)->lpVtbl -> GetHashCode(This,i,n,v) ) 

#define ICSGVector_Equals(This,i,pb,ib,c,p)	\
    ( (This)->lpVtbl -> Equals(This,i,pb,ib,c,p) ) 

#define ICSGVector_CompareTo(This,i,pb,ib,sign)	\
    ( (This)->lpVtbl -> CompareTo(This,i,pb,ib,sign) ) 

#define ICSGVector_Copy(This,i,pb,ib,c)	\
    ( (This)->lpVtbl -> Copy(This,i,pb,ib,c) ) 

#define ICSGVector_GetValue(This,i,p)	\
    ( (This)->lpVtbl -> GetValue(This,i,p) ) 

#define ICSGVector_SetValue(This,i,p)	\
    ( (This)->lpVtbl -> SetValue(This,i,p) ) 

#define ICSGVector_Execute1(This,op,ic,pa,ia)	\
    ( (This)->lpVtbl -> Execute1(This,op,ic,pa,ia) ) 

#define ICSGVector_Execute2(This,op,ic,pa,ia,pb,ib)	\
    ( (This)->lpVtbl -> Execute2(This,op,ic,pa,ia,pb,ib) ) 

#define ICSGVector_SinCos(This,i,angel,prec)	\
    ( (This)->lpVtbl -> SinCos(This,i,angel,prec) ) 

#define ICSGVector_WriteToStream(This,str,i,n)	\
    ( (This)->lpVtbl -> WriteToStream(This,str,i,n) ) 

#define ICSGVector_ReadFromStream(This,str,i,n)	\
    ( (This)->lpVtbl -> ReadFromStream(This,str,i,n) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICSGVector_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_csg_0000_0002 */
/* [local] */ 

typedef 
enum CSG_MESH_CHECK
    {
        CSG_MESH_CHECK_ALL	= 0,
        CSG_MESH_CHECK_DUP_POINTS	= 1,
        CSG_MESH_CHECK_BAD_INDEX	= 2,
        CSG_MESH_CHECK_UNUSED_POINT	= 4,
        CSG_MESH_CHECK_OPENINGS	= 8,
        CSG_MESH_CHECK_PLANES	= 16,
        CSG_MESH_CHECK_DUP_PLANES	= 32
    } 	CSG_MESH_CHECK;



extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0002_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0002_v0_0_s_ifspec;

#ifndef __ICSGMesh_INTERFACE_DEFINED__
#define __ICSGMesh_INTERFACE_DEFINED__

/* interface ICSGMesh */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICSGMesh;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("BE338702-B776-4178-AA13-963B4EB53EDF")
    ICSGMesh : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Update( 
            /* [in] */ CSGVAR vertices,
            /* [in] */ CSGVAR indices) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CopyTo( 
            ICSGMesh *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Transform( 
            /* [in] */ CSGVAR m) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CopyBuffer( 
            /* [in] */ UINT ib,
            /* [in] */ UINT ab,
            /* [in] */ CSGVAR p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_VertexCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetVertex( 
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetVertex( 
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IndexCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetIndex( 
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE SetIndex( 
            /* [in] */ UINT i,
            /* [in] */ UINT p) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_PlaneCount( 
            /* [retval][out] */ UINT *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetPlane( 
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE WriteToStream( 
            /* [in] */ IStream *str) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE ReadFromStream( 
            /* [in] */ IStream *str) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateBox( 
            /* [in] */ CSGVAR a,
            /* [in] */ CSGVAR b) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE Check( 
            /* [in] */ CSG_MESH_CHECK check,
            /* [out] */ CSG_MESH_CHECK *p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetModified( 
            /* [retval][out] */ BOOL *p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICSGMeshVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICSGMesh * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICSGMesh * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICSGMesh * This);
        
        HRESULT ( STDMETHODCALLTYPE *Update )( 
            ICSGMesh * This,
            /* [in] */ CSGVAR vertices,
            /* [in] */ CSGVAR indices);
        
        HRESULT ( STDMETHODCALLTYPE *CopyTo )( 
            ICSGMesh * This,
            ICSGMesh *p);
        
        HRESULT ( STDMETHODCALLTYPE *Transform )( 
            ICSGMesh * This,
            /* [in] */ CSGVAR m);
        
        HRESULT ( STDMETHODCALLTYPE *CopyBuffer )( 
            ICSGMesh * This,
            /* [in] */ UINT ib,
            /* [in] */ UINT ab,
            /* [in] */ CSGVAR p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_VertexCount )( 
            ICSGMesh * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetVertex )( 
            ICSGMesh * This,
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p);
        
        HRESULT ( STDMETHODCALLTYPE *SetVertex )( 
            ICSGMesh * This,
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IndexCount )( 
            ICSGMesh * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetIndex )( 
            ICSGMesh * This,
            /* [in] */ UINT i,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *SetIndex )( 
            ICSGMesh * This,
            /* [in] */ UINT i,
            /* [in] */ UINT p);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_PlaneCount )( 
            ICSGMesh * This,
            /* [retval][out] */ UINT *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetPlane )( 
            ICSGMesh * This,
            /* [in] */ UINT i,
            /* [in] */ CSGVAR p);
        
        HRESULT ( STDMETHODCALLTYPE *WriteToStream )( 
            ICSGMesh * This,
            /* [in] */ IStream *str);
        
        HRESULT ( STDMETHODCALLTYPE *ReadFromStream )( 
            ICSGMesh * This,
            /* [in] */ IStream *str);
        
        HRESULT ( STDMETHODCALLTYPE *CreateBox )( 
            ICSGMesh * This,
            /* [in] */ CSGVAR a,
            /* [in] */ CSGVAR b);
        
        HRESULT ( STDMETHODCALLTYPE *Check )( 
            ICSGMesh * This,
            /* [in] */ CSG_MESH_CHECK check,
            /* [out] */ CSG_MESH_CHECK *p);
        
        HRESULT ( STDMETHODCALLTYPE *GetModified )( 
            ICSGMesh * This,
            /* [retval][out] */ BOOL *p);
        
        END_INTERFACE
    } ICSGMeshVtbl;

    interface ICSGMesh
    {
        CONST_VTBL struct ICSGMeshVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICSGMesh_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICSGMesh_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICSGMesh_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICSGMesh_Update(This,vertices,indices)	\
    ( (This)->lpVtbl -> Update(This,vertices,indices) ) 

#define ICSGMesh_CopyTo(This,p)	\
    ( (This)->lpVtbl -> CopyTo(This,p) ) 

#define ICSGMesh_Transform(This,m)	\
    ( (This)->lpVtbl -> Transform(This,m) ) 

#define ICSGMesh_CopyBuffer(This,ib,ab,p)	\
    ( (This)->lpVtbl -> CopyBuffer(This,ib,ab,p) ) 

#define ICSGMesh_get_VertexCount(This,p)	\
    ( (This)->lpVtbl -> get_VertexCount(This,p) ) 

#define ICSGMesh_GetVertex(This,i,p)	\
    ( (This)->lpVtbl -> GetVertex(This,i,p) ) 

#define ICSGMesh_SetVertex(This,i,p)	\
    ( (This)->lpVtbl -> SetVertex(This,i,p) ) 

#define ICSGMesh_get_IndexCount(This,p)	\
    ( (This)->lpVtbl -> get_IndexCount(This,p) ) 

#define ICSGMesh_GetIndex(This,i,p)	\
    ( (This)->lpVtbl -> GetIndex(This,i,p) ) 

#define ICSGMesh_SetIndex(This,i,p)	\
    ( (This)->lpVtbl -> SetIndex(This,i,p) ) 

#define ICSGMesh_get_PlaneCount(This,p)	\
    ( (This)->lpVtbl -> get_PlaneCount(This,p) ) 

#define ICSGMesh_GetPlane(This,i,p)	\
    ( (This)->lpVtbl -> GetPlane(This,i,p) ) 

#define ICSGMesh_WriteToStream(This,str)	\
    ( (This)->lpVtbl -> WriteToStream(This,str) ) 

#define ICSGMesh_ReadFromStream(This,str)	\
    ( (This)->lpVtbl -> ReadFromStream(This,str) ) 

#define ICSGMesh_CreateBox(This,a,b)	\
    ( (This)->lpVtbl -> CreateBox(This,a,b) ) 

#define ICSGMesh_Check(This,check,p)	\
    ( (This)->lpVtbl -> Check(This,check,p) ) 

#define ICSGMesh_GetModified(This,p)	\
    ( (This)->lpVtbl -> GetModified(This,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICSGMesh_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_csg_0000_0003 */
/* [local] */ 

typedef 
enum CSG_UNIT
    {
        CSG_UNIT_DOUBLE	= 0,
        CSG_UNIT_RATIONAL	= 1
    } 	CSG_UNIT;



extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0003_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_csg_0000_0003_v0_0_s_ifspec;

#ifndef __ICSGFactory_INTERFACE_DEFINED__
#define __ICSGFactory_INTERFACE_DEFINED__

/* interface ICSGFactory */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID_ICSGFactory;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("2a576402-2276-435d-bd1a-640ff1c19f90")
    ICSGFactory : public IUnknown
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Version( 
            /* [retval][out] */ UINT *pVal) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateTesselator( 
            /* [in] */ CSG_UNIT unit,
            /* [retval][out] */ ICSGTesselator **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateVector( 
            /* [in] */ UINT len,
            /* [retval][out] */ ICSGVector **p) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE CreateMesh( 
            /* [retval][out] */ ICSGMesh **p) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct ICSGFactoryVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            ICSGFactory * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            ICSGFactory * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            ICSGFactory * This);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Version )( 
            ICSGFactory * This,
            /* [retval][out] */ UINT *pVal);
        
        HRESULT ( STDMETHODCALLTYPE *CreateTesselator )( 
            ICSGFactory * This,
            /* [in] */ CSG_UNIT unit,
            /* [retval][out] */ ICSGTesselator **p);
        
        HRESULT ( STDMETHODCALLTYPE *CreateVector )( 
            ICSGFactory * This,
            /* [in] */ UINT len,
            /* [retval][out] */ ICSGVector **p);
        
        HRESULT ( STDMETHODCALLTYPE *CreateMesh )( 
            ICSGFactory * This,
            /* [retval][out] */ ICSGMesh **p);
        
        END_INTERFACE
    } ICSGFactoryVtbl;

    interface ICSGFactory
    {
        CONST_VTBL struct ICSGFactoryVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define ICSGFactory_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define ICSGFactory_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define ICSGFactory_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define ICSGFactory_get_Version(This,pVal)	\
    ( (This)->lpVtbl -> get_Version(This,pVal) ) 

#define ICSGFactory_CreateTesselator(This,unit,p)	\
    ( (This)->lpVtbl -> CreateTesselator(This,unit,p) ) 

#define ICSGFactory_CreateVector(This,len,p)	\
    ( (This)->lpVtbl -> CreateVector(This,len,p) ) 

#define ICSGFactory_CreateMesh(This,p)	\
    ( (This)->lpVtbl -> CreateMesh(This,p) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __ICSGFactory_INTERFACE_DEFINED__ */



#ifndef __csgLib_LIBRARY_DEFINED__
#define __csgLib_LIBRARY_DEFINED__

/* library csgLib */
/* [version][uuid] */ 


EXTERN_C const IID LIBID_csgLib;

EXTERN_C const CLSID CLSID_CSGFactory;

#ifdef __cplusplus

class DECLSPEC_UUID("54ca8e82-bdb3-41db-8ed5-3b890279c431")
CSGFactory;
#endif
#endif /* __csgLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  BSTR_UserSize(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  HFONT_UserSize(     unsigned long *, unsigned long            , HFONT * ); 
unsigned char * __RPC_USER  HFONT_UserMarshal(  unsigned long *, unsigned char *, HFONT * ); 
unsigned char * __RPC_USER  HFONT_UserUnmarshal(unsigned long *, unsigned char *, HFONT * ); 
void                      __RPC_USER  HFONT_UserFree(     unsigned long *, HFONT * ); 

unsigned long             __RPC_USER  BSTR_UserSize64(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal64(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal64(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree64(     unsigned long *, BSTR * ); 

unsigned long             __RPC_USER  HFONT_UserSize64(     unsigned long *, unsigned long            , HFONT * ); 
unsigned char * __RPC_USER  HFONT_UserMarshal64(  unsigned long *, unsigned char *, HFONT * ); 
unsigned char * __RPC_USER  HFONT_UserUnmarshal64(unsigned long *, unsigned char *, HFONT * ); 
void                      __RPC_USER  HFONT_UserFree64(     unsigned long *, HFONT * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


