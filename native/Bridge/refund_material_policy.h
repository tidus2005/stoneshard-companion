#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <limits.h>
// Versioned, character-save-owned ledger. No external balance or wall clock.
#define BR_LEDGER_BASE 1000000000
#define BR_CONTRACT_LIMIT 1000000
#define BR_GEM_COUNT 14
#define BR_GEM_LIMIT 127
#define BR_REFUND_VALUE 600
// Supported 0.9.4.25 item table Price, independent of merchant modifiers.
static const char* br_gem_keys[BR_GEM_COUNT]={"o_inv_jade","o_inv_topaz","o_inv_amethyst","o_inv_aquamarine","o_inv_ruby","o_inv_emerald","o_inv_sapphire","o_inv_diamond","o_inv_amber","o_inv_agate","o_inv_turquoise","o_inv_moonstone","o_inv_morion","o_inv_seapearl"};
static const int br_gem_values[BR_GEM_COUNT]={125,175,150,200,375,450,525,600,40,60,80,100,250,300};
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
static bool br_materials_allowed(char type,const int* inventory,const int* selected){
    if((type!='A'&&type!='S')||!inventory||!selected)return false;
    int total=0;
    for(int i=0;i<BR_GEM_COUNT;i++){
        if(inventory[i]<0||inventory[i]>BR_GEM_LIMIT||selected[i]<0||selected[i]>inventory[i])return false;
        total+=selected[i]*br_gem_values[i];
    }
    return total>=BR_REFUND_VALUE;
}
// Strict v23 wire format: token|A/S|point-id|fourteen comma-separated quantities.
// Reject old recipe IDs, partial lists, signed/overflowing numbers and suffixes.
static bool br_refund_parse(const char* p,uint64_t* token,char* type,int* id,int* selected){
    if(!p)return false;*token=0;
    for(int i=0;i<16;i++){char c=*p++;int digit=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;if(digit<0)return false;*token=(*token<<4)|(unsigned)digit;}
    if(*p++!='|')return false;*type=*p++;if((*type!='A'&&*type!='S')||*p++!='|')return false;
    *id=0;if(*p<'0'||*p>'9')return false;
    while(*p>='0'&&*p<='9'){int d=*p++-'0';if(*id>(INT_MAX-d)/10)return false;*id=*id*10+d;}
    if(*p++!='|')return false;
    for(int i=0;i<BR_GEM_COUNT;i++){
        int n=0;if(*p<'0'||*p>'9')return false;
        do{n=n*10+(*p++-'0');if(n>BR_GEM_LIMIT)return false;}while(*p>='0'&&*p<='9');selected[i]=n;
        if(i<BR_GEM_COUNT-1){if(*p++!=',')return false;}else if(*p)return false;
    }
    return true;
}
