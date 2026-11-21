// DTDLoader.dll: bộ nạp BepInEx 6 (IL2CPP) cho DTD Trainer, KHÔNG cần file nào trong thư mục game.
//
// Trainer nạp DLL này vào tiến trình game ngay khi game vừa mở (CreateRemoteThread + LoadLibraryW), trước khi
// UnityPlayer nạp GameAssembly.dll. Ở DllMain, bộ nạp móc GetProcAddress trong bảng import của UnityPlayer.dll để
// bắt "il2cpp_init". Khi Unity khởi tạo IL2CPP, bộ nạp chạy il2cpp_init gốc rồi khởi động CoreCLR và gọi
// Doorstop.Entrypoint.Start của BepInEx, y như UnityDoorstop 4.5 (cùng các biến môi trường DOORSTOP_*).
//
// Mọi đường dẫn tính từ thư mục chứa DLL này (Tool-DTD\runtime):
//   runtime\BepInEx\core\BepInEx.Unity.IL2CPP.dll   runtime\dotnet\coreclr.dll   runtime\DTDLoader.log
//
// Build: gcc -O2 -shared -s -o DTDLoader.dll dtdloader.c -lkernel32   (xem build.ps1)

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <wchar.h>

typedef int (*il2cpp_init_fn)(const char *domain_name);
typedef int (*coreclr_initialize_fn)(const char *exe_path, const char *domain_name, int property_count,
                                     const char **property_keys, const char **property_values,
                                     void **host_handle, unsigned int *domain_id);
typedef int (*coreclr_create_delegate_fn)(void *host_handle, unsigned int domain_id, const char *assembly_name,
                                          const char *type_name, const char *method_name, void **delegate);

static wchar_t g_dir[MAX_PATH];          // thư mục chứa DTDLoader.dll
static il2cpp_init_fn g_real_il2cpp_init;
static volatile LONG g_bootstrapped;

// ------------------------------------------------------------------ log

static void log_line(const char *fmt, ...)
{
    wchar_t path[MAX_PATH];
    _snwprintf(path, MAX_PATH, L"%ls\\DTDLoader.log", g_dir);
    FILE *f = _wfopen(path, L"a");
    if (!f) return;
    SYSTEMTIME t;
    GetLocalTime(&t);
    fprintf(f, "[%02d:%02d:%02d.%03d] ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
    va_list args;
    va_start(args, fmt);
    vfprintf(f, fmt, args);
    va_end(args);
    fputc('\n', f);
    fclose(f);
}

// Chuỗi UTF-8 cấp phát trên heap (CoreCLR nhận đường dẫn UTF-8 ở mọi nền tảng).
static char *to_utf8(const wchar_t *w)
{
    int n = WideCharToMultiByte(CP_UTF8, 0, w, -1, NULL, 0, NULL, NULL);
    char *s = (char *)HeapAlloc(GetProcessHeap(), 0, n);
    WideCharToMultiByte(CP_UTF8, 0, w, -1, s, n, NULL, NULL);
    return s;
}

// Danh sách mọi *.dll trong thư mục, nối bằng ';' (TRUSTED_PLATFORM_ASSEMBLIES).
static wchar_t *list_dlls(const wchar_t *dir)
{
    size_t cap = 1 << 16, len = 0;
    wchar_t *out = (wchar_t *)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cap * sizeof(wchar_t));
    wchar_t pattern[MAX_PATH];
    _snwprintf(pattern, MAX_PATH, L"%ls\\*.dll", dir);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE) return out;
    do
    {
        size_t need = wcslen(dir) + 1 + wcslen(fd.cFileName) + 2;
        if (len + need >= cap)
        {
            cap *= 2;
            out = (wchar_t *)HeapReAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, out, cap * sizeof(wchar_t));
        }
        len += _snwprintf(out + len, cap - len, L"%ls%ls\\%ls", len ? L";" : L"", dir, fd.cFileName);
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    return out;
}

// ------------------------------------------------------------------ khởi động BepInEx

