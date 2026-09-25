#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
typedef struct RV {double real;const char* text;} RV;
static RV numeric(double n){return (RV){n,0};}
static double number(RV v){return v.real;}
static void release_value(RV* v){}
static bool valid_object(RV v){return v.real>0;}
static const char* text_value(RV v){return v.text?v.text:"";}
static struct {int scene_ready,ui_flags,walk_state,scene_generation;} state,*shared=&state;
#define WALK_ACTIVE 1
#define VK_LBUTTON 1
#define VK_RBUTTON 2
static uint64_t tick=10000,inventory_input_at,telemetry_next;
static uint64_t GetTickCount64(void){return tick;}
static int GetAsyncKeyState(int key){return 0;}
static unsigned automation_flags=4;
static int forage_phase,stow_status,stow_count;
static double inventory_pending=-1;
static bool open=true,threat,foreign,closed,full,ambiguous;
static int moves,calls,grid_calls[16],item_calls[16];
static double owners[4];
static const char* keys[]={"o_inv_amethyst","o_inv_amber","o_inv_bandage","o_inv_bread"};
static bool inventory_menu_open(void){return open;}
static RV find_instance(const char* name){return numeric(!strcmp(name,"o_player")?1:!strcmp(name,"o_inv_back")?2:-1);}
static RV instance_from_id(RV id){return id;}
static RV object_name(RV v){return (RV){0,v.real==3?"o_inv_backpack_treasure":v.real==4?"o_container_backpack_treasure":v.real>=10&&v.real<14?keys[(int)v.real-10]:""};}
static double member_number(RV v,const char* key){
 if(!strcmp(key,"id"))return v.real;
 if(!strcmp(key,"turn_available")||!strcmp(key,"movingIsDone"))return 1;
 if(!strcmp(key,"owner"))return v.real>=10&&v.real<14?owners[(int)v.real-10]:v.real==6||v.real==7?4:NAN;
 if(!strcmp(key,"parent"))return foreign?99:3;
 if(!strcmp(key,"is_close"))return closed?1:0;
 if(!strcmp(key,"rows")||!strcmp(key,"columns"))return v.real==6?1:v.real==7?5:NAN;
 return NAN;
}
static RV get_member(RV v,const char* key){return numeric(!strcmp(key,"children")?3:!strcmp(key,"itemsContainer")?5:!strcmp(key,"guiChildrenList")?8:member_number(v,key));}
static bool inventory_owned(RV v){return valid_object(v)&&member_number(v,"owner")==9;}
static bool walk_threat(RV player){return threat;}
static RV indexed_instance(const char* key,int i){return numeric(!strcmp(key,"o_container_parent")?(i==0?4:-1):i<4?10+i:-1);}
static int safe_list_size(RV list){return 2;}
static RV list_value(RV list,int i){return numeric(i==0?7:6);}
static RV call_script(uintptr_t address,RV self,int n,RV* args){
 if(address!=0x10deed0||n!=3||args[0].real!=4||args[1].real!=self.real)exit(9);
 grid_calls[calls]=(int)args[2].real;item_calls[calls++]=(int)self.real;
 if(ambiguous)return numeric(1);
 if(full||args[2].real==6)return numeric(0);
 owners[(int)self.real-10]=4;moves++;return numeric(1);
}
#include "../Bridge/inventory_stow.h"
static void check(bool ok,const char* name){if(!ok){fprintf(stderr,"FAIL %s\n",name);exit(1);}printf("PASS %s\n",name);}
static void reset(void){
 state.scene_ready=1;state.ui_flags=1;state.walk_state=0;state.scene_generation=1;
 tick=10000;automation_flags=4;inventory_input_at=0;inventory_pending=-1;forage_phase=0;
 stow_tried_count=0;stow_due=stow_input_seen=stow_scene=0;stow_bag=-1;stow_failed=false;stow_count=0;
 open=true;threat=foreign=closed=full=ambiguous=false;calls=moves=0;for(int i=0;i<4;i++)owners[i]=9;
}
int main(void){
 reset();reconcile_stow(0);check(moves==1&&owners[0]==4&&owners[1]==9,"valuable gemstone precedes cheaper loot");
 check(calls==2&&grid_calls[0]==6&&grid_calls[1]==7,"small pocket attempted before main backpack; native rejection falls back");
 tick+=400;reconcile_stow(0);tick+=400;reconcile_stow(0);check(moves==2&&owners[2]==9&&owners[3]==9&&stow_status==5,"medical supplies and food stay in main inventory");
 reset();full=true;for(int i=0;i<8;i++){reconcile_stow(0);tick+=500;}check(calls==4&&!moves&&owners[0]==9&&owners[1]==9,"full backpack retains loot and has bounded attempts");
 reset();foreign=true;reconcile_stow(0);check(!calls&&stow_status==2,"nearby container cannot substitute for equipped backpack");
 reset();closed=true;reconcile_stow(0);check(!calls,"closed backpack is never modified");
 reset();inventory_input_at=tick;reconcile_stow(0);check(!calls,"manual interaction suspends inventory moves");tick+=1201;reconcile_stow(0);check(moves==1,"idle delay permits automatic sorting");
 reset();ambiguous=true;reconcile_stow(0);tick+=500;reconcile_stow(0);check(calls==1&&stow_failed&&!moves,"unconfirmed native success stops without moving another item");
 for(int gate=0;gate<7;gate++){reset();if(gate==0)open=false;if(gate==1)automation_flags=0;if(gate==2)threat=true;if(gate==3)state.walk_state=1;if(gate==4)inventory_pending=10;if(gate==5)state.ui_flags=2;reconcile_stow(gate==6?1:0);check(!calls,"inventory, toggle, enemy, walking, cleanup, UI and focus gates block moves");}
 const char* protected[]={"o_inv_bandage","o_inv_splint","o_inv_flask_water","o_inv_silver_cup_water","o_inv_bread","o_inv_lentils","o_inv_herbal","o_inv_contract_grimoir"};
 for(int i=0;i<8;i++)check(stow_catalog_price(protected[i])<0,"supplies and quest items absent from loot allowlist");
 puts("Inventory stow tests passed; mocked native API, no game accessed.");
}
