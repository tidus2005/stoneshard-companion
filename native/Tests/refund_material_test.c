#include <stdio.h>
#include <stdbool.h>
#include <assert.h>
#include <string.h>
#include "../Bridge/refund_material_policy.h"
int main(void){
 int next,balance,n=0;
 #define CHECK(x) do{assert(x);n++;}while(0)
 CHECK(br_ledger_sync(0,37,&next,&balance)&&balance==2&&next==1000000372);
 CHECK(br_ledger_sync(next,37,&next,&balance)&&balance==2);
 CHECK(br_ledger_sync(next,38,&next,&balance)&&balance==4);
 CHECK(br_ledger_sync(next,40,&next,&balance)&&balance==6);
 CHECK(br_ledger_sync(next,45,&next,&balance)&&balance==6);
 next--;CHECK(br_ledger_sync(next,45,&next,&balance)&&balance==5);
 CHECK(br_ledger_sync(next,46,&next,&balance)&&balance==6);
 CHECK(!br_ledger_sync(next,44,&next,&balance));
 CHECK(!br_ledger_sync(123,37,&next,&balance));CHECK(!br_ledger_sync(1000000377,37,&next,&balance));
 CHECK(!br_ledger_sync(0,-1,&next,&balance));CHECK(!br_ledger_sync(0,1000001,&next,&balance));
 // Loading a prior save restores its complete balance and counter together.
 CHECK(br_ledger_sync(1000000371,37,&next,&balance)&&balance==1);
 CHECK(br_ledger_sync(1000000371,38,&next,&balance)&&balance==3);
 int gems[BR_GEM_COUNT],selected[BR_GEM_COUNT]={0};for(int i=0;i<BR_GEM_COUNT;i++)gems[i]=127;
 selected[7]=1;CHECK(br_materials_allowed('A',gems,selected));CHECK(br_materials_allowed('S',gems,selected)); // diamond exactly 600
 selected[7]=0;selected[5]=1;selected[2]=1;CHECK(br_materials_allowed('A',gems,selected)); // emerald + amethyst 600
 memset(selected,0,sizeof(selected));selected[0]=5;CHECK(br_materials_allowed('S',gems,selected)); // 625, no change
 memset(selected,0,sizeof(selected));selected[8]=15;CHECK(br_materials_allowed('A',gems,selected)); // amber
 memset(selected,0,sizeof(selected));selected[9]=10;CHECK(br_materials_allowed('S',gems,selected)); // agate
 memset(selected,0,sizeof(selected));selected[4]=1;selected[8]=4;selected[9]=1;CHECK(!br_materials_allowed('A',gems,selected)); // 595
 memset(selected,0,sizeof(selected));CHECK(!br_materials_allowed('A',gems,selected));
 selected[7]=1;CHECK(!br_materials_allowed('X',gems,selected));gems[7]=0;CHECK(!br_materials_allowed('A',gems,selected));gems[7]=127;
 selected[0]=-1;CHECK(!br_materials_allowed('S',gems,selected));selected[0]=128;CHECK(!br_materials_allowed('S',gems,selected));selected[0]=INT_MAX;CHECK(!br_materials_allowed('S',gems,selected));selected[0]=0;
 gems[0]=-1;CHECK(!br_materials_allowed('A',gems,selected));gems[0]=128;CHECK(!br_materials_allowed('A',gems,selected));gems[0]=127;
 CHECK(!br_materials_allowed('A',NULL,selected));CHECK(!br_materials_allowed('A',gems,NULL));
 for(int g=0;g<BR_GEM_COUNT;g++){memset(selected,0,sizeof(selected));selected[g]=(600+br_gem_values[g]-1)/br_gem_values[g];CHECK(br_materials_allowed('A',gems,selected));CHECK(br_materials_allowed('S',gems,selected));}
 uint64_t token;char type;int id;
 const char* valid="0123456789abcdef|S|2147483647|0,0,1,0,0,1,0,0,0,0,0,0,0,0";
 CHECK(br_refund_parse(valid,&token,&type,&id,selected)&&token==0x0123456789abcdefULL&&type=='S'&&id==INT_MAX&&selected[2]==1&&selected[5]==1);
 CHECK(br_materials_allowed(type,gems,selected));
 const char* invalid[]={"0123456789abcdef|A|0|5","0123456789abcdeg|A|0|0,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcde|A|0|0,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|X|0|0,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|A|-1|0,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|A|2147483648|0,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0,0,0,0,0","0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0,0,0,0,0,0,0","0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0,-1,0,0,0,0","0123456789abcdef|A|0|128,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|A|0|999999999999,0,0,0,0,0,0,1,0,0,0,0,0,0","0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0,0x,0,0,0,0","0123456789abcdef|A|0|0,0,0,0,0,0,0,1,0, 0,0,0,0,0"};
 for(unsigned i=0;i<sizeof(invalid)/sizeof(invalid[0]);i++)CHECK(!br_refund_parse(invalid[i],&token,&type,&id,selected));
 for(size_t len=0;len<strlen(valid);len++){char prefix[100];memcpy(prefix,valid,len);prefix[len]=0;CHECK(!br_refund_parse(prefix,&token,&type,&id,selected));}
 // Exhaust every 0..2 selection across the first ten gems (additional gems checked separately) for both point kinds.
 for(int code=0;code<59049;code++){int v=code,total=0;for(int i=0;i<BR_GEM_COUNT;i++){selected[i]=v%3;v/=3;total+=selected[i]*br_gem_values[i];}CHECK(br_materials_allowed('A',gems,selected)==(total>=600));CHECK(br_materials_allowed('S',gems,selected)==(total>=600));}
 printf("PASS gem refund native policy (%d checks)\n",n);return 0;
}
