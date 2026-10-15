#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include "../Bridge/runner_context.h"
static int checks;
static void check(bool ok){if(!ok)exit(2);checks++;}
int main(void){
    int original_self,original_lookup,active_frame,changed;
    void *frame=NULL,*self=&original_self,*lookup=&original_lookup;
    bool entered=false;RunnerContext saved,nested;
    // A window callback inside an Alarm/Step event must make no engine calls.
    frame=&active_frame;
    check(!runner_enter(&saved,&entered,&frame,&self,&lookup));
    check(!entered&&frame==&active_frame&&self==&original_self&&lookup==&original_lookup);
    frame=NULL;
    check(runner_enter(&saved,&entered,&frame,&self,&lookup));
    check(entered);
    check(!runner_enter(&nested,&entered,&frame,&self,&lookup));
    // Model script prologues and variable_instance_get changing runner slots.
    frame=&active_frame;self=&changed;lookup=NULL;
    runner_leave(&saved);
    check(!entered&&frame==NULL&&self==&original_self&&lookup==&original_lookup);
    check(runner_enter(&saved,&entered,&frame,&self,&lookup));
    runner_leave(&saved);
    check(!entered&&self==&original_self&&lookup==&original_lookup);
    printf("%d runner-context checks passed; nested game scripts deferred, implicit lookup restored. No game accessed.\n",checks);
}
