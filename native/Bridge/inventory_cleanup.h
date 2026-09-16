#pragma once
// Open-backpack maintenance. No synthetic keys, ground interactions or item deletion.
static uint64_t inventory_input_at,inventory_action_due;
static double inventory_pending=-1,inventory_seed_before;
static bool inventory_peeling,inventory_blocked;
static void reconcile_inventory(uint32_t reasons){
    if(!inventory_menu_open()){
        inventory_pending=-1;inventory_blocked=false;inventory_action_due=GetTickCount64()+1200;return;
    }
    uint64_t now=GetTickCount64();
    if(reasons||!(automation_flags&2)||!shared->scene_ready||(shared->ui_flags&~9u)||shared->walk_state==WALK_ACTIVE||inventory_blocked||now<inventory_action_due||now<inventory_input_at+1200)return;
    if((GetAsyncKeyState(VK_LBUTTON)&0x8000)||(GetAsyncKeyState(VK_RBUTTON)&0x8000))return;
    RV player=find_instance("o_player");bool safe=valid_object(player)&&member_number(player,"turn_available")==1&&member_number(player,"movingIsDone")==1&&!walk_threat(player);release_value(&player);if(!safe)return;
    if(inventory_pending>=0){
        RV source=instance_from_id(numeric(inventory_pending));bool remains=inventory_owned(source);release_value(&source);
        if(remains){inventory_blocked=true;return;}
        if(inventory_peeling&&(!forage_inventory()||forage_totals[14]<=inventory_seed_before)){inventory_blocked=true;return;}
        inventory_pending=-1;inventory_action_due=now+1200;telemetry_next=0;return;
    }
    RV context=find_instance("o_context_button");bool menu=valid_object(context);release_value(&context);if(menu)return;
    for(int i=0;i<512;i++){
        RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
        RV name=object_name(item);bool spoiled=inventory_owned(item)&&rotten_berry_key(text_value(name));release_value(&name);
        if(spoiled){
            inventory_pending=member_number(item,"id");inventory_peeling=false;
            bool ok=native_inventory_action(item,"Drop");release_value(&item);
            inventory_blocked=!ok;inventory_action_due=now+2000;telemetry_next=0;return;
        }
        release_value(&item);
    }
    int peeled=peel_surplus();
    if(peeled>0){inventory_pending=peel_source;inventory_seed_before=peel_before;inventory_peeling=true;inventory_action_due=now+2000;telemetry_next=0;}
    else if(peeled<0)inventory_blocked=true;
    else inventory_action_due=now+1000;
}
