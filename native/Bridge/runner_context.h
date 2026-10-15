#pragma once
// YYC scripts keep a linked stack frame and implicit instance lookup state.
// Window messages can be dispatched by a script/extension's nested message
// pump. Being on the window thread alone does not make a GML call safe.
typedef struct RunnerContext {
    void **frame_slot, **self_slot, **lookup_slot;
    void *frame, *self, *lookup;
    bool *entered;
} RunnerContext;
static bool runner_enter(RunnerContext* saved, bool* entered,
                         void** frame, void** self, void** lookup) {
    if (*entered || *frame) return false;
    *saved=(RunnerContext){frame,self,lookup,*frame,*self,*lookup,entered};
    *entered=true;
    return true;
}
static void runner_leave(RunnerContext* saved) {
    *saved->frame_slot=saved->frame;
    *saved->self_slot=saved->self;
    *saved->lookup_slot=saved->lookup;
    *saved->entered=false;
}
