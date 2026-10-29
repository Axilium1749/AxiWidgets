using System;
using System.IO;
using Microsoft.Win32;

namespace AxiWidgets;
internal sealed class Autostart(string keyPath=@"Software\Microsoft\Windows\CurrentVersion\Run",string name="AxiWidgets")
{
    internal bool Enabled {
        get {using var key=Registry.CurrentUser.OpenSubKey(keyPath);return key?.GetValue(name) is string value&&value==Command;}
    }
    internal static string Executable=>Path.Combine(AppContext.BaseDirectory,"AxiWidgets.exe");
    internal static string Command=>$"\"{Executable}\" --startup";
    internal void SetEnabled(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(keyPath,true);
        if(enabled){if(!File.Exists(Executable))throw new FileNotFoundException("AxiWidgets.exe is missing",Executable);key.SetValue(name,Command,RegistryValueKind.String);}
        else key.DeleteValue(name,false);
    }
}
