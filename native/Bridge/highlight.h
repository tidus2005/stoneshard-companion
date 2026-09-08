#pragma once
// Answer the original highlight query only at the ground-label creation and
// retention call sites. All movement/action queries and actual keys stay native.
static GameScript original_label_renderer;
static bool highlight_allowed(bool requested,uint32_t reasons,uint32_t ui_flags){
    return requested&&!(reasons&(1|2|8))&&!(ui_flags&(2|4|16|32));
}
static bool label_query_site(uintptr_t caller){return caller==0x3992a2f||caller==0x49cc96e;}
static RV* query_labels_at(uintptr_t caller,void* self,void* other,RV* out,int count,RV** args){
    RV* result=original_label_renderer(self,other,out,count,args);
    if(label_query_site(caller)){
        shared->label_draw_calls++;
        if(highlight_applied){release_value(result);*result=numeric(1);shared->label_draw_overrides++;}
    }
    return result;
}
static void reconcile_highlight(bool enabled){highlight_applied=enabled&&original_label_renderer!=NULL;}
#ifndef COMPANION_NATIVE_TEST
__attribute__((noinline)) static RV* draw_labels(void* self,void* other,RV* out,int count,RV** args){
    uintptr_t caller=(uintptr_t)__builtin_return_address(0)-(uintptr_t)game;
    return query_labels_at(caller,self,other,out,count,args);
}
static bool label_hook_attempted;
static bool install_label_renderer(void){
    if(label_hook_attempted)return original_label_renderer!=NULL;
    label_hook_attempted=true;
    // 19 whole position-independent prologue bytes of scr_check_keyboard_array.
    // Both exact label call instructions are guarded as well as the entry.
    // Installed on the game's window thread, between native game events.
    const BYTE expected[]={0x55,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x56,0x57,0x53,0x48,0x81,0xec,0x68,0x01,0,0};
    BYTE* entry=game+0x13515c0;
    if(memcmp(entry,expected,sizeof(expected)))return false;
    const uintptr_t sites[]={0x3992a2a,0x49cc969};
    for(int i=0;i<2;i++){BYTE* call=game+sites[i];int32_t relative;memcpy(&relative,call+1,4);if(call[0]!=0xe8||call+5+relative!=entry)return false;}
    BYTE* trampoline=VirtualAlloc(NULL,64,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);
    if(!trampoline)return false;
    memcpy(trampoline,expected,sizeof(expected));
    BYTE jump[14]={0xff,0x25,0,0,0,0};void* back=entry+sizeof(expected);
    memcpy(jump+6,&back,8);memcpy(trampoline+sizeof(expected),jump,sizeof(jump));
    DWORD old;
    if(!VirtualProtect(trampoline,64,PAGE_EXECUTE_READ,&old)){VirtualFree(trampoline,0,MEM_RELEASE);return false;}
    FlushInstructionCache(GetCurrentProcess(),trampoline,64);
    if(!VirtualProtect(entry,sizeof(expected),PAGE_EXECUTE_READWRITE,&old)){VirtualFree(trampoline,0,MEM_RELEASE);return false;}
    original_label_renderer=(GameScript)trampoline;
    void* hook=(void*)&draw_labels;memcpy(jump+6,&hook,8);
    memcpy(entry,jump,sizeof(jump));memset(entry+sizeof(jump),0x90,sizeof(expected)-sizeof(jump));
    DWORD unused;VirtualProtect(entry,sizeof(expected),old,&unused);
    FlushInstructionCache(GetCurrentProcess(),entry,sizeof(expected));return true;
}
#endif