static void bootstrap(void)
{
    if (InterlockedExchange(&g_bootstrapped, 1)) return;
    if (GetEnvironmentVariableW(L"DOORSTOP_INITIALIZED", NULL, 0) > 0)
    {
        log_line("DOORSTOP_INITIALIZED da co (bo nap khac da chay), bo qua.");
        return;
    }
    SetEnvironmentVariableW(L"DOORSTOP_INITIALIZED", L"TRUE");

    wchar_t exe[MAX_PATH], target[MAX_PATH], core_dir[MAX_PATH], corlib[MAX_PATH], coreclr[MAX_PATH];
    wchar_t search[MAX_PATH * 2 + 2], preloader_log[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    _snwprintf(core_dir, MAX_PATH, L"%ls\\BepInEx\\core", g_dir);
    _snwprintf(target, MAX_PATH, L"%ls\\BepInEx.Unity.IL2CPP.dll", core_dir);
    _snwprintf(corlib, MAX_PATH, L"%ls\\dotnet", g_dir);
    _snwprintf(coreclr, MAX_PATH, L"%ls\\coreclr.dll", corlib);
    _snwprintf(search, MAX_PATH * 2 + 2, L"%ls;%ls", corlib, core_dir);
    _snwprintf(preloader_log, MAX_PATH, L"%ls\\BepInEx\\preloader_error.log", g_dir);

    // Giống UnityDoorstop 4.5: BepInEx đọc các biến này để biết thư mục gốc, thư mục game, runtime.
    SetEnvironmentVariableW(L"DOORSTOP_INVOKE_DLL_PATH", target);
    SetEnvironmentVariableW(L"DOORSTOP_MANAGED_FOLDER_DIR", corlib);
    SetEnvironmentVariableW(L"DOORSTOP_PROCESS_PATH", exe);
    SetEnvironmentVariableW(L"DOORSTOP_DLL_SEARCH_DIRS", search);
    // Nếu khởi động lỗi, BepInEx ghi log ở đây thay vì thư mục game.
    SetEnvironmentVariableW(L"BEPINEX_PRELOADER_LOG", preloader_log);

    HMODULE clr = LoadLibraryW(coreclr);
    if (!clr)
    {
        log_line("Khong nap duoc coreclr.dll (loi %lu).", GetLastError());
        return;
    }
    coreclr_initialize_fn initialize = (coreclr_initialize_fn)GetProcAddress(clr, "coreclr_initialize");
    coreclr_create_delegate_fn create_delegate = (coreclr_create_delegate_fn)GetProcAddress(clr, "coreclr_create_delegate");
    if (!initialize || !create_delegate)
    {
        log_line("coreclr.dll thieu ham khoi tao.");
        return;
    }

    char *exe8 = to_utf8(exe);
    char *search8 = to_utf8(search);
    char *tpa8 = to_utf8(list_dlls(corlib));
    const char *keys[] = {"TRUSTED_PLATFORM_ASSEMBLIES", "APP_PATHS"};
    const char *values[] = {tpa8, search8};
    void *host = NULL;
    unsigned int domain = 0;
    int hr = initialize(exe8, "Doorstop Domain", 2, keys, values, &host, &domain);
    if (hr < 0)
    {
        log_line("coreclr_initialize loi 0x%08x.", hr);
        return;
    }

    void (*start)(void) = NULL;
    hr = create_delegate(host, domain, "BepInEx.Unity.IL2CPP", "Doorstop.Entrypoint", "Start", (void **)&start);
    if (hr < 0 || !start)
    {
        log_line("Khong tim thay Doorstop.Entrypoint.Start (0x%08x).", hr);
        return;
    }
    SetEnvironmentVariableW(L"DOORSTOP_DISABLE", L"TRUE");
    log_line("Khoi dong BepInEx.");
    start();
    log_line("BepInEx da chay xong buoc khoi dong.");
}

static int il2cpp_init_hook(const char *domain_name)
{
    log_line("il2cpp_init(\"%s\").", domain_name ? domain_name : "");
    int result = g_real_il2cpp_init(domain_name);
    bootstrap();
    return result;
}

// ------------------------------------------------------------------ móc GetProcAddress của UnityPlayer

static FARPROC WINAPI get_proc_address_hook(HMODULE module, LPCSTR name)
{
    FARPROC real = GetProcAddress(module, name);
    if (real && HIWORD((ULONG_PTR)name) != 0 && strcmp(name, "il2cpp_init") == 0)
    {
        g_real_il2cpp_init = (il2cpp_init_fn)real;
        log_line("Bat duoc il2cpp_init.");
        return (FARPROC)il2cpp_init_hook;
    }
    return real;
}

// Thay mục GetProcAddress trong bảng import (IAT) của một module. Tìm theo tên hàm trong bảng tên import.
static BOOL hook_iat(HMODULE module)
{
    BYTE *base = (BYTE *)module;
    IMAGE_NT_HEADERS *nt = (IMAGE_NT_HEADERS *)(base + ((IMAGE_DOS_HEADER *)base)->e_lfanew);
    IMAGE_DATA_DIRECTORY dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!dir.VirtualAddress) return FALSE;
    BOOL hooked = FALSE;
    for (IMAGE_IMPORT_DESCRIPTOR *imp = (IMAGE_IMPORT_DESCRIPTOR *)(base + dir.VirtualAddress); imp->Name; imp++)
    {
        if (!imp->OriginalFirstThunk) continue;
        IMAGE_THUNK_DATA *names = (IMAGE_THUNK_DATA *)(base + imp->OriginalFirstThunk);
        IMAGE_THUNK_DATA *funcs = (IMAGE_THUNK_DATA *)(base + imp->FirstThunk);
        for (; names->u1.AddressOfData; names++, funcs++)
        {
            if (IMAGE_SNAP_BY_ORDINAL(names->u1.Ordinal)) continue;
            IMAGE_IMPORT_BY_NAME *by_name = (IMAGE_IMPORT_BY_NAME *)(base + names->u1.AddressOfData);
            if (strcmp((const char *)by_name->Name, "GetProcAddress") != 0) continue;
            DWORD old;
            if (!VirtualProtect(&funcs->u1.Function, sizeof(ULONG_PTR), PAGE_READWRITE, &old)) continue;
            funcs->u1.Function = (ULONG_PTR)get_proc_address_hook;
            VirtualProtect(&funcs->u1.Function, sizeof(ULONG_PTR), old, &old);
            hooked = TRUE;
        }
    }
    return hooked;
}

