#pragma once
// Centered aspect-preserving viewport; supports letterboxing and VM scaling.
static bool exit_click_point(int direction,double px,double py,double cx,double cy,double cw,double ch,int width,int height,double* x,double* y){
    int d=direction>=11&&direction<=14?direction-10:direction;
    if(d<1||d>4||!isfinite(px)||!isfinite(py)||!isfinite(cx)||!isfinite(cy)||!isfinite(cw)||!isfinite(ch)||cw<=0||ch<=0||width<=0||height<=0)return false;
    double scale=fmin(width/cw,height/ch),ox=(width-cw*scale)/2,oy=(height-ch*scale)/2;
    double tx=px+(d==3?-26:d==4?26:0),ty=py+(d==1?-26:d==2?26:0);
    *x=ox+(tx-cx)*scale;*y=oy+(ty-cy)*scale;
    return tx>=cx&&ty>=cy&&tx<cx+cw&&ty<cy+ch&&*x>=1&&*y>=1&&*x<width-1&&*y<height-1;
}
#ifndef COMPANION_NATIVE_TEST
#define WALK_MOUSE_MARKER ((ULONG_PTR)0x53435739)
// Steam Build 24780451: registered camera_get_view_* functions. Read the
// actual world camera, including game-frame offsets, not cached GUI geometry.
static double exit_camera_value(uintptr_t routine,RV camera){
    RV result=call_builtin(routine,1,&camera);double value=number(result);release_value(&result);return value;
}
static int exit_mouse_phase;static uint64_t exit_mouse_due;static POINT exit_mouse_point;
static void native_cancel_exit_click(void){
    if(exit_mouse_phase==2){INPUT up={0};up.type=INPUT_MOUSE;up.mi.dwFlags=MOUSEEVENTF_LEFTUP;up.mi.dwExtraInfo=WALK_MOUSE_MARKER;SendInput(1,&up,sizeof(up));}
    exit_mouse_phase=0;
}
static bool native_pump_exit_click(void){
    if(!exit_mouse_phase)return true;
    POINT cursor;bool safe=GetForegroundWindow()==game_window&&GetCursorPos(&cursor)&&
        abs(cursor.x-exit_mouse_point.x)<=2&&abs(cursor.y-exit_mouse_point.y)<=2&&
        WindowFromPoint(cursor)==game_window&&!(shared->ui_flags&~8u)&&shared->scene_ready&& !walk_modifiers_down();
    if(!safe){native_cancel_exit_click();return false;}
    if(GetTickCount64()<exit_mouse_due)return true;
    INPUT event={0};event.type=INPUT_MOUSE;event.mi.dwExtraInfo=WALK_MOUSE_MARKER;
    event.mi.dwFlags=exit_mouse_phase==1?MOUSEEVENTF_LEFTDOWN:MOUSEEVENTF_LEFTUP;
    if(SendInput(1,&event,sizeof(event))!=1){native_cancel_exit_click();return false;}
    if(exit_mouse_phase==1){exit_mouse_phase=2;exit_mouse_due=GetTickCount64()+100;}else exit_mouse_phase=0;
    return true;
}
static bool native_click_exit(RV player,int direction){
    if(GetForegroundWindow()!=game_window||walk_modifiers_down()||
       ((GetAsyncKeyState(VK_LBUTTON)|GetAsyncKeyState(VK_RBUTTON)|GetAsyncKeyState(VK_MBUTTON))&0x8000))return false;
    RECT rc;if(!GetClientRect(game_window,&rc))return false;
    RV camera=get_global("cameraMain");if(!isfinite(number(camera))||number(camera)<0){release_value(&camera);return false;}
    double cx=exit_camera_value(0x527a0b0,camera),cy=exit_camera_value(0x527a120,camera);
    double cw=exit_camera_value(0x527a040,camera),ch=exit_camera_value(0x5279a90,camera);release_value(&camera);
    double x,y;
    if(!exit_click_point(direction,member_number(player,"x"),member_number(player,"y"),
        cx,cy,cw,ch,rc.right,rc.bottom,&x,&y))return false;
    POINT point={(LONG)lround(x),(LONG)lround(y)};
    if(!ClientToScreen(game_window,&point)||WindowFromPoint(point)!=game_window)return false;
    int vx=GetSystemMetrics(SM_XVIRTUALSCREEN),vy=GetSystemMetrics(SM_YVIRTUALSCREEN),vw=GetSystemMetrics(SM_CXVIRTUALSCREEN),vh=GetSystemMetrics(SM_CYVIRTUALSCREEN);
    if(vw<2||vh<2||point.x<vx||point.y<vy||point.x>=vx+vw||point.y>=vy+vh)return false;
    INPUT move={0};move.type=INPUT_MOUSE;move.mi.dwExtraInfo=WALK_MOUSE_MARKER;
    move.mi.dx=(LONG)(((double)(point.x-vx)+.5)*65536/vw);move.mi.dy=(LONG)(((double)(point.y-vy)+.5)*65536/vh);
    move.mi.dwFlags=MOUSEEVENTF_MOVE|MOUSEEVENTF_ABSOLUTE|MOUSEEVENTF_VIRTUALDESK;
    if(SendInput(1,&move,sizeof(move))!=1)return false;
    exit_mouse_point=point;exit_mouse_phase=1;exit_mouse_due=GetTickCount64()+150;
    return true;
}
#endif
