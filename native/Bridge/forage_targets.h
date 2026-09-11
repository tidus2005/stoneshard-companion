#pragma once
// Discovery is independent of the brief idle frames between walking turns.
// Only visible plants enter this cache; path queries remain on idle frames.
typedef struct ForageTarget { double id,failed_x,failed_y; int kind,attempts; bool done; uint64_t retry; } ForageTarget;
static ForageTarget forage_targets[256];
static int forage_target_count,forage_cursor[2],forage_kind,forage_active=-1;
static uint64_t forage_discovery_due;
static void forage_reset_targets(void){
    memset(forage_targets,0,sizeof(forage_targets));forage_target_count=0;
    forage_cursor[0]=forage_cursor[1]=forage_kind=0;forage_active=-1;forage_discovery_due=0;
}
static void forage_discover(uint64_t now){
    if(now<forage_discovery_due)return;forage_discovery_due=now+50;
    int ended=0;
    for(int budget=0;budget<64;budget++){
        int kind=forage_kind;forage_kind=1-forage_kind;
        RV item=indexed_instance(kind?"o_interactive_harvest":"o_abstractGrow",forage_cursor[kind]++);
        if(!valid_object(item)){release_value(&item);forage_cursor[kind]=0;ended|=1<<kind;if(ended==3)break;continue;}
        double id=member_number(item,"id");int at=-1;
        for(int i=0;i<forage_target_count;i++)if(forage_targets[i].id==id){at=i;break;}
        if(at<0&&isfinite(id)&&forage_selected(item,kind)&&forage_visible(item)){
            if(forage_target_count<256)at=forage_target_count++;
            else for(int i=0;i<256;i++)if(forage_targets[i].done){at=i;break;}
            if(at>=0)forage_targets[at]=(ForageTarget){.id=id,.kind=kind};
        }
        release_value(&item);
        if(GetTickCount64()-now>=4)break;
    }
}
static int forage_nearest(double px,double py,uint64_t now){
    int best=-1;double distance=INFINITY;
    for(int i=0;i<forage_target_count;i++){
        ForageTarget* t=forage_targets+i;if(t->done)continue;
        bool moved=fabs(px-t->failed_x)>=52||fabs(py-t->failed_y)>=52;
        if(t->attempts&&(now<t->retry||(!moved&&t->attempts>=2)))continue;
        RV item=instance_from_id(numeric(t->id));
        if(!valid_object(item)){t->done=true;release_value(&item);continue;}
        bool available=forage_selected(item,t->kind)&&forage_visible(item);
        double x=member_number(item,"x"),y=member_number(item,"y");release_value(&item);
        if(!available||!isfinite(x)||!isfinite(y))continue;
        double d=(x-px)*(x-px)+(y-py)*(y-py);
        if(d<distance){distance=d;best=i;}
    }
    return best;
}
static void forage_defer(double px,double py){
    if(forage_active<0)return;ForageTarget* t=forage_targets+forage_active;
    if(fabs(px-t->failed_x)>=52||fabs(py-t->failed_y)>=52)t->attempts=0;
    t->failed_x=px;t->failed_y=py;t->attempts++;t->retry=GetTickCount64()+2000;
}
