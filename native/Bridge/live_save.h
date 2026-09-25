#pragma once
// 0.9.4.25: the same zero-argument script called by o_autosave_trigger Step.
// It queues native scene serialization + screenshot, without sleep or exit.
static int request_live_save(void){
    RV player=find_instance("o_player");
    bool ready=valid_object(player)&&member_number(player,"turn_available")==1&&
        member_number(player,"movingIsDone")==1&&member_number(player,"HP")>0;
    if(!ready||shared->walk_state==WALK_ACTIVE||forage_phase){release_value(&player);return 23;}
    const char* blockers[]={"o_smoothRoomChanger","o_loading","o_gameLoader","o_save_error_panel"};
    for(size_t i=0;i<sizeof(blockers)/sizeof(blockers[0]);i++){
        RV obj=find_instance(blockers[i]);bool exists=valid_object(obj);release_value(&obj);
        if(exists){release_value(&player);return 23;}
    }
    // Reject a changed binary rather than calling an unverified address.
    static const BYTE prolog[]={0x55,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x56,0x57,0x53,0x48,0x81,0xec,0x58,0x01,0,0};
    if(memcmp(game+0xb55530,prolog,sizeof(prolog))){release_value(&player);return 24;}
    RV out=call_script(0xb55530,player,0,NULL);release_value(&out);release_value(&player);
    return 0; // Queued only. The controller must verify new, valid files.
}
