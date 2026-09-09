// Simulates original containers and recipe; never accesses a game or save.
#include "../Bridge/protocol.h"
#include <stdbool.h>
#include <stdio.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>
typedef struct RV{double real;int item;const char* text;}RV;
typedef struct Item{bool alive;double owner,value;const char* key;}Item;
static Item items[520];static int total,checks,calls,opens;static bool safe,refuse,context,containers,full,move_fail;static double produced;
static uint64_t telemetry_next;static SharedState storage,*shared=&storage;
static RV numeric(double d){return (RV){d,-1,NULL};}
static double number(RV r){return r.real;}
static bool valid_object(RV r){return r.real>=0;}
static RV find_instance(const char* name){return numeric(!strcmp(name,"o_inventory")?1000:!strcmp(name,"o_player")?2000:context?3000:-1);}
static bool supply_safe(RV player){return safe;}
static void release_value(RV* r){}
static double member_number(RV r,const char* name){if(!strcmp(name,"id"))return r.real;return r.item>=0?(!strcmp(name,"owner")?items[r.item].owner:items[r.item].value):NAN;}
static RV get_member(RV r,const char* name){if(!strcmp(name,"id"))return numeric(r.real);return numeric(containers?(r.real==1000?4000:5000):-1);}
static bool has_member(RV r,const char* name){return containers;}
static RV instance_from_id(RV r){if(r.real>=1000)return r;int i=(int)r.real-1;return i>=0&&i<total&&items[i].alive?(RV){i+1,i,NULL}:numeric(-1);}
static RV fodder_item(int n){int seen=0;for(int i=0;i<total;i++)if(items[i].alive){if(seen++==n)return (RV){i+1,i,NULL};}return numeric(-1);}
static RV indexed_instance(const char* name,int n){return fodder_item(n);}
static bool fodder_carried(RV item,double inventory){return items[item.item].owner==inventory&&items[item.item].value>0;}
static RV object_name(RV item){return (RV){0,-1,items[item.item].key};}
static const char* text_value(RV r){return r.text?r.text:"";}
static RV call_builtin(uintptr_t address,int count,RV* args){if(address!=0x51d0270||count!=2||args[0].real!=1)exit(2);return args[1];}
static RV call_script(uintptr_t address,RV self,int count,RV* args){
    if(address==0xb0ca80){if(count||self.real!=2000||context)exit(3);context=true;opens++;return numeric(3000);}
    if(address==0x10deed0){if(count!=2||self.real!=args[1].real)exit(4);if((args[0].real==1000&&full)||(args[0].real==3000&&move_fail))return numeric(0);items[self.item].owner=args[0].real;return numeric(1);}
    if(address!=0x1107680||self.real!=3000||!context||!containers||count!=1||args[0].real!=1)exit(5);calls++;
    if(!refuse){double value=0;for(int i=0;i<total;i++)if(items[i].alive&&items[i].owner==3000&&items[i].value>0){items[i].alive=false;value+=items[i].value;}produced+=value;if(value>0)items[total++]=(Item){true,3000,0,"fodder"};}
    return numeric(0);
}
#include "../Bridge/fodder.h"
static void check(bool ok,const char* label){if(!ok){fprintf(stderr,"FAIL %s\n",label);exit(1);}checks++;}
static void reset(void){memset(shared,0,sizeof(*shared));memset(items,0,sizeof(items));total=4;calls=opens=0;safe=containers=true;context=refuse=full=move_fail=false;produced=0;items[0]=(Item){true,1000,4,"berry"};items[1]=(Item){true,1000,8,"herb"};items[2]=(Item){true,9999,5,"berry"};items[3]=(Item){true,1000,0,"medicine"};strcpy(shared->fodder_selection,"|berry|");}
int main(void){
 reset();check(craft_selected_fodder()==0&&calls==1&&opens==1&&produced==4,"selected material goes through original crafting container and recipe");
 check(items[1].alive&&items[1].owner==1000&&items[1].value==8&&items[2].alive&&items[3].alive,"unselected herbs other owners and medicine are untouched");
 check(items[4].alive&&items[4].owner==1000,"output returned through original inventory placement");
 reset();strcpy(shared->fodder_selection,"|berry||herb|");check(craft_selected_fodder()==0&&produced==12,"multiple selected ingredients preserve native batch yield");
 reset();shared->fodder_selection[0]=0;check(craft_selected_fodder()==7&&!opens&&!calls,"empty selection does not even open crafting UI");
 reset();safe=false;check(craft_selected_fodder()==7&&!opens,"unsafe state prevents all actions");
 reset();shared->walk_state=1;check(craft_selected_fodder()==7&&!opens,"walking prevents all actions");
 reset();context=true;check(craft_selected_fodder()==7&&!calls&&items[0].owner==1000,"existing user crafting context is never mixed with selected ingredients");
 reset();containers=false;check(craft_selected_fodder()==7&&!calls&&items[0].owner==1000,"incomplete native menu cannot call recipe");
 reset();move_fail=true;check(craft_selected_fodder()==7&&!calls&&items[0].owner==1000,"native placement refusal cannot consume ingredient");
 reset();refuse=true;check(craft_selected_fodder()==7&&calls==1&&items[0].alive&&items[0].owner==1000,"native recipe refusal returns staged ingredient");
 reset();full=true;check(craft_selected_fodder()==12&&items[4].alive&&items[4].owner==3000,"full bag preserves produced fodder in original crafting panel");
 reset();total=256;for(int i=4;i<256;i++)items[i]=(Item){true,1000,8,"herb"};check(craft_selected_fodder()==7&&!opens&&!calls,"bounded incomplete enumeration cannot craft");
 check(!fodder_selected("|berry_juice|","berry")&&!fodder_selected("|berry|","")&&fodder_selected("|berry||herb|","herb"),"selection uses exact material keys");
 printf("%d fodder adapter checks passed; no game accessed.\n",checks);
}
