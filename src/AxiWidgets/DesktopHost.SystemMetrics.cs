using System.Runtime.InteropServices;
namespace AxiWidgets;
// Managed native interop belongs to the DesktopHost boundary.
internal static class SystemMetricsNative {
    [DllImport("AxiWidgets.Native.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiMetrics_Create")]internal static extern int Create(out nint handle);
    [DllImport("AxiWidgets.Native.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiMetrics_Read")]internal static extern int Read(nint handle,out SystemSample sample);
    [DllImport("AxiWidgets.Native.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiMetrics_Destroy")]internal static extern void Destroy(nint handle);
}
