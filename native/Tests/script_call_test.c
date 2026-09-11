#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
typedef struct RV{union{double real;void* ptr;};int flags,kind;}RV;
typedef RV* (*GameScript)(void*,void*,RV*,int,RV**);
static uintptr_t game;
static int calls,expected;
static RV values[8];
static RV* stub(void* self,void* other,RV* out,int count,RV** args){
    if(self!=values||other!=self||count!=expected)exit(2);
    for(int i=0;i<count;i++)if(args[i]!=values+i||args[i]->real!=i+10)exit(3);
    calls++;*out=(RV){.real=42,.kind=0};return out;
}
#include "../Bridge/script_call.h"
int main(void){
    RV self={.ptr=values,.kind=6};for(int i=0;i<8;i++)values[i]=(RV){.real=i+10};
    int counts[]={0,2,4,6,8};for(int i=0;i<5;i++){expected=counts[i];RV out=call_script((uintptr_t)stub,self,expected,values);if(out.kind||out.real!=42)exit(4);}
    if(calls!=5)exit(5);
    call_script((uintptr_t)stub,self,9,values);call_script((uintptr_t)stub,self,-1,values);call_script((uintptr_t)stub,self,6,NULL);
    call_script((uintptr_t)stub,(RV){.kind=6},6,values);call_script((uintptr_t)stub,(RV){.ptr=values,.kind=0},6,values);
    if(calls!=5)exit(6);
    puts("10 production script dispatcher checks passed, including six-argument native stacking; no game accessed.");
}
