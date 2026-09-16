#pragma once
static bool inventory_menu_open(void){
    RV hud=find_instance("o_modificatorsMenu");bool open=valid_object(hud)&&member_number(hud,"inventoryMenuActive")==1;release_value(&hud);return open;
}
static bool inventory_owned(RV item){
    RV inv=find_instance("o_inventory");double id=member_number(inv,"id");
    bool owned=valid_object(inv)&&valid_object(item)&&isfinite(id)&&member_number(item,"owner")==id;release_value(&inv);return owned;
}
static bool native_inventory_action(RV item,const char* action){
    if(!inventory_menu_open()||!inventory_owned(item))return false;
    RV existing=find_instance("o_context_button");bool occupied=valid_object(existing);release_value(&existing);if(occupied)return false;
    // Let the item's original right-click handler establish inventory/grid context.
    // A synthetic context-menu constructor omits that context.
    RV args[2]={numeric(6),numeric(5)},opened=call_instance_builtin(0x51ebd00,item,2,args);release_value(&opened);
    double id=member_number(item,"id");
    for(int i=0;i<64;i++){
        RV button=indexed_instance("o_context_button",i);if(!valid_object(button)){release_value(&button);break;}
        RV func=get_member(button,"func");bool selected=member_number(button,"interact_id")==id&&!strcmp(text_value(func),action);release_value(&func);
        if(selected&&inventory_owned(item)){
            args[1]=numeric(4);RV out=call_instance_builtin(0x51ebd00,button,2,args);release_value(&out);release_value(&button);return true;
        }
        release_value(&button);
    }
    return false;
}
static bool rotten_berry_key(const char* key){
    static const char* keys[]={"o_inv_whortleberry_rot","o_inv_raspberry_rot","o_inv_blueberry_rot","o_inv_lingonberry_rot","o_inv_gooseberry_rot","o_inv_barberry_rot","o_inv_grape_rot"};
    if(key)for(size_t i=0;i<sizeof(keys)/sizeof(*keys);i++)if(!strcmp(key,keys[i]))return true;
    return false;
}
