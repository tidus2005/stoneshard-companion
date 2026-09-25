#pragma once
// Installed 0.9.4.25 consum_stat_data: Cat/Subcat herb or berry, fodder > 0.
// Curated defaults exclude rhubarb/lentil cooking ingredients from the 19 native rows.
// Match full inventory object names; medicine, meals and unsupported herbs stay.
// Yield still comes from the live item's fodder_value and the original recipe.
static bool forage_material(const char* key){
    static const char* keys[]={
        "o_inv_whortleberry","o_inv_raspberry","o_inv_blueberry",
        "o_inv_lingonberry","o_inv_gooseberry","o_inv_barberry","o_inv_grape",
        "o_inv_fleawort","o_inv_agrimony","o_inv_bogbean",
        "o_inv_burdock","o_inv_mindwort","o_inv_peppermint","o_inv_thyme",
        "o_inv_burnet","o_inv_wormwood","o_inv_lavender"
    };
    if(!key)return false;
    for(size_t i=0;i<sizeof(keys)/sizeof(*keys);i++)if(!strcmp(key,keys[i]))return true;
    return false;
}
