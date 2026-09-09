#pragma once
// Pixel projection is accepted only for a full-client viewport of matching aspect.
static bool exit_click_point(int direction,double px,double py,double cx,double cy,double cw,double ch,int width,int height,double* x,double* y){
    int d=direction>=11&&direction<=14?direction-10:direction;
    if(d<1||d>4||!isfinite(px)||!isfinite(py)||!isfinite(cx)||!isfinite(cy)||!isfinite(cw)||!isfinite(ch)||cw<=0||ch<=0||width<=0||height<=0)return false;
    if(fabs((double)width/height-cw/ch)>.01*(cw/ch))return false;
    double tx=px+(d==3?-26:d==4?26:0),ty=py+(d==1?-26:d==2?26:0);
    *x=(tx-cx)/cw*width;*y=(ty-cy)/ch*height;
    return *x>=1&&*y>=1&&*x<width-1&&*y<height-1;
}
#ifndef COMPANION_NATIVE_TEST
#define WALK_MOUSE_MARKER ((ULONG_PTR)0x53435739)
// Steam Build 24780451: registered camera_get_view_* functions. Read the
// actual world camera, including game-frame offsets, not cached GUI geometry.
static double exit_camera_value(uintptr_t routine,RV camera){
    RV result=call_builtin(routine,1,&camera);double value=number(result);release_value(&result);return value;
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
    INPUT input[3]={0};for(int i=0;i<3;i++){input[i].type=INPUT_MOUSE;input[i].mi.dwExtraInfo=WALK_MOUSE_MARKER;}
    input[0].mi.dx=(LONG)(((double)(point.x-vx)+.5)*65536/vw);input[0].mi.dy=(LONG)(((double)(point.y-vy)+.5)*65536/vh);
    input[0].mi.dwFlags=MOUSEEVENTF_MOVE|MOUSEEVENTF_ABSOLUTE|MOUSEEVENTF_VIRTUALDESK;
    input[1].mi.dwFlags=MOUSEEVENTF_LEFTDOWN;input[2].mi.dwFlags=MOUSEEVENTF_LEFTUP;
    UINT sent=SendInput(3,input,sizeof(INPUT));
    if(sent==2)SendInput(1,&input[2],sizeof(INPUT)); // release a partially inserted press
    return sent==3;
}
#endif
