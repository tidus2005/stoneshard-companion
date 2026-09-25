#pragma once
// Use the original inventory right-click menu and exact Flay action.
// Only carried plants in an open backpack are eligible; no ground dispatch.
#ifndef FORAGE_NATIVE_PEEL_DEFINED
#include "inventory_actions.h"
static bool native_peel(RV item){return native_inventory_action(item,"Flay");}
#endif
static double peel_before,peel_source=-1;
// 0 no eligible surplus, 1 dispatched exactly once, -1 refusal.
static int peel_surplus(void){
    if(!inventory_menu_open())return 0;
    if(!forage_rules[13].enabled||!(forage_rules[13].flags&2))return 0;
    if(!forage_inventory())return -1;
    if(forage_totals[13]<=0)return 0;
    RV inventory=find_instance("o_inventory");double owner=member_number(inventory,"id");release_value(&inventory);
    for(int i=0;i<512;i++){
        RV item=indexed_instance("o_inv_slot",i);if(!valid_object(item)){release_value(&item);break;}
        RV name=object_name(item);bool selected=!strcmp(text_value(name),"o_inv_lentil")&&member_number(item,"owner")==owner;release_value(&name);
        double quantity=forage_quantity(item);
        if(selected&&isfinite(quantity)){
            peel_before=forage_totals[14];peel_source=member_number(item,"id");
            bool ok=native_peel(item);release_value(&item);return ok?1:-1;
        }
        release_value(&item);
    }
    return 0;
}
