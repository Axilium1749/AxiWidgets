using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
namespace AxiWidgets;
// One collector per manager; no work when no System cards are present.
internal static class SystemMonitor
{
    internal static SystemSample Current {get;private set;}=SystemSample.Empty;
    internal static readonly List<double> CpuHistory=[];
    private static Action? changed;
    private static CancellationTokenSource? lifetime;
    internal static void Subscribe(Action callback){changed+=callback;if(lifetime==null){lifetime=new();_ = Run(lifetime.Token);}}
    internal static void Unsubscribe(Action callback){changed-=callback;if(changed==null){lifetime?.Cancel();lifetime?.Dispose();lifetime=null;}}
    private static async Task Run(CancellationToken token){
        nint handle=0;
        try {
            await Task.Run(()=>NativeCard.Check(SystemMetricsNative.Create(out handle)));
            while(!token.IsCancellationRequested){
                var sample=await Task.Run(()=>{NativeCard.Check(SystemMetricsNative.Read(handle,out var value));return value;});
                if(token.IsCancellationRequested)break;
                Current=sample;CpuHistory.Add(sample.Cpu);if(CpuHistory.Count>31)CpuHistory.RemoveAt(0);changed?.Invoke();
                await Task.Delay(1000,token);
            }
        }catch(OperationCanceledException){}catch(Exception e){Log.Write("System metrics failed: "+e);}
        finally{if(handle!=0)await Task.Run(()=>SystemMetricsNative.Destroy(handle));}
    }
}
[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
internal struct SystemSample {
    internal double Cpu,Ghz,RamUsed,RamTotal,Gpu,VramUsed,VramTotal,Down,Up;
    [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]internal string CpuName;
    [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]internal string GpuName;
    [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]internal string RamName;
    internal static SystemSample Empty=>new(){Cpu=-1,Ghz=-1,Gpu=-1,VramUsed=-1,Down=-1,Up=-1,CpuName="CPU",GpuName="GPU",RamName="RAM · —"};
}
