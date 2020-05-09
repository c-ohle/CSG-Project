#include "pch.h"
#include "framework.h"
#include "resource.h"
#include "csg_i.h"
#include "dllmain.h"

CcsgModule _AtlModule;

extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
#if _DEBUG
  if(dwReason == DLL_PROCESS_ATTACH)
    _CrtSetDbgFlag(_CrtSetDbgFlag(_CRTDBG_REPORT_FLAG) | _CRTDBG_LEAK_CHECK_DF);
#endif
  return _AtlModule.DllMain(dwReason, lpReserved);
}
