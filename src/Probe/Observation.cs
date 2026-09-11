using System.IO.MemoryMappedFiles;
using StoneshardCompanion;

internal static class Observation
{
    public static object Read(GameSession session)
    {
        using var map=MemoryMappedFile.OpenExisting($"Local\\StoneshardCompanion.v14.{session.Pid}",MemoryMappedFileRights.Read);
        using var view=map.CreateViewAccessor(0,65536,MemoryMappedFileAccess.Read);
        for(int attempt=0;attempt<30;attempt++){
            int before=view.ReadInt32(56);if((before&1)!=0){Thread.Sleep(1);continue;}
            var bytes=new byte[EngineBridge.SnapshotSize];view.ReadArray(0,bytes,0,bytes.Length);Thread.MemoryBarrier();
            if(view.ReadInt32(56)!=before)continue;
            double D(int p)=>BitConverter.ToDouble(bytes,p);int I(int p)=>BitConverter.ToInt32(bytes,p);ulong U(int p)=>BitConverter.ToUInt64(bytes,p);
            return new {time=DateTimeOffset.Now,ready=I(12),sceneReady=I(3772),foreground=session.IsForeground,preferred=D(3792),multiplier=D(88),target=D(80),suspend=I(3800),uiFlags=I(3804),sceneGeneration=U(3776),windowGeneration=U(3784),updated=U(3872),hunger=D(3808),thirst=D(3816),pain=D(3824),intoxication=D(3832),vitalValid=I(3856),highlight=I(3860),highlightApplied=I(3912),supplyFlags=I(3896),waterUses=I(3868),torchState=I(3864),torchCount=I(3900),torchDuration=D(3904),walkState=I(3916),walkDirection=I(3920),walkPhase=I(3960),walkKeysEnabled=I(3964),labelHookReady=I(3924),walkX=D(3928),walkY=D(3936),labelDrawCalls=U(3944),labelDrawOverrides=U(3952),detail=EngineBridge.DecodeSnapshot(bytes).Detail,telemetry=EngineBridge.DecodeSnapshot(bytes).Telemetry,error=I(64),gameWindow=U(168),playerX=D(144),playerY=D(152),cameraX=D(96),cameraY=D(104),mapWidth=D(128),mapHeight=D(136),visor=I(164)};
        }
        throw new IOException("Could not obtain a consistent snapshot");
    }
}
