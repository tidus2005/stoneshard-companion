#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <math.h>
#include "../Bridge/protocol.h"
#include "../Bridge/stats_catalog.h"
typedef struct RV{double real;void* ptr;int kind;const char* text;}RV;
static SharedState state,*shared=&state;
static int checks;static bool fog,visible=true,alive=true;static double current_id=200,selected=-4;
static RV numeric(double n){return(RV){.real=n};}
static double number(RV v){return v.kind==0?v.real:NAN;}
static bool valid_object(RV v){return v.kind==6&&v.ptr;}
static const char* text_value(RV v){return v.text?v.text:"";}
static void release_value(RV* v){memset(v,0,sizeof(*v));}
static RV string_value(const char* s){return(RV){.kind=1,.text=s};}
static RV instance_from_id(RV id){return number(id)==200||number(id)==100?(RV){.kind=6,.ptr=(void*)(uintptr_t)number(id)}:(RV){.kind=5};}
static RV find_instance(const char* key){return(RV){.kind=6,.ptr=(void*)300};}
static double member_number(RV obj,const char* key){if(!strcmp(key,"id"))return (double)(uintptr_t)obj.ptr;if(!strcmp(key,"is_in_fog"))return fog;return 26;}
static RV live_member(RV obj,const char* key){
    if(!strcmp(key,"name"))return string_value("Test target");
    if(!strcmp(key,"is_life"))return numeric(alive);
    if(!strcmp(key,"isVisible"))return numeric(visible);
    if(!strcmp(key,"Hit_Chance"))return numeric(90);
    if(!strcmp(key,"EVS"))return numeric(10);
    if(!strcmp(key,"FMB"))return numeric(5);
    if(!strcmp(key,"buffs"))return numeric(0);
    return(RV){.kind=5};
}
static double character_stat(const char* key){return NAN;}
static RV get_global(const char* key){return numeric(selected);}
static RV object_name(RV obj){return string_value("o_enemy_test");}
static double builtin_var(uintptr_t addr){return 26;}
static RV call_builtin(uintptr_t addr,int count,RV* args){return numeric(addr==0x5336680?12:current_id);}
static int safe_list_size(RV list){return 0;}
static RV list_value(RV list,int i){return(RV){.kind=5};}
static DWORD test_foreground=42;
static HWND test_window(void){return(HWND)1;}
static DWORD test_pid(HWND h,DWORD* p){*p=test_foreground;return 1;}
static DWORD test_current(void){return 42;}
static BOOL test_cursor(LPPOINT p){p->x=100;p->y=200;return TRUE;}
static HWND game_window=(HWND)1;
static BOOL test_rect(HWND h,LPRECT r){r->right=1920;r->bottom=1080;return TRUE;}
static BOOL test_origin(HWND h,LPPOINT p){p->x=0;p->y=0;return TRUE;}
#define GetClientRect test_rect
#define ClientToScreen test_origin
#define GetForegroundWindow test_window
#define GetWindowThreadProcessId test_pid
#define GetCurrentProcessId test_current
#define GetCursorPos test_cursor
#include "../Bridge/combat_preview.h"
static void check(bool yes,const char* message){if(!yes){fprintf(stderr,"FAIL %s\n",message);exit(1);}checks++;}
static void tick(void){combat_due=0;refresh_combat(true,(RV){.kind=6,.ptr=(void*)100});}
int main(void){
    shared->scene_ready=1;shared->scene_generation=7;shared->combat_enabled=1;shared->heartbeat=GetTickCount64();
    tick();check(strstr(shared->combat_response,"Test target")!=NULL,"visible target captured");
    visible=false;tick();check(!*shared->combat_response,"hidden target clears prior snapshot");visible=true;
    fog=true;tick();check(!*shared->combat_response,"fog clears prior snapshot");fog=false;
    alive=false;tick();check(!*shared->combat_response,"dead target clears prior snapshot");alive=true;
    current_id=100;tick();check(!*shared->combat_response,"self rejected");current_id=200;
    shared->ui_flags=1;tick();check(!*shared->combat_response,"inventory blocks hover");shared->ui_flags=8;tick();check(*shared->combat_response,"native tooltip allowed");shared->ui_flags=0;
    test_foreground=43;tick();check(!*shared->combat_response,"background clears preview");test_foreground=42;
    shared->combat_enabled=0;tick();check(!*shared->combat_response,"disabled clears preview");shared->combat_enabled=1;
    shared->scene_ready=0;tick();check(!*shared->combat_response,"loading clears preview");shared->scene_ready=1;
    selected=1;tick();check(strstr(shared->combat_response,"\"skillSelected\":true")!=NULL,"selection mode never becomes normal attack");
    combat_at=sizeof(combat_buffer)-2;combat_full=false;cj("too long");check(combat_full,"bounded writer refuses overflow");
    printf("%d combat native read-only snapshot checks passed (mocked engine).\n",checks);return 0;
}
