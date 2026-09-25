#pragma once
#include "stow_catalog.h"
// Operate only on the open container belonging to the equipped back slot.
// Opening the backpack remains a native player action, including its turn cost.
static double stow_tried[512];
static int stow_tried_count;
static uint64_t stow_due,stow_input_seen,stow_scene;
static double stow_bag=-1;
static bool stow_failed;
static double stow_price(RV item){
    if(!inventory_owned(item)||member_number(item,"equipped")==1||member_number(item,"equipped_id")>0||member_number(item,"is_quest")==1||member_number(item,"quest_item")==1||member_number(item,"is_medicine")==1||member_number(item,"is_food")==1||member_number(item,"is_alcohol")==1)return -1;
    RV name=object_name(item);const char* key=text_value(name);
    double price=stow_catalog_price(key);
    if(!strncmp(key,"o_inv_backpack",14)||!strncmp(key,"o_inv_bag",9))price=-1;
    release_value(&name);
    if(price<0)return -1;
    double actual=member_number(item,"base_price");return isfinite(actual)&&actual>0?actual:price;
}
static double stow_equipped_bag(void){
    RV slot=find_instance("o_inv_back"),id=get_member(slot,"children"),bag=instance_from_id(id);
    RV name=object_name(bag);bool ok=valid_object(slot)&&valid_object(bag)&&!strncmp(text_value(name),"o_inv_backpack",14);
    double result=ok?member_number(bag,"id"):-1;
    release_value(&name);release_value(&bag);release_value(&id);release_value(&slot);return result;
}
static RV stow_container(double bag){
    if(bag<0)return numeric(-1);
    for(int i=0;i<64;i++){
        RV c=indexed_instance("o_container_parent",i);if(!valid_object(c)){release_value(&c);break;}
        RV name=object_name(c);bool match=!strncmp(text_value(name),"o_container_backpack",20)&&member_number(c,"parent")==bag&&member_number(c,"is_close")==0;release_value(&name);
        if(match)return c;release_value(&c);
    }
    return numeric(-1);
}
// Native child grids carry their own content restrictions. Small compartments
// precede the main grid; scr_inventory_add still decides whether an item fits.
static int stow_grids(RV container,double* ids){
    RV wrapperId=get_member(container,"itemsContainer"),wrapper=instance_from_id(wrapperId),list=get_member(wrapper,"guiChildrenList");
    int size=safe_list_size(list),count=0;double areas[16];
    if(size>0&&size<=16)for(int i=0;i<size;i++){
        RV id=list_value(list,i),grid=instance_from_id(id);double rows=member_number(grid,"rows"),cols=member_number(grid,"columns");
        if(valid_object(grid)&&member_number(grid,"owner")==member_number(container,"id")&&isfinite(rows)&&isfinite(cols)&&rows>0&&cols>0&&rows<=64&&cols<=64){
            int pos=count++;double area=rows*cols;
            while(pos>0&&areas[pos-1]>area){areas[pos]=areas[pos-1];ids[pos]=ids[pos-1];pos--;}
            areas[pos]=area;ids[pos]=member_number(grid,"id");
        }
        release_value(&grid);release_value(&id);
    }
    release_value(&list);release_value(&wrapper);release_value(&wrapperId);return count;
}
static void reconcile_stow(uint32_t reasons){
    uint64_t now=GetTickCount64();
    if(!(automation_flags&4)||!inventory_menu_open()){
        stow_status=(automation_flags&4)?1:0;stow_tried_count=0;stow_failed=false;stow_due=now+1200;return;
    }
    if(stow_scene!=shared->scene_generation||stow_input_seen!=inventory_input_at){
        stow_scene=shared->scene_generation;stow_input_seen=inventory_input_at;stow_tried_count=0;stow_failed=false;
    }
    if(reasons||!shared->scene_ready||(shared->ui_flags&~9u)||shared->walk_state==WALK_ACTIVE||forage_phase||inventory_pending>=0||now<inventory_input_at+1200){stow_status=3;return;}
    if(stow_failed){stow_status=6;return;}if(now<stow_due)return;
    if((GetAsyncKeyState(VK_LBUTTON)&0x8000)||(GetAsyncKeyState(VK_RBUTTON)&0x8000))return;
    RV player=find_instance("o_player");bool safe=valid_object(player)&&member_number(player,"turn_available")==1&&member_number(player,"movingIsDone")==1&&!walk_threat(player);release_value(&player);
    if(!safe){stow_status=3;return;}
    RV menu=find_instance("o_context_button");bool busy=valid_object(menu);release_value(&menu);if(busy)return;
    double bag=stow_equipped_bag();if(bag!=stow_bag){stow_bag=bag;stow_tried_count=0;}
    RV destination=stow_container(bag);if(!valid_object(destination)){release_value(&destination);stow_status=2;return;}
    double best=-1,price=-1;
    for(int i=0;i<512;i++){
        RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
        double id=member_number(item,"id");bool tried=false;for(int j=0;j<stow_tried_count;j++)if(stow_tried[j]==id){tried=true;break;}
        double p=tried?-1:stow_price(item);if(p>price){price=p;best=id;}release_value(&item);
    }
    if(best<0||stow_tried_count>=512){stow_status=5;release_value(&destination);stow_due=now+500;return;}
    stow_tried[stow_tried_count++]=best;
    RV item=instance_from_id(numeric(best));double grids[16];int count=stow_grids(destination,grids);
    double owner=member_number(destination,"id");bool moved=false;
    for(int i=0;i<count;i++){
        if(stow_price(item)<0)break;
        RV args[3]={numeric(owner),numeric(best),numeric(grids[i])},out=call_script(0x10deed0,item,3,args);double result=number(out);release_value(&out);
        release_value(&item);item=instance_from_id(numeric(best));
        moved=valid_object(item)&&member_number(item,"owner")==owner;
        if(moved)break;
        // Never retry an ambiguous success or a source no longer in inventory.
        if(result==1||!inventory_owned(item)){stow_failed=true;break;}
    }
    if(moved)stow_count++;
    stow_status=stow_failed?6:(count?4:2);stow_due=now+350;telemetry_next=0;
    release_value(&item);release_value(&destination);
}
