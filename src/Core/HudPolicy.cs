namespace StoneshardCompanion;

public static class HudPolicy
{
    // Only a current inventory/character panel suppresses the HUD. Main menu,
    // loading, lost connection and game exit must leave backup controls available.
    public static bool Show(bool folded,bool currentState,uint uiFlags)=>!folded&&(!currentState||(uiFlags&1u)==0);
    public static bool CanWalk(bool ready,bool fresh,bool sceneReady,bool foreground,uint uiFlags)=>ready&&fresh&&sceneReady&&foreground&&(uiFlags&~8u)==0;
}
