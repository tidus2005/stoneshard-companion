// Execute the production automation state machine against controlled native events.
#include "../Bridge/protocol.h"
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
typedef struct RV{double real;const char* text;}RV;
static SharedState storage,*shared=&storage;
static unsigned automation_flags=3;
static int forage_phase,forage_count,exit_mouse_phase;
static char automation_selection[2048];
static uint64_t tick=10000,telemetry_next,walk_scene=1,walk_window=1,walk_started,walk_progress;
static double walk_player_id=1,walk_last_x,walk_last_y,px=507,py=507,tx=533,ty=507,bag_value,visor;
static bool walk_dispatch_pending,safe=true,threat,visible=true,plant=true,menu,click_ok=true,pump_ok=true,reachable=true;
static int crafts,craft_result,clicks,closes,visors,stops,queries;
static bool multiple,alive[10];
enum{WALK_ACTIVE=1,WALK_MANUAL=3,WALK_THREAT=4,WALK_UI=5,WALK_BLOCKED=7,WALK_BACKGROUND=8,WALK_TIMEOUT=9};
#define GetTickCount64() tick
static RV numeric(double n){return (RV){n,NULL};}
static double number(RV r){return r.real;}
static bool valid_object(RV r){return r.real>=1;}
static void release_value(RV* r){}
static const char* text_value(RV r){return r.text?r.text:"";}
static RV find_instance(const char* n){return numeric(!strcmp(n,"o_player")?1:!strcmp(n,"o_inventory")?2:menu?5:-1);}
static double member_number(RV r,const char* k){
 if(!strcmp(k,"id"))return r.real;
 if(!strcmp(k,"x"))return r.real==1?px:multiple&&r.real>=100&&r.real<110?533+26*((int)r.real-100):tx;if(!strcmp(k,"y"))return r.real==1?py:ty;
 if(!strcmp(k,"turn_available")||!strcmp(k,"movingIsDone"))return safe;
 if(!strcmp(k,"is_sleeping")||!strcmp(k,"is_execute"))return 0;
 if(!strcmp(k,"isOpen"))return visor;if(!strcmp(k,"visible"))return visible;
 if(!strcmp(k,"fodder_value"))return bag_value;return NAN;
}
static RV get_member(RV r,const char* k){return numeric(!strcmp(k,"inv_object")||!strcmp(k,"berryType")?(r.real>=2000?2000:100):member_number(r,k));}
static RV instance_from_id(RV r){return numeric((r.real==3&&!plant)||(multiple&&r.real>=100&&r.real<110&&!alive[(int)r.real-100])?-1:r.real);}
static RV visor_helmet(void){return numeric(6);}
static int toggle_visor(void){visors++;visor=1-visor;return 0;}
static bool walk_threat(RV r){return threat;}
static bool walk_safe(RV r){return safe&&!threat;}
static void finish_walk(int reason,bool stop){shared->walk_state=reason;stops++;}
static bool native_pump_exit_click(void){return pump_ok;}
static bool native_click_world(double x,double y){if(!click_ok)return false;clicks++;return true;}
static RV fodder_item(int i){return numeric(i==0&&bag_value>0?4:-1);}
static bool fodder_carried(RV r,double owner){return r.real==4&&owner==2;}
static RV object_name(RV r){return (RV){0,r.real>=2000?"o_tree":"o_inv_fleawort"};}
static bool fodder_selected(const char* s,const char* k){char b[128];snprintf(b,sizeof(b),"|%s|",k);return strstr(s,b)!=NULL;}
static RV call_builtin(uintptr_t a,int n,RV* args){if(a!=0x5335220||n!=1)exit(2);return object_name(args[0]);}
static RV call_script(uintptr_t a,RV self,int n,RV* args){
 if(a==0xbbaab0)return numeric(visible);
 if(a==0x1805180||a==0x19123a0)return numeric(0);
 exit(3);
}
static RV call_instance_builtin(uintptr_t a,RV self,int n,RV* args){if(a!=0x51ebd00||self.real!=5||n!=2||args[0].real!=7||args[1].real!=25)exit(4);menu=false;closes++;return numeric(0);}
static int craft_fodder(bool automatic){if(!automatic)exit(6);if(shared->walk_state==WALK_ACTIVE)exit(5);crafts++;menu=true;if(!craft_result)bag_value=0;return craft_result;}
static RV indexed_instance(const char* n,int i){
 if(!multiple)return numeric(plant&&i==0&&!strcmp(n,"o_abstractGrow")?3:-1);
 int kind=!strcmp(n,"o_interactive_harvest");
 if(i<400)return numeric(2000+i+kind*400);
 i-=400;for(int p=kind;p<10;p+=2)if(alive[p]&&i--==0)return numeric(100+p);
 return numeric(-1);
}
typedef struct WalkCandidate{int dx,dy;}WalkCandidate;
static WalkCandidate center_candidate(int i){static const WalkCandidate c[]={{0,0},{0,-1},{1,-1},{1,0},{1,1},{0,1},{-1,1},{-1,0},{-1,-1}};return c[i];}
static bool native_center_reachable(RV p,double x,double y){queries++;return reachable;}
#include "../Bridge/fodder_catalog.h"
#include "../Bridge/automation.h"
static void check(bool ok,const char* name){if(!ok){fprintf(stderr,"FAIL %s\n",name);exit(1);}printf("PASS %s\n",name);}
static void reset(void){
 memset(shared,0,sizeof(*shared));shared->walk_state=WALK_ACTIVE;shared->scene_ready=1;shared->scene_generation=shared->window_generation=1;shared->map_w=shared->map_h=2340;shared->walk_x=2100;shared->walk_y=507;
 multiple=false;memset(alive,1,sizeof(alive));tick=10000;automation_flags=3;strcpy(automation_selection,"|o_inv_herb|");forage_phase=forage_count=0;forage_reset_targets();
 forage_scan_due=visor_next=visor_last_threat=0;forage_seen_scene=1;walk_dispatch_pending=false;bag_value=0;visor=1;safe=visible=plant=click_ok=pump_ok=reachable=true;threat=menu=false;crafts=craft_result=clicks=closes=visors=stops=queries=0;px=py=507;tx=533;ty=507;
}
static void approach(void){reconcile_forage(0);check(forage_phase==5,"selected visible material interrupts active route");tick+=200;reconcile_forage(0);check(forage_phase==1&&forage_move_pending,"path query waits for native cancellation frame");reconcile_forage(0);check(!forage_move_pending,"detour dispatched after cancellation settles");px=forage_x;py=forage_y;reconcile_forage(0);check(forage_phase==2&&shared->walk_phase==2&&!clicks,"hide overlay before injecting pickup");tick+=180;reconcile_forage(0);check(!clicks,"pickup waits for the moving camera to settle");tick+=340;reconcile_forage(0);check(forage_phase==3&&clicks==1,"one native pickup click after camera delay");}
int main(void){
 reset();approach();bag_value=4;reconcile_forage(0);check(forage_phase==4&&!walk_dispatch_pending,"craft cleanup settles before resuming");tick+=550;reconcile_forage(0);check(crafts==1&&closes==1&&forage_count==1&&forage_phase==0&&walk_dispatch_pending&&shared->walk_x==2100&&shared->walk_y==507,"inventory growth crafts once, closes owned menu and resumes exact route");
 reset();approach();tick+=4001;reconcile_forage(0);check(!crafts&&shared->walk_state==WALK_ACTIVE&&walk_dispatch_pending,"failed pickup preserves materials and resumes route with bounded deferred retry");
 reset();approach();tick+=1100;reconcile_forage(0);check(clicks==2&&!crafts,"missed click retries the same nearby visible plant");tick+=1100;reconcile_forage(0);tick+=1100;reconcile_forage(0);check(clicks==3&&!crafts,"pickup retries are bounded to three total clicks");bag_value=2;reconcile_forage(0);check(crafts==1&&forage_count==1,"successful retry crafts once");
 reset();approach();bag_value=4;craft_result=12;reconcile_forage(0);check(menu&&!closes&&shared->walk_state==WALK_BLOCKED,"full output bag preserves original crafting panel and stops");
 for(int reason=0;reason<5;reason++){
  reset();reconcile_forage(0);if(reason==0)threat=true;if(reason==1)shared->ui_flags=1;if(reason==2)automation_flags=0;if(reason==3)shared->scene_generation=2;
  reconcile_forage(reason==4?1:0);check(shared->walk_state!=WALK_ACTIVE&&!clicks&&!crafts,"enemy, UI, toggle, scene or foreground change cancels collection before input");
 }
 reset();strcpy(automation_selection,"|o_inv_other|");reconcile_forage(0);check(forage_phase==5,"automatic harvesting does not require a remembered manual selection");
 reset();tx=1001;reconcile_forage(0);check(forage_phase==5,"visible plants beyond eight tiles are approached");tx=533;
 reset();visible=false;reconcile_forage(0);check(!forage_phase&&!queries,"fogged plants are not approached");
 reset();px=403;reachable=false;reconcile_forage(0);tick+=200;reconcile_forage(0);check(!forage_phase&&queries==8&&walk_dispatch_pending,"unreachable plant checks bounded adjacent cells");
 reset();shared->walk_state=WALK_MANUAL;reconcile_forage(0);check(!forage_phase&&!clicks&&!crafts,"manual stop never resumes a former route");
 reset();threat=true;reconcile_visor(0);check(visor==0&&visors==1,"threat closes native visor");threat=false;tick+=1000;reconcile_visor(0);check(visor==0,"brief threat disappearance does not open visor");tick+=600;reconcile_visor(0);check(visor==1&&visors==2,"sustained safe state reopens visor");
 reset();threat=true;safe=false;reconcile_visor(0);check(!visors,"visor waits for available native turn");safe=true;reconcile_visor(1);check(!visors,"background never toggles visor");
 check(forage_material("o_inv_agrimony")&&forage_material("o_inv_barberry")&&!forage_material("o_inv_horsetail")&&!forage_material("o_inv_henbane")&&!forage_material("o_inv_blueberry_rot"),"native fodder catalog excludes unsupported and rotten plants");

 reset();safe=false;reconcile_forage(0);check(forage_phase==5&&forage_target_count==1&&!queries,"moving player discovers visible plant before idle; no path queries while moving");tick+=200;reconcile_forage(0);check(forage_phase==5&&!queries,"detour waits for turn readiness");safe=true;reconcile_forage(0);check(forage_phase==1,"queued moving discovery starts once native turn is ready");
 reset();approach();tick+=4100;reconcile_forage(0);tick+=600;reconcile_forage(0);check(!forage_phase,"failed plant respects cooldown instead of looping");tick+=1500;walk_dispatch_pending=false;reconcile_forage(0);check(forage_phase==5,"failed plant is retried after cooldown instead of blacklisted for whole scene");
 reset();approach();bag_value=2;reconcile_forage(0);tick+=550;reconcile_forage(0);tick+=700;walk_dispatch_pending=false;reconcile_forage(0);check(!forage_phase&&crafts==1,"completed plant is not harvested twice");
 check(!forage_material("o_inv_rhubarb")&&!forage_material("o_inv_lentil")&&!forage_material("o_inv_lentils")&&!forage_material("o_inv_leek")&&!forage_material("o_inv_acorn"),"default catalog preserves cooking ingredients and rejects nuts without fodder value");

 // Live bogbean: target tile corner (858,1768), player center (897,1755).
 check(forage_adjacent(897,1755,858,1768),"live bogbean tile-corner origin remains adjacent from northeast");
 check(!forage_adjacent(923,1755,858,1768)&&!forage_adjacent(NAN,1755,858,1768),"distant or invalid coordinates cannot trigger pickup");
 for(int ox=0;ox<=25;ox+=5)for(int oy=0;oy<=25;oy+=5)for(int direction=1;direction<=8;direction++){
  reset();tx=520+ox;ty=520+oy;WalkCandidate c=center_candidate(direction);px=533+c.dx*26;py=533+c.dy*26;
  forage_phase=2;forage_id=3;forage_due=tick+5000;forage_scan_due=tick;
  reconcile_forage(0);if(forage_phase!=3||clicks!=1)exit(11);
 }
 check(true,"all eight adjacent directions accept 36 plant-origin offsets without false blockage");
 reset();multiple=true;safe=false;
 for(int i=0;i<30&&forage_phase==0;i++){tick+=50;reconcile_forage(0);}
 check(forage_phase==5&&forage_target_count>0,"dense map discovers plants among 800 non-material objects while moving");
 safe=true;
 for(int frames=0;frames<3000&&forage_count<10;frames++){
  tick+=60;
  if(forage_phase==1&&!forage_move_pending){px=forage_x;py=forage_y;}
  if(forage_phase==3){int id=(int)forage_id-100;if(id<0||id>=10||!alive[id])exit(9);alive[id]=false;bag_value=2;}
  reconcile_forage(0);
  if(!forage_phase&&walk_dispatch_pending)walk_dispatch_pending=false;
 }
 check(forage_count==10&&crafts==10&&closes==10,"ten visible plants across both object families are all collected and crafted despite shifting instance indexes");
 tick+=600;reconcile_forage(0);
 check(shared->walk_state==WALK_ACTIVE&&shared->walk_x==2100&&shared->walk_y==507,"ten consecutive detours preserve the original journey target");
 puts("Automation state machine checks passed; no game accessed.");
}
