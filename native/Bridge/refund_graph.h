#pragma once
static signed char br_marks[1024];
static bool br_eval(int at,int removed);
// Native ctr_SkillConnection.checkConnected: OR of AND groups; empty = true.
static bool br_groups(RV node,const char* key,int removed){RV groups=live_member(node,key);int n=ba_size(groups);bool any=n==0;if(n<0)br_bad=true;for(int i=0;i<n;i++){RV group=ba_get(groups,i);int m=ba_size(group);bool all=m>=0;if(m<0)br_bad=true;for(int j=0;j<m;j++){RV ref=ba_get(group,j);int index=br_find(ref);release_value(&ref);if(index<0){br_bad=true;all=false;}else if(!br_eval(index,removed))all=false;}any|=all;release_value(&group);}release_value(&groups);return any;}
static bool br_parents(int at,int removed){bool p=br_groups(br_nodes[at].value,"pointsArray",removed),l=br_groups(br_nodes[at].value,"linesArray",removed);return p&&l;}
static bool br_eval(int at,int removed){if(br_marks[at]==1){br_bad=true;return false;}if(br_marks[at]>=2)return br_marks[at]==3;br_marks[at]=1;BrNode* n=&br_nodes[at];bool active=(n->skill<0||(n->learned&&at!=removed))&&br_parents(at,removed);br_marks[at]=active?3:2;return active;}
static int br_blocker(int removed){memset(br_marks,0,sizeof(br_marks));for(int i=0;i<br_count;i++)if(i!=removed&&br_nodes[i].skill>=0&&br_nodes[i].learned&&!br_parents(i,removed))return i;return -1;}
