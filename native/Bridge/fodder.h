#pragma once
#include "fodder_catalog.h"
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
static bool fodder_return(RV item,RV inventory){
    RV name=object_name(item);bool output=!strcmp(text_value(name),"o_inv_caravan_fodder");release_value(&name);
    if(!output)return fodder_move(item,inventory,"itemsContainer");
    // Native stack-and-transfer: destination, source, defaults, owner-only,
    // quiet, simple stack mode. It fills partial stacks, places the remainder,
    // and destroys an empty source through scr_item_destroy itself.
    RV args[6]={get_member(inventory,"id"),get_member(item,"id"),numeric(1),numeric(1),numeric(0),numeric(1)};
    RV out=call_script(0x10e0ab0,item,6,args);bool ok=number(out)==1;
    release_value(&out);
    // A crafting output is not a draggable pickup: the stack routine's own
    // placement precheck may refuse an otherwise valid remainder. Resolve the
    // ID again (a complete merge destroys it), then use container placement.
    RV remainder=instance_from_id(args[1]);
    if(valid_object(remainder))ok=member_number(remainder,"owner")==number(args[0])||fodder_move(remainder,inventory,"itemsContainer");
    release_value(&remainder);release_value(&args[0]);release_value(&args[1]);return ok;
}
static int craft_fodder(bool automatic){
    RV player=find_instance("o_player");if(!valid_object(player)||!supply_safe(player)||shared->walk_state==1){release_value(&player);return 7;}
    char selection[2048];memcpy(selection,shared->fodder_selection,sizeof(selection));selection[sizeof(selection)-1]=0;
    RV inventory=find_instance("o_inventory");double inventory_id=member_number(inventory,"id");
    RV selected[256];int count=0;bool complete=false;
    for(int i=0;i<256;i++){
        RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);complete=true;break;}
        if(fodder_carried(item,inventory_id)){RV name=object_name(item);bool chosen=automatic?forage_material(text_value(name)):fodder_selected(selection,text_value(name));release_value(&name);if(chosen){selected[count++]=get_member(item,"id");}}
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
            double owner=member_number(context,"id");RV returns[512];int return_count=0;
            // Snapshot IDs before merging: native stack transfer can destroy a
            // source, shifting instance_find indexes and otherwise skipping items.
            for(int i=0;i<512;i++){RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
                double item_owner=member_number(item,"owner");bool collect=item_owner==owner;
                // Also consolidate small carried stacks left by older versions.
                if(item_owner==inventory_id){RV name=object_name(item);collect=!strcmp(text_value(name),"o_inv_caravan_fodder");release_value(&name);}
                if(collect)returns[return_count++]=get_member(item,"id");release_value(&item);}
            for(int i=0;i<return_count;i++){RV item=instance_from_id(returns[i]);if(valid_object(item)&&!fodder_return(item,inventory))result=12;release_value(&item);release_value(&returns[i]);}
        }
    }
    for(int i=0;i<count;i++)release_value(&selected[i]);release_value(&context);release_value(&inventory);release_value(&player);telemetry_next=0;return result;
}
static int craft_selected_fodder(void){return craft_fodder(false);}
