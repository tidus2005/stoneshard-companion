#define WIN32_LEAN_AND_MEAN
#include "protocol.h"
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <stdbool.h>

// GameMaker x64 ABI. Only numeric values are used by the initial speed backend.
typedef struct RV { union { double real; int64_t integer; void* ptr; }; uint32_t flags; int32_t kind; } RV;
_Static_assert(sizeof(RV)==16, "GameMaker value ABI");
typedef void (*Builtin)(RV*,void*,void*,int,RV*);
static BYTE* game;
static HWND game_window;
static WNDPROC original_proc;
static SharedState* shared;
static HANDLE mapping;
static volatile LONG started;
static LONG processed_seq;
static double baseline;
static double preferred=1;
static bool speed_owned;
static bool highlight_requested,highlight_applied;
static UINT_PTR rebind_timer;
static DWORD window_thread;
static UINT_PTR tick_timer;
static bool in_callback;
static RV numeric(double d){ RV v={0};v.real=d;return v; }
#include "gm.h"
static double get_speed(void) {
    RV output={.kind=5},arg=numeric(0);
    ((Builtin)(game+0x51ec6b0))(&output,NULL,NULL,1,&arg);
    return output.kind==0 ? output.real : -1;
}
static void set_speed(double fps) {
    RV out={.kind=5},args[2]={numeric(fps),numeric(0)};
    ((Builtin)(game+0x51ec720))(&out,NULL,NULL,2,args);
}
#include "highlight.h"
#include "walk_native.h"
#include "walk.h"
static void reconcile_speed(void){
    DWORD pid=0;GetWindowThreadProcessId(GetForegroundWindow(),&pid);
    uint64_t heartbeat=(uint64_t)InterlockedCompareExchange64(&shared->heartbeat,0,0);
    uint32_t reasons=0;
    if(pid!=GetCurrentProcessId())reasons|=1;
    if(!shared->scene_ready)reasons|=2;
    if(shared->ui_flags&(2|4|16|32))reasons|=4;
    if(GetTickCount64()>heartbeat+1500){reasons|=8;preferred=1;auto_center=false;highlight_requested=false;shared->walk_keys_enabled=0;}
    shared->suspend_reasons=reasons;shared->preferred_multiplier=preferred;
    if(!reasons){
        double target=baseline*preferred;
        if(fabs(get_speed()-target)>0.01)set_speed(target);
        speed_owned=true;
    }else if(speed_owned){
        // Release speed once. Menus/loading may manage their own frame rate;
        // repeatedly forcing the gameplay baseline interferes with native input.
        set_speed(baseline);speed_owned=false;
    }
    if(reasons&&(centered_camera_id>=0))restore_camera(false);
    // Keep item labels through inventory/equipment and hover panels. Only actual
    // focus loss, loading, dialogs, confirmations or lease expiry release input.
    reconcile_highlight(highlight_allowed(highlight_requested,reasons,shared->ui_flags));
    shared->highlight_state=highlight_requested?1:0;
    shared->highlight_applied=highlight_applied?1:0;
    reconcile_walk(reasons);
}
static void publish(int error,const char* message) {
    InterlockedIncrement(&shared->status_seq);
    refresh_scene();
    reconcile_speed();
    shared->target_speed=get_speed();
    shared->base_speed=baseline;
    shared->multiplier=baseline>0 ? shared->target_speed/baseline : 1;
    shared->samples++;
    shared->updated_at=GetTickCount64();
    shared->error=error;
    if(message)snprintf(shared->detail,sizeof(shared->detail),"%s",message);
    MemoryBarrier();
    InterlockedIncrement(&shared->status_seq);
}
static void on_command(void) {
    LONG seq=InterlockedCompareExchange(&shared->request_seq,0,0);
    if(seq==processed_seq)return;
    uint32_t cmd=shared->command;
    double arg=shared->argument;
    uint64_t deadline=shared->deadline;
    MemoryBarrier();
    if(seq!=InterlockedCompareExchange(&shared->request_seq,0,0))return;
    processed_seq=seq; // At most once, including rejected commands.
    InterlockedIncrement(&shared->status_seq);refresh_scene();InterlockedIncrement(&shared->status_seq);
    DWORD foreground_pid=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground_pid);
    bool action=(cmd>=CMD_CENTER&&cmd<=CMD_VISOR)||(cmd>=11&&cmd<=13)||(cmd==14&&arg!=0);
    if(GetTickCount64()>deadline){publish(3,"Request expired");}
    else if(shared->request_window_generation!=shared->window_generation || (action&&shared->request_scene_generation!=shared->scene_generation))publish(9,"Scene or window changed; action cancelled");
    else if(action&&(!shared->scene_ready||(shared->ui_flags&(cmd==14?~8u:~0u))))publish(7,"Native UI or scene blocks actions");
    else if(((cmd>=CMD_SPEED&&cmd<=CMD_VISOR)||action)&&foreground_pid!=GetCurrentProcessId())publish(8,"Game is not in foreground");
    else if(cmd==CMD_SPEED){
        if(!isfinite(arg)||arg<1 || arg>4 || floor(arg)!=arg)publish(4,"Invalid multiplier");
        else {preferred=arg;publish(0,"Preferred engine speed applied");}
    }
    else if(cmd==CMD_RESET){finish_walk(WALK_MANUAL,true);set_walk_keys(false);preferred=1;auto_center=false;highlight_requested=false;set_speed(baseline);restore_camera(false);publish(0,"Normal speed and camera restored");}
    else if(cmd==9){restore_camera(false);publish(0,"Background state restored; preference retained");}
    else if(cmd==CMD_CENTER){bool ok=center_camera();publish(ok?0:5,ok?"Camera centered":"Enter a playable map first");}
    else if(cmd==CMD_PLAYER){bool ok=restore_camera(true);publish(ok?0:5,ok?"Camera returned to player":"Enter a playable map first");}
    else if(cmd==CMD_VISOR){int result=toggle_visor();publish(result,result==0?"Visor toggled":result==6?"Equipped helmet has no movable visor":"Visor is unavailable in the current state");}
    else if(cmd==8){auto_center=arg==1;scene_ready_since=0;publish(0,auto_center?"Auto center enabled for the next map":"Auto center disabled");}
    else if(cmd==CMD_REFRESH){publish(0,"Engine connected");}
    else if(cmd==CMD_DIAGNOSTIC){if(!global_scope)init_gm();collect_diagnostic();publish(0,"Diagnostic captured");}
    else if(cmd==10){if(!global_scope)init_gm();inspect_variables(arg);publish(0,"Variable names inspected");}
    else if(cmd==13){
        if(foreground_pid!=GetCurrentProcessId()||!shared->scene_ready||shared->ui_flags)publish(7,"Highlight unavailable");
        else {highlight_requested=!highlight_requested;publish(0,"Highlight preference changed");}
    }
    else if(cmd==15){if(arg!=0&&arg!=1)publish(4,"Invalid highlight state");else{highlight_requested=arg==1;publish(0,"Highlight intent synchronized");}}
    else if(cmd==14){int result=(!isfinite(arg)||floor(arg)!=arg||arg<0||arg>9)?4:request_walk((int)arg);publish(result,result?"Native walk unavailable":"Native map journey requested");}
    else if(cmd==16){if(arg!=0&&arg!=1)publish(4,"Invalid walk keys state");else{set_walk_keys(arg==1);publish(0,"Walk keys intent synchronized");}}
    else if(cmd==11){if(arg!=0&&arg!=1)publish(4,"Invalid water mode");else{int result=drink_water(arg==1);publish(result,result?"Water action unavailable or not confirmed":"Water consumed by native action");}}
    else if(cmd==12){if(arg!=0&&arg!=1&&arg!=2&&arg!=17&&arg!=18)publish(4,"Invalid torch mode");else{int mode=(int)arg;int result=toggle_torch(mode&3,(mode&16)!=0);publish(result,result?"Torch action unavailable or not confirmed":"Torch native action confirmed");}}
    else publish(2,"Capability not initialized");
    InterlockedExchange(&shared->ack_seq,seq);
}
static BOOL CALLBACK find_window(HWND,LPARAM);
static LRESULT CALLBACK bridge_proc(HWND,UINT,WPARAM,LPARAM);
static bool bind_window(HWND hwnd){
    WNDPROC next=(WNDPROC)GetWindowLongPtrW(hwnd,GWLP_WNDPROC);
    if(!next||next==bridge_proc)return false;
    original_proc=next;game_window=hwnd;
    SetLastError(0);
    if(!SetWindowLongPtrW(hwnd,GWLP_WNDPROC,(LONG_PTR)bridge_proc)&&GetLastError())return false;
    tick_timer=0x534843;
    if(!SetTimer(hwnd,tick_timer,100,NULL)){SetWindowLongPtrW(hwnd,GWLP_WNDPROC,(LONG_PTR)original_proc);return false;}
    processed_seq=InterlockedCompareExchange(&shared->request_seq,0,0);
    shared->game_hwnd=(uint64_t)hwnd;shared->window_generation++;
    MemoryBarrier();InterlockedExchange((LONG*)&shared->ready,1);return true;
}
static void CALLBACK rebind_tick(HWND ignored,UINT msg,UINT_PTR timer,DWORD time){
    game_window=NULL;EnumWindows(find_window,0);
    if(game_window&&bind_window(game_window)){KillTimer(NULL,timer);rebind_timer=0;}
}
static LRESULT CALLBACK bridge_proc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp) {
    if(msg==WM_KEYDOWN&&shared->walk_keys_enabled&&walk_key_direction((unsigned)wp)&&!in_callback){
        in_callback=true;void* saved_self=*(void**)(game+0x990b738);
        DWORD pid=0;GetWindowThreadProcessId(GetForegroundWindow(),&pid);
        uint64_t now=GetTickCount64(),heartbeat=(uint64_t)InterlockedCompareExchange64(&shared->heartbeat,0,0);
        InterlockedIncrement(&shared->status_seq);refresh_scene();
        bool consumed=handle_walk_key((unsigned)wp,((uintptr_t)lp&(1u<<30))!=0,walk_modifiers_down(),pid==GetCurrentProcessId(),now>=heartbeat&&now-heartbeat<=1500);
        InterlockedIncrement(&shared->status_seq);
        if(consumed)publish(shared->error,NULL);
        *(void**)(game+0x990b738)=saved_self;in_callback=false;
        if(consumed)return 0;
    }
    if(msg==WM_LBUTTONDOWN||msg==WM_RBUTTONDOWN||msg==WM_MBUTTONDOWN||
       (msg==WM_KEYDOWN&&wp!=VK_MENU&&wp!=VK_CONTROL&&wp!=VK_SHIFT))
        if(shared->walk_state==WALK_ACTIVE&&!in_callback){
            // Stop our old path before forwarding this input. The native event
            // may then start its replacement path without a later timer killing it.
            in_callback=true;void* saved_self=*(void**)(game+0x990b738);
            finish_walk(WALK_MANUAL,true);
            *(void**)(game+0x990b738)=saved_self;in_callback=false;
        }
    if(msg==BRIDGE_MESSAGE && wp==BRIDGE_MAGIC){
        if(in_callback)return 0;in_callback=true;
        void* saved_self=*(void**)(game+0x990b738);
        if(!global_scope)init_gm();shared->label_hook_ready=install_label_renderer();on_command();
        *(void**)(game+0x990b738)=saved_self;in_callback=false;return 0;
    }
    if(msg==WM_TIMER && wp==tick_timer){
        if(in_callback)return 0;in_callback=true;void* saved_self=*(void**)(game+0x990b738);
        if(!global_scope)init_gm();shared->label_hook_ready=install_label_renderer();publish(shared->error,NULL);
        *(void**)(game+0x990b738)=saved_self;in_callback=false;
        return 0;
    }
    if(msg==WM_NCDESTROY){
        reconcile_highlight(false);
        KillTimer(hwnd,tick_timer);
        InterlockedExchange((LONG*)&shared->ready,0);
        processed_seq=InterlockedCompareExchange(&shared->request_seq,0,0);
        WNDPROC old=original_proc;
        SetWindowLongPtrW(hwnd,GWLP_WNDPROC,(LONG_PTR)old);
        game_window=NULL;
        if(!rebind_timer)rebind_timer=SetTimer(NULL,0,100,rebind_tick);
        return CallWindowProcW(old,hwnd,msg,wp,lp);
    }
    return CallWindowProcW(original_proc,hwnd,msg,wp,lp);
}
static BOOL CALLBACK find_window(HWND hwnd,LPARAM unused){
    DWORD pid=0;GetWindowThreadProcessId(hwnd,&pid);
    if(pid==GetCurrentProcessId() && IsWindowVisible(hwnd) && !GetWindow(hwnd,GW_OWNER)&&(!window_thread||GetWindowThreadProcessId(hwnd,NULL)==window_thread)){game_window=hwnd;return FALSE;}
    return TRUE;
}

