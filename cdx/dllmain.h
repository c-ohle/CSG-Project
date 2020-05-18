// dllmain.h : Deklaration der Modulklasse.

class CcdxModule : public ATL::CAtlDllModuleT< CcdxModule >
{
public :
	DECLARE_LIBID(LIBID_cdxLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_CDX, "{6d43a611-b5a3-489c-aa3a-dc01bb9225e8}")
};

extern class CcdxModule _AtlModule;
