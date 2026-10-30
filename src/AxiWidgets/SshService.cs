using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AxiWidgets.Core;
namespace AxiWidgets;
internal sealed class SshService : IDisposable {
 internal SshStore Store {get;}=new(Path.Combine(Log.Folder,"ssh.json"));
 internal readonly Dictionary<Guid,long?> Latency=[];
 internal static string ConfigPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".ssh","config");
 internal event Action? Changed;
 CancellationTokenSource? lifetime;SemaphoreSlim? refresh;Dictionary<Guid,string> hosts=[];int clients;SshSettingsWindow? settings;
 internal SshService(){Store.Changed+=StoreChanged;}
 void Notify(){foreach(var id in Latency.Keys.Where(id=>!Store.Items.Any(c=>c.Id==id)).ToArray())Latency.Remove(id);Changed?.Invoke();}
 void StoreChanged(){
  foreach(var c in Store.Items)if(!hosts.TryGetValue(c.Id,out var host)||host!=c.Host)Latency.Remove(c.Id);
  hosts=Store.Items.ToDictionary(c=>c.Id,c=>c.Host);Notify();
  if(refresh is {} signal&&signal.CurrentCount==0)signal.Release();
 }
 internal void Attach(bool import=true){try{if(import){var items=Store.Items.ToList();if(SshStore.Merge(items,SshConfig.Import(ConfigPath))>0)Store.Save(items);}}catch(Exception e){Log.Write("SSH import failed: "+e.GetType().Name);}if(clients++==0){lifetime=new();refresh=new(0,1);_ = Poll(lifetime.Token,refresh);}}
 internal void Detach(){if(--clients==0){lifetime?.Cancel();lifetime?.Dispose();lifetime=null;refresh=null;}}
 async Task Poll(CancellationToken token,SemaphoreSlim signal){
  try{
   while(!token.IsCancellationRequested){
    var items=Store.Items.Select(c=>c with{}).ToArray();using var gate=new SemaphoreSlim(4);
    await Task.WhenAll(items.Select(async c=>{
     await gate.WaitAsync(token);
     try{
      long? latency=null;
      try{using var ping=new Ping();var reply=await ping.SendPingAsync(c.Host,TimeSpan.FromSeconds(1.5),cancellationToken:token);if(reply.Status==IPStatus.Success)latency=reply.RoundtripTime;}
      catch(PingException){}catch(ArgumentException){}
      if(!token.IsCancellationRequested&&Store.Items.Any(x=>x.Id==c.Id&&x.Host==c.Host)){Latency[c.Id]=latency;Notify();}
     }finally{gate.Release();}
    }));
    // Saving a server interrupts the cooldown. Signals coalesce, so passes never overlap.
    await signal.WaitAsync(TimeSpan.FromSeconds(15),token);
   }
  }catch(OperationCanceledException){}catch(Exception e){Log.Write("SSH ping failed: "+e.GetType().Name);}
  finally{signal.Dispose();if(ReferenceEquals(refresh,signal))refresh=null;}
 }
 internal async Task SmokeImmediatePing(){
  var saved=Store.Items.ToArray();var first=new SshConnection{Name="Loopback",Host="127.0.0.1"};
  var second=new SshConnection{Name="New loopback",Host="127.0.0.1",Port=2222};
  var initial=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var added=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  void Updated(){if(Latency.TryGetValue(first.Id,out var a)&&a.HasValue)initial.TrySetResult();if(Latency.TryGetValue(second.Id,out var b)&&b.HasValue)added.TrySetResult();}
  Changed+=Updated;Store.Save([first]);Attach(false);
  try{
   await initial.Task.WaitAsync(TimeSpan.FromSeconds(5));
   var watch=Stopwatch.StartNew();Store.Save([first,second]);
   await added.Task.WaitAsync(TimeSpan.FromSeconds(4));
   Log.Write($"SSH immediate ping PASS: new loopback server checked in {watch.ElapsedMilliseconds} ms during cooldown.");
  }finally{Changed-=Updated;Detach();Store.Save(saved);}
 }
 internal void OpenSettings(){if(settings!=null){settings.Activate();return;}settings=new(this);settings.Closed+=(_,_)=>settings=null;settings.Show();}
 internal static void Connect(SshConnection connection){try{string command=Convert.ToBase64String(Encoding.Unicode.GetBytes(SshConfig.PowerShellCommand(connection)));string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");var terminal=new ProcessStartInfo("wt.exe"){UseShellExecute=false};foreach(string arg in new[]{"new-tab",shell,"-NoLogo","-NoExit","-EncodedCommand",command})terminal.ArgumentList.Add(arg);try{Process.Start(terminal)?.Dispose();}catch(System.ComponentModel.Win32Exception){var fallback=new ProcessStartInfo(shell){UseShellExecute=false};foreach(string arg in new[]{"-NoLogo","-NoExit","-EncodedCommand",command})fallback.ArgumentList.Add(arg);Process.Start(fallback)?.Dispose();}}catch(Exception e){MessageBox.Show(e.Message,"SSH connection",MessageBoxButton.OK,MessageBoxImage.Error);}}
 public void Dispose(){settings?.Close();lifetime?.Cancel();lifetime?.Dispose();Store.Changed-=StoreChanged;}
}
