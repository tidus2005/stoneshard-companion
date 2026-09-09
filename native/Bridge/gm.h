#pragma once
typedef bool (*VarGetter)(void*,int,RV*);
typedef void (*ObjectEvent)(void*,void*);
typedef RV* (*GameScript)(void*,void*,RV*,int,RV**);
static void* global_scope;
static void init_gm(void);
static void release_value(RV* v){((void(*)(RV*))(game+0x1980))(v);v->kind=5;v->ptr=NULL;}
static RV call_builtin(uintptr_t offset,int count,RV* args){RV r={.kind=5};((Builtin)(game+offset))(&r,global_scope,global_scope,count,args);return r;}
static double number(RV r){switch(r.kind&0xffffff){case 0:case 13:return r.real;case 7:return (int32_t)r.integer;case 10:return (double)r.integer;case 15:return (int32_t)r.integer;default:return NAN;}}
static const char* text_value(RV r){return (r.kind&0xffffff)==1 && r.ptr ? *(const char**)r.ptr : "";}
typedef struct CachedString {const char* name;RV value;} CachedString;
static CachedString string_cache[512];static size_t string_count;
static RV string_value(const char* s){
    for(size_t i=0;i<string_count;i++)if(!strcmp(string_cache[i].name,s))return string_cache[i].value;
    if(string_count>=512)return (RV){.kind=5};
    CachedString* c=&string_cache[string_count++];c->name=s;c->value=(RV){.kind=5};
    ((void(*)(RV*,const char*))(game+0x51b2890))(&c->value,s);return c->value;
}
static RV get_global(const char* name){RV s=string_value(name);if(number(call_builtin(0x51edb00,1,&s))!=1)return (RV){.kind=5};return call_builtin(0x51edbd0,1,&s);}
static bool set_global(const char* name,RV value){RV s=string_value(name);if(number(call_builtin(0x51edb00,1,&s))!=1)return false;RV args[2]={s,value};call_builtin(0x51edca0,2,args);return true;}
static RV instance_from_id(RV id){if(!isfinite(number(id))||number(id)<0)return (RV){.kind=5};if(number(call_builtin(0x51f28b0,1,&id))!=1)return (RV){.kind=5};return call_builtin(0x5211030,1,&id);}
static RV find_instance(const char* name){
    RV s=string_value(name),a=call_builtin(0x5336680,1,&s);
    if(number(a)<0 || !isfinite(number(a)))return (RV){.kind=5};
    RV args[2]={a,numeric(0)},id=call_builtin(0x51f2980,2,args);
    if(number(id)<0 || !isfinite(number(id)))return (RV){.kind=5};
    return call_builtin(0x5211030,1,&id);
}
static bool has_member(RV obj,const char* name){RV args[2]={obj,string_value(name)};return number(call_builtin(0x51edd30,2,args))==1;}
static RV get_member(RV obj,const char* name){
    if((obj.kind&0xffffff)!=6 || !obj.ptr || !has_member(obj,name))return (RV){.kind=5};
    RV args[2]={obj,string_value(name)};return call_builtin(0x51edfa0,2,args);
}
static bool set_member(RV obj,const char* name,RV value){
    if(!has_member(obj,name))return false;
    RV args[3]={obj,string_value(name),value};call_builtin(0x51ee370,3,args);return true;
}
static RV call_script(uintptr_t address,RV self,int count,RV* values){
    RV out={.kind=5};RV* args[4]={0};
    if((self.kind&0xffffff)!=6||!self.ptr||count>4)return out;
    for(int i=0;i<count;i++)args[i]=values+i;
    ((GameScript)(game+address))(self.ptr,self.ptr,&out,count,args);return out;
}
static RV call_instance_builtin(uintptr_t address,RV self,int count,RV* values){
    RV out={.kind=5};
    if((self.kind&0xffffff)==6&&self.ptr)((Builtin)(game+address))(&out,self.ptr,self.ptr,count,values);
    return out;
}
static double builtin_var(uintptr_t address){RV r={.kind=5};((VarGetter)(game+address))(global_scope,0,&r);return number(r);}
static double member_number(RV obj,const char* name){RV r=get_member(obj,name);double n=number(r);release_value(&r);return n;}
static double global_number(const char* name){RV r=get_global(name);double n=number(r);release_value(&r);return n;}
static bool valid_object(RV v){return (v.kind&0xffffff)==6 && v.ptr;}
static double centered_camera_id=-1,old_free_camera,center_player_x,center_player_y;
static bool auto_center;
static double observed_camera_id=-1;
static uint64_t scene_ready_since;
static uint64_t playable_since;
static RV visor_helmet(void){RV slot=find_instance("o_inv_head");if(!valid_object(slot))return (RV){.kind=5};RV child=get_member(slot,"children");RV result=instance_from_id(child);release_value(&child);return result;}
static int visor_state(void){RV helmet=visor_helmet();if(!valid_object(helmet)||!has_member(helmet,"visorSwitch"))return -1;double n=member_number(helmet,"isOpen");return n==1?1:n==0?0:-1;}
static bool scene_objects(RV* player,RV* camera){*player=find_instance("o_player");*camera=find_instance("oCamera");return valid_object(*player)&&valid_object(*camera)&&has_member(*camera,"freeCamera");}
static void update_view(RV camera){
    double w=global_number("cameraWidth"),h=global_number("cameraHeight");
    double x=member_number(camera,"x")-w/2,y=member_number(camera,"y")-h/2;
    if(!isfinite(x)||!isfinite(y)||w<=0||h<=0)return;
    // The original script updates the world camera and its companion surfaces.
    RV values[3]={numeric(x),numeric(y),numeric(1)},out={.kind=5};RV* args[3]={values,values+1,values+2};
    ((GameScript)(game+0x1b76d60))(camera.ptr,camera.ptr,&out,3,args);release_value(&out);
}
static bool restore_camera(bool force_player){
    RV player,camera;if(!scene_objects(&player,&camera)){centered_camera_id=-1;return false;}
    double id=member_number(camera,"id");
    if(!force_player && id!=centered_camera_id){centered_camera_id=-1;return true;}
    set_member(camera,"freeCamera",numeric(force_player?0:old_free_camera));
    double x=member_number(player,"draw_x"),y=member_number(player,"draw_y");
    if(!isfinite(x))x=member_number(player,"x");if(!isfinite(y))y=member_number(player,"y");
    set_member(camera,"x",numeric(x));set_member(camera,"y",numeric(y));
    centered_camera_id=-1;update_view(camera);return true;
}
static bool center_camera(void){
    RV player,camera;if(!scene_objects(&player,&camera))return false;
    double w=builtin_var(0x5295690),h=builtin_var(0x5295590);
    if(!isfinite(w)||!isfinite(h)||w<=0||h<=0)return false;
    double id=member_number(camera,"id");if(id!=centered_camera_id)old_free_camera=member_number(camera,"freeCamera");
    if(!isfinite(old_free_camera))return false;
    centered_camera_id=id;center_player_x=member_number(player,"x");center_player_y=member_number(player,"y");
    set_member(camera,"freeCamera",numeric(1));set_member(camera,"x",numeric(w/2));set_member(camera,"y",numeric(h/2));
    update_view(camera);return true;
}
static int toggle_visor(void){
    RV player,camera;if(!scene_objects(&player,&camera))return 5;
    RV button=find_instance("o_visor_toggle"),helmet=visor_helmet();
    if(!valid_object(button)||!valid_object(helmet)||!has_member(helmet,"visorSwitch"))return 6;
    double previous=member_number(helmet,"isOpen");if(previous!=0&&previous!=1)return 6;
    // Same guarded user event as the native visor button. No item attributes are fabricated.
    ((ObjectEvent)(game+0x32d4520))(button.ptr,button.ptr);
    bool changed=member_number(helmet,"isOpen")!=previous;
    return changed?0:7;
}
static bool any_visible_gui(const char* parent){
    RV name=string_value(parent),asset=call_builtin(0x5336680,1,&name);
    if(!isfinite(number(asset))||number(asset)<0)return false;
    for(int i=0;i<128;i++){
        RV args[2]={asset,numeric(i)},id=call_builtin(0x51f2980,2,args);
        if(!isfinite(number(id))||number(id)<0)break;
        RV obj=instance_from_id(id);
        if(member_number(obj,"guiVisible")==1 || member_number(obj,"active")==1)return true;
    }
    return false;
}
static double character_stat(const char* key){
    RV map=get_global("characterDataMap");if(!isfinite(number(map)))return NAN;
    RV args[2]={map,string_value(key)};
    if(number(call_builtin(0x51e3f20,2,args))!=1){release_value(&map);return NAN;}
    RV value=call_builtin(0x51e4510,2,args);double result=number(value);release_value(&value);release_value(&map);return result;
}
static void refresh_vitals(bool play){
    shared->vital_valid=0;
    const char* keys[]={"Hunger","Thirsty","Pain","Intoxication"};
    double* fields[]={&shared->hunger,&shared->thirst,&shared->pain,&shared->intoxication};
    for(int i=0;i<4;i++){
        double value=play?character_stat(keys[i]):NAN;
        bool valid=isfinite(value)&&value>=0&&value<=100;
        *fields[i]=valid?value:0;if(valid)shared->vital_valid|=1u<<i;
    }
}
static uint32_t blocking_ui(void){
    // Native menu visibility snapshot maintained by the original HUD controller.
    RV menu=find_instance("o_modificatorsMenu");
    const char* flags[]={"inventoryMenuActive","characterMenuActive","skillMenuActive","tradeMenuActive","stashLeftMenuActive","stashRightMenuActive","journalActive","mapActive","escMenuActive","fullscreenMenuActive","cookingMenuActive","exploreMenuActive","bookActive"};
    uint32_t result=0;
    for(int i=0;i<13;i++)if(member_number(menu,flags[i])==1)result|=i<2?1:32;
    if(valid_object(find_instance("o_dialogue")))result|=2;
    if(any_visible_gui("o_confirm_panel"))result|=4;
    if(valid_object(find_instance("o_hoverRenderContent")))result|=8;
    // The full-screen world map leaves o_modificatorsMenu.mapActive at zero.
    // Its own instance exists only while open (including the journal map).
    RV world_map=find_instance("o_globalmap");
    if(valid_object(world_map))result|=16;
    release_value(&world_map);
    return result;
}
#include "supplies.h"
#include "telemetry.h"
static void refresh_scene(void){
    if(!global_scope)init_gm();
    RV player,camera;bool play=scene_objects(&player,&camera);
    shared->capabilities=1|(play?2|64:0)|(play&&visor_state()>=0?4:0);
    shared->visor_state=play?visor_state():-1;
    shared->map_w=builtin_var(0x5295690);shared->map_h=builtin_var(0x5295590);
    shared->camera_w=global_number("cameraWidth");shared->camera_h=global_number("cameraHeight");
    shared->camera_x=global_number("cameraX");shared->camera_y=global_number("cameraY");
    shared->player_x=play?member_number(player,"x"):0;shared->player_y=play?member_number(player,"y"):0;
    shared->gui_width=number(call_builtin(0x52aed20,0,NULL));
    shared->gui_height=number(call_builtin(0x52aece0,0,NULL));
    shared->ui_flags=blocking_ui();
    refresh_vitals(play);
    if(!play){centered_camera_id=-1;observed_camera_id=-1;scene_ready_since=0;playable_since=0;}
    else {
        double id=member_number(camera,"id");
        if(centered_camera_id==id&&(shared->player_x!=center_player_x||shared->player_y!=center_player_y))restore_camera(false);
        if(observed_camera_id!=id){observed_camera_id=id;scene_ready_since=GetTickCount64();playable_since=scene_ready_since;shared->scene_generation++;}
        DWORD foreground_pid=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground_pid);
        if(auto_center&&!shared->ui_flags&&foreground_pid==GetCurrentProcessId()&&scene_ready_since&&GetTickCount64()-scene_ready_since>=600){center_camera();scene_ready_since=0;}
    }
    shared->camera_mode=centered_camera_id>=0?1:0;
    shared->auto_center=auto_center?1:0;
    shared->scene_ready=play&&playable_since&&GetTickCount64()-playable_since>=600;
    refresh_supplies(play,player);
    refresh_telemetry(play,player);
}
// Read-only development diagnostic. Enumerate variable names through the runner,
// never infer a field from a numerical offset or keep dynamic string pointers.
static void diagnostic_value(char*,size_t,const char*,RV);
static void inspect_variables(double page){
    if(page>=1000&&page<2000){
        RV map=get_global("characterDataMap");char* out=shared->diagnostic;out[0]=0;
        if(!isfinite(number(map)))return;
        RV key=call_builtin(0x51e3ff0,1,&map);int first=((int)page-1000)*28;
        for(int i=0;i<2000&&(key.kind&0xffffff)!=5;i++){
            RV args[2]={map,key};
            if(i>=first&&i<first+28){RV value=call_builtin(0x51e4510,2,args);diagnostic_value(out,sizeof(shared->diagnostic),text_value(key),value);release_value(&value);}
            RV next=call_builtin(0x51e4270,2,args);release_value(&key);key=next;
            if(i>=first+28)break;
        }
        release_value(&key);release_value(&map);return;
    }
    bool globals=page<0;int first=(int)(globals?-page-1:page)*28;
    RV obj=globals?(RV){.ptr=global_scope,.kind=6}:find_instance("o_player");
    if(page>=10000){int encoded=(int)page-10000;RV args[2]={numeric(encoded/100),numeric(0)};obj=instance_from_id(call_builtin(0x51f2980,2,args));first=(encoded%100)*28;}
    char* out=shared->diagnostic;out[0]=0;if(!valid_object(obj))return;
    RV names=call_builtin(0x51ee150,1,&obj);
    if((names.kind&0xffffff)!=2){release_value(&names);return;}
    double count=number(call_builtin(0x51d2250,1,&names));
    snprintf(out,sizeof(shared->diagnostic),"%s count=%.0f page=%d\n",globals?"global":"player",count,first/28);
    for(int i=first;i<count&&i<first+28;i++){
        RV args[2]={names,numeric(i)},name=call_builtin(0x51d1630,2,args);
        RV get_args[2]={obj,name},v=call_builtin(globals?0x51edbd0:0x51edfa0,globals?1:2,globals?&name:get_args);
        diagnostic_value(out,sizeof(shared->diagnostic),text_value(name),v);
        release_value(&v);release_value(&name);
    }
    release_value(&names);
}
static void diagnostic_value(char* buf,size_t capacity,const char* label,RV value){
    size_t at=strlen(buf);if(at>=capacity-100)return;
    int k=value.kind&0xffffff;
    if(k==5 || k==0xffffff)return;
    if(k==1)snprintf(buf+at,capacity-at,"%s=\"%.60s\"; ",label,text_value(value));
    else if(k==6)snprintf(buf+at,capacity-at,"%s=object:%p; ",label,value.ptr);
    else snprintf(buf+at,capacity-at,"%s=%.2f(k%d); ",label,number(value),k);
}
static void collect_diagnostic(void){
    const char* objects[]={"o_player","oCamera","o_cameraController","o_camera","o_camera_target","o_visor_toggle","o_inv_head"};
    const char* fields[]={"id","x","y","cameraMain","cameraCurrent","cameraX","cameraY","cameraWidth","cameraHeight","cameraScale","camera_target","target","target_x","target_y","camera_x","camera_y","view_x","view_y","view_w","view_h","centerCamera","returnCamera","return_camera","freeCamera","devCamera","edgeCamera","enabled","active","can_use","canUse","isOpen","is_open","visorSwitch","image_index","children","is_load","helm","helmet","head","item","usable"};
    char* out=shared->diagnostic;out[0]=0;
    for(size_t i=0;i<sizeof(objects)/sizeof(objects[0]);i++){
        RV obj=find_instance(objects[i]);if((obj.kind&0xffffff)!=6||!obj.ptr)continue;
        size_t at=strlen(out);snprintf(out+at,sizeof(shared->diagnostic)-at,"\n%s: ",objects[i]);
        for(size_t j=0;j<sizeof(fields)/sizeof(fields[0]);j++){RV v=get_member(obj,fields[j]);diagnostic_value(out,sizeof(shared->diagnostic),fields[j],v);release_value(&v);}
    }
    size_t at=strlen(out);snprintf(out+at,sizeof(shared->diagnostic)-at,"\nglobal: ");
    for(size_t j=3;j<sizeof(fields)/sizeof(fields[0]);j++){RV v=get_global(fields[j]);diagnostic_value(out,sizeof(shared->diagnostic),fields[j],v);release_value(&v);}
    RV helm=visor_helmet();diagnostic_value(out,sizeof(shared->diagnostic),"equipped_helmet",helm);
    if(valid_object(helm)){const char* extra[]={"visorSwitch","idName","isOpen","i_index_shift"};for(int i=0;i<4;i++){RV v=get_member(helm,extra[i]);diagnostic_value(out,sizeof(shared->diagnostic),extra[i],v);release_value(&v);}}
    at=strlen(out);snprintf(out+at,sizeof(shared->diagnostic)-at,"\nroom=%.0fx%.0f fps_real=%.2f",builtin_var(0x5295690),builtin_var(0x5295590),builtin_var(0x5293e70));
}
static void init_gm(void){global_scope=call_builtin(0x52110c0,0,NULL).ptr;}