// Chạy bootstrap trong thread riêng khi game đã nạp sẵn GameAssembly.dll (chèn vào game đang chạy)
static DWORD WINAPI bootstrap_thread(LPVOID param)
{
    (void)param;
    // Chờ 50ms để DllMain thoát và giải phóng Windows loader lock
    Sleep(50);

    HMODULE game_asm = GetModuleHandleW(L"GameAssembly.dll");
    if (game_asm)
    {
        typedef void *(*il2cpp_domain_get_fn)(void);
        typedef void *(*il2cpp_thread_attach_fn)(void *domain);
        il2cpp_domain_get_fn domain_get = (il2cpp_domain_get_fn)GetProcAddress(game_asm, "il2cpp_domain_get");
        il2cpp_thread_attach_fn thread_attach = (il2cpp_thread_attach_fn)GetProcAddress(game_asm, "il2cpp_thread_attach");
        if (domain_get && thread_attach)
        {
            void *domain = domain_get();
            if (domain)
            {
                thread_attach(domain);
                log_line("Da attach thread vao IL2CPP domain.");
            }
        }
    }

    log_line("Tien hanh bootstrap trong thread phu cho game dang chay.");
    bootstrap();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason != DLL_PROCESS_ATTACH) return TRUE;
    DisableThreadLibraryCalls(instance);

    GetModuleFileNameW(instance, g_dir, MAX_PATH);
    wchar_t *slash = wcsrchr(g_dir, L'\\');
    if (slash) *slash = 0;

    log_line("DTDLoader nap vao tien trinh %lu.", GetCurrentProcessId());
    if (GetModuleHandleW(L"GameAssembly.dll"))
    {
        log_line("GameAssembly.dll da nap san -> Chay bootstrap qua thread moi.");
        HANDLE th = CreateThread(NULL, 0, bootstrap_thread, NULL, 0, NULL);
        if (th) CloseHandle(th);
    }
    else
    {
        HMODULE player = GetModuleHandleW(L"UnityPlayer.dll");
        BOOL ok = player && hook_iat(player);
        ok = hook_iat(GetModuleHandleW(NULL)) || ok; // bản Unity cũ: player nằm trong exe
        log_line(ok ? "Da moc GetProcAddress, cho il2cpp_init." : "Khong moc duoc GetProcAddress.");
    }
    return TRUE;
}
