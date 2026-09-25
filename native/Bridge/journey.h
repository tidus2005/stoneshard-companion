#pragma once
static double journey_attribute(RV item,const char* key){
    RV map=get_member(item,"attributes_data");double n=NAN;
    if(isfinite(number(map))&&number(map)>=0){RV args[2]={map,string_value(key)},exists=call_builtin(0x51e3f20,2,args);if(number(exists)==1){RV value=call_builtin(0x51e4510,2,args);n=number(value);release_value(&value);}release_value(&exists);}
    release_value(&map);return n;
}
static void append_journey(RV player){
    RV time=call_script(0x199d2e0,player,0,NULL);tj(",\"journey\":{\"minutes\":");tn(number(time));release_value(&time);
    tj(",\"provisions\":[");bool first=true,complete=false;
    for(int i=0;i<256;i++){
        if(telemetry_at>sizeof(telemetry_buffer)-5500)break;
        RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);complete=true;break;}
        RV category=get_member(item,"Category");bool food=!strcmp(text_value(category),"food");release_value(&category);
        if(food&&is_carried(item)){
            double uses=member_number(item,"charge"),hunger=journey_attribute(item,"Hunger"),thirst=journey_attribute(item,"Thirsty");
            if(uses>0&&uses<=10000){RV label=get_member(item,"name");if(!first)tj(",");first=false;tj("{\"name\":");tq(text_value(label));release_value(&label);
                tj(",\"uses\":");tn(floor(uses));tj(",\"hunger\":");tn(hunger);tj(",\"thirst\":");tn(thirst);tj(",\"freshHours\":");double fresh=data_number(item,"Fresh");tn(isfinite(fresh)?fmax(0,fresh):NAN);tj(",\"water\":false}");}
        }release_value(&item);
    }
    bool water_complete=false;
    for(int i=0;i<128;i++){
        if(telemetry_at>sizeof(telemetry_buffer)-4500)break;
        RV item=indexed_instance("o_inv_bottle_water",i);if(!valid_object(item)){release_value(&item);water_complete=true;break;}
        double uses=member_number(item,"charge");
        if(uses>0&&uses<=10000&&is_carried(item)){
            RV label=get_member(item,"name");if(!first)tj(",");first=false;tj("{\"name\":");tq(text_value(label));release_value(&label);
            tj(",\"uses\":");tn(floor(uses));tj(",\"hunger\":null,\"thirst\":");tn(journey_attribute(item,"Thirsty"));tj(",\"freshHours\":null,\"water\":true}");
        }release_value(&item);
    }
    tj("],\"equipment\":[");first=true;int emitted=0;
    for(int i=0;i<512&&emitted<32;i++){
        if(telemetry_at>sizeof(telemetry_buffer)-500)break;
        RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
        if(member_number(item,"equipped")==1&&member_number(item,"durability_change")==1){
            double current=data_number(item,"Duration"),maximum=data_number(item,"MaxDuration");
            if(isfinite(current)&&isfinite(maximum)&&maximum>0){RV id=get_member(item,"id"),label=call_script(0x132ba10,item,1,&id);release_value(&id);if(!first)tj(",");first=false;emitted++;
                tj("{\"name\":");tq(text_value(label));release_value(&label);tj(",\"current\":");tn(current);tj(",\"maximum\":");tn(maximum);tj("}");}
        }release_value(&item);
    }
    tj("],\"complete\":");tj(complete&&water_complete?"true":"false");tj("}");
}
