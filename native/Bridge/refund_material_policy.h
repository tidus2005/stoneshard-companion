#pragma once
// Versioned, character-save-owned ledger. No external balance or wall clock.
#define BR_LEDGER_BASE 1000000000
#define BR_CONTRACT_LIMIT 1000000
#define BR_GEM_COUNT 8
#define BR_RECIPE_COUNT 6
static const char* br_gem_keys[BR_GEM_COUNT]={"o_inv_jade","o_inv_topaz","o_inv_amethyst","o_inv_aquamarine","o_inv_ruby","o_inv_emerald","o_inv_sapphire","o_inv_diamond"};
// A, S, A, A, S, either. Each recipe returns exactly one point.
static const char br_recipe_types[BR_RECIPE_COUNT]={'A','S','A','A','S','*'};
static const int br_recipes[BR_RECIPE_COUNT][BR_GEM_COUNT]={{2,1,0,0,0,0,0,0},{0,0,2,2,0,0,0,0},{0,0,0,0,1,0,0,0},{0,0,0,0,0,1,0,0},{0,0,1,0,0,0,1,0},{0,0,0,0,0,0,0,1}};
static int br_ledger_pack(int contracts,int balance){return BR_LEDGER_BASE+contracts*10+balance;}
static bool br_ledger_sync(int encoded,int contracts,int* next,int* balance){
    if(contracts<0||contracts>BR_CONTRACT_LIMIT)return false;
    if(encoded==0){*balance=2;*next=br_ledger_pack(contracts,2);return true;}
    if(encoded<BR_LEDGER_BASE)return false;
    int seen=(encoded-BR_LEDGER_BASE)/10,old=(encoded-BR_LEDGER_BASE)%10;
    if(seen>contracts||old>6)return false;
    int earned=2*(contracts-seen);*balance=old+earned>6?6:old+earned;
    *next=br_ledger_pack(contracts,*balance);return true;
}
static bool br_recipe_allowed(int recipe,char type,const int* inventory){
    if(recipe<0||recipe>=BR_RECIPE_COUNT||(type!='A'&&type!='S')||(br_recipe_types[recipe]!='*'&&br_recipe_types[recipe]!=type))return false;
    for(int i=0;i<BR_GEM_COUNT;i++)if(inventory[i]<br_recipes[recipe][i])return false;
    return true;
}
