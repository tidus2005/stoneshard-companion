using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StoneshardCompanion;

public static class GameConfiguration
{
    public static string? DiscoverDirectory()
    {
        static string? Valid(string? path)=>!string.IsNullOrWhiteSpace(path)&&File.Exists(Path.Combine(path,"StoneShard.exe"))?Path.GetFullPath(path):null;
        foreach(var process in Process.GetProcessesByName("StoneShard"))using(process){
            try{if(Valid(Path.GetDirectoryName(process.MainModule?.FileName)) is {} active)return active;}
            catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
        }
        if(Valid(Environment.GetEnvironmentVariable("STONESHARD_DIR")) is {} explicitPath)return explicitPath;
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}){
            var path=Registry.GetValue(key,key.StartsWith("HKEY_CURRENT_USER")?"SteamPath":"InstallPath",null) as string;
            if(!string.IsNullOrWhiteSpace(path))roots.Add(path);
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        foreach(var root in roots.ToArray()){
            string file=Path.Combine(root,"steamapps","libraryfolders.vdf");
            try{if(File.Exists(file))foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"path\"\\s*\"([^\"]+)\""))roots.Add(match.Groups[1].Value.Replace("\\\\","\\"));}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
        foreach(string root in roots)if(Valid(Path.Combine(root,"steamapps","common","Stoneshard")) is {} installed)return installed;
        if(Valid(AppContext.BaseDirectory) is {} beside)return beside;
        return null;
    }
    public static void MigratePreferences(string legacyFolder,string sharedFolder)
    {
        string source=Path.Combine(legacyFolder,"preferences.json"),target=Path.Combine(sharedFolder,"preferences.json");
        if(File.Exists(target)||!File.Exists(source))return;
        Directory.CreateDirectory(sharedFolder);
        // Keep the old file intact and never replace an existing shared config.
        File.Copy(source,target,false);
    }
}
