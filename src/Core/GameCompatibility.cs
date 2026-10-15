using System.Buffers.Binary;

namespace StoneshardCompanion;

public enum GameDataRuntime { Unknown, Native, VirtualMachine }

public static class GameCompatibility
{
    public const string SupportedDataHash="84034525FCDEF3C6FD9803628B8DE6C43FEA479C930E168A95906DE37A2AE19C";
    // Existing ShardTest0 copy: only the project/save namespace is redirected.
    public const string IsolatedDataHash="53604DE8FE39EA37FEE143FBAE4415045F94BFD74B9D40B7B668660F10A53F85";
    // Inspect only the container structure. Do not execute or patch game data.
    public static GameDataRuntime InspectRuntime(Stream data)
    {
        if(!data.CanSeek||!data.CanRead)return GameDataRuntime.Unknown;
        long original=data.Position;
        try{
            data.Position=0;Span<byte> header=stackalloc byte[8];
            data.ReadExactly(header);
            if(!header[..4].SequenceEqual("FORM"u8))return GameDataRuntime.Unknown;
            long end=8L+BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            if(end!=data.Length)return GameDataRuntime.Unknown;
            bool vm=false;
            while(data.Position<end){
                if(end-data.Position<8)return GameDataRuntime.Unknown;
                data.ReadExactly(header);
                uint size=BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
                if(size>end-data.Position)return GameDataRuntime.Unknown;
                if(header[..4].SequenceEqual("CODE"u8)&&size>0)vm=true;
                data.Position+=size;
            }
            return vm?GameDataRuntime.VirtualMachine:GameDataRuntime.Native;
        }catch(IOException){return GameDataRuntime.Unknown;}
        finally{data.Position=original;}
    }
    public static string? Rejection(string version,string executableHash,string dataHash,GameDataRuntime runtime)
    {
        if(runtime==GameDataRuntime.VirtualMachine)
            return "检测到 GameMaker VM 数据包（非空 CODE，常见于 modbranch／MSL 模组版）。当前桥接仅适配原版 0.9.4.25 的 YYC 版本，已停止连接；存档备份与历史管理仍可用。请反馈模组名称、版本及下方指纹，不要替换正在使用的游戏文件。";
        if(!string.Equals(executableHash,GameSession.SupportedHash,StringComparison.OrdinalIgnoreCase)&&!string.Equals(executableHash,GameSession.UncappedHash,StringComparison.OrdinalIgnoreCase))
            return $"当前游戏 {version} 的可执行文件尚未适配，已停止连接；存档备份与历史管理仍可用。请反馈游戏版本、商店来源、模组名称及下方指纹。";
        if(!string.Equals(dataHash,SupportedDataHash,StringComparison.OrdinalIgnoreCase)&&!string.Equals(dataHash,IsolatedDataHash,StringComparison.OrdinalIgnoreCase))
            return "游戏数据包与已适配的原版 0.9.4.25 不同，可能来自模组或其他版本，已停止连接；存档备份与历史管理仍可用。请反馈模组名称、版本及下方指纹。";
        return null;
    }
    public static string Fingerprints(string executableHash,string dataHash)=>$"\nEXE SHA256: {executableHash}\ndata.win SHA256: {dataHash}";
}
