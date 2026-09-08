#pragma once
// One journey: native path to the inner edge, then one native click on the
// outer transition tile. Never retry an interrupted route or chain maps.
enum { WALK_NONE=0,WALK_ACTIVE=1,WALK_ARRIVED=2,WALK_MANUAL=3,WALK_THREAT=4,
       WALK_UI=5,WALK_SCENE=6,WALK_BLOCKED=7,WALK_BACKGROUND=8,WALK_TIMEOUT=9 };
static uint64_t walk_scene,walk_window,walk_started,walk_progress;
static double walk_player_id,walk_last_x,walk_last_y;
static bool walk_dispatch_pending;
static bool walk_modifiers_down(void){
#ifdef COMPANION_NATIVE_TEST
    return test_modifiers_down;
#else
    return ((GetAsyncKeyState(VK_CONTROL)|GetAsyncKeyState(VK_MENU)|GetAsyncKeyState(VK_SHIFT))&0x8000)!=0;
#endif
}
static bool walk_target(int direction,double width,double height,bool outer,double* x,double* y){
    if(direction<1||direction>5||!isfinite(width)||!isfinite(height)||width<104||height<104||width>26000||height>26000)return false;
    int cols=(int)floor(width/26),rows=(int)floor(height/26);
    *x=(cols/2)*26+13;*y=(rows/2)*26+13;
    int inset=outer?0:1;
    if(direction==1)*y=inset*26+13;if(direction==2)*y=(rows-1-inset)*26+13;
    if(direction==3)*x=inset*26+13;if(direction==4)*x=(cols-1-inset)*26+13;
    return true;
}
static bool walk_threat(RV player){
    return member_number(player,"is_see_enemy")!=0||member_number(player,"is_damage_taken")!=0||member_number(player,"is_take_injury")!=0;
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
    double x,y;if(!walk_target(direction,shared->map_w,shared->map_h,false,&x,&y))return 4;
    if(shared->walk_state==WALK_ACTIVE)return 7;
    RV player=find_instance("o_player");
    if(!valid_object(player)||!supply_safe(player)){release_value(&player);return 7;}
    shared->walk_direction=direction;shared->walk_x=x;shared->walk_y=y;shared->walk_phase=0;
    if(fabs(shared->player_x-x)<2&&fabs(shared->player_y-y)<2){
        if(direction==5){shared->walk_state=WALK_ARRIVED;release_value(&player);return 0;}
        walk_target(direction,shared->map_w,shared->map_h,true,&shared->walk_x,&shared->walk_y);shared->walk_phase=1;
    }
    walk_player_id=member_number(player,"id");walk_scene=shared->scene_generation;walk_window=shared->window_generation;
    walk_started=walk_progress=GetTickCount64();walk_last_x=shared->player_x;walk_last_y=shared->player_y;
    // Global hotkeys arrive while Ctrl/Alt are still held. Dispatch after release
    // so native movement sees the same conditions as an ordinary ground click.
    walk_dispatch_pending=true;
    release_value(&player);shared->walk_state=WALK_ACTIVE;return 0;
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
        if(elapsed>2000){release_value(&player);finish_walk(WALK_TIMEOUT,false);return;}
        if(elapsed<150||walk_modifiers_down()){release_value(&player);return;}
        if(!supply_safe(player)){release_value(&player);finish_walk(WALK_BLOCKED,false);return;}
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
        if(shared->walk_direction==5){finish_walk(WALK_ARRIVED,false);return;}
        if(shared->walk_phase==0){
            if(!safe){finish_walk(WALK_BLOCKED,false);return;}
            if(!walk_target(shared->walk_direction,shared->map_w,shared->map_h,true,&shared->walk_x,&shared->walk_y)){finish_walk(WALK_BLOCKED,false);return;}
            shared->walk_phase=1;walk_dispatch_pending=true;walk_started=walk_progress=now;
            shared->supply_flags&=~3u;return;
        }
        // Native border logic owns the transition. A missing/blocked exit is
        // not a success and never causes repeated clicks or forced room writes.
        if(now-walk_progress>2000)finish_walk(WALK_BLOCKED,false);
        return;
    }
    if(idle&&now-walk_started>700&&(!isfinite(path)||path<0)){finish_walk(WALK_BLOCKED,false);return;}
    if(now-walk_progress>8000||now-walk_started>180000)finish_walk(WALK_TIMEOUT,true);
    if(shared->walk_state==WALK_ACTIVE)shared->supply_flags&=~3u;
}
