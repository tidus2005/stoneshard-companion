#pragma once
// Read-only hover snapshot. Never call attack/skill scripts: they may roll RNG,
// spend a turn, apply modifiers or alter the target. Same event-thread seqlock.
static uint64_t combat_due;
static char combat_buffer[8192];static size_t combat_at;static bool combat_full;
static void cj(const char* s){size_t n=strlen(s);if(combat_at+n>=sizeof(combat_buffer)){combat_full=true;return;}memcpy(combat_buffer+combat_at,s,n+1);combat_at+=n;}
static void cq(const char* s){cj("\"");for(int i=0;s&&s[i]&&i<192;i++){unsigned char c=s[i];char b[8];if(c=='"'||c=='\\'){b[0]='\\';b[1]=c;b[2]=0;cj(b);}else if(c<32){snprintf(b,sizeof(b),"\\u%04x",c);cj(b);}else{b[0]=c;b[1]=0;cj(b);}}cj("\"");}
static void cn(double v){char b[48];if(!isfinite(v)){cj("null");return;}snprintf(b,sizeof(b),"%.12g",v);cj(b);}
static double combat_number(RV unit,const char* key){RV v=live_member(unit,key);double n=number(v);release_value(&v);return n;}
static void combat_name(RV obj){
    RV name=live_member(obj,"name");
    if(!*text_value(name)){release_value(&name);name=live_member(obj,"id_name");}
    if(!*text_value(name)){release_value(&name);name=object_name(obj);}
    cq(text_value(name));release_value(&name);
}
static void combat_stats(RV obj,bool player){
    cj("{");bool first=true;
    // Keep keys shared with the C-panel, plus native unit armor/block fields.
    for(size_t i=0;i<sizeof(stat_keys)/sizeof(*stat_keys);i++){
        const char* key=stat_keys[i];double n=player&&strcmp(key,"Block_Power")?character_stat(key):combat_number(obj,key);
        if(!isfinite(n))n=combat_number(obj,key);
        if(!isfinite(n))continue;if(!first)cj(",");first=false;cq(key);cj(":");cn(n);
    }
    const char* extra[]={"DEF","ArmorDurability","Block_PowerMax","is_range","is_mage"};
    for(int i=0;i<5;i++){double n=combat_number(obj,extra[i]);if(!isfinite(n))continue;if(!first)cj(",");first=false;cq(extra[i]);cj(":");cn(n);}cj("}");
}
static void combat_buffs(RV obj){
    RV list=live_member(obj,"buffs");int count=safe_list_size(list);bool complete=count>=0&&count<=24;
    cj("[ ");bool first=true;
    for(int i=0;i<count&&i<24;i++){
        if(combat_at>sizeof(combat_buffer)-280){complete=false;break;}
        RV id=list_value(list,i),buff=instance_from_id(id);release_value(&id);
        if(!valid_object(buff)){complete=false;release_value(&buff);continue;}
        if(!first)cj(",");first=false;cj("{\"name\":");combat_name(buff);
        cj("}");release_value(&buff);
    }
    release_value(&list);cj("],\"buffsComplete\":");cj(complete?"true":"false");
}
static void refresh_combat(bool play,RV player){
    uint64_t now=GetTickCount64();DWORD foreground=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground);
    uint64_t beat=(uint64_t)InterlockedCompareExchange64(&shared->heartbeat,0,0);
    if(!shared->combat_enabled||!play||!shared->scene_ready||(shared->ui_flags&~8u)||foreground!=GetCurrentProcessId()||now>beat+1500){shared->combat_response[0]=0;combat_due=0;return;}
    if(now<combat_due)return;combat_due=now+120;shared->combat_response[0]=0;
    POINT cursor,origin={0};RECT client;
    if(!GetCursorPos(&cursor)||!GetClientRect(game_window,&client)||!ClientToScreen(game_window,&origin)||cursor.x<origin.x||cursor.y<origin.y||cursor.x>=origin.x+client.right||cursor.y>=origin.y+client.bottom)return;
    double mx=builtin_var(0x5294590),my=builtin_var(0x52945c0);
    RV floor=find_instance("o_floor_target");bool fog=member_number(floor,"is_in_fog")!=0;release_value(&floor);
    if(fog||!isfinite(mx)||!isfinite(my))return;
    RV key=string_value("o_unit"),asset=call_builtin(0x5336680,1,&key);
    if(!isfinite(number(asset))||number(asset)<0){release_value(&asset);return;}
    RV args[5]={numeric(mx),numeric(my),asset,numeric(0),numeric(0)};
    RV id=call_builtin(0x51f5e70,5,args),target=instance_from_id(id);release_value(&id);release_value(&asset);
    double target_id=member_number(target,"id");
    // Missing visibility information is not permission to expose hidden units.
    if(!valid_object(target)||target_id==member_number(player,"id")||combat_number(target,"is_life")!=1||combat_number(target,"isVisible")!=1){release_value(&target);return;}
    combat_at=0;combat_full=false;combat_buffer[0]=0;
    cj("{\"at\":");cn(now);cj(",\"scene\":");cn(shared->scene_generation);
    cj(",\"cursorX\":");cn(cursor.x);cj(",\"cursorY\":");cn(cursor.y);
    cj(",\"targetId\":");cn(target_id);cj(",\"name\":");combat_name(target);
    cj(",\"distance\":");cn(fmax(fabs(member_number(player,"x")-member_number(target,"x")),fabs(member_number(player,"y")-member_number(target,"y")))/26);
    RV active=get_global("skill_activate"),selection=get_global("skill_select");
    // Selection can be an instance ID or a boolean mode flag. Only IDs resolve.
    RV skill=instance_from_id(selection);if(!valid_object(skill)){release_value(&skill);skill=instance_from_id(active);}
    cj(",\"skillSelected\":");cj(valid_object(skill)||number(active)>0||number(selection)>0?"true":"false");
    cj(",\"skillName\":");if(valid_object(skill))combat_name(skill);else cq("");
    release_value(&skill);release_value(&active);release_value(&selection);
    cj(",\"player\":");combat_stats(player,true);cj(",\"target\":");combat_stats(target,false);
    cj(",\"buffs\":");combat_buffs(target);cj("}");release_value(&target);
    if(!combat_full)memcpy(shared->combat_response,combat_buffer,combat_at+1);
}
