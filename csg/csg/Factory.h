
#pragma once
#include "resource.h"
#include "csg_i.h"

using namespace ATL;

class ATL_NO_VTABLE CCSGFactory :
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CCSGFactory, &CLSID_CSGFactory>,
	public ICSGFactory, IAgileObject
{
public:
	DECLARE_REGISTRY_RESOURCEID(107)
	DECLARE_CLASSFACTORY_SINGLETON(CCSGFactory)
	DECLARE_NOT_AGGREGATABLE(CCSGFactory)
	BEGIN_COM_MAP(CCSGFactory)
		COM_INTERFACE_ENTRY(ICSGFactory)
		COM_INTERFACE_ENTRY(IAgileObject)
	END_COM_MAP()
public:
	STDMETHOD(get_Version)(UINT* pVal);
	STDMETHOD(CreateTesselator)(CSG_UNIT unit, ICSGTesselator** p);
	STDMETHOD(CreateVector)(UINT len, ICSGVector** p);
	STDMETHOD(CreateMesh)(ICSGMesh** p);
};

OBJECT_ENTRY_AUTO(__uuidof(CSGFactory), CCSGFactory)

