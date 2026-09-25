#pragma once
static RV call_script(uintptr_t address,RV self,int count,RV* values){
    RV out={.kind=5};RV* args[8]={0};
    if((self.kind&0xffffff)!=6||!self.ptr||count<0||count>8||(count&&!values))return out;
    for(int i=0;i<count;i++)args[i]=values+i;
    ((GameScript)(game+address))(self.ptr,self.ptr,&out,count,args);return out;
}
