#pragma once
// Cardinal grid journeys cross once; center, corners and keyboard journeys
// stop at their target. Never retry an interrupted route or chain maps.
enum { WALK_NONE=0,WALK_ACTIVE=1,WALK_ARRIVED=2,WALK_MANUAL=3,WALK_THREAT=4,
       WALK_UI=5,WALK_SCENE=6,WALK_BLOCKED=7,WALK_BACKGROUND=8,WALK_TIMEOUT=9 };
static uint64_t walk_scene,walk_window,walk_started,walk_progress;
static double walk_player_id,walk_last_x,walk_last_y;
static bool walk_dispatch_pending;
static bool walk_exit_activated;
typedef struct WalkCandidate {int dx,dy,distance;double from_player;} WalkCandidate;
static WalkCandidate walk_centers[289];
static int walk_center_count,walk_center_cursor;
static void prepare_center(double x,double y){
    walk_center_count=walk_center_cursor=0;
    for(int dx=-8;dx<=8;dx++)for(int dy=-8;dy<=8;dy++){
        double tx=x+dx*26,ty=y+dy*26;
        if(tx<39||ty<39||tx>floor(shared->map_w/26)*26-39||ty>floor(shared->map_h/26)*26-39)continue;
        WalkCandidate c={dx,dy,dx*dx+dy*dy,(tx-shared->player_x)*(tx-shared->player_x)+(ty-shared->player_y)*(ty-shared->player_y)};
        int at=walk_center_count++;
        while(at>0&&(walk_centers[at-1].distance>c.distance||(walk_centers[at-1].distance==c.distance&&walk_centers[at-1].from_player>c.from_player))){walk_centers[at]=walk_centers[at-1];at--;}
        walk_centers[at]=c;
    }
}
static bool walk_crosses_map(int direction){return direction>=1&&direction<=4;}
static bool walk_modifiers_down(void){
#ifdef COMPANION_NATIVE_TEST
    return test_modifiers_down;
#else
    return ((GetAsyncKeyState(VK_CONTROL)|GetAsyncKeyState(VK_MENU)|GetAsyncKeyState(VK_SHIFT)|GetAsyncKeyState(VK_LWIN)|GetAsyncKeyState(VK_RWIN))&0x8000)!=0;
#endif
}
static bool walk_target(int direction,double width,double height,bool outer,double* x,double* y){
    if(direction<1||direction>9||!isfinite(width)||!isfinite(height)||width<104||height<104||width>26000||height>26000)return false;
    int cols=(int)floor(width/26),rows=(int)floor(height/26);
    *x=(cols/2)*26+13;*y=(rows/2)*26+13;
    int inset=outer?0:1;
    if(direction==1||direction==6||direction==7)*y=inset*26+13;
    if(direction==2||direction==8||direction==9)*y=(rows-1-inset)*26+13;
    if(direction==3||direction==6||direction==8)*x=inset*26+13;
    if(direction==4||direction==7||direction==9)*x=(cols-1-inset)*26+13;
    return true;
}
// Keyboard routes preserve the character's column/row, independent of camera.
// They stop on the inner boundary. A fresh press there requests one exit click.
static bool walk_destination(int direction,double* x,double* y){
    bool relative=direction>=11&&direction<=14;
    if(!walk_target(relative?direction-10:direction,shared->map_w,shared->map_h,false,x,y))return false;
    if(relative){
        double px=shared->player_x,py=shared->player_y;
        if(!isfinite(px)||!isfinite(py)||px<0||py<0||px>=shared->map_w||py>=shared->map_h)return false;
        if(direction<=12)*x=floor(px/26)*26+13;else *y=floor(py/26)*26+13;
        // Do not route along an outer transition row or column.
        if(*x<39||*y<39||*x>floor(shared->map_w/26)*26-39||*y>floor(shared->map_h/26)*26-39)return false;
    }
    return true;
}
static bool walk_threat(RV player){
    return member_number(player,"is_see_enemy")!=0||member_number(player,"is_damage_taken")!=0||member_number(player,"is_take_injury")!=0;
}
static int walk_cardinal(int direction){return direction>=11&&direction<=14?direction-10:direction>=1&&direction<=4?direction:0;}
static bool walk_exit_target(int direction,double* x,double* y){
    int d=walk_cardinal(direction);double px=shared->player_x,py=shared->player_y;
    if(!d||!walk_target(d,shared->map_w,shared->map_h,true,x,y)||!isfinite(px)||!isfinite(py))return false;
    // Only consider a nearby real exit once the character reached the inner
    // boundary (or the actual transition cell lies inward of that assumption).
    if(native_exit_target(direction,px,py,x,y)&&fabs(*x-px)<28&&fabs(*y-py)<28)return true;
    int cols=(int)floor(shared->map_w/26),rows=(int)floor(shared->map_h/26);
    if(px<0||py<0||px>=cols*26||py>=rows*26)return false;
    int col=(int)floor(px/26),row=(int)floor(py/26);
    bool edge=d==1?row<=1:d==2?row>=rows-2:d==3?col<=1:col>=cols-2;
    if(!edge)return false;
    // Retain the current column/row, never drag a border player to its midpoint.
    if(d<=2){if(col<1||col>cols-2)return false;*x=col*26+13;}
    else{if(row<1||row>rows-2)return false;*y=row*26+13;}
    return true;
}
static void finish_walk(int reason,bool stop_native){
    if(shared->walk_state!=WALK_ACTIVE)return;
    // Never cancel a new scene's or a different character's path.
    if(stop_native&&!walk_dispatch_pending&&shared->scene_ready&&walk_scene==shared->scene_generation&&walk_window==shared->window_generation){
        RV player=find_instance("o_player");
        if(valid_object(player)&&member_number(player,"id")==walk_player_id){RV flag=numeric(0),out=call_script(0x19123a0,player,1,&flag);release_value(&out);}
        release_value(&player);
    }
    shared->walk_state=reason;walk_dispatch_pending=false;
}
static int start_walk(int direction){
    if(direction==0){finish_walk(WALK_MANUAL,true);return 0;}
    double x,y;bool exit=walk_exit_target(direction,&x,&y);
    if(!exit&&!walk_destination(direction,&x,&y))return 4;
    if(shared->walk_state==WALK_ACTIVE)return 7;
    RV player=find_instance("o_player");
    if(!valid_object(player)||!supply_safe(player)){release_value(&player);return 7;}
    shared->walk_direction=direction;shared->walk_x=x;shared->walk_y=y;shared->walk_phase=exit?1:0;
    walk_exit_activated=false;
    if(direction==5)prepare_center(x,y);
    if(!exit&&fabs(shared->player_x-x)<2&&fabs(shared->player_y-y)<2){
        if(!walk_crosses_map(direction)){shared->walk_state=WALK_ARRIVED;release_value(&player);return 0;}
        walk_target(direction,shared->map_w,shared->map_h,true,&shared->walk_x,&shared->walk_y);shared->walk_phase=1;
    }
    walk_player_id=member_number(player,"id");walk_scene=shared->scene_generation;walk_window=shared->window_generation;
    walk_started=walk_progress=GetTickCount64();walk_last_x=shared->player_x;walk_last_y=shared->player_y;
    // Global hotkeys arrive while Ctrl/Alt are still held. Dispatch after release
    // so native movement sees the same conditions as an ordinary ground click.
    walk_dispatch_pending=true;
    release_value(&player);shared->walk_state=WALK_ACTIVE;return 0;
}
static int request_walk(int direction){
    if(shared->walk_state==WALK_ACTIVE&&direction!=0){
        bool arrived=false;
        if(walk_cardinal(direction)!=0&&walk_cardinal(direction)==walk_cardinal(shared->walk_direction)&&shared->walk_phase==0&&!walk_dispatch_pending&&
           shared->scene_ready&&walk_scene==shared->scene_generation&&walk_window==shared->window_generation){
            RV player=find_instance("o_player");
            double exit_x,exit_y,px=member_number(player,"x"),py=member_number(player,"y");
            arrived=valid_object(player)&&member_number(player,"id")==walk_player_id&&supply_safe(player)&&
                ((fabs(px-shared->walk_x)<2&&fabs(py-shared->walk_y)<2)||
                 (native_exit_target(direction,px,py,&exit_x,&exit_y)&&fabs(exit_x-px)<28&&fabs(exit_y-py)<28));
            release_value(&player);
        }
        // Handle the short gap between native arrival and the next timer sample.
        finish_walk(arrived?WALK_ARRIVED:WALK_MANUAL,!arrived);
        if(!arrived)return 0;
    }
    return start_walk(direction);
}
static void reconcile_walk(uint32_t reasons){
    if(shared->walk_state!=WALK_ACTIVE)return;
    if(!shared->scene_ready||shared->scene_generation!=walk_scene||shared->window_generation!=walk_window){finish_walk(WALK_SCENE,false);return;}
    if(reasons&(1|8)){finish_walk(WALK_BACKGROUND,true);return;}
    if(shared->ui_flags&~8u){finish_walk(WALK_UI,true);return;}
    RV player=find_instance("o_player");
    if(!valid_object(player)||member_number(player,"id")!=walk_player_id){release_value(&player);finish_walk(WALK_SCENE,false);return;}
    if(walk_threat(player)){release_value(&player);finish_walk(WALK_THREAT,true);return;}
    if(walk_dispatch_pending){
        uint64_t elapsed=GetTickCount64()-walk_started;
        if(elapsed>(shared->walk_direction==5?8000:2000)){release_value(&player);finish_walk(WALK_TIMEOUT,false);return;}
        if(elapsed<150||walk_modifiers_down()){release_value(&player);return;}
        if(!supply_safe(player)){release_value(&player);finish_walk(WALK_BLOCKED,false);return;}
        if(shared->walk_direction==5){
            double cx,cy;walk_target(5,shared->map_w,shared->map_h,false,&cx,&cy);
            bool found=false;uint64_t scan_start=GetTickCount64();
            for(int budget=0;budget<8&&walk_center_cursor<walk_center_count;budget++){
                WalkCandidate c=walk_centers[walk_center_cursor++];double tx=cx+c.dx*26,ty=cy+c.dy*26;
                bool here=fabs(member_number(player,"x")-tx)<2&&fabs(member_number(player,"y")-ty)<2;
                if(here||native_center_reachable(player,tx,ty)){shared->walk_x=tx;shared->walk_y=ty;found=true;break;}
                if(GetTickCount64()-scan_start>=5)break;
            }
            if(!found){release_value(&player);if(walk_center_cursor==walk_center_count)finish_walk(WALK_BLOCKED,false);return;}
            if(fabs(member_number(player,"x")-shared->walk_x)<2&&fabs(member_number(player,"y")-shared->walk_y)<2){release_value(&player);finish_walk(WALK_ARRIVED,false);return;}
        }
        if(shared->walk_phase==1){
            double tx,ty,px=member_number(player,"x"),py=member_number(player,"y");
            if(!native_exit_target(shared->walk_direction,px,py,&tx,&ty)){release_value(&player);finish_walk(WALK_BLOCKED,false);return;}
            shared->walk_x=tx;shared->walk_y=ty;
            if(fabs(px-tx)<2&&fabs(py-ty)<2){native_activate_exit(player);walk_exit_activated=true;release_value(&player);walk_dispatch_pending=false;walk_progress=walk_started=GetTickCount64();return;}
        }
        restore_camera(true);
        RV args[2]={numeric(shared->walk_x),numeric(shared->walk_y)},out=call_script(0x1805180,player,2,args);
        release_value(&out);release_value(&player);walk_dispatch_pending=false;
        walk_started=walk_progress=GetTickCount64();return;
    }
    double x=member_number(player,"x"),y=member_number(player,"y");
    RV state=get_member(player,"state");bool idle=!strcmp(text_value(state),"idle")&&member_number(player,"movingIsDone")==1;
    double path=member_number(player,"path");bool safe=supply_safe(player);release_value(&state);release_value(&player);
    uint64_t now=GetTickCount64();
    if(fabs(x-walk_last_x)>1||fabs(y-walk_last_y)>1){walk_progress=now;walk_last_x=x;walk_last_y=y;}
    if(idle&&fabs(x-shared->walk_x)<2&&fabs(y-shared->walk_y)<2){
        if(shared->walk_phase==0&&!walk_crosses_map(shared->walk_direction)){finish_walk(WALK_ARRIVED,false);return;}
        if(shared->walk_phase==0){
            if(!safe){finish_walk(WALK_BLOCKED,false);return;}
            if(!walk_target(shared->walk_direction,shared->map_w,shared->map_h,true,&shared->walk_x,&shared->walk_y)){finish_walk(WALK_BLOCKED,false);return;}
            shared->walk_phase=1;walk_dispatch_pending=true;walk_started=walk_progress=now;
            shared->supply_flags&=~3u;return;
        }
        // Native border logic owns the transition. A missing/blocked exit is
        // not a success and never causes repeated clicks or forced room writes.
        if(!walk_exit_activated&&safe){
            double tx,ty;
            if(!native_exit_target(shared->walk_direction,x,y,&tx,&ty)||fabs(x-tx)>=2||fabs(y-ty)>=2){finish_walk(WALK_BLOCKED,false);return;}
            RV current=find_instance("o_player");
            if(valid_object(current)&&member_number(current,"id")==walk_player_id&&supply_safe(current))native_activate_exit(current);
            release_value(&current);walk_exit_activated=true;walk_progress=now;
        }
        if(now-walk_progress>2000)finish_walk(WALK_BLOCKED,false);
        return;
    }
    if(idle&&now-walk_started>700&&(!isfinite(path)||path<0)){finish_walk(WALK_BLOCKED,false);return;}
    if(now-walk_progress>8000||now-walk_started>180000)finish_walk(WALK_TIMEOUT,true);
    if(shared->walk_state==WALK_ACTIVE)shared->supply_flags&=~3u;
}
static int walk_key_direction(unsigned key){
    switch(key){case VK_UP:return 11;case VK_DOWN:return 12;case VK_LEFT:return 13;case VK_RIGHT:return 14;default:return 0;}
}
static void set_walk_keys(bool enabled){
    shared->walk_keys_enabled=enabled?1:0;
    if(!enabled&&shared->walk_direction>=11&&shared->walk_direction<=14)finish_walk(WALK_MANUAL,true);
}
// Called only for the game's WM_KEYDOWN. No global unmodified hotkeys.
static bool handle_walk_key(unsigned key,bool repeat,bool modifiers,bool foreground,bool lease_live){
    int direction=walk_key_direction(key);
    if(!direction||!shared->walk_keys_enabled||modifiers||!foreground||!lease_live||!shared->ready||!shared->scene_ready||(shared->ui_flags&~8u))return false;
    if(repeat)return true; // A held key never restarts an interrupted journey.
    int result=request_walk(direction);
    if(result){
        RV player=find_instance("o_player");
        shared->walk_state=valid_object(player)&&walk_threat(player)?WALK_THREAT:WALK_BLOCKED;
        release_value(&player);
    }
    return true; // Unsafe attempts must not fall through into native movement.
}
