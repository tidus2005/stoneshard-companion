#pragma once
static bool fodder_selected(const char* selection,const char* key){
    if(!key||!*key||strlen(key)>120)return false;char token[128];snprintf(token,sizeof(token),"|%s|",key);return strstr(selection,token)!=NULL;
}
static int craft_selected_fodder(void){
    RV player=find_instance("o_player");if(!valid_object(player)||!supply_safe(player)||shared->walk_state==1){release_value(&player);return 7;}
    char selection[2048];memcpy(selection,shared->fodder_selection,sizeof(selection));selection[sizeof(selection)-1]=0;
    RV inventory_object=find_instance("o_inventory");double inventory=member_number(inventory_object,"id");release_value(&inventory_object);
    RV masked[256];double original[256];int masked_count=0,selected_count=0;bool complete=false;
    // Native forage scans all direct inventory consumables. Temporarily mask only
    // unselected candidates, call the original recipe once, restore those values.
    // No item quantities, IDs, recipe yield or turn count are synthesized.
    for(int i=0;i<256;i++){
        RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);complete=true;break;}
        if(fodder_carried(item,inventory)){
            RV key=object_name(item);bool selected=fodder_selected(selection,text_value(key));release_value(&key);
            if(selected)selected_count++;else{masked[masked_count]=item;original[masked_count++]=member_number(item,"fodder_value");continue;}
        }release_value(&item);
    }
    int result=7,applied=0;
    if(complete&&selected_count){
        bool ok=true;for(int i=0;i<masked_count;i++){if(!set_member(masked[i],"fodder_value",numeric(0))){ok=false;break;}applied++;}
        if(ok){RV yes=numeric(1),out=call_script(0x1107680,player,1,&yes);release_value(&out);
            int remaining=0;for(int i=0;i<256;i++){RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);break;}if(fodder_carried(item,inventory)){RV key=object_name(item);if(fodder_selected(selection,text_value(key)))remaining++;release_value(&key);}release_value(&item);}
            result=remaining<selected_count?0:7;}
    }
    for(int i=0;i<masked_count;i++){if(i<applied&&(!set_member(masked[i],"fodder_value",numeric(original[i]))||member_number(masked[i],"fodder_value")!=original[i]))result=11;release_value(&masked[i]);}
    release_value(&player);telemetry_next=0;return result;
}
