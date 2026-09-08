#pragma once
// Static analysis baseline: 0.9.4.25, full file hashes checked by the host.
// Water Other_24 decrements charge and calls scr_next_turn (0x16c1e60).
// Torches Other_24 toggles data["is_fire"] through scr_torch_set_lit and ends a turn.
// Dispatch via event_perform preserves inherited-event context, unlike calling an
// inherited object's compiled event directly from a timer.
static uint64_t next_supply_action;
static double data_number(RV item,const char* key){
    RV map=get_member(item,"data");double result=NAN;
    if(isfinite(number(map))&&number(map)>=0){
        RV args[2]={map,string_value(key)};
        if(number(call_builtin(0x51e3f20,2,args))==1){RV v=call_builtin(0x51e4510,2,args);result=number(v);release_value(&v);}
    }
    release_value(&map);return result;
}
static RV indexed_instance(const char* name,int index){
    RV s=string_value(name),asset=call_builtin(0x5336680,1,&s);
    if(!isfinite(number(asset))||number(asset)<0)return (RV){.kind=5};
    RV args[2]={asset,numeric(index)};return instance_from_id(call_builtin(0x51f2980,2,args));
}
static bool is_carried(RV item){
    // Inventory objects may exist for a closed chest or trader. Never count those
    // as supplies: trace ownership to the player's inventory or equipped slots.
    if(member_number(item,"equipped")==1)return true;
    RV inv=find_instance("o_inventory"),owner=get_member(item,"owner");
    double inventory_id=member_number(inv,"id");release_value(&inv);
    for(int depth=0;depth<8;depth++){
        if(isfinite(inventory_id)&&number(owner)==inventory_id){release_value(&owner);return true;}
        RV object=valid_object(owner)?owner:instance_from_id(owner);
        if(!valid_object(owner))release_value(&owner);
        if(!valid_object(object))return false;
        if((isfinite(inventory_id)&&member_number(object,"id")==inventory_id)||member_number(object,"equipped")==1){release_value(&object);return true;}
        RV next=get_member(object,"owner");
        if(!isfinite(number(next))&&!valid_object(next)){release_value(&next);next=get_member(object,"parent");}
        release_value(&object);owner=next;
    }
    release_value(&owner);return false;
}
static bool native_can_use(RV item,bool torch){
    if(!valid_object(item)||!has_member(item,"data")||!has_member(item,"charge")||!has_member(item,"owner")||!has_member(item,"is_open"))return false;
    RV id=get_member(item,"id"),result=call_script(0x697ca0,item,1,&id);
    bool ok=number(result)==1;release_value(&result);release_value(&id);
    if(ok&&torch){result=call_script(0x69ad80,item,0,NULL);ok=number(result)==1;release_value(&result);}
    return ok;
}
static bool supply_can_act(RV player){
    if(!shared->scene_ready||shared->ui_flags||GetTickCount64()<next_supply_action)return false;
    RV state=get_member(player,"state");bool idle=!strcmp(text_value(state),"idle");release_value(&state);
    return idle&&member_number(player,"turn_available")==1&&member_number(player,"movingIsDone")==1&&member_number(player,"is_sleeping")==0;
}
static bool supply_safe(RV player){
    // Unknown threat fields fail closed. Manual actions still use native limits.
    return supply_can_act(player)&&member_number(player,"is_see_enemy")==0&&member_number(player,"is_damage_taken")==0&&member_number(player,"is_take_injury")==0;
}
static RV pick_water(int* total,bool usable_only){
    RV best={.kind=5};*total=0;double least=INFINITY;
    for(int i=0;i<256;i++){
        RV item=indexed_instance("o_inv_bottle_water",i);if(!valid_object(item))break;
        double charge=member_number(item,"charge");
        if(isfinite(charge)&&charge>0&&charge<=10000&&is_carried(item)){
            *total+=(int)floor(charge);
            if(charge<least&&(!usable_only||native_can_use(item,false))){release_value(&best);best=item;least=charge;continue;}
        }
        release_value(&item);
    }
    return best;
}
static RV pick_torch(int* count,int* lit,double* duration){
    RV best={.kind=5};int best_score=-1;*count=0;*lit=0;*duration=-1;
    for(int i=0;i<256;i++){
        RV item=indexed_instance("o_inv_torches",i);if(!valid_object(item))break;
        // The torch Create event sets charge=-1 (not a drink/stack charge).
        // Remaining burn time is data["Duration"], also read by scr_can_use_torch.
        double remaining=data_number(item,"Duration"),fire=data_number(item,"is_fire");
        if(isfinite(remaining)&&remaining>0&&is_carried(item)&&(fire==0||fire==1)){
            bool equipped=member_number(item,"equipped")==1;
            // Equipped and attached torches take precedence over bag spares.
            bool attached=data_number(item,"isAttached")==1;
            // A lit but unequipped object is not an active light or a replacement.
            // Avoid equipping it and accidentally extinguishing it with a toggle.
            if(fire==1&&!equipped&&!attached){release_value(&item);continue;}
            (*count)++;
            int score=(equipped||attached?4:0)+(fire==1?2:0);
            if((equipped||attached)&&fire==1)*lit=1;
            if(score>best_score){release_value(&best);best=item;best_score=score;*duration=remaining;continue;}
        }
        release_value(&item);
    }
    return best;
}
static void refresh_supplies(bool play,RV player){
    shared->supply_flags=0;shared->water_uses=0;shared->torch_count=0;shared->torch_state=-1;shared->torch_duration=-1;
    if(!play)return;
    RV water=pick_water(&shared->water_uses,false);if(valid_object(water))shared->capabilities|=16;release_value(&water);
    int lit=0;RV torch=pick_torch(&shared->torch_count,&lit,&shared->torch_duration);
    shared->torch_state=lit;
    if(valid_object(torch)){shared->capabilities|=32;shared->supply_flags|=4;}release_value(&torch);
    if(supply_can_act(player))shared->supply_flags|=1;
    if(supply_safe(player))shared->supply_flags|=2;
}
static void use_native_event(RV item){
    RV args[2]={numeric(7),numeric(24)},out=call_instance_builtin(0x51ebd00,item,2,args);release_value(&out);
}
static int drink_water(bool automatic){
    RV player=find_instance("o_player");bool allowed=automatic?supply_safe(player):supply_can_act(player);release_value(&player);
    if(!allowed)return 7;
    int count=0;RV item=pick_water(&count,true);if(!valid_object(item))return count>0?7:20;
    if(!native_can_use(item,false)){release_value(&item);return 7;}
    RV id=get_member(item,"id");double before=member_number(item,"charge");
    next_supply_action=GetTickCount64()+1500;use_native_event(item);release_value(&item);
    RV after=instance_from_id(id);release_value(&id);
    bool changed=!valid_object(after)||member_number(after,"charge")<before;release_value(&after);
    return changed?0:22;
}
static int toggle_torch(int desired,bool automatic){
    RV player=find_instance("o_player");bool allowed=automatic?supply_safe(player):supply_can_act(player);release_value(&player);
    if(!allowed)return 7;
    int count=0,lit=0;double duration;RV item=pick_torch(&count,&lit,&duration);
    if(!valid_object(item))return 21;
    if(desired==0)desired=lit?2:1;
    if((desired==1&&lit==1)||(desired==2&&lit==0)){release_value(&item);return 0;}
    // Equip through the native helper before lighting, preserving hand restrictions.
    if(!native_can_use(item,false)){release_value(&item);return 7;}
    RV id=get_member(item,"id");
    if(member_number(item,"equipped")!=1&&data_number(item,"isAttached")!=1){
        RV equipped=call_script(0x71f0d0,item,1,&id);release_value(&equipped);
    }
    if(!native_can_use(item,true)){release_value(&id);release_value(&item);return 7;}
    double before=data_number(item,"is_fire");
    next_supply_action=GetTickCount64()+1500;use_native_event(item);release_value(&item);
    RV after=instance_from_id(id);release_value(&id);double after_fire=data_number(after,"is_fire");release_value(&after);
    return isfinite(after_fire)&&after_fire!=before&&after_fire==(desired==1?1:0)?0:22;
}
