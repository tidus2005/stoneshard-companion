// No process attachment or input. The original context action is simulated.
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <stdint.h>
typedef struct RV {double real;const char* text;} RV;
static bool bag_open=true,foreign,rotten;
static bool plant,existing,menu,refuse,wrong;static double seeds;static int actions,opens,checks;
static RV numeric(double n){return (RV){n,NULL};}
static RV string_value(const char* s){return (RV){0,s};}
static const char* text_value(RV r){return r.text?r.text:"";}
static void release_value(RV* r){}
static bool valid_object(RV r){return r.real>0;}
static double member_number(RV r,const char* key){
 if(!strcmp(key,"turn_available")||!strcmp(key,"movingIsDone"))return 1;
 if(!strcmp(key,"id"))return r.real;
 if(!strcmp(key,"owner"))return foreign?99:10;
 if(!strcmp(key,"inventoryMenuActive"))return bag_open;
 if(!strcmp(key,"can_stack"))return r.real==2?1:0;
 if(!strcmp(key,"stack"))return seeds;
 if(!strcmp(key,"interact_id"))return wrong?999:1;
 return NAN;
}
static RV get_member(RV r,const char* key){return !strcmp(key,"func")?string_value(wrong?"Eat":rotten?"Drop":"Flay"):numeric(member_number(r,key));}
static RV find_instance(const char* key){return numeric(!strcmp(key,"o_player")?40:!strcmp(key,"o_inventory")?10:!strcmp(key,"o_modificatorsMenu")?20:existing?30:-1);}
static RV indexed_instance(const char* key,int i){
 if(!strcmp(key,"o_context_button"))return numeric(menu&&i==0?30:-1);
 if(plant&&i--==0)return numeric(1);
 return numeric(seeds>0&&i==0?2:-1);
}
static RV object_name(RV r){return string_value(r.real==1?(rotten?"o_inv_blueberry_rot":"o_inv_lentil"):"o_inv_lentils");}
static RV call_script(uintptr_t address,RV self,int n,RV* args){
 if(address!=0x17b3800||self.real!=1||n!=1||strcmp(text_value(args[0]),"Flay"))exit(2);
 opens++;menu=true;return numeric(30);
}
static RV call_instance_builtin(uintptr_t address,RV self,int n,RV* args){
 if(address==0x51ebd00&&self.real==1&&n==2&&args[0].real==6&&args[1].real==5){opens++;menu=true;return numeric(0);}
 if(address!=0x51ebd00||self.real!=30||n!=2||args[0].real!=6||args[1].real!=4)exit(3);
 actions++;if(!refuse){if(rotten)foreign=true;else{plant=false;seeds+=2;}menu=false;}return numeric(0);
}
#include "../Bridge/forage_policy.h"
#include "../Bridge/peel.h"
static uint64_t test_tick=10000,telemetry_next;
#define GetTickCount64() test_tick
#define VK_LBUTTON 1
#define VK_RBUTTON 2
#define WALK_ACTIVE 1
static int held;
static int GetAsyncKeyState(int key){return held;}
static unsigned automation_flags=2;
static struct {unsigned scene_ready,ui_flags,walk_state;} test_shared={1,1,0},*shared=&test_shared;
static bool walk_threat(RV player){return false;}
static RV instance_from_id(RV id){return numeric(id.real==1&&!plant?-1:id.real);}
#include "../Bridge/inventory_cleanup.h"
static void check(bool ok,const char* name){if(!ok){fprintf(stderr,"FAIL %s\n",name);exit(1);}checks++;}
static void reset(void){inventory_pending=-1;inventory_blocked=false;inventory_action_due=inventory_input_at=0;rotten=foreign=false;held=0;bag_open=true;plant=true;existing=menu=refuse=wrong=false;seeds=actions=opens=0;forage_configure("F1;13,0,6;14,5,0;");}
int main(void){
 reset();bag_open=false;check(peel_surplus()==0&&!actions&&!opens&&plant,"closed backpack never invokes ground peeling");bag_open=true;
 reset();foreign=true;check(!native_peel(numeric(1))&&!actions&&!opens,"ground or chest-owned plant cannot be peeled");foreign=false;
 check(rotten_berry_key("o_inv_blueberry_rot")&&!rotten_berry_key("o_inv_blueberry")&&!rotten_berry_key("o_inv_meat_rot"),"only exact rotten berry allowlist can be discarded");
 reset();check(peel_surplus()==1&&actions==1&&opens==1&&!plant&&seeds==2,"surplus plant uses exact original Flay menu action, creates seeds and releases source");
 reset();forage_configure("F1;13,1,6;14,5,0;");check(peel_surplus()==1&&actions==1&&!plant&&forage_keep(14)==5,"reserve protects processed seeds, not unpeeled plants");
 reset();existing=true;check(peel_surplus()==-1&&!opens&&!actions&&plant,"existing user menu prevents dispatch");
 reset();wrong=true;check(peel_surplus()==-1&&!actions&&plant,"unrelated context action is never clicked");
 reset();refuse=true;check(peel_surplus()==1&&plant&&seeds==0&&actions==1,"native capacity refusal cannot fabricate seeds or remove source");
 reset();forage_configure("F1;13,0,6;14,5,0;");forage_inventory();check(forage_wanted(13),"seed deficit can request more plants despite disabled direct seed collection");
 seeds=5;forage_inventory();check(!forage_wanted(13),"seed reserve reached ends extra plant collection");
 forage_configure("F1;13,0,6;14,5,5;");forage_inventory();check(forage_wanted(13),"explicit seed surplus recipe permits continued collection");
 reset();forage_configure("F1;13,9,6;14,5,5;");check(forage_keep(14)==9,"seed fodder honors the higher peeling reserve");
 reset();forage_configure("F1;32,6,4;");forage_inventory();forage_totals[7]=2;forage_totals[8]=3;check(forage_wanted(9)&&forage_material("o_inv_morel"),"mixed edible mushrooms collect toward one shared reserve");
 forage_totals[10]=1;check(!forage_wanted(7)&&!forage_wanted(9),"shared threshold stops every edible type");
 check(!forage_wanted(11)&&!forage_material("o_inv_flyagaric"),"shared group never opts into toxic mushrooms");
 forage_configure("F1;33,2,4;");forage_inventory();check(forage_material("o_inv_wildegg")&&forage_wanted(33),"wild eggs enter harvest selection below reserve");
 forage_totals[33]=2;check(!forage_wanted(33),"wild egg reserve stops collecting nests");
 forage_configure("F1;33,0,4;");check(!forage_wanted(33),"zero egg reserve does not collect unwanted eggs");
 reset();rotten=true;forage_inventory();check(forage_totals[2]==0&&forage_rotten_totals[2]==1,"rotten pickups confirm harvest without satisfying healthy reserve");
 reset();rotten=true;reconcile_inventory(1);check(!actions,"background maintenance cannot discard");
 reconcile_inventory(0);check(actions==1&&foreign&&plant&&seeds==0,"rotten berry uses original Drop and remains a ground object");
 test_tick+=2100;reconcile_inventory(0);check(inventory_pending<0&&!inventory_blocked,"drop confirmation uses ownership change");
 reset();rotten=true;held=0x8000;reconcile_inventory(0);check(!actions,"held mouse protects inventory dragging");
 reset();rotten=true;refuse=true;reconcile_inventory(0);test_tick+=2100;reconcile_inventory(0);check(inventory_blocked&&actions==1&&!foreign,"refused drop stops instead of repeated attempts");
 reset();reconcile_inventory(0);check(actions==1&&seeds==2,"backpack maintenance peels carried plant");
 test_tick+=2100;reconcile_inventory(0);check(inventory_pending<0&&!inventory_blocked,"peeling confirms inventory seed increase");
 printf("%d peeling checks passed; simulated native calls only\n",checks);return 0;
}
