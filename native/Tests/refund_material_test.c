#include <stdio.h>
#include <stdbool.h>
#include <assert.h>
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
 int gems[]={2,1,2,2,1,1,1,1};
 for(int r=0;r<6;r++)CHECK(br_recipe_allowed(r,br_recipe_types[r]=='*'?'A':br_recipe_types[r],gems));
 CHECK(br_recipe_allowed(5,'S',gems));CHECK(!br_recipe_allowed(0,'S',gems));CHECK(!br_recipe_allowed(4,'A',gems));CHECK(!br_recipe_allowed(-1,'A',gems));CHECK(!br_recipe_allowed(6,'A',gems));CHECK(!br_recipe_allowed(5,'X',gems));
 gems[6]=0;CHECK(!br_recipe_allowed(4,'S',gems));gems[0]=1;CHECK(!br_recipe_allowed(0,'A',gems));
 printf("PASS gem refund native policy (%d checks)\n",n);return 0;
}
