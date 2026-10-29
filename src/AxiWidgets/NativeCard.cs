using System;
using System.Runtime.InteropServices;

namespace AxiWidgets;

internal static class NativeCard
{
    private const string Library="AxiWidgets.Native.dll";
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetScroll")] internal static extern int SetScroll(nint hwnd,float position);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_AnimateScroll")] internal static extern int AnimateScroll(nint hwnd,[In]float[] positions,int count,float interval);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    internal struct Command {
        public int Kind;public float X,Y,W,H,R,Size;public uint Color;public int Align;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Text;
    }
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_CreateWidget")] internal static extern int CreateWidget(out nint hwnd,float width,float height);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetContent")] internal static extern int SetContent(nint hwnd,[In]Command[] commands,int count);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_Create")] internal static extern int Create(out nint hwnd);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetText",CharSet=CharSet.Unicode)] internal static extern int SetText(nint hwnd,string time,string date);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetDpi")] internal static extern int SetDpi(nint hwnd,float scale);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetStyle")] internal static extern int SetStyle(nint hwnd,byte alpha,uint rgb,[MarshalAs(UnmanagedType.Bool)]bool editing);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_SetGuides")] internal static extern int SetGuides(nint hwnd,float x,float y);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_Destroy")] internal static extern int Destroy(nint hwnd);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="AxiCard_Shutdown")] internal static extern int Shutdown();
    internal static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
}
