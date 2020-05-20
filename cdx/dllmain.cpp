#include "pch.h"
#include "framework.h"
#include "resource.h"
#include "cdx_i.h"
#include "dllmain.h"

CcdxModule _AtlModule;
CRITICAL_SECTION Critical::p;
void* baseptr, * stackptr;
ICSGFactory* csgfac;

extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
#if _DEBUG
  if (dwReason == DLL_PROCESS_ATTACH)
    _CrtSetDbgFlag(_CrtSetDbgFlag(_CRTDBG_REPORT_FLAG) | _CRTDBG_LEAK_CHECK_DF);
  if (dwReason == DLL_PROCESS_DETACH)
    _CrtDumpMemoryLeaks();
#endif
  if (dwReason == DLL_PROCESS_ATTACH)
  {
    InitializeCriticalSection(&Critical::p);
    baseptr = stackptr = VirtualAlloc(0, 100000000, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
  }
  return _AtlModule.DllMain(dwReason, lpReserved);
}

typedef HRESULT(WINAPI* PFN_DllGetClassObject)(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ LPVOID* ppv);

ICSGFactory* _CSGFactory()
{
  if (csgfac) return csgfac;
  auto t1 = LoadLibraryA(sizeof(void*) == 8 ? "csg64.dll" : "csg32.dll");
  auto t2 = (PFN_DllGetClassObject)GetProcAddress(t1, "DllGetClassObject");
  CComPtr<IClassFactory> cf;
  t2(__uuidof(CSGFactory), __uuidof(IClassFactory), (void**)&cf.p);
  cf.p->CreateInstance(0, __uuidof(ICSGFactory), (void**)&csgfac);
  return csgfac;
}


