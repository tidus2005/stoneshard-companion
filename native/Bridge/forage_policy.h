#pragma once
#include <stdlib.h>
// Wire indexes match ForagePolicy.All; protocol v18 prevents use by old clients.
static const char* forage_keys[]={
    "o_inv_whortleberry",
    "o_inv_raspberry",
    "o_inv_blueberry",
    "o_inv_lingonberry",
    "o_inv_gooseberry",
    "o_inv_barberry",
    "o_inv_grape",
    "o_inv_pinecap",
    "o_inv_pennybun",
    "o_inv_chanterelle",
    "o_inv_morel",
    "o_inv_stool",
    "o_inv_flyagaric",
    "o_inv_lentil",
    "o_inv_lentils",
    "o_inv_rhubarb",
    "o_inv_fleawort",
    "o_inv_agrimony",
    "o_inv_bogbean",
    "o_inv_burdock",
    "o_inv_mindwort",
    "o_inv_peppermint",
    "o_inv_thyme",
    "o_inv_burnet",
    "o_inv_wormwood",
    "o_inv_lavender",
    "o_inv_hop",
    "o_inv_nettle",
    "o_inv_henbane",
    "o_inv_horsetail",
    "o_inv_poppy",
    "o_inv_hemp",
    "@edible_mushrooms",
    "o_inv_wildegg",
};
#define FORAGE_RULE_COUNT (sizeof(forage_keys)/sizeof(*forage_keys))
typedef struct ForageRule { bool enabled; unsigned keep,flags; } ForageRule;
static ForageRule forage_rules[FORAGE_RULE_COUNT];
static int forage_rule_index(const char* key){
    if(key)for(size_t i=0;i<FORAGE_RULE_COUNT;i++)if(!strcmp(key,forage_keys[i]))return (int)i;
    return -1;
}
static bool forage_configure(const char* payload){
    ForageRule next[FORAGE_RULE_COUNT]={0};bool seen[FORAGE_RULE_COUNT]={0};
    if(!payload||strncmp(payload,"F1;",3))return false;
    const char* p=payload+3;
    while(*p){
        unsigned values[3];
        for(int i=0;i<3;i++){
            if(*p<'0'||*p>'9')return false;
            unsigned value=0,digits=0;
            while(*p>='0'&&*p<='9'){if(++digits>3)return false;value=value*10+(*p++-'0');}
            if(*p++!=(i==2?';':','))return false;values[i]=value;
        }
        unsigned index=values[0];
        if(index>=FORAGE_RULE_COUNT||values[1]>999||values[2]>7||seen[index])return false;
        seen[index]=true;next[index]=(ForageRule){(values[2]&4)!=0,values[1],values[2]&3};
    }
    memcpy(forage_rules,next,sizeof(next));return true;
}
static bool forage_material(const char* key){int i=forage_rule_index(key);return i>=0&&(forage_rules[i].enabled||(i>=7&&i<=10&&forage_rules[32].enabled));}
static unsigned forage_keep(int i){
    if(i==14&&forage_rules[13].enabled&&(forage_rules[13].flags&2)&&forage_rules[13].keep>forage_rules[14].keep)return forage_rules[13].keep;
    return forage_rules[i].keep;
}

// Count whole carried units, not food charges. Never split a native stack or
// consume one that would cross the reserve. An indivisible remainder is kept.
static double forage_quantity(RV item){
    if(member_number(item,"can_stack")!=1)return 1;
    double n=member_number(item,"stack");
    return isfinite(n)&&n>=1&&n<=10000&&floor(n)==n?n:NAN;
}
static double forage_totals[FORAGE_RULE_COUNT],forage_rotten_totals[7];
static bool forage_inventory_complete;
static bool forage_inventory(void){
    memset(forage_rotten_totals,0,sizeof(forage_rotten_totals));
    memset(forage_totals,0,sizeof(forage_totals));forage_inventory_complete=false;
    RV inv=find_instance("o_inventory");double owner=member_number(inv,"id");
    bool valid=valid_object(inv)&&isfinite(owner);release_value(&inv);if(!valid)return false;
    for(int i=0;i<512;i++){
        RV item=indexed_instance("o_inv_slot",i);
        if(!valid_object(item)){release_value(&item);forage_inventory_complete=true;return true;}
        if(member_number(item,"owner")==owner){
            RV name=object_name(item);int index=forage_rule_index(text_value(name));
            if(index<0)for(int berry=0;berry<7;berry++){
                char rotten[128];snprintf(rotten,sizeof(rotten),"%s_rot",forage_keys[berry]);
                if(!strcmp(text_value(name),rotten)){double n=forage_quantity(item);if(!isfinite(n)){release_value(&name);release_value(&item);return false;}forage_rotten_totals[berry]+=n;break;}
            }
            release_value(&name);
            if(index>=0){double n=forage_quantity(item);if(!isfinite(n)){release_value(&item);return false;}forage_totals[index]+=n;}
        }
        release_value(&item);
    }
    return false;
}
static bool forage_wanted(int i){
    if(i>=7&&i<=10&&forage_rules[32].enabled)return forage_inventory_complete&&forage_totals[7]+forage_totals[8]+forage_totals[9]+forage_totals[10]<forage_rules[32].keep;
    if(i==13&&forage_rules[13].enabled&&(forage_rules[13].flags&2))return forage_inventory_complete&&(forage_totals[14]<forage_keep(14)||(forage_rules[14].enabled&&(forage_rules[14].flags&1)));
    return i>=0&&i<(int)FORAGE_RULE_COUNT&&i!=32&&forage_inventory_complete&&forage_rules[i].enabled&&
        (forage_totals[i]<forage_rules[i].keep||
         ((forage_rules[i].flags&2)?(forage_totals[14]<forage_rules[14].keep||(forage_rules[14].enabled&&(forage_rules[14].flags&1))):(forage_rules[i].flags&1)));
}
