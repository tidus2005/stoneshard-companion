// Original recipe is simulated against fake inventory; no game/save access.
#include "../Bridge/protocol.h"
#include <stdbool.h>
#include <stdio.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>
typedef struct RV{double real;int item;const char* text;}RV;
typedef struct Item{bool alive;double owner,value;const char* key;}Item;
static Item items[260];static int total,checks,calls,fail_mask=-1;static bool safe=true,refuse=false;static double produced;
static uint64_t telemetry_next;static SharedState storage,*shared=&storage;
static RV numeric(double d){return (RV){d,-1,NULL};}
static RV find_instance(const char* name){return (RV){!strcmp(name,"o_inventory")?100:200,-1,NULL};}
static bool valid_object(RV r){return r.real>=0;}
static bool supply_safe(RV player){return safe;}
static void release_value(RV* r){}
static double member_number(RV r,const char* name){if(!strcmp(name,"id"))return r.real;return r.item>=0?items[r.item].value:NAN;}
static bool set_member(RV r,const char* name,RV value){if(value.real==0&&r.item==fail_mask)return false;items[r.item].value=value.real;return true;}
static RV fodder_item(int n){int seen=0;for(int i=0;i<total;i++)if(items[i].alive){if(seen++==n)return (RV){i+1,i,NULL};}return numeric(-1);}
static bool fodder_carried(RV item,double inventory){return items[item.item].owner==inventory&&items[item.item].value>0;}
static RV object_name(RV item){return (RV){0,-1,items[item.item].key};}
static const char* text_value(RV r){return r.text?r.text:"";}
static RV call_script(uintptr_t address,RV player,int count,RV* args){
    if(address!=0x1107680||count!=1||args[0].real!=1)exit(2);calls++;
    if(!refuse)for(int i=0;i<total;i++)if(items[i].alive&&items[i].owner==100&&items[i].value!=0){items[i].alive=false;produced+=items[i].value;}
    return numeric(0);
}
#include "../Bridge/fodder.h"
static void check(bool ok,const char* label){if(!ok){fprintf(stderr,"FAIL %s\n",label);exit(1);}checks++;}
static void reset(void){memset(shared,0,sizeof(*shared));total=4;calls=0;safe=true;refuse=false;produced=0;fail_mask=-1;items[0]=(Item){true,100,4,"berry"};items[1]=(Item){true,100,8,"herb"};items[2]=(Item){true,999,5,"berry"};items[3]=(Item){true,100,0,"medicine"};strcpy(shared->fodder_selection,"|berry|");}
int main(void){
    reset();check(craft_selected_fodder()==0&&calls==1&&produced==4,"native recipe converts only selected material");
    check(items[1].alive&&items[1].value==8&&items[2].alive&&items[3].alive,"unselected herbs trader inventory and ineligible medicine remain intact");
    check(craft_selected_fodder()==7&&calls==1,"repeated request with no remaining material never calls recipe");
    reset();strcpy(shared->fodder_selection,"|berry||herb|");check(craft_selected_fodder()==0&&produced==12,"multiple material selections retain original yields");
    reset();shared->fodder_selection[0]=0;check(craft_selected_fodder()==7&&calls==0&&items[1].value==8,"empty selection has no mutation");
    reset();safe=false;check(craft_selected_fodder()==7&&calls==0,"threat and unsafe action state prevent crafting");
    reset();shared->walk_state=1;check(craft_selected_fodder()==7&&calls==0,"active route prevents crafting");
    reset();refuse=true;check(craft_selected_fodder()==7&&calls==1&&items[0].alive&&items[1].value==8,"native refusal restores excluded values and reports unconfirmed");
    reset();fail_mask=1;check(craft_selected_fodder()==7&&calls==0&&items[0].alive,"failed exclusion never invokes recipe");
    reset();total=256;for(int i=4;i<256;i++)items[i]=(Item){true,100,8,"herb"};check(craft_selected_fodder()==7&&calls==0&&items[1].value==8,"incomplete bounded inventory enumeration refuses all mutations");
    check(!fodder_selected("|berry_juice|","berry")&&!fodder_selected("|berry|","")&&fodder_selected("|berry||herb|","herb"),"selection uses exact whole material keys");
    printf("%d fodder adapter checks passed; no game accessed.\n",checks);
}
