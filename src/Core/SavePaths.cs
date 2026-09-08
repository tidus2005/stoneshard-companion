using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StoneshardCompanion;

// A redirected user folder is a valid configured root. Links *inside* an
// archive/save tree remain forbidden: traversal must never escape that root.
public static class SavePaths
{
    public static string ResolveDirectoryRoot(string path)
    {
        path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string root=Path.GetPathRoot(path)!;
        string current=root;
        foreach(string part in path[root.Length..].Split(Path.DirectorySeparatorChar,StringSplitOptions.RemoveEmptyEntries)){
            current=Path.Combine(current,part);
            FileAttributes attributes;
            try{attributes=File.GetAttributes(current);}
            catch(FileNotFoundException){continue;}catch(DirectoryNotFoundException){continue;}
            if((attributes&FileAttributes.Directory)==0)throw new IOException("目录路径被文件占用："+current);
            if((attributes&FileAttributes.ReparsePoint)!=0&&IsNameSurrogateTag(ReadTag(current))){
                var resolved=Directory.ResolveLinkTarget(current,true);
                if(resolved is null)throw new IOException("无法解析重定向目录，请在存档管理中选择 Windows 本地备份目录："+current);
                current=Path.GetFullPath(resolved.FullName);
            }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    // Microsoft documents bit 29 as name substitution. Cloud/virtual-storage
    // tags without that bit are not symlinks and can be read through their driver.
    public static bool IsNameSurrogateTag(uint tag)=>tag==0||(tag&0x20000000)!=0;
    public static bool IsLink(string path)=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0&&IsNameSurrogateTag(ReadTag(path));
    public static void AssertNotLink(string path)
    {
        try{
            if(IsLink(path))
                throw new IOException("存档或备份内部不能包含目录链接或文件链接："+path);
        }catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    private static uint ReadTag(string path)
    {
        // FindFirstFile reports the tag without opening/hydrating the target.
        string full=Path.GetFullPath(path);
        if(!full.StartsWith(@"\\?\",StringComparison.Ordinal))full=full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full[2..]:@"\\?\"+full;
        nint handle=FindFirstFileW(full,out var data);
        if(handle==new nint(-1))throw new IOException("无法识别路径类型："+path,new Win32Exception(Marshal.GetLastWin32Error()));
        try{return data.Reserved0;}finally{FindClose(handle);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct FindData
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
        public uint SizeHigh,SizeLow,Reserved0,Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=14)]public string AlternateName;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]
    private static extern nint FindFirstFileW(string path,out FindData data);
    [DllImport("kernel32.dll",ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool FindClose(nint handle);
}
