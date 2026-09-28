#pragma once
// These adapters are supplied by build_refund.h. Keeping the commit ordering
// separate lets fault-injection tests exercise the production transaction.
static bool br_mutated,br_refund_locked;
static const char* br_commit(void){
    if(br_refund_locked)return "上次退点结果异常，本次未执行；请退出游戏并核对保险备份后再使用";
    br_mutated=true;
    br_points_set(true);
    if(!br_points_match(true)){
        br_points_set(false);
        if(br_points_match(false)){br_mutated=false;return "点数写入未通过核对，已撤回点数修改；未消耗宝石或历练";}
        br_refund_locked=true;return "点数撤回未通过核对，未消耗宝石；退点已锁定，请使用保险备份恢复";
    }
    // Never destroy a gem until the allocation and returned point are verified.
    if(!br_cost_commit()){
        br_refund_locked=true;return "已返还点数，但材料或历练扣除异常；退点已锁定，请勿重试并核对保险备份";
    }
    if(!br_finish_commit()||!br_points_match(true)){
        br_refund_locked=true;return "退点最终核对异常；退点已锁定，请勿重试并核对保险备份";
    }
    return NULL;
}
