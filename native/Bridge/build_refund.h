#pragma once
// All reads and writes run on the game's event thread. No saved-file edits.
static char br_debug[200];
static size_t br_at;static bool br_full,br_bad;
static void bj(const char* s){size_t n=strlen(s);if(br_at+n>=sizeof(shared->build_response)){br_full=true;return;}memcpy(shared->build_response+br_at,s,n+1);br_at+=n;}
static void bq(const char* s){bj("\"");for(int i=0;s&&s[i]&&i<256;i++){unsigned char c=s[i];char b[8];if(c=='"'||c=='\\'){b[0]='\\';b[1]=c;b[2]=0;bj(b);}else if(c<32){snprintf(b,sizeof(b),"\\u%04x",c);bj(b);}else{b[0]=c;b[1]=0;bj(b);}}bj("\"");}
static void bn(double n){char b[40];if(!isfinite(n)){bj("null");return;}snprintf(b,sizeof(b),"%.12g",n);bj(b);}
static int ba_size(RV a){if((a.kind&0xffffff)!=2)return -1;RV n=call_builtin(0x51d2250,1,&a);int count=(int)number(n);release_value(&n);return count>=0&&count<=512?count:-1;}
static RV ba_get(RV a,int i){RV args[2]={a,numeric(i)};return call_builtin(0x51d1630,2,args);}
typedef struct BrNode{RV value,icon;int skill,learned,category;char key[128];}BrNode;
static BrNode br_nodes[1024];static int br_count;static char br_character[40];static int br_values[8],br_gold,br_base[5];
static const char* br_attrs[]={"STR","AGL","PRC","Vitality","WIL","AP","SP","LVL"};
static uint64_t br_hash;
static void br_hash_value(uint64_t n){for(int i=0;i<8;i++){br_hash^=(unsigned char)(n>>(i*8));br_hash*=1099511628211ULL;}}
static void br_release(void){for(int i=0;i<br_count;i++){release_value(&br_nodes[i].value);release_value(&br_nodes[i].icon);}br_count=0;}
static int br_find(RV v){for(int i=0;i<br_count;i++)if(br_nodes[i].value.ptr==v.ptr)return i;return -1;}
static void br_add(RV v,int category,bool point){
    if(br_find(v)>=0){release_value(&v);return;}if(!valid_object(v)||br_count==1024){snprintf(br_debug,sizeof(br_debug),"node c%d kind%d count%d",category,v.kind,br_count);br_bad=true;release_value(&v);return;}
    BrNode* n=&br_nodes[br_count++];memset(n,0,sizeof(*n));n->value=v;n->icon=(RV){.kind=5};n->skill=-1;n->category=category;
    if(point){RV id=live_member(v,"skill");n->icon=instance_from_id(id);release_value(&id);RV key=object_name(n->icon);snprintf(n->key,sizeof(n->key),"%s",text_value(key));release_value(&key);double asset=member_number(n->icon,"object_index"),learned=member_number(n->icon,"is_open");
        if(!valid_object(n->icon)||!isfinite(asset)||(learned!=0&&learned!=1)||!n->key[0]){snprintf(br_debug,sizeof(br_debug),"icon c%d asset%.0f learned%.0f key%s",category,asset,learned,n->key);br_bad=true;}else{n->skill=(int)asset;n->learned=(int)learned;}}
}
static int br_money(void){int total=0;const char* keys[]={"o_inv_gold","o_inv_moneybag"};for(int k=0;k<2;k++)for(int i=0;i<128;i++){RV item=indexed_instance(keys[k],i);if(!valid_object(item)){release_value(&item);break;}if(i==127)br_bad=true;if(inventory_owned(item)){double stack=member_number(item,"stack");if(k==1){RV data=get_member(item,"data"),args[2]={data,string_value("Stack")},v=call_builtin(0x51e4510,2,args);stack=number(v);release_value(&v);release_value(&data);}if(!isfinite(stack)||stack<0||stack>1000000||floor(stack)!=stack){snprintf(br_debug,sizeof(br_debug),"gold k%d stack%g",k,stack);br_bad=true;}else{total+=(int)stack;br_hash_value((uint64_t)member_number(item,"id"));br_hash_value((uint64_t)stack);}}release_value(&item);}return total;}
#include "refund_materials.h"
static bool br_capture(void){
    br_release();br_bad=false;br_hash=14695981039346656037ULL;
    RV player=find_instance("o_player");if(!valid_object(player)){release_value(&player);return false;}br_hash_value((uint64_t)member_number(player,"id"));release_value(&player);
    RV map=get_global("characterDataMap"),args[2]={map,string_value("nameKey")},name=call_builtin(0x51e4510,2,args);snprintf(br_character,sizeof(br_character),"%s",text_value(name));release_value(&name);release_value(&map);
    const char* names[]={"Arna","Jorgrim","Dirwin","Jonna","Velmir"};const int bases[][5]={{11,11,10,11,10},{11,10,11,11,10},{10,11,11,11,10},{10,10,11,11,11},{11,11,11,10,10}};int c=-1;for(int i=0;i<5;i++)if(!strcmp(names[i],br_character))c=i;if(c<0)return false;memcpy(br_base,bases[c],sizeof(br_base));
    for(int i=0;i<8;i++){double v=character_stat(br_attrs[i]);if(!isfinite(v)||v<0||v>1000||floor(v)!=v)return false;br_values[i]=(int)v;br_hash_value((uint64_t)v);}for(int i=0;i<5;i++)if(br_values[i]<br_base[i]||br_values[i]>30)return false;
    br_gold=br_money();br_hash_value(shared->scene_generation);
    if(!br_progress(true)||!br_materials_capture()){snprintf(br_debug,sizeof(br_debug),"materials or contract ledger");return false;}br_safe_place=br_location();br_hash_value(br_ledger);br_hash_value(br_safe_place);
    for(int c=0;c<32;c++){RV cat=indexed_instance("o_skill_category",c);if(!valid_object(cat)){release_value(&cat);break;}if(c==31)br_bad=true;RV id=live_member(cat,"connectionsRender"),render=instance_from_id(id);RV available=live_member(cat,"skill");bool empty=ba_size(available)==0;if(ba_size(available)==1){RV first=ba_get(available,0);empty=number(first)==-4||!strcmp(text_value(first),"1");release_value(&first);}release_value(&available);release_value(&id);release_value(&cat);if(empty&&!valid_object(render)){release_value(&render);continue;}
        if(!valid_object(render)){snprintf(br_debug,sizeof(br_debug),"render c%d",c);release_value(&render);br_bad=true;break;}
        const char* keys[]={"pointsArray","linesArray"};for(int k=0;k<2;k++){RV array=live_member(render,keys[k]);int count=ba_size(array);if(count<0){snprintf(br_debug,sizeof(br_debug),"array c%d k%d kind%d",c,k,array.kind);br_bad=true;}for(int i=0;i<count;i++)br_add(ba_get(array,i),c,k==0);release_value(&array);}release_value(&render);
    }
    for(int i=0;i<br_count;i++)if(br_nodes[i].skill>=0){br_hash_value(br_nodes[i].skill);br_hash_value(br_nodes[i].learned);}
    return !br_bad&&br_count>0;
}
#include "refund_graph.h"
static bool br_fixed(const char* key){const char* keys[]={"o_skill_butchering_ico","o_skill_craft_ico","o_pass_skill_Sudden_Attacks","o_skill_trap_search_ico","o_skill_torch_strike_ico"};for(int i=0;i<5;i++)if(!strcmp(key,keys[i]))return true;return false;}
static const char* br_skill_reason(int at){BrNode* n=&br_nodes[at];if(!n->learned)return "尚未学习";if(br_fixed(n->key))return "基础动作不可退点";
    // Dirwin's survival milestone grants attribute points. Do not mint points by
    // refunding that tree until its trait reversal has been verified.
    if(!strcmp(br_character,"Dirwin")&&n->category==17)return "德温生存树涉及额外属性点，暂不支持退回";
    int blocker=br_blocker(at);if(br_bad)return "技能依赖数据未通过校验";if(blocker>=0)return br_nodes[blocker].key;return "";
}
// Native attribute unlock threshold sums the listed attributes above 10.
// Preserve an already satisfied threshold conservatively when taking points back.
static const char* br_attribute_reason(int attr){
    if(br_values[attr]<=br_base[attr])return "已达角色初始值";
    for(int i=0;i<br_count;i++)if(br_nodes[i].skill>=0&&br_nodes[i].learned&&!br_fixed(br_nodes[i].key)){
        RV icon=br_nodes[i].icon;double threshold=member_number(icon,"attributes_value_to_open"),level=member_number(icon,"level_to_open");
        if(!isfinite(threshold)||threshold<=0||(level>0&&br_values[7]>=level))continue;
        RV names=live_member(icon,"attributes_names_to_open");int n=ba_size(names),sum=0,weight=0;
        if(n<0||n>5){release_value(&names);return "技能属性门槛未知，暂不退回此属性";}
        for(int j=0;j<n;j++){RV name=ba_get(names,j);int a=-1;for(int k=0;k<5;k++)if(!strcmp(text_value(name),br_attrs[k]))a=k;
            release_value(&name);if(a<0){release_value(&names);return "技能属性门槛未知，暂不退回此属性";}sum+=br_values[a]-10;if(a==attr)weight++;}
        release_value(&names);if(weight&&sum>=threshold&&sum-weight<threshold)return br_nodes[i].key;
    }return "";
}
static void build_read(int unused){(void)unused;br_at=0;br_full=false;shared->build_response[0]=0;if(!br_capture()){bj("{\"error\":\"角色技能树读取失败\",\"nodes\":");bn(br_count);bj(",\"character\":");bq(br_character);bj(",\"invalid\":");bn(br_bad);bj(",\"debug\":");bq(br_debug);bj("}");br_release();return;}
    bj("{\"character\":");bq(br_character);char token[24];snprintf(token,sizeof(token),"%016llx",(unsigned long long)br_hash);bj(",\"token\":");bq(token);bj(",\"crowns\":");bn(br_gold);bj(",\"credits\":");bn(br_credit);bj(",\"contracts\":");bn(br_contracts);bj(",\"ledger\":");bn(br_ledger);bj(",\"safePlace\":");bj(br_safe_place?"true":"false");bj(",\"place\":");bq(br_place);bj(",\"gems\":[");for(int g=0;g<BR_GEM_COUNT;g++){if(g)bj(",");bn(br_gems[g]);}bj("],\"attributeReasons\":[");for(int a=0;a<5;a++){if(a)bj(",");bq(br_attribute_reason(a));}bj("],\"attributes\":[");for(int i=0;i<8;i++){if(i)bj(",");bn(br_values[i]);}bj("],\"baseline\":[");for(int i=0;i<5;i++){if(i)bj(",");bn(br_base[i]);}bj("],\"skills\":[");bool first=true;
    for(int i=0;i<br_count;i++){BrNode* n=&br_nodes[i];if(n->skill<0)continue;if(!first)bj(",");first=false;bj("{\"id\":");bn(n->skill);bj(",\"key\":");bq(n->key);bj(",\"name\":");RV label=live_member(n->icon,"name");bq(text_value(label));release_value(&label);bj(",\"category\":");bn(n->category);bj(",\"learned\":");bj(n->learned?"true":"false");bj(",\"reason\":");bq(br_skill_reason(i));bj("}");}bj("]}");if(br_full||br_bad)strcpy(shared->build_response,"{\"error\":\"技能依赖数据不完整，未开放退点\"}");br_release();
}
static void br_set_stat(const char* key,int n){RV args[2]={string_value(key),numeric(n)},player=find_instance("o_player"),out=call_script(0xfc2a50,player,2,args);release_value(&out);release_value(&player);}
static int br_list_count(RV list){double id=number(list);if(!isfinite(id)||id<0||floor(id)!=id)return -1;RV a[2]={list,numeric(2)},exists=call_builtin(0x51dfef0,2,a);bool ok=number(exists)==1;release_value(&exists);if(!ok)return -1;RV n=call_builtin(0x51e3260,1,&list);int count=(int)number(n);release_value(&n);return count>=0&&count<=128?count:-1;}
static RV br_list_get(RV list,int i){RV a[2]={list,numeric(i)};return call_builtin(0x51e28f0,2,a);}
// The actual native quickbar is o_skill_fast_panel. Reject absent/invalid
// lists before invoking a GameMaker builtin (undefined IDs abort the runner).
static bool br_hotbar(RV icon,bool write){RV panel=find_instance("o_skill_fast_panel");if(!valid_object(panel)){release_value(&panel);return false;}RV lists=live_member(panel,"skills");int n=br_list_count(lists);double child=member_number(icon,"child_skill");bool ok=n>=0&&(isfinite(child)||member_number(icon,"passive")==1);for(int i=0;i<n;i++){RV list=br_list_get(lists,i);int m=br_list_count(list);if(m<0)ok=false;for(int j=0;j<m;j++){RV v=br_list_get(list,j);if(write&&isfinite(child)&&child>=0&&number(v)==child){RV args[3]={list,numeric(j),numeric(-4)},out=call_builtin(0x51e2f00,3,args);release_value(&out);}release_value(&v);}release_value(&list);}release_value(&lists);release_value(&panel);return ok;}
static bool br_mutated;
static const char* build_refund(const char* payload){
    br_mutated=false;
    unsigned long long expected=0;char type=0,tail=0;int id=-1,recipe=-1;if(sscanf(payload,"%16llx|%c|%d|%d%c",&expected,&type,&id,&recipe,&tail)!=4)return "退点请求无效";
    RV player=find_instance("o_player");bool ready=valid_object(player)&&member_number(player,"turn_available")==1&&member_number(player,"movingIsDone")==1&&member_number(player,"HP")>0&&!walk_threat(player)&&shared->walk_state!=WALK_ACTIVE&&!forage_phase;
    release_value(&player);if(!ready)return "请停步，离开战斗并等待回合结束";
    if(!br_capture()){br_release();return "当前角色数据不可用";}const char* error=NULL;int at=-1;
    if(br_hash!=expected)error="角色、材料或历练已变化，请刷新后重新确认";
    else if(type=='A'){if(id<0||id>=5||br_values[id]<=br_base[id])error="不能低于角色初始属性";else if(*br_attribute_reason(id))error="此属性维持着已学技能门槛，请先退回关联技能";}
    else if(type=='S'){for(int i=0;i<br_count;i++)if(br_nodes[i].skill==id)at=i;if(at<0)error="未知技能";else if(*br_skill_reason(at))error="请先退回依赖此技能的后续技能；基础动作及特殊赠点技能不可退回";else if(!br_hotbar(br_nodes[at].icon,false))error="快捷栏结构未通过校验";}
    else error="未知退点类型";
    // Do not allow reallocation during a skill's recovery window.
    for(int i=0;!error&&i<br_count;i++)if(br_nodes[i].skill>=0&&br_nodes[i].learned&&member_number(br_nodes[i].icon,"KD")>0)error="请等待技能冷却结束后再退点";
    if(!error&&!br_safe_place)error="请回到城镇或马车营地再退点";
    if(!error&&br_credit<1)error="历练机会不足，请完成并交付契约";
    if(!error&&!br_recipe_allowed(recipe,type,br_gems))error="配方无效或主背包材料不足";
    if(error){br_release();return error;}
    // Every expected failure is checked before this event-thread transaction.
    // No native gold payment is involved in the material policy.
    br_mutated=true;player=find_instance("o_player");RV out={.kind=5};
    if(!br_materials_consume(recipe)){release_value(&player);br_release();return "材料消耗结果异常，请勿重试，使用保险备份恢复";}
    RV ledgerMap=get_global("characterDataMap");bool ledgerOk=br_map_number_set(ledgerMap,br_ledger_key,br_ledger-1);release_value(&ledgerMap);
    if(!ledgerOk){release_value(&player);br_release();return "历练写入异常，请使用保险备份恢复";}
    if(type=='A'){br_set_stat(br_attrs[id],br_values[id]-1);br_set_stat("AP",br_values[5]+1);}
    // Already learned skills retain their unlocked knowledge when refunded.
    // Native C/S still controls prerequisites and spending the returned point.
    else{set_member(br_nodes[at].icon,"is_open",numeric(0));set_member(br_nodes[at].icon,"can_learn",numeric(1));br_hotbar(br_nodes[at].icon,true);br_set_stat("SP",br_values[6]+1);}
    out=call_script(0x1ce3050,player,0,NULL);release_value(&out);
    RV recalc[2]={numeric(member_number(player,"id")),{.kind=5}};out=call_script(0xb66380,player,2,recalc);release_value(&out);release_value(&player);br_release();return NULL;
}
