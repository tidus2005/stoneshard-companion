// Fully mocked Win32 input adapter. Never accesses real windows or input.
#include "../Bridge/protocol.h"
#include <stdbool.h>
#include <math.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
typedef struct RV{double real;}RV;
static SharedState storage,*shared=&storage;
static HWND game_window=(HWND)1;
static POINT cursor={0};static bool foreground=true,covered=false,modifiers=false,held=false,fail=false;
static uint64_t now=1000;static int moves,downs,ups,checks;
static HWND fake_foreground(void){return foreground?game_window:(HWND)2;}
static SHORT fake_key(int key){return held?0x8000:0;}
static BOOL fake_rect(HWND h,RECT* r){*r=(RECT){0,0,1920,1080};return true;}
static BOOL fake_screen(HWND h,POINT* p){p->x+=100;p->y+=200;return true;}
static HWND fake_at(POINT p){return covered?(HWND)2:game_window;}
static BOOL fake_cursor(POINT* p){*p=cursor;return true;}
static int fake_metric(int n){switch(n){case SM_XVIRTUALSCREEN:return -1920;case SM_YVIRTUALSCREEN:return 0;case SM_CXVIRTUALSCREEN:return 3840;default:return 2160;}}
static UINT fake_input(UINT n,INPUT* event,int size){
    if(fail)return 0;if(n!=1||size!=sizeof(INPUT))exit(2);
    if(event->mi.dwExtraInfo!=0x53435739)exit(3);
    if(event->mi.dwFlags&MOUSEEVENTF_MOVE){moves++;cursor.x=-1920+(LONG)floor(event->mi.dx*3840.0/65536);cursor.y=(LONG)floor(event->mi.dy*2160.0/65536);}
    if(event->mi.dwFlags&MOUSEEVENTF_LEFTDOWN)downs++;
    if(event->mi.dwFlags&MOUSEEVENTF_LEFTUP)ups++;return 1;
}
#define GetForegroundWindow fake_foreground
#define GetAsyncKeyState fake_key
#define GetClientRect fake_rect
#define ClientToScreen fake_screen
#define WindowFromPoint fake_at
#define GetCursorPos fake_cursor
#define GetSystemMetrics fake_metric
#define SendInput fake_input
#define GetTickCount64() now
static bool walk_modifiers_down(void){return modifiers;}
static double number(RV r){return r.real;}
static RV get_global(const char* name){return (RV){1};}
static void release_value(RV* r){}
static double member_number(RV r,const char* key){return !strcmp(key,"x")?507:767;}
static RV call_builtin(uintptr_t addr,int n,RV* args){return (RV){addr==0x527a0b0?27:addr==0x527a120?497:addr==0x527a040?960:540};}
#include "../Bridge/walk_click.h"
static void check(bool ok,const char* text){if(!ok){fprintf(stderr,"FAIL %s\n",text);exit(1);}checks++;}
static void reset(void){exit_mouse_phase=0;foreground=true;covered=modifiers=held=fail=false;moves=downs=ups=0;now=1000;shared->scene_ready=1;shared->ui_flags=0;}
int main(void){
    RV p={0};
    for(int d=1;d<=4;d++){
        reset();check(native_click_exit(p,d)&&moves==1&&downs==0&&ups==0,"only move is sent at queue time");
        check(cursor.x==(d==3?1008:d==4?1112:1060)&&cursor.y==(d==1?668:d==2?792:740),"adjacent physical pixel with desktop origin and scaling");
        now=1069;check(native_pump_exit_click()&&downs==0,"wait for hover update before press");
        now=1070;check(native_pump_exit_click()&&downs==1&&ups==0,"press in a later frame");
        now=1139;native_pump_exit_click();check(ups==0,"hold across frame boundary");
        now=1140;native_pump_exit_click();native_pump_exit_click();check(ups==1&&downs==1&&exit_mouse_phase==0,"one release and no repeat");
    }
    for(int reason=0;reason<5;reason++){
        reset();native_click_exit(p,2);if(reason==0)foreground=false;if(reason==1)covered=true;if(reason==2)cursor.x+=20;if(reason==3)shared->scene_ready=0;if(reason==4)modifiers=true;
        now=1200;check(!native_pump_exit_click()&&downs==0,"interruption after mouse move never presses");
        reset();native_click_exit(p,2);now=1200;native_pump_exit_click();if(reason==0)foreground=false;if(reason==1)covered=true;if(reason==2)cursor.y+=20;if(reason==3)shared->scene_ready=0;if(reason==4)modifiers=true;
        check(!native_pump_exit_click()&&ups==1,"interrupted hold releases mouse");
    }
    reset();covered=true;check(!native_click_exit(p,2)&&moves==0,"overlay obstruction rejected before moving mouse");
    reset();held=true;check(!native_click_exit(p,2)&&moves==0,"physical held buttons are preserved");
    reset();fail=true;check(!native_click_exit(p,2)&&exit_mouse_phase==0,"failed SendInput cannot arm future press");
    reset();native_click_exit(p,2);native_cancel_exit_click();now+=500;native_pump_exit_click();check(downs==0,"manual stop before press cancels deferred input");
    printf("%d mouse adapter checks passed; no real input sent.\n",checks);
}
