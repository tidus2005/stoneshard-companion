#include <stdbool.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
// Failure injection at the actual commit boundary: dropped point write,
// cache refresh undoing it, failed rollback, partial charge and final mismatch.
static int points,learned,gems,credits,charge_calls,apply_calls,checks;
static bool drop_point,undo_refresh,bad_rollback,fail_cost,fail_finish;
static void br_points_set(bool apply){
    apply_calls++;
    if(apply){learned=0;if(!drop_point)points++;if(undo_refresh)points--;}
    else if(!bad_rollback){learned=1;points=0;}
}
static bool br_points_match(bool apply){return points==(apply?1:0)&&learned==(apply?0:1);}
static bool br_cost_commit(void){charge_calls++;gems--;if(fail_cost)return false;credits--;return true;}
static bool br_finish_commit(void){if(fail_finish){points=0;return false;}return gems==1&&credits==1;}
#include "../Bridge/refund_commit.h"
#include "../Bridge/action_ui_policy.h"
static void check(bool ok){checks++;if(!ok){printf("FAIL %d\n",checks);exit(1);}}
static void reset(void){points=0;learned=1;gems=credits=2;charge_calls=apply_calls=0;drop_point=undo_refresh=bad_rollback=fail_cost=fail_finish=br_refund_locked=br_mutated=false;}
int main(void){
 reset();check(br_commit()==NULL);check(points==1&&learned==0&&gems==1&&credits==1&&charge_calls==1&&!br_refund_locked);
 reset();drop_point=true;check(br_commit()!=NULL);check(points==0&&learned==1&&gems==2&&credits==2&&charge_calls==0&&!br_mutated&&!br_refund_locked);
 reset();undo_refresh=true;check(br_commit()!=NULL);check(points==0&&learned==1&&gems==2&&credits==2&&charge_calls==0&&!br_mutated);
 reset();drop_point=bad_rollback=true;check(br_commit()!=NULL);check(br_refund_locked&&br_mutated&&charge_calls==0&&gems==2);
 int calls=apply_calls;check(br_commit()!=NULL&&apply_calls==calls&&charge_calls==0);
 reset();fail_cost=true;check(br_commit()!=NULL);check(points==1&&gems==1&&credits==2&&br_refund_locked);check(br_commit()!=NULL&&charge_calls==1&&gems==1);
 reset();fail_finish=true;check(br_commit()!=NULL);check(br_refund_locked);check(br_commit()!=NULL&&charge_calls==1);
 // Every combination of known UI flags; only a tooltip can accompany these actions.
 for(unsigned flags=0;flags<64;flags++)for(unsigned cmd=1;cmd<=21;cmd++)check(action_ui_blocked(cmd,flags)==((cmd==14||cmd==19||cmd==21)?(flags&~8u)!=0:flags!=0));
 printf("PASS native refund commit fault injection and UI guards (%d checks)\n",checks);
}