static DWORD start_failed(DWORD error){
    if(shared){UnmapViewOfFile(shared);shared=NULL;}
    if(mapping){CloseHandle(mapping);mapping=NULL;}
    InterlockedExchange(&started,0);return error;
}

__declspec(dllexport) DWORD WINAPI BridgeStart(void* ignored) {
    if(InterlockedCompareExchange(&started,1,0)!=0)return shared&&shared->ready?0:1;
    game=(BYTE*)GetModuleHandleW(NULL);
    IMAGE_NT_HEADERS64* pe=(IMAGE_NT_HEADERS64*)(game+((IMAGE_DOS_HEADER*)game)->e_lfanew);
    // Current-build validation is also performed using full SHA256 by the host.
    static const BYTE get_prolog[]={0x40,0x53,0x48,0x83,0xec,0x20,0xc7,0x41,0x0c,0,0,0,0};
    static const BYTE set_prolog[]={0x48,0x83,0xec,0x38,0x48,0x8b,0x4c,0x24,0x60};
    if(pe->FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64 || pe->OptionalHeader.SizeOfImage!=0xa257000 || memcmp(game+0x51ec6b0,get_prolog,sizeof(get_prolog)) || memcmp(game+0x51ec720,set_prolog,sizeof(set_prolog)))return start_failed(10);
    game_window=NULL;EnumWindows(find_window,0);if(!game_window)return start_failed(11);
    window_thread=GetWindowThreadProcessId(game_window,NULL);
    // Startup can expose a window before the frame manager is initialized.
    // Do not publish a mapping until a valid baseline exists; allow a later retry.
    baseline=get_speed();if(!isfinite(baseline)||baseline<1 || baseline>240)return start_failed(15);
    wchar_t name[128];swprintf(name,128,L"Local\\StoneshardCompanion.v8.%lu",GetCurrentProcessId());
    mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,4096,name);
    if(!mapping)return start_failed(12);
    if(GetLastError()==ERROR_ALREADY_EXISTS)return start_failed(13);
    shared=(SharedState*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,4096);if(!shared)return start_failed(14);
    ZeroMemory(shared,4096);
    FILETIME create,exit,kernel,user;GetProcessTimes(GetCurrentProcess(),&create,&exit,&kernel,&user);
    shared->process_start=((uint64_t)create.dwHighDateTime<<32)|create.dwLowDateTime;
    shared->magic=BRIDGE_MAGIC;shared->version=BRIDGE_VERSION;shared->pid=GetCurrentProcessId();
    shared->heartbeat=(LONG64)GetTickCount64();shared->game_hwnd=(uint64_t)game_window;
    shared->visor_state=-1;shared->capabilities=1;
    if(!bind_window(game_window))return start_failed(16);
    HMODULE pinned;GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)&BridgeStart,&pinned);
    shared->base_speed=baseline;shared->target_speed=baseline;shared->multiplier=1;
    snprintf(shared->detail,sizeof(shared->detail),"Engine connected");shared->ready=1;
    return 0;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){
    if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(instance);
    return TRUE;
}
