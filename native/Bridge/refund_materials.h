#pragma once
#include "refund_material_policy.h"
static const char* br_ledger_key="companionGemRefundV1";
static int br_contracts,br_credit,br_ledger,br_gems[BR_GEM_COUNT];
static bool br_safe_place;
static char br_place[80];
static double br_gem_ids[BR_GEM_COUNT][128];
static bool br_map_valid(RV map){double n=number(map);if(!isfinite(n)||n<0||floor(n)!=n)return false;RV a[2]={map,numeric(1)},v=call_builtin(0x51dfef0,2,a);bool ok=number(v)==1;release_value(&v);return ok;}
static RV br_map_get(RV map,const char* key){if(!br_map_valid(map))return (RV){.kind=5};RV a[2]={map,string_value(key)};return call_builtin(0x51e4510,2,a);}
static bool br_map_number_set(RV map,const char* key,double n){if(!br_map_valid(map))return false;RV a[3]={map,string_value(key),numeric(n)},v=call_builtin(0x51e55a0,3,a);release_value(&v);v=br_map_get(map,key);bool ok=number(v)==n;release_value(&v);return ok;}
static bool br_progress(bool initialize){
    RV stats=get_global("characterStatsDataMap"),count=br_map_get(stats,"contractsCompleted");double n=number(count);release_value(&count);release_value(&stats);
    if(!isfinite(n)||n<0||n>BR_CONTRACT_LIMIT||floor(n)!=n)return false;br_contracts=(int)n;
    RV map=get_global("characterDataMap"),raw=br_map_get(map,br_ledger_key);bool missing=(raw.kind&0xffffff)==5;double old=missing?0:number(raw);release_value(&raw);
    int next=0;bool ok=isfinite(old)&&old>=0&&old<=1100000006&&floor(old)==old&&br_ledger_sync((int)old,br_contracts,&next,&br_credit);
    if(ok&&initialize&&next!=(int)old)ok=br_map_number_set(map,br_ledger_key,next);
    br_ledger=next;release_value(&map);return ok;
}
static void br_progress_tick(void){static uint64_t due;if(!shared->scene_ready||GetTickCount64()<due)return;due=GetTickCount64()+1000;br_progress(true);}
static bool br_materials_capture(void){
    memset(br_gems,0,sizeof(br_gems));
    for(int g=0;g<BR_GEM_COUNT;g++)for(int i=0;i<128;i++){
        RV item=indexed_instance(br_gem_keys[g],i);if(!valid_object(item)){release_value(&item);break;}
        if(i==127){release_value(&item);return false;}
        RV name=object_name(item);bool exact=!strcmp(text_value(name),br_gem_keys[g]);release_value(&name);
        if(exact&&inventory_owned(item)&&member_number(item,"equipped")!=1&&member_number(item,"is_quest")!=1&&member_number(item,"quest_item")!=1){
            // Native gems occupy one slot each. Reject unexpected stacks instead
            // of deleting more than the displayed recipe.
            double stack=member_number(item,"stack");if(isfinite(stack)&&stack!=-4&&stack!=0&&stack!=1){release_value(&item);return false;}
            double id=member_number(item,"id");br_gem_ids[g][br_gems[g]++]=id;br_hash_value((uint64_t)id);
        }release_value(&item);
    }
    for(int g=0;g<BR_GEM_COUNT;g++)br_hash_value(br_gems[g]);return true;
}
static bool br_location(void){
    // Use the game's save-title resolver, including camp/interior overrides.
    // locationTitleKey is a character-map key, not a global variable. Refresh it
    // from the current scene so a town save cannot authorize a refund in a dungeon.
    RV player=find_instance("o_player"),out=call_script(0x1ab6770,player,0,NULL);
    release_value(&out);release_value(&player);
    RV map=get_global("characterDataMap"),title=br_map_get(map,"locationTitleKey");
    snprintf(br_place,sizeof(br_place),"%s",text_value(title));release_value(&title);release_value(&map);
    const char* allowed[]={"Osbrook","Mannshire","Brynn","Brynn_NW","Brynn_NE","Brynn_SW","Brynn_SE","RottenWillowInn","CaravanCamp"};
    for(int i=0;i<9;i++)if(!strcmp(br_place,allowed[i]))return true;return false;
}
static bool br_materials_consume(int recipe){
    for(int g=0;g<BR_GEM_COUNT;g++)for(int i=0;i<br_recipes[recipe][g];i++){
        RV id=numeric(br_gem_ids[g][i]),item=instance_from_id(id);
        if(!valid_object(item)||!inventory_owned(item)){release_value(&item);return false;}
        RV args[2]={id,numeric(1)},out=call_script(0x1b4cea0,item,2,args);release_value(&out);release_value(&item);
        item=instance_from_id(id);bool removed=!valid_object(item);release_value(&item);if(!removed)return false;
    }return true;
}
