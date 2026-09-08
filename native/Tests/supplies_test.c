// Standalone console test: real selection/action code, fake GameMaker objects.
// No game process, window, input, DLL injection or real save files are accessed.
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
typedef struct RV {union {double real;void* ptr;int64_t integer;};int flags,kind;} RV;
typedef struct Item {int id,type;double charge,duration,fire,equipped,attached;RV owner;bool allowed,torch_allowed,alive; } Item;
static Item items[32];static int item_count,events,equips,passed;static uint64_t now=10000;
static bool can_equip=true,confirm_event=true,enemy=false,player_idle=true;
static struct {int scene_ready,ui_flags,water_uses,torch_count,torch_state;uint32_t capabilities,supply_flags;double torch_duration;} state,*shared=&state;
static RV numeric(double n){return (RV){.real=n};}
static RV invalid(void){return (RV){.kind=5};}
static RV object(Item* i){return i&&i->alive?(RV){.ptr=i,.kind=6}:invalid();}
static double number(RV r){return r.kind==0?r.real:NAN;}
static bool valid_object(RV r){return r.kind==6&&r.ptr;}
static RV string_value(const char* s){return (RV){.ptr=(void*)s,.kind=1};}
static const char* text_value(RV r){return r.kind==1?r.ptr:"";}
static void release_value(RV* r){*r=invalid();}
static uint64_t GetTickCount64(void){return now;}
static RV instance_from_id(RV id){for(int n=0;n<item_count;n++)if(items[n].id==number(id))return object(items+n);return invalid();}
static RV find_instance(const char* name){return !strcmp(name,"o_inventory")?object(items):!strcmp(name,"o_player")?object(items+1):invalid();}
static RV get_member(RV r,const char* key){
    if(!valid_object(r))return invalid();Item* i=r.ptr;
    if(!strcmp(key,"id"))return numeric(i->id);
    if(!strcmp(key,"owner"))return i->owner;
    if(!strcmp(key,"data"))return numeric(i->id);
    if(!strcmp(key,"charge"))return numeric(i->charge);
    if(!strcmp(key,"equipped"))return numeric(i->equipped);
    if(!strcmp(key,"is_open"))return numeric(0);
    if(i==items+1){
        if(!strcmp(key,"state"))return string_value(player_idle?"idle":"move");
        if(!strcmp(key,"turn_available")||!strcmp(key,"movingIsDone"))return numeric(1);
        if(!strcmp(key,"is_see_enemy"))return numeric(enemy?1:0);
        if(!strcmp(key,"is_moving"))return numeric(player_idle?0:1);
        if(!strcmp(key,"is_sleeping")||!strcmp(key,"is_damage_taken")||!strcmp(key,"is_take_injury"))return numeric(0);
    }
    return invalid();
}
static double member_number(RV r,const char* k){return number(get_member(r,k));}
static bool has_member(RV r,const char* k){return get_member(r,k).kind!=5;}
static RV call_builtin(uintptr_t address,int count,RV* a){
    if(address==0x5336680)return numeric(!strcmp(text_value(a[0]),"o_inv_bottle_water")?10:20);
    if(address==0x51f2980){int index=(int)number(a[1]);for(int n=0;n<item_count;n++)if(items[n].alive&&items[n].type==number(a[0])&&index--==0)return numeric(items[n].id);return numeric(-4);}
    if(address==0x51e3f20)return numeric(valid_object(instance_from_id(a[0]))?1:0);
    if(address==0x51e4510){RV r=instance_from_id(a[0]);if(!valid_object(r))return invalid();Item* i=r.ptr;return numeric(!strcmp(text_value(a[1]),"is_fire")?i->fire:!strcmp(text_value(a[1]),"Duration")?i->duration:i->attached);}
    fprintf(stderr,"Unknown builtin %llx\n",(unsigned long long)address);exit(1);
}
static RV call_script(uintptr_t address,RV self,int count,RV* values){
    Item* i=self.ptr;
    if(address==0x697ca0){if(count!=1||number(values[0])!=i->id)exit(2);return numeric(i->allowed);}
    if(address==0x69ad80)return numeric(i->torch_allowed&&(i->equipped==1||i->attached==1));
    if(address==0x71f0d0){equips++;if(can_equip)i->equipped=1;return numeric(can_equip);}
    exit(3);
}
static RV call_instance_builtin(uintptr_t address,RV self,int count,RV* values){
    if(address!=0x51ebd00||count!=2||number(values[0])!=7||number(values[1])!=24)exit(4);
    events++;Item* i=self.ptr;if(confirm_event){if(i->type==10)i->charge--;else i->fire=1-i->fire;}return invalid();
}
#include "../Bridge/supplies.h"
static void check(bool ok,const char* name){if(!ok){fprintf(stderr,"FAIL %s\n",name);exit(5);}printf("PASS %s\n",name);passed++;}
static void reset(void){
    memset(items,0,sizeof(items));memset(&state,0,sizeof(state));item_count=2;events=equips=0;next_supply_action=0;now=10000;
    enemy=false;player_idle=can_equip=confirm_event=true;state.scene_ready=1;
    items[0]=(Item){.id=100,.alive=true};items[1]=(Item){.id=101,.alive=true};
}
static Item* add(int type,double charge,bool carried){
    Item* i=items+item_count;*i=(Item){.id=100+item_count,.type=type,.charge=type==20?-1:charge,.duration=charge,.owner=numeric(carried?100:999),.alive=true,.allowed=true,.torch_allowed=true};item_count++;return i;
}
int main(void){
    reset();refresh_supplies(true,object(items+1));check(state.water_uses==0&&state.torch_count==0,"empty inventory telemetry");check(drink_water(false)==20&&events==0,"no water refused without event");
    Item* chest=add(10,9,false);Item* bottle=add(10,3,true);Item* low=add(10,1,true);
    refresh_supplies(true,object(items+1));check(state.water_uses==4,"closed chest excluded");
    check(drink_water(false)==0&&low->charge==0&&bottle->charge==3&&chest->charge==9&&events==1,"least-filled carried water consumed once");
    check(drink_water(false)==7&&events==1,"native cooldown prevents repeated event");
    now+=2000;enemy=true;check(drink_water(true)==7&&events==1,"auto water refuses visible enemy");check(drink_water(false)==0&&events==2,"manual drinking uses native rules in combat");
    reset();bottle=add(10,4,true);bottle->owner=object(items);check(is_carried(object(bottle)),"object-valued inventory owner supported");
    Item* bag=add(30,1,true);bottle->owner=object(bag);check(is_carried(object(bottle)),"nested carried container supported");bag->owner=object(bottle);check(!is_carried(object(bottle)),"ownership cycle bounded");
    reset();low=add(10,1,true);low->allowed=false;bottle=add(10,4,true);check(drink_water(false)==0&&bottle->charge==3&&low->charge==1,"unusable bottle skipped for usable one");
    reset();bottle=add(10,4,true);confirm_event=false;check(drink_water(false)==22&&events==1&&bottle->charge==4,"unconfirmed drink reports failure without fake change");
    reset();add(10,4,true);state.ui_flags=1;check(drink_water(false)==7&&events==0,"inventory UI blocks consuming");state.ui_flags=0;player_idle=false;check(drink_water(false)==7&&events==0,"moving player blocks consuming");
    reset();add(10,4,true);state.ui_flags=16;check(drink_water(true)==7&&events==0,"world map blocks automatic consumption");
    reset();check(toggle_torch(0,false)==21&&events==0,"no torch refused");Item* torch=add(20,80,true);torch->equipped=1;
    check(toggle_torch(0,false)==0&&torch->fire==1&&events==1,"equipped torch lights with one native turn event");
    now+=2000;check(toggle_torch(1,true)==0&&events==1,"ensure lit is idempotent");check(toggle_torch(0,false)==0&&torch->fire==0&&events==2,"manual toggle extinguishes");
    reset();torch=add(20,80,true);check(toggle_torch(1,true)==0&&torch->equipped==1&&torch->fire==1&&equips==1&&events==1,"spare uses native equip then one light event");
    reset();torch=add(20,80,true);can_equip=false;check(toggle_torch(1,true)==7&&events==0&&torch->fire==0,"native hand restriction blocks lighting");
    reset();torch=add(20,80,true);torch->attached=1;check(toggle_torch(1,true)==0&&equips==0&&events==1,"attached torch avoids equipping");
    reset();torch=add(20,0,true);torch->equipped=1;Item* spare=add(20,50,true);check(toggle_torch(1,true)==0&&spare->fire==1&&events==1,"exhausted torch skipped for spare");
    reset();torch=add(20,50,false);check(toggle_torch(1,true)==21&&events==0,"torch in chest never equipped");
    reset();torch=add(20,50,true);torch->fire=1;check(toggle_torch(1,true)==21&&equips==0&&events==0,"lit unequipped object cannot be accidentally extinguished");
    reset();torch=add(20,50,true);torch->equipped=1;confirm_event=false;check(toggle_torch(1,true)==22&&events==1,"unconfirmed torch action reports failure");
    reset();add(20,50,true);enemy=true;check(toggle_torch(1,true)==7&&equips==0&&events==0,"automatic torch refuses combat before equipping");
    printf("%d native supply checks passed. GameMaker API is mocked; no game accessed.\n",passed);return 0;
}
