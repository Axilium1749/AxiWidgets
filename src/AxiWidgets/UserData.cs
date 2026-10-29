using System;
using System.IO;
using System.Linq;

namespace AxiWidgets;
internal static class UserData
{
    internal static string Resolve(string[] args)
    {
        var explicitPath=args.FirstOrDefault(x=>x.StartsWith("--data="));
        if(explicitPath!=null)return Path.GetFullPath(explicitPath[7..]);
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AxiWidgets");
        var old=Path.Combine(AppContext.BaseDirectory,".local");
        MigrateLegacy(folder,old);
        return folder;
    }
    internal static void MigrateLegacy(string folder,string old)
    {
        if(!File.Exists(Path.Combine(folder,"layout.json"))&&Directory.Exists(old)){
            Directory.CreateDirectory(folder);
            // Layout is copied last, so an interrupted migration can be retried.
            foreach(string name in new[]{"reminders.json","appearance.json","layout.json"}){
                var source=Path.Combine(old,name);var target=Path.Combine(folder,name);
                if(File.Exists(source)&&!File.Exists(target))File.Copy(source,target,false);
            }
        }
    }
}
