// Native render and route policies with fake GameMaker functions. No game or UI.
#define COMPANION_NATIVE_TEST
#include "../Bridge/protocol.h"
#include <stdbool.h>
#include <stdio.h>
#include <math.h>
#include <string.h>
#include <stdlib.h>
typedef struct RV {union{double real;void* ptr;int64_t integer;};uint32_t flags;int32_t kind;} RV;
typedef RV* (*GameScript)(void*,void*,RV*,int,RV**);
static SharedState storage,*shared=&storage;
static bool test_modifiers_down;
static bool highlight_applied,idle=true,enemy=false,damage=false,injury=false,can_act=true,valid=true;
static double px=500,py=500,path=-1,player_id=100;
static double sent_x,sent_y;
static int move_calls,stop_calls,draw_calls,passed;
static uint64_t test_now=10000;
#define GetTickCount64() test_now
static RV numeric(double n){return (RV){.real=n};}
static RV find_instance(const char* name){return valid?(RV){.kind=6,.ptr=&storage}:(RV){.kind=5};}
static bool valid_object(RV r){return r.kind==6;}
static double member_number(RV r,const char* key){
    if(!strcmp(key,"id"))return player_id;
    if(!strcmp(key,"x"))return px;if(!strcmp(key,"y"))return py;
    if(!strcmp(key,"path"))return path;if(!strcmp(key,"movingIsDone"))return idle;
    if(!strcmp(key,"is_see_enemy"))return enemy;
    if(!strcmp(key,"is_damage_taken"))return damage;if(!strcmp(key,"is_take_injury"))return injury;
    return NAN;
}
static RV get_member(RV r,const char* key){return (RV){.kind=1,.ptr=idle?"idle":"move"};}
static const char* text_value(RV r){return r.ptr;}
static void release_value(RV* r){r->kind=5;}
static bool supply_safe(RV p){return can_act&&idle&&!enemy&&!damage&&!injury;}
static bool restore_camera(bool force){return true;}
static RV call_script(uintptr_t address,RV self,int count,RV* args){
    if(address==0x1805180){if(count!=2)exit(2);move_calls++;sent_x=args[0].real;sent_y=args[1].real;}
    else if(address==0x19123a0){if(count!=1||args[0].real!=0)exit(3);stop_calls++;path=-1;}
    else exit(4);return (RV){.kind=5};
}
static RV* fake_draw(void* self,void* other,RV* out,int count,RV** args){
    draw_calls++;*out=count?*args[0]:numeric(-1);return out;
}
static bool exit_exists=true;
static int exit_calls,center_queries;
static double exit_inset=0,center_min_distance=0;
static bool native_exit_target(int direction,double x,double y,double* tx,double* ty){
    int d=direction>=11?direction-10:direction;
    if(!exit_exists||d<1||d>4)return false;
    *tx=floor(x/26)*26+13;*ty=floor(y/26)*26+13;
    if(d==1)*ty=13+exit_inset*26;if(d==2)*ty=floor(shared->map_h/26)*26-13-exit_inset*26;
    if(d==3)*tx=13+exit_inset*26;if(d==4)*tx=floor(shared->map_w/26)*26-13-exit_inset*26;
    return fabs(*tx-x)<=52.5&&fabs(*ty-y)<=52.5;
}
static bool native_center_reachable(RV player,double x,double y){center_queries++;return (x-1183)*(x-1183)+(y-1183)*(y-1183)>=center_min_distance;}
static void native_activate_exit(RV player){exit_calls++;}
#include "../Bridge/highlight.h"
#include "../Bridge/walk.h"
static void check(bool ok,const char* name){if(!ok){fprintf(stderr,"FAIL %s\n",name);exit(1);}printf("PASS %s\n",name);passed++;}
static void reset(void){memset(shared,0,sizeof(*shared));shared->ready=shared->scene_ready=1;shared->scene_generation=2;shared->window_generation=1;shared->map_w=shared->map_h=2340;shared->player_x=px=500;shared->player_y=py=500;idle=valid=can_act=true;enemy=damage=injury=false;player_id=100;path=-1;move_calls=stop_calls=0;test_now=10000;walk_dispatch_pending=test_modifiers_down=false;exit_exists=true;exit_inset=center_min_distance=0;exit_calls=center_queries=0;}
static int begin(int d){int result=start_walk(d);test_now+=200;reconcile_walk(0);return result;}
int main(void){
    original_label_renderer=fake_draw;
    RV out={0},input=numeric(0),second=numeric(9);RV* args[]={&input,&second};
    reconcile_highlight(true);query_labels_at(0x3992a2f,NULL,NULL,&out,2,args);check(out.real==1&&input.real==0&&second.real==9,"label override preserves caller arguments");
    idle=false;query_labels_at(0x3992a2f,NULL,NULL,&out,2,args);check(out.real==1,"labels remain enabled while moving");
    query_labels_at(0x49cc96e,NULL,NULL,&out,2,args);check(out.real==1,"label retention stays enabled between steps");
    query_labels_at(0x1805180,NULL,NULL,&out,2,args);check(out.real==0,"movement query remains native while labels enabled");
    query_labels_at(0x3992a30,NULL,NULL,&out,2,args);check(out.real==0,"unknown call site is never overridden");
    check(highlight_allowed(true,0,1|8),"inventory character panel and hover preserve labels");
    for(int reason=1;reason<=8;reason*=2)if(reason!=4)check(!highlight_allowed(true,reason,0),"focus scene or lease suppresses labels");
    for(int flag=2;flag<=32;flag*=2)if(flag!=8)check(!highlight_allowed(true,4,flag),"blocking native UI suppresses labels");
    reconcile_highlight(false);query_labels_at(0x3992a2f,NULL,NULL,&out,2,args);check(out.real==0,"disabled hook delegates original display argument");
    input=numeric(1);query_labels_at(0x3992a2f,NULL,NULL,&out,2,args);check(out.real==1,"native Alt display remains available when disabled");
    double x,y;
    for(int d=1;d<=4;d++){check(walk_target(d,2340,2340,false,&x,&y)&&x>=39&&x<=2301&&y>=39&&y<=2301,"edge destination is inside valid tile centers");}
    check(!walk_target(0,2340,2340,false,&x,&y)&&!walk_target(10,2340,2340,false,&x,&y)&&!walk_target(1,NAN,2340,false,&x,&y)&&!walk_target(1,52,52,false,&x,&y),"invalid direction and maps fail closed");
    reset();test_modifiers_down=true;check(start_walk(1)==0&&move_calls==0,"hotkey waits for modifier release");
    test_now+=300;reconcile_walk(0);check(move_calls==0,"held modifiers never dispatch movement");
    test_modifiers_down=false;reconcile_walk(0);check(move_calls==1,"released hotkey dispatches exactly once");
    reset();start_walk(1);start_walk(0);test_now+=300;reconcile_walk(0);check(move_calls==0&&stop_calls==0,"cancel before dispatch never changes native path");
    reset();enemy=true;check(begin(1)==7&&move_calls==0,"visible enemy prevents starting route");
    reset();check(begin(1)==0&&move_calls==1&&px==500&&py==500,"route calls native movement once without changing position");
    check(begin(2)==7&&move_calls==1,"duplicate direction cannot replace active route");
    idle=false;path=5;for(int i=0;i<30;i++){test_now+=100;px+=1;reconcile_walk(0);}check(shared->walk_state==WALK_ACTIVE&&move_calls==1,"long native route is never reissued");
    idle=true;px=shared->walk_x;py=shared->walk_y;reconcile_walk(0);check(shared->walk_state==WALK_ACTIVE&&walk_dispatch_pending&&shared->walk_phase==1&&move_calls==1,"inner edge queues exit rather than ending or immediately clicking");
    reset();begin(2);test_now+=800;reconcile_walk(0);check(shared->walk_state==WALK_BLOCKED&&move_calls==1,"unreachable or native interrupted route reports stop without retries");
    reset();begin(3);finish_walk(WALK_MANUAL,true);check(shared->walk_state==WALK_MANUAL&&stop_calls==1,"manual input stops old path before native input handling");
    path=77;idle=false;test_now+=200;reconcile_walk(0);check(path==77&&stop_calls==1,"timer never cancels the player's replacement route");
    reset();begin(4);begin(0);check(shared->walk_state==WALK_MANUAL&&stop_calls==1,"explicit stop calls original stop script once");
    reset();begin(1);enemy=true;reconcile_walk(0);check(shared->walk_state==WALK_THREAT&&stop_calls==1,"new enemy stops active native path");
    reset();begin(1);shared->ui_flags=8;reconcile_walk(0);check(shared->walk_state==WALK_ACTIVE,"hover does not cancel a route");
    reset();begin(1);shared->ui_flags=1;reconcile_walk(0);check(shared->walk_state==WALK_UI&&stop_calls==1,"inventory stops walking independently of speed");
    reset();begin(1);shared->scene_generation++;reconcile_walk(0);check(shared->walk_state==WALK_SCENE&&stop_calls==0,"map transition does not touch new scene's path");
    reset();begin(1);reconcile_walk(8);check(shared->walk_state==WALK_BACKGROUND&&stop_calls==1,"lost controller lease stops route");
    reset();begin(1);idle=false;test_now+=8100;reconcile_walk(0);check(shared->walk_state==WALK_TIMEOUT&&stop_calls==1,"no progress timeout stops route");
    for(int d=1;d<=4;d++){
        reset();begin(d);idle=true;px=sent_x;py=sent_y;reconcile_walk(0);test_now+=200;reconcile_walk(0);
        double ex=d==3?13:d==4?2327:1183,ey=d==1?13:d==2?2327:1183;
        check(move_calls==2&&shared->walk_phase==1&&sent_x==ex&&sent_y==ey,"each direction dispatches exactly one outer tile click after inner arrival");
        shared->scene_ready=0;reconcile_walk(0);shared->scene_ready=1;shared->scene_generation++;test_now+=1000;reconcile_walk(0);
        check(shared->walk_state==WALK_SCENE&&move_calls==2&&stop_calls==0,"loading then next map ends journey without crossing another map");
    }
    reset();begin(5);check(sent_x==1183&&sent_y==1183,"center direction requests original path to map center");
    px=sent_x;py=sent_y;idle=true;reconcile_walk(0);test_now+=5000;reconcile_walk(0);
    check(shared->walk_state==WALK_ARRIVED&&move_calls==1&&shared->walk_phase==0,"center arrival never requests an exit click");
    reset();shared->player_x=px=1183;shared->player_y=py=39;begin(1);check(move_calls==1&&sent_y==13&&shared->walk_phase==1,"starting at inner edge proceeds directly to exit once");
    reset();shared->player_x=shared->player_y=1183;begin(5);check(shared->walk_state==WALK_ARRIVED&&move_calls==0,"already centered needs no movement");
    for(int cause=0;cause<6;cause++){
        reset();begin(1);px=sent_x;py=sent_y;reconcile_walk(0);
        if(cause==0)enemy=true;if(cause==1)damage=true;if(cause==2)injury=true;if(cause==3)shared->ui_flags=2;
        if(cause==4)finish_walk(WALK_MANUAL,true);if(cause==5)can_act=false;
        test_now+=200;reconcile_walk(0);test_now+=5000;enemy=damage=injury=false;reconcile_walk(0);
        check(shared->walk_state!=WALK_ACTIVE&&move_calls==1,"threat UI manual input or unsafe state before crossing cancels exit without retry");
    }
    reset();begin(1);px=sent_x;py=sent_y;reconcile_walk(0);test_modifiers_down=true;test_now+=300;reconcile_walk(0);
    check(move_calls==1,"exit click also waits for physical modifiers to release");
    test_modifiers_down=false;reconcile_walk(0);enemy=true;idle=false;reconcile_walk(0);
    check(shared->walk_state==WALK_THREAT&&stop_calls==1&&move_calls==2,"enemy during final exit step stops native route without attack calls");
    reset();begin(1);px=sent_x;py=sent_y;reconcile_walk(0);test_now+=200;reconcile_walk(0);px=sent_x;py=sent_y;reconcile_walk(0);test_now+=2100;reconcile_walk(0);
    check(shared->walk_state==WALK_BLOCKED&&move_calls==2,"outer tile without native transition reports blocked without repeated clicks");
    reset();begin(1);px=sent_x;py=sent_y;reconcile_walk(0);reconcile_walk(1);
    check(shared->walk_state==WALK_BACKGROUND&&move_calls==1,"background between phases cancels crossing");
    reset();begin(1);px=sent_x;py=sent_y;reconcile_walk(0);player_id++;test_now+=200;reconcile_walk(0);
    check(shared->walk_state==WALK_SCENE&&move_calls==1&&stop_calls==0,"changed character never receives pending exit click");
    for(int d=6;d<=9;d++){
        reset();begin(d);
        check(move_calls==1&&sent_x==((d==6||d==8)?39:2301)&&sent_y==(d<=7?39:2301),"diagonal uses correct inner corner tile");
        px=sent_x;py=sent_y;reconcile_walk(0);test_now+=5000;reconcile_walk(0);
        check(shared->walk_state==WALK_ARRIVED&&move_calls==1&&shared->walk_phase==0,"corner arrival stops without crossing either boundary");
    }
    for(int d=11;d<=14;d++){
        reset();shared->player_x=px=507;shared->player_y=py=767;begin(d);
        double ex=d==13?39:d==14?2301:507,ey=d==11?39:d==12?2301:767;
        check(move_calls==1&&sent_x==ex&&sent_y==ey,"relative arrow route preserves character row or column");
        px=sent_x;py=sent_y;reconcile_walk(0);test_now+=5000;reconcile_walk(0);
        check(shared->walk_state==WALK_ARRIVED&&move_calls==1,"relative arrival does not cross map or restart");
    }
    reset();shared->map_w=2600;shared->map_h=1560;begin(9);
    check(sent_x==2561&&sent_y==1521,"rectangular maps use independent dimensions");
    reset();shared->player_x=NAN;check(start_walk(11)==4&&move_calls==0,"unknown character coordinate refuses relative route");
    reset();shared->player_x=13;check(start_walk(11)==4&&move_calls==0,"relative origin on transition column fails closed");
    reset();shared->player_x=px=507;shared->player_y=py=39;begin(11);
    check(shared->walk_state==WALK_ACTIVE&&shared->walk_phase==1&&move_calls==1&&sent_x==507&&sent_y==13,"fresh relative request at boundary clicks exit");
    reset();check(!handle_walk_key(VK_UP,false,false,true,true)&&move_calls==0,"disabled arrow mode delegates native key");
    for(int cause=0;cause<7;cause++){
        reset();set_walk_keys(true);
        if(cause==3)shared->scene_ready=0;if(cause==4)shared->ui_flags=1;if(cause==5)shared->ready=0;
        check(!handle_walk_key(cause==6?'A':VK_UP,false,cause==0,cause!=1,cause!=2)&&shared->walk_state!=WALK_ACTIVE,"modified background expired loading panel disconnected or non-arrow input passes through");
    }
    for(int flag=1;flag<=32;flag*=2){
        reset();set_walk_keys(true);shared->ui_flags=flag;
        check(handle_walk_key(VK_UP,false,false,true,true)==(flag==8),"only hover panel permits arrow automation");
    }
    unsigned keys[]={VK_UP,VK_DOWN,VK_LEFT,VK_RIGHT};
    for(int i=0;i<4;i++){
        reset();set_walk_keys(true);shared->player_x=px=507;shared->player_y=py=767;
        check(handle_walk_key(keys[i],false,false,true,true)&&shared->walk_direction==i+11&&move_calls==0,"each game arrow queues correct relative direction");
        test_now+=200;reconcile_walk(0);
        for(int j=0;j<20;j++)handle_walk_key(keys[i],true,false,true,true);
        check(move_calls==1&&stop_calls==0&&shared->walk_state==WALK_ACTIVE,"held arrow cannot issue repeated paths or cancel active route");
        handle_walk_key(keys[i],false,false,true,true);
        check(shared->walk_state==WALK_MANUAL&&stop_calls==1&&move_calls==1,"second physical arrow press stops current route exactly once");
        for(int j=0;j<20;j++)handle_walk_key(keys[i],true,false,true,true);
        check(shared->walk_state==WALK_MANUAL&&move_calls==1,"held key after cancellation never resumes path");
    }
    for(int cause=0;cause<3;cause++){
        reset();set_walk_keys(true);enemy=cause==0;damage=cause==1;injury=cause==2;
        check(handle_walk_key(VK_RIGHT,false,false,true,true)&&shared->walk_state==WALK_THREAT&&move_calls==0,"unsafe arrow consumed without native movement or attack");
        enemy=damage=injury=false;handle_walk_key(VK_RIGHT,true,false,true,true);test_now+=300;reconcile_walk(0);
        check(move_calls==0,"threat clearing while arrow held does not restart");
    }
    reset();set_walk_keys(true);handle_walk_key(VK_LEFT,false,false,true,true);set_walk_keys(false);test_now+=300;reconcile_walk(0);
    check(!shared->walk_keys_enabled&&move_calls==0,"disable before dispatch cancels pending keyboard route");
    reset();set_walk_keys(true);begin(13);set_walk_keys(false);
    check(!shared->walk_keys_enabled&&shared->walk_state==WALK_MANUAL&&stop_calls==1,"disable stops already dispatched keyboard path");
    reset();set_walk_keys(true);begin(6);set_walk_keys(false);
    check(shared->walk_state==WALK_ACTIVE&&stop_calls==0,"disabling keyboard mode does not cancel independent grid route");
    reset();set_walk_keys(true);begin(11);shared->scene_generation++;reconcile_walk(0);
    handle_walk_key(VK_UP,true,false,true,true);test_now+=300;reconcile_walk(0);
    check(shared->walk_keys_enabled&&shared->walk_state==WALK_SCENE&&move_calls==1&&stop_calls==0,"map change retains mode but held key never restarts in new scene");
    reset();set_walk_keys(true);begin(11);idle=false;enemy=true;reconcile_walk(0);
    check(shared->walk_state==WALK_THREAT&&stop_calls==1&&move_calls==1,"relative journey stops when a new enemy appears");
    for(int d=1;d<=4;d++)for(int relative=0;relative<2;relative++){
        reset();set_walk_keys(true);
        shared->player_x=px=d==3?39:d==4?2301:507;shared->player_y=py=d==1?39:d==2?2301:767;
        int direction=d+(relative?10:0);request_walk(direction);test_now+=200;reconcile_walk(0);
        check(move_calls==1&&shared->walk_phase==1&&sent_x==(d==3?13:d==4?2327:507)&&sent_y==(d==1?13:d==2?2327:767),"fresh edge request clicks current exit without routing to border midpoint");
        for(int i=0;i<5;i++)handle_walk_key(keys[d-1],true,false,true,true);
        check(move_calls==1&&shared->walk_state==WALK_ACTIVE,"held key does not repeat or cancel exit click");
        shared->scene_generation++;reconcile_walk(0);handle_walk_key(keys[d-1],true,false,true,true);test_now+=300;reconcile_walk(0);
        check(move_calls==1&&stop_calls==0&&shared->walk_state==WALK_SCENE,"exit journey ends after exactly one transition");
    }
    for(int d=1;d<=4;d++){
        reset();set_walk_keys(true);shared->player_x=px=507;shared->player_y=py=767;
        begin(d+10);shared->player_x=px=sent_x;shared->player_y=py=sent_y;
        // Key arrives before the next supervisor timer has recorded arrival.
        handle_walk_key(keys[d-1],false,false,true,true);test_now+=200;reconcile_walk(0);
        check(move_calls==2&&stop_calls==0&&shared->walk_phase==1,"second press at arrival dispatches exit even before timer acknowledgement");
    }
    reset();set_walk_keys(true);begin(11);shared->player_x=px=sent_x;shared->player_y=py=sent_y;reconcile_walk(0);
    check(shared->walk_state==WALK_ARRIVED&&move_calls==1,"initial relative journey still stops at edge");
    handle_walk_key(VK_UP,true,false,true,true);test_now+=300;reconcile_walk(0);
    check(move_calls==1&&shared->walk_state==WALK_ARRIVED,"arrival with key still held never exits automatically");
    handle_walk_key(VK_UP,false,false,true,true);test_now+=200;reconcile_walk(0);
    check(move_calls==2&&shared->walk_phase==1&&sent_y==13,"new physical press after recorded arrival clicks exit");
    reset();request_walk(1);request_walk(2);test_now+=200;reconcile_walk(0);
    check(shared->walk_state==WALK_MANUAL&&move_calls==0,"grid second click cancels pending route instead of replacing it");
    reset();begin(1);request_walk(2);
    check(shared->walk_state==WALK_MANUAL&&stop_calls==1,"grid second click still stops active movement");
    for(int cause=0;cause<4;cause++){
        reset();set_walk_keys(true);shared->player_x=px=507;shared->player_y=py=39;
        handle_walk_key(VK_UP,false,false,true,true);
        if(cause==0)enemy=true;if(cause==1)damage=true;if(cause==2)shared->ui_flags=1;if(cause==3)shared->scene_generation++;
        test_now+=200;reconcile_walk(0);
        check(move_calls==0&&shared->walk_state!=WALK_ACTIVE,"threat panel or scene change before exit dispatch cancels it");
    }
    reset();set_walk_keys(true);shared->player_x=px=507;shared->player_y=py=39;begin(11);px=sent_x;py=sent_y;reconcile_walk(0);test_now+=2100;reconcile_walk(0);
    check(shared->walk_state==WALK_BLOCKED&&move_calls==1,"exit without native transition reports blocked and never retries");
    reset();shared->player_x=px=507;shared->player_y=py=65;begin(11);
    check(shared->walk_phase==0&&sent_y==39,"two tiles away still approaches edge rather than clicking exit");
    reset();shared->player_x=px=507;shared->player_y=py=13;begin(11);
    check(shared->walk_phase==1&&move_calls==0&&exit_calls==1,"already on real exit activates native transition without a zero length path");
    reset();shared->player_x=px=507;shared->player_y=py=39;begin(12);
    check(shared->walk_phase==0&&sent_y==2301,"opposite direction from border travels inward normally");
    reset();exit_inset=1;shared->player_x=px=507;shared->player_y=py=39;set_walk_keys(true);
    handle_walk_key(VK_UP,false,false,true,true);test_now+=200;reconcile_walk(0);
    check(exit_calls==1&&move_calls==0&&shared->walk_y==39,"exit on inner row uses real transition instead of assumed outer row");
    test_now+=2100;reconcile_walk(0);reconcile_walk(0);
    check(exit_calls==1&&shared->walk_state==WALK_BLOCKED,"native transition with no scene change never repeats activation");
    reset();exit_exists=false;shared->player_x=px=507;shared->player_y=py=39;begin(11);
    check(move_calls==0&&exit_calls==0&&shared->walk_state==WALK_BLOCKED,"missing real exit does not click arbitrary border ground");
    reset();center_min_distance=1;begin(5);
    check(move_calls==1&&center_queries==2&&((sent_x-1183)*(sent_x-1183)+(sent_y-1183)*(sent_y-1183))==676,"occupied center chooses closest reachable neighbor before moving");
    reset();center_min_distance=5*676;begin(5);
    check(move_calls==0&&center_queries==8&&walk_dispatch_pending,"large blocked center is scanned in bounded batches without moving");
    for(int i=0;i<40&&walk_dispatch_pending;i++){test_now+=100;reconcile_walk(0);}
    check(move_calls==1&&((sent_x-1183)*(sent_x-1183)+(sent_y-1183)*(sent_y-1183))==5*676,"center fallback is sorted by distance, not scan order");
    reset();center_min_distance=INFINITY;begin(5);enemy=true;test_now+=100;reconcile_walk(0);
    check(move_calls==0&&center_queries==8&&shared->walk_state==WALK_THREAT,"enemy during center search cancels without a path or attack");
    reset();center_min_distance=INFINITY;begin(5);
    for(int i=0;i<40&&walk_dispatch_pending;i++){test_now+=100;reconcile_walk(0);}
    check(move_calls==0&&center_queries==289&&shared->walk_state==WALK_BLOCKED,"fully blocked center neighborhood stops after bounded search");
    printf("%d native interaction checks passed. No game accessed.\n",passed);return 0;
}
