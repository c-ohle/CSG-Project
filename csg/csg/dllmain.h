// dllmain.h : Deklaration der Modulklasse.

class CcsgModule : public ATL::CAtlDllModuleT< CcsgModule >
{
public :
	DECLARE_LIBID(LIBID_csgLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_CSG, "{41c66e67-2de1-4273-a1bb-f0a013dc2a0f}")
};

extern class CcsgModule _AtlModule;
