#pragma once
#include "stats_catalog.h"
// All reads occur on the game thread. Dynamic keys own their strings rather
// than retaining pointers in the bridge's static string cache.
static RV live_member(RV obj,const char* name){
    RV key={.kind=5};((void(*)(RV*,const char*))(game+0x51b2890))(&key,name);
    RV args[2]={obj,key},exists=call_builtin(0x51edd30,2,args),value={.kind=5};
    if(number(exists)==1)value=call_builtin(0x51edfa0,2,args);
    release_value(&exists);release_value(&key);return value;
}
static RV list_value(RV list,int index){RV args[2]={list,numeric(index)};return call_builtin(0x51e28f0,2,args);}
static int safe_list_size(RV list){
    if(!isfinite(number(list))||number(list)<0)return -1;
    // ds_exists dispatch case 2 uses the same list table (RVA 0x9916190)
    // as ds_list_size. Case 1 is a different structure and may share its ID.
    RV args[2]={list,numeric(2)},ok=call_builtin(0x51dfef0,2,args);bool exists=number(ok)==1;release_value(&ok);
    if(!exists)return -1;RV value=call_builtin(0x51e3260,1,&list);double n=number(value);release_value(&value);
    return isfinite(n)&&n>=0&&n<=4096?(int)n:-1;
}
static RV fodder_item(int index){RV args[2]={numeric(4770),numeric(index)},id=call_builtin(0x51f2980,2,args);RV item=instance_from_id(id);release_value(&id);return item;}
static bool fodder_carried(RV item,double inventory){return isfinite(inventory)&&member_number(item,"owner")==inventory&&member_number(item,"fodder_value")>0;}
static RV object_name(RV item){RV asset=get_member(item,"object_index");RV name=call_builtin(0x5335220,1,&asset);release_value(&asset);return name;}
static char telemetry_buffer[57344];static size_t telemetry_at;static bool telemetry_full;
static void tj(const char* value){size_t n=strlen(value);if(telemetry_at+n>=sizeof(telemetry_buffer)){telemetry_full=true;return;}memcpy(telemetry_buffer+telemetry_at,value,n+1);telemetry_at+=n;}
static void tq(const char* s){tj("\"");for(int i=0;s&&s[i]&&i<512;i++){unsigned char c=s[i];char b[8];if(c=='"'||c=='\\'){b[0]='\\';b[1]=c;b[2]=0;tj(b);}else if(c<32){snprintf(b,sizeof(b),"\\u%04x",c);tj(b);}else{b[0]=c;b[1]=0;tj(b);}}tj("\"");}
static void tn(double n){char b[48];if(!isfinite(n)){tj("null");return;}snprintf(b,sizeof(b),"%.12g",n);tj(b);}
static uint64_t telemetry_next,telemetry_scene;
static void refresh_telemetry(bool play,RV player){
    if(!play||!shared->scene_ready){shared->telemetry[0]=0;telemetry_next=0;return;}
    uint64_t now=GetTickCount64();if(now<telemetry_next&&telemetry_scene==shared->scene_generation)return;
    telemetry_scene=shared->scene_generation;telemetry_next=now+750;telemetry_at=0;telemetry_full=false;telemetry_buffer[0]=0;
    tj("{\"at\":");tn(now);tj(",\"player\":");tn(member_number(player,"id"));tj(",\"stats\":[");
    for(size_t i=0;i<sizeof(stat_keys)/sizeof(*stat_keys);i++){
        if(i)tj(",");const char* key=stat_keys[i];RV value=live_member(player,key);double current=number(value);release_value(&value);
        if(!isfinite(current))current=character_stat(key);
        char base[100];snprintf(base,sizeof(base),"b%s",key);value=live_member(player,base);double baseline=number(value);release_value(&value);
        tj("{\"key\":");tq(key);tj(",\"value\":");tn(current);tj(",\"baseline\":");tn(baseline);tj("}");
    }
    tj("],\"sources\":[");
    RV list=get_member(player,"buffer_buff_list");int count=safe_list_size(list);bool first=true,sources_truncated=count>1024;
    // scr_add_atr_source appends four values; C-panel consumes the same stride.
    // Keep source ID and label; unknown formats remain explicitly unavailable.
    for(int i=0;count%4==0&&i+3<count&&i<1024;i+=4){
        if(telemetry_at>sizeof(telemetry_buffer)-18000){sources_truncated=true;break;}
        RV key=list_value(list,i),delta=list_value(list,i+1),source=list_value(list,i+2),label=list_value(list,i+3);
        if(*text_value(key)&&isfinite(number(delta))){if(!first)tj(",");first=false;tj("{\"key\":");tq(text_value(key));tj(",\"delta\":");tn(number(delta));tj(",\"sourceId\":");tn(number(source));tj(",\"label\":");tq(text_value(label));tj("}");}
        release_value(&key);release_value(&delta);release_value(&source);release_value(&label);
    }
    release_value(&list);tj("],\"sourcesAvailable\":");tj(count>=0&&count%4==0?"true":"false");tj(",\"sourcesTruncated\":");tj(sources_truncated?"true":"false");
    tj(",\"foods\":[");RV inv=find_instance("o_inventory");double inventory=member_number(inv,"id");release_value(&inv);first=true;bool foods_complete=false;
    for(int i=0;i<256;i++){
        if(telemetry_at>sizeof(telemetry_buffer)-7000)break;
        RV item=fodder_item(i);if(!valid_object(item)){release_value(&item);foods_complete=true;break;}
        if(fodder_carried(item,inventory)){
            RV key=object_name(item),label=get_member(item,"name");if(!first)tj(",");first=false;
            tj("{\"key\":");tq(text_value(key));tj(",\"name\":");tq(text_value(label));tj(",\"value\":");tn(member_number(item,"fodder_value"));tj("}");release_value(&key);release_value(&label);
        }release_value(&item);
    }
    tj("],\"foodsComplete\":");tj(foods_complete?"true":"false");tj("}");if(telemetry_full)shared->telemetry[0]=0;else memcpy(shared->telemetry,telemetry_buffer,telemetry_at+1);
}
#include "fodder.h"
