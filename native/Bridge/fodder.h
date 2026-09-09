#pragma once
static bool fodder_selected(const char* selection,const char* key){
    if(!key||!*key||strlen(key)>120)return false;char token[128];snprintf(token,sizeof(token),"|%s|",key);return strstr(selection,token)!=NULL;
}
static bool fodder_move(RV item,RV destination,const char* container_name){
    RV container=get_member(destination,container_name),object=instance_from_id(container);
    if(!valid_object(object)){release_value(&object);release_value(&container);return false;}release_value(&object);
    // itemsContainer is a GUI wrapper, not a cell grid (it has no owner/index).
    // Omitting the third argument lets scr_inventory_get_containers resolve the
    // actual child grids, including their native placement filters.
    RV args[2]={get_member(destination,"id"),get_member(item,"id")};
    RV result=call_script(0x10deed0,item,2,args);bool ok=number(result)==1;
    release_value(&result);release_value(&args[0]);release_value(&args[1]);release_value(&container);return ok;
}
static int craft_selected_fodder(void){
    RV player=find_instance("o_player");if(!valid_object(player)||!supply_safe(player)||shared->walk_state==1){release_value(&player);return 7;}
    char selection[2048];memcpy(selection,shared->fodder_selection,sizeof(selection));selection[sizeof(selection)-1]=0;
    RV inventory=find_instance("o_inventory");double inventory_id=member_number(inventory,"id");
    RV selected[256];int count=0;bool complete=false;
    for(int i=0;i<256;i++){
        RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);complete=true;break;}
        if(fodder_carried(item,inventory_id)){RV name=object_name(item);bool chosen=fodder_selected(selection,text_value(name));release_value(&name);if(chosen){selected[count++]=get_member(item,"id");}}
        release_value(&item);
    }
    int result=7;RV context=find_instance("o_craftingConsumsMenu");
    // Never mix user-staged ingredients with this request. The original recipe
    // scans the crafting menu's owner ID, not the player's inventory ID.
    if(complete&&count&&!valid_object(context)){
        release_value(&context);RV opened=call_script(0xb0ca80,player,0,NULL);release_value(&opened);context=find_instance("o_craftingConsumsMenu");
        if(valid_object(context)&&has_member(context,"itemsContainer")&&has_member(context,"consumsContainer")){
            int moved=0;
            for(int i=0;i<count;i++){RV item=instance_from_id(selected[i]);bool ok=valid_object(item)&&fodder_move(item,context,"itemsContainer");release_value(&item);if(!ok)break;moved++;}
            if(moved){RV yes=numeric(1),crafted=call_script(0x1107680,context,1,&yes);release_value(&crafted);
                int remaining=0;for(int i=0;i<moved;i++){RV item=instance_from_id(selected[i]);if(valid_object(item))remaining++;release_value(&item);}result=remaining==0&&moved==count?0:7;}
            // Return both recipe output and any unconsumed staged ingredients
            // with the game's placement routine. On full bags the original
            // crafting panel remains open so nothing is silently discarded.
            double owner=member_number(context,"id");
            for(int i=0;i<512;i++){RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
                if(member_number(item,"owner")==owner&&!fodder_move(item,inventory,"itemsContainer"))result=12;release_value(&item);}
        }
    }
    for(int i=0;i<count;i++)release_value(&selected[i]);release_value(&context);release_value(&inventory);release_value(&player);telemetry_next=0;return result;
}
