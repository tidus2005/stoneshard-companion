#pragma once
// All work runs on the game's own timer thread, under the foreground/lease gate.
static uint64_t visor_next,visor_last_threat,forage_due,forage_scan_due;
static double forage_id=-1,forage_x,forage_y,forage_route_x,forage_route_y,forage_before;
static int forage_clicks,forage_path_tries;
static bool forage_move_pending;
static uint64_t forage_seen_scene;
static uint64_t forage_path_due;
static double forage_last_x,forage_last_y;
static void reconcile_visor(uint32_t reasons){
    if(!(automation_flags&1)||reasons||(shared->ui_flags&~8u)||!shared->scene_ready)return;
    RV player=find_instance("o_player");if(!valid_object(player)){release_value(&player);return;}
    uint64_t now=GetTickCount64();bool threat=walk_threat(player);
    if(threat)visor_last_threat=now;
    // Only opening is delayed, to avoid oscillation when an enemy momentarily
    // disappears behind cover. Closing remains immediate when native input allows.
    bool close=threat||now-visor_last_threat<1500;
    RV helmet=visor_helmet();double open=member_number(helmet,"isOpen");
    if(now>=visor_next&&member_number(player,"turn_available")==1&&
       member_number(player,"movingIsDone")==1&&member_number(player,"is_sleeping")==0&&
       ((close&&open==1)||(!close&&open==0))){toggle_visor();visor_next=now+1000;}
    release_value(&helmet);release_value(&player);
}
static double selected_fodder_value(void){
    RV inventory=find_instance("o_inventory");double owner=member_number(inventory,"id"),total=0;release_value(&inventory);
    for(int i=0;i<256;i++){RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);return total;}
        if(fodder_carried(item,owner)){RV name=object_name(item);if(forage_material(text_value(name)))total+=member_number(item,"fodder_value");release_value(&name);}release_value(&item);}
    return NAN; // Incomplete inventory cannot confirm collection.
}
// Plants use tile corners while actors use tile centers; compare occupied cells.
static bool forage_adjacent(double px,double py,double x,double y){
    return isfinite(px)&&isfinite(py)&&isfinite(x)&&isfinite(y)&&
        fabs(floor(px/26)-floor(x/26))<=1&&fabs(floor(py/26)-floor(y/26))<=1;
}
static bool forage_visible(RV item){
    if(!valid_object(item)||member_number(item,"visible")!=1)return false;
    RV id=get_member(item,"id"),v=call_script(0xbbaab0,item,1,&id);
    bool visible=number(v)==1;release_value(&v);release_value(&id);return visible;
}
static bool forage_selected(RV item,int kind){
    if(!valid_object(item))return false;
    if(kind==1&&member_number(item,"is_execute")!=0)return false;
    RV asset=get_member(item,kind==1?"berryType":"inv_object");
    if(!isfinite(number(asset))||number(asset)<0){release_value(&asset);return false;}
    RV name=call_builtin(0x5335220,1,&asset);bool chosen=forage_material(text_value(name));
    release_value(&name);release_value(&asset);return chosen;
}
#include "forage_targets.h"
static void forage_stop(int reason){forage_phase=0;finish_walk(reason,true);telemetry_next=0;}
static void forage_resume(RV player){
    shared->walk_x=forage_route_x;shared->walk_y=forage_route_y;shared->walk_phase=0;
    forage_phase=0;walk_dispatch_pending=true;walk_started=walk_progress=GetTickCount64();
    walk_last_x=member_number(player,"x");walk_last_y=member_number(player,"y");
    forage_scan_due=GetTickCount64()+600;telemetry_next=0;
}
static void forage_skip(RV player){
    RV flag=numeric(0),stopped=call_script(0x19123a0,player,1,&flag);release_value(&stopped);
    forage_defer(member_number(player,"x"),member_number(player,"y"));forage_resume(player);
}
static bool forage_approach(RV player){
    RV target=instance_from_id(numeric(forage_id));
    if(!forage_visible(target)){release_value(&target);return false;}
    double x=member_number(target,"x"),y=member_number(target,"y");release_value(&target);
    double px=member_number(player,"x"),py=member_number(player,"y");
    if(!isfinite(x)||!isfinite(y))return false;
    // Prefer a reachable adjacent cell nearest to the current position.
    int first=-1;double nearest=INFINITY;
    for(int i=1;i<=8;i++){WalkCandidate c=center_candidate(i);double dx=x+c.dx*26-px,dy=y+c.dy*26-py;
        if(dx*dx+dy*dy<nearest){nearest=dx*dx+dy*dy;first=i;}}
    for(int n=0;n<8;n++){int i=1+(first-1+n)%8;WalkCandidate c=center_candidate(i);
        double tx=floor(x/26)*26+13+c.dx*26,ty=floor(y/26)*26+13+c.dy*26;
        if(tx<39||ty<39||tx>shared->map_w-39||ty>shared->map_h-39)continue;
        if((fabs(px-tx)<2&&fabs(py-ty)<2)||native_center_reachable(player,tx,ty)){
            forage_x=tx;forage_y=ty;forage_phase=1;forage_move_pending=true;forage_scan_due=GetTickCount64();
            forage_last_x=px;forage_last_y=py;forage_path_tries=0;forage_path_due=GetTickCount64()+700;
            forage_due=GetTickCount64()+15000;return true;
        }
    }
    return false;
}
static bool reconcile_forage(uint32_t reasons){
    if(forage_seen_scene!=shared->scene_generation){forage_seen_scene=shared->scene_generation;forage_reset_targets();}
    if(shared->walk_state!=WALK_ACTIVE){forage_phase=0;return false;}
    if(reasons||!(automation_flags&2)||!shared->scene_ready||walk_scene!=shared->scene_generation||walk_window!=shared->window_generation){
        if(forage_phase){forage_stop(reasons?WALK_BACKGROUND:WALK_MANUAL);return true;}return false;}
    if(shared->ui_flags&~8u){if(forage_phase){forage_stop(WALK_UI);return true;}return false;}
    RV player=find_instance("o_player");
    if(!valid_object(player)||member_number(player,"id")!=walk_player_id||walk_threat(player)){
        release_value(&player);if(forage_phase){forage_stop(WALK_THREAT);return true;}return false;}
    uint64_t now=GetTickCount64();double px=member_number(player,"x"),py=member_number(player,"y");
    if(forage_phase){
        if(!native_pump_exit_click()){release_value(&player);forage_stop(WALK_MANUAL);return true;}
        if(now>forage_due){
            if(forage_phase==1||forage_phase==2||forage_phase==3||forage_phase==5)forage_skip(player);
            else forage_stop(WALK_TIMEOUT);
            release_value(&player);return true;
        }
        if(!walk_safe(player)){release_value(&player);return true;}
        if(forage_phase==5){
            if(now>=forage_scan_due&&!forage_approach(player))forage_skip(player);
            release_value(&player);return true;
        }
        if(forage_phase==4){
            if(now>=forage_scan_due)forage_resume(player);
            release_value(&player);return true;
        }
        if(forage_phase==1&&forage_move_pending){
            if(now<forage_scan_due){release_value(&player);return true;}
            RV args[2]={numeric(forage_x),numeric(forage_y)},out=call_script(0x1805180,player,2,args);release_value(&out);
            forage_move_pending=false;forage_path_tries++;forage_path_due=now+700;release_value(&player);return true;
        }
        if(forage_phase==1&&(fabs(px-forage_last_x)>1||fabs(py-forage_last_y)>1)){
            forage_last_x=px;forage_last_y=py;forage_path_due=now+700;
        }
        if(forage_phase==1&&(fabs(px-forage_x)>=2||fabs(py-forage_y)>=2)&&now>=forage_path_due&&forage_path_tries<2){
            forage_move_pending=true;forage_scan_due=now;release_value(&player);return true;
        }
        if(forage_phase==1&&fabs(px-forage_x)<2&&fabs(py-forage_y)<2){
            RV target=instance_from_id(numeric(forage_id));
            if(!forage_visible(target)){release_value(&target);forage_resume(player);release_value(&player);return true;}
            // Yield overlays first; real input goes through the original pickup
            // / harvest mouse handler, including its range and capacity checks.
            // The camera eases after the final movement frame. Let it settle
            // before projecting the plant to screen coordinates.
            forage_phase=2;shared->walk_phase=2;forage_due=now+5000;forage_scan_due=now+500;forage_clicks=0;
            release_value(&target);telemetry_next=0;
        }else if(forage_phase==2&&now>=forage_scan_due){
            RV target=instance_from_id(numeric(forage_id));bool visible=forage_visible(target);
            double tx=member_number(target,"x"),ty=member_number(target,"y");release_value(&target);
            if(!visible||!forage_adjacent(px,py,tx,ty)||!native_click_world(tx,ty)){release_value(&player);forage_stop(WALK_BLOCKED);return true;}
            forage_phase=3;forage_due=now+4000;forage_scan_due=now+1000;forage_clicks++;
        }else if(forage_phase==3&&selected_fodder_value()>forage_before){
            if(exit_mouse_phase){release_value(&player);return true;}
            shared->walk_state=0;shared->walk_phase=0;
            int result=craft_fodder(true);shared->walk_state=WALK_ACTIVE;
            if(result==0){
                // The native close routine runs the menu's return/cleanup event.
                // Only our own empty crafting session is closed; failed capacity
                // checks leave its contents visible for manual recovery.
                RV menu=find_instance("o_craftingConsumsMenu");
                if(valid_object(menu)){RV args[2]={numeric(7),numeric(25)},out=call_instance_builtin(0x51ebd00,menu,2,args);release_value(&out);}release_value(&menu);
                if(forage_active>=0)forage_targets[forage_active].done=true;
                forage_cursor[0]=forage_cursor[1]=0; // Destroyed plants can shift instance enumeration.
                forage_count++;forage_phase=4;forage_due=now+4000;forage_scan_due=now+500;telemetry_next=0;
            }else forage_stop(WALK_BLOCKED);
        }else if(forage_phase==3&&!exit_mouse_phase&&now>=forage_scan_due&&forage_clicks<3){
            // A late camera frame can turn the first click into an adjacent
            // move. Retry only this still-visible nearby plant, at most twice.
            RV target=instance_from_id(numeric(forage_id));
            double tx=member_number(target,"x"),ty=member_number(target,"y");
            bool retry=forage_visible(target)&&forage_adjacent(px,py,tx,ty);
            release_value(&target);
            if(retry&&native_click_world(tx,ty)){forage_clicks++;forage_scan_due=now+1000;}
            else {release_value(&player);forage_stop(WALK_BLOCKED);return true;}
        }
        release_value(&player);return true;
    }
    if(shared->walk_phase){release_value(&player);return false;}
    forage_discover(now);
    if(now<forage_scan_due){release_value(&player);return false;}
    int next=forage_nearest(px,py,now);
    if(next>=0){
        forage_before=selected_fodder_value();if(!isfinite(forage_before)){release_value(&player);return false;}
        forage_active=next;forage_id=forage_targets[next].id;forage_route_x=shared->walk_x;forage_route_y=shared->walk_y;
        RV flag=numeric(0),stopped=call_script(0x19123a0,player,1,&flag);release_value(&stopped);
        // Stop even while moving, then resolve the detour on a settled idle frame.
        walk_dispatch_pending=false;forage_scan_due=now+180;
        forage_phase=5;forage_due=now+4000;telemetry_next=0;
    }
    release_value(&player);return forage_phase!=0;
}
