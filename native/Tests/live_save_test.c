#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
typedef unsigned char BYTE;
typedef struct {double id;} RV;
static struct {int walk_state;} state,*shared=&state;
#define WALK_ACTIVE 1
static int forage_phase,calls;static bool turn=true,moving=true,alive=true,blocker=false;
static BYTE* game;
static RV find_instance(const char* name){return (RV){!strcmp(name,"o_player")?1:blocker?2:0};}
static bool valid_object(RV v){return v.id>0;}
static double member_number(RV v,const char* key){return !strcmp(key,"HP")?alive:!strcmp(key,"turn_available")?turn:moving;}
static void release_value(RV* v){}
static RV call_script(uintptr_t addr,RV self,int count,RV* args){if(addr!=0xb55530||self.id!=1||count||args)abort();calls++;return (RV){0};}
#include "../Bridge/live_save.h"
static void check(bool ok){if(!ok)abort();}
int main(void){
 game=calloc(1,0xb55530+64);BYTE prolog[]={0x55,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x56,0x57,0x53,0x48,0x81,0xec,0x58,1,0,0};memcpy(game+0xb55530,prolog,sizeof(prolog));
 turn=false;check(request_live_save()==23);turn=true;moving=false;check(request_live_save()==23);moving=true;
 alive=false;check(request_live_save()==23);alive=true;blocker=true;check(request_live_save()==23);blocker=false;
 forage_phase=1;check(request_live_save()==23);forage_phase=0;state.walk_state=1;check(request_live_save()==23);state.walk_state=0;
 game[0xb55530]=0;check(request_live_save()==24);game[0xb55530]=0x55;
 check(calls==0);check(request_live_save()==0&&calls==1);free(game);puts("PASS live-save native guards and exact zero-argument ABI (9 checks)");
}
