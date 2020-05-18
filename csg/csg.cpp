#include "pch.h"
#include "framework.h"
#include "resource.h"
#include "csg_i.h"
#include "dllmain.h"
#include "factory.h"

struct _CFactory : CFactory
{
	HRESULT __stdcall QueryInterface(REFIID riid, void** p)
	{
		if (riid == __uuidof(IUnknown) || riid == __uuidof(ICSGFactory) || riid == __uuidof(IAgileObject))
		{
			*p = static_cast<ICSGFactory*>(this); return 0;
		}
		return E_NOINTERFACE;
	}
	ULONG __stdcall AddRef(void)
	{
		return 1;
	}
	ULONG __stdcall Release(void)
	{
		return 1;
	}
};
_CFactory theFactory;

struct _CClassFactory : IClassFactory
{
	HRESULT __stdcall QueryInterface(REFIID riid, void** p)
	{
		if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory) || riid == __uuidof(IAgileObject))
		{
			*p = static_cast<IClassFactory*>(this); return 0;
		}
		return E_NOINTERFACE;
	}
	ULONG __stdcall AddRef(void)
	{
		return 1;
	}
	ULONG __stdcall Release(void)
	{
		return 1;
	}
	HRESULT __stdcall CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
	{
		return theFactory.QueryInterface(riid, ppv);
	}
	HRESULT __stdcall LockServer(BOOL fLock) { return 0; }
};
_CClassFactory theClassFactory;

using namespace ATL;

_Use_decl_annotations_
STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ LPVOID* ppv)
{
	return theClassFactory.QueryInterface(riid, ppv);
	//return _AtlModule.DllGetClassObject(rclsid, riid, ppv);
}

_Use_decl_annotations_
STDAPI DllCanUnloadNow(void)
{
	return _AtlModule.DllCanUnloadNow();
}

_Use_decl_annotations_
STDAPI DllRegisterServer(void)
{
	HRESULT hr = _AtlModule.DllRegisterServer();
	return hr;
}

_Use_decl_annotations_
STDAPI DllUnregisterServer(void)
{
	HRESULT hr = _AtlModule.DllUnregisterServer();
	return hr;
}

STDAPI DllInstall(BOOL bInstall, _In_opt_  LPCWSTR pszCmdLine)
{
	HRESULT hr = E_FAIL;
	static const wchar_t szUserSwitch[] = L"user";
	if (pszCmdLine != nullptr)
	{
		if (_wcsnicmp(pszCmdLine, szUserSwitch, _countof(szUserSwitch)) == 0)
		{
			ATL::AtlSetPerUserRegistration(true);
		}
	}
	if (bInstall)
	{
		hr = DllRegisterServer();
		if (FAILED(hr))
		{
			DllUnregisterServer();
		}
	}
	else
	{
		hr = DllUnregisterServer();
	}
	return hr;
}


