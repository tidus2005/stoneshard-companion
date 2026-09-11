#pragma once
// Build 24780451: o_tile_transition exposes grid_x/y and outward dX/dY.
// Read actual transition instances; their playable cells need not be row 0.
static bool native_exit_target(int direction,double px,double py,double* x,double* y){
    int d=direction>=11&&direction<=14?direction-10:direction;
    if(d<1||d>4||!isfinite(px)||!isfinite(py))return false;
    RV name=string_value("o_tile_transition"),asset=call_builtin(0x5336680,1,&name);
    double index=number(asset);release_value(&asset);if(!isfinite(index)||index<0)return false;
    bool found=false;double best=INFINITY;
    for(int i=0;i<4096;i++){
        RV args[2]={numeric(index),numeric(i)},id=call_builtin(0x51f2980,2,args);
        double n=number(id);if(!isfinite(n)||n<0){release_value(&id);break;}
        RV tile=instance_from_id(id);release_value(&id);if(!valid_object(tile)){release_value(&tile);continue;}
        double gx=member_number(tile,"grid_x"),gy=member_number(tile,"grid_y");
        double dx=member_number(tile,"dX"),dy=member_number(tile,"dY");release_value(&tile);
        if(!isfinite(gx)||!isfinite(gy)||floor(gx)!=gx||floor(gy)!=gy||gx<0||gy<0||gx>=floor(shared->map_w/26)||gy>=floor(shared->map_h/26))continue;
        if(!((d==1&&dx==0&&dy==-1)||(d==2&&dx==0&&dy==1)||(d==3&&dx==-1&&dy==0)||(d==4&&dx==1&&dy==0)))continue;
        double tx=gx*26+13,ty=gy*26+13;
        double forward=d==1?py-ty:d==2?ty-py:d==3?px-tx:tx-px;
        double lateral=d<=2?fabs(tx-(floor(px/26)*26+13)):fabs(ty-(floor(py/26)*26+13));
        if(lateral>2||forward < -2||forward>52.5)continue;
        double distance=fabs(tx-px)+fabs(ty-py);
        if(distance<best){best=distance;*x=tx;*y=ty;found=true;}
    }
    return found;
}
static bool native_center_reachable(RV player,double x,double y){
    double px=member_number(player,"x"),py=member_number(player,"y");
    if(!isfinite(px)||!isfinite(py)||!isfinite(x)||!isfinite(y)||px<0||py<0||px>=shared->map_w||py>=shared->map_h)return false;
    RV unit_name=string_value("o_unit"),unit=call_builtin(0x5336680,1,&unit_name);
    double unit_index=number(unit);release_value(&unit);if(!isfinite(unit_index)||unit_index<0)return false;
    RV collision_args[5]={numeric(x),numeric(y),numeric(unit_index),numeric(0),numeric(1)};
    RV collision=call_instance_builtin(0x51f14f0,player,5,collision_args);
    double occupied=number(collision);release_value(&collision);
    if(!isfinite(occupied)||occupied!=-4)return false; // noone, excluding self
    // The native query uses newgrid.temp_path, never player.path.
    // Its default fifth argument multiplies GRID coordinates by 26, then adds 13.
    RV args[4]={numeric(floor(px/26)),numeric(floor(py/26)),numeric(floor(x/26)),numeric(floor(y/26))};
    RV result=call_script(0x17206a0,player,4,args);bool ok=number(result)==1;
    release_value(&result);return ok;
}
static void native_activate_exit(RV player){
    // The same routine called by o_player.Other_17 on native arrival. It checks
    // the player's grid against real transition tiles and lets the game load.
    RV result=call_script(0x19b1e50,player,0,NULL);release_value(&result);
}
