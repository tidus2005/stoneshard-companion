#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef struct Value Value;
typedef struct {Value* ptr;} RV;
struct Value{int count;RV children[8],points,lines;};
typedef struct {RV value;int skill,learned;}BrNode;
static BrNode br_nodes[1024];static int br_count;static bool br_bad;
static RV live_member(RV v,const char* k){return !strcmp(k,"pointsArray")?v.ptr->points:v.ptr->lines;}
static int ba_size(RV v){return v.ptr?v.ptr->count:-1;}
static RV ba_get(RV v,int i){return v.ptr->children[i];}
static void release_value(RV* v){}
static int br_find(RV v){for(int i=0;i<br_count;i++)if(br_nodes[i].value.ptr==v.ptr)return i;return -1;}
#include "../Bridge/refund_graph.h"
static Value pool[100];static int used,checks;
static RV array(int n,RV* values){Value* v=&pool[used++];memset(v,0,sizeof(*v));v->count=n;for(int i=0;i<n;i++)v->children[i]=values[i];return (RV){v};}
static int node(bool learned,bool line){int i=br_count++;RV v=array(0,NULL);v.ptr->points=array(0,NULL);v.ptr->lines=array(0,NULL);br_nodes[i]=(BrNode){v,line?-1:i,learned};return i;}
static void parents(int n,int a,int b,bool either,bool line){RV refs[2]={br_nodes[a].value,b<0?(RV){0}:br_nodes[b].value};RV groups[2]={array(b<0||either?1:2,refs)};if(either)groups[1]=array(1,refs+1);RV outer=array(either?2:1,groups);if(line)br_nodes[n].value.ptr->lines=outer;else br_nodes[n].value.ptr->points=outer;}
static void check(bool ok){checks++;if(!ok){printf("FAIL %d\n",checks);exit(1);}}
static void reset(void){used=br_count=0;br_bad=false;}
int main(void){
 reset();node(true,false);node(true,false);parents(1,0,-1,false,false);check(br_blocker(0)==1);check(br_blocker(1)==-1);check(!br_bad);
 node(true,false);parents(2,1,-1,false,false);check(br_blocker(1)==2);check(br_blocker(2)==-1);
 reset();node(true,false);node(true,false);node(true,false);parents(2,0,1,false,false);check(br_blocker(0)==2);check(br_blocker(1)==2);check(br_blocker(2)==-1);
 parents(2,0,1,true,false);check(br_blocker(0)==-1);check(br_blocker(1)==-1);br_nodes[1].learned=0;check(br_blocker(0)==2);
 reset();node(true,false);node(false,true);node(true,false);parents(1,0,-1,false,false);parents(2,1,-1,false,true);check(br_blocker(0)==2);check(br_blocker(2)==-1);
 parents(0,2,-1,false,false);br_blocker(-1);check(br_bad); // Corrupt cycle fails closed.
 reset();node(true,false);br_nodes[0].value.ptr->points=(RV){0};br_blocker(-1);check(br_bad);
 printf("PASS refund dependency graph (%d checks: chain, AND, OR, lines, cycle, missing data)\n",checks);
}
