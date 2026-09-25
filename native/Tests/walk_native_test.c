// Exercise the production adapter with actual-shaped transition instances.
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <string.h>
typedef struct RV {double real;int kind;} RV;
static struct {double map_w,map_h;char detail[512];} storage={2340,2340},*shared=&storage;
static struct {double gx,gy,dx,dy;} tiles[8];
static int count,queries,activations,checks;
static double px=507,py=39,query_result=1,occupant=-4;
static RV numeric(double n){return (RV){n,0};}
static double number(RV r){return r.kind==5?NAN:r.real;}
static void release_value(RV* r){r->kind=5;}
static RV string_value(const char* name){return numeric(!strcmp(name,"o_unit")?3804:7877);}
static RV call_builtin(uintptr_t address,int n,RV* args){
    if(address==0x5336680&&n==1)return args[0];
    if(address==0x51f2980&&n==2&&args[0].real==7877)return numeric(args[1].real<count?args[1].real+1:-4);
    exit(2);
}
static RV instance_from_id(RV id){return (RV){id.real,6};}
static bool valid_object(RV r){return r.kind==6;}
static RV call_instance_builtin(uintptr_t address,RV player,int count,RV* args){
    if(address!=0x51f14f0||player.real!=0||count!=5||args[2].real!=3804||args[3].real!=0||args[4].real!=1)exit(4);
    return numeric(occupant);
}
#include <string.h>
static double member_number(RV r,const char* key){
    if(r.real==0){if(!strcmp(key,"x"))return px;if(!strcmp(key,"y"))return py;return NAN;}
    int i=(int)r.real-1;
    if(!strcmp(key,"grid_x"))return tiles[i].gx;if(!strcmp(key,"grid_y"))return tiles[i].gy;
    if(!strcmp(key,"dX"))return tiles[i].dx;if(!strcmp(key,"dY"))return tiles[i].dy;return NAN;
}
static RV call_script(uintptr_t address,RV self,int n,RV* args){
    if(address==0x17206a0&&self.real==0&&n==4&&args[0].real==floor(px/26)&&args[1].real==floor(py/26)&&args[2].real==45&&args[3].real==45){queries++;return numeric(query_result);}
    if(address==0x19b1e50&&self.real==0&&n==0&&args==NULL){activations++;return (RV){0,5};}
    exit(3);
}
#include "../Bridge/walk_native.h"
static void check(bool ok,const char* label){if(!ok){fprintf(stderr,"FAIL %s\n",label);exit(1);}checks++;printf("PASS %s\n",label);}
int main(void){
    double x,y;
    for(int d=1;d<=4;d++){
        px=d==3?39:d==4?2301:507;py=d==1?39:d==2?2301:767;
        count=1;tiles[0].gx=floor(px/26);tiles[0].gy=floor(py/26);tiles[0].dx=d==3?-1:d==4?1:0;tiles[0].dy=d==1?-1:d==2?1:0;
        check(native_exit_target(d+10,px,py,&x,&y)&&x==px&&y==py,"actual inner transition tile selected for all four directions");
        check(!native_exit_target((d%4)+11,px,py,&x,&y),"wrong outward direction is never used");
    }
    px=507;py=39;count=3;
    tiles[0]=(typeof(tiles[0])){19,0,0,1}; // wrong direction
    tiles[1]=(typeof(tiles[0])){18,1,0,-1}; // neighboring column
    tiles[2]=(typeof(tiles[0])){19,1,0,-1};
    check(native_exit_target(11,px,py,&x,&y)&&x==507&&y==39,"matching row and direction wins over nearer unrelated arrows");
    tiles[2].gx=NAN;check(!native_exit_target(11,px,py,&x,&y),"invalid tile metadata cannot trigger a transition");
    count=1;tiles[0]=(typeof(tiles[0])){19,0,0,-1};
    check(!native_exit_target(11,507,767,&x,&y),"distant transition cannot be activated from the map interior");
    check(!native_exit_target(11,NAN,39,&x,&y),"nonfinite character position fails closed");
    count=0;check(!native_exit_target(11,px,py,&x,&y),"missing transition instance is a failure, not arbitrary ground");
    RV player={0,6};check(native_center_reachable(player,1183,1183)&&queries==1,"path query passes grid cells; native script multiplies by 26 and adds 13");
    query_result=0;check(!native_center_reachable(player,1183,1183),"blocked path is rejected before movement");
    query_result=NAN;check(!native_center_reachable(player,1183,1183),"invalid path query result fails closed");
    query_result=1;occupant=123;int before=queries;
    check(!native_center_reachable(player,1183,1183)&&queries==before,"NPC or enemy on center cell is rejected even if path query would accept it");
    occupant=NAN;check(!native_center_reachable(player,1183,1183),"unknown occupancy cannot be treated as empty ground");
    occupant=-4;px=NAN;check(!native_center_reachable(player,1183,1183),"invalid character origin never enters the game's path query");px=507;
    native_activate_exit(player);check(activations==1,"exit activation calls original player transition routine with no coordinate writes");
    printf("%d native walk adapter checks passed. No game accessed.\n",checks);return 0;
}
