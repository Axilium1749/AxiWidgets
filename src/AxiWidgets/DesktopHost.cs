using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using AxiWidgets.Core;

namespace AxiWidgets;

internal static class Native
{
    internal delegate bool EnumProc(nint hwnd, nint data);
    internal delegate bool MonitorProc(nint monitor,nint dc,nint rect,nint data);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rectangle { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct MonitorInfo { public int Size; public Rectangle Monitor,Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
    [StructLayout(LayoutKind.Sequential)] internal struct Margins { public int Left,Right,Top,Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct AccentPolicy { public int State,Flags; public uint Color; public int Animation; }
    [StructLayout(LayoutKind.Sequential)] internal struct CompositionData { public int Attribute; public nint Data; public nuint Size; }
    [DllImport("user32.dll",SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowCompositionAttribute(nint hwnd,ref CompositionData data);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetFocus();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint FindWindow(string cls,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent,nint after,string? cls,string? title);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(nint hwnd,StringBuilder name,int size);
    [DllImport("user32.dll")] internal static extern nint SendMessageTimeout(nint hwnd,uint msg,nint w,nint l,uint flags,uint timeout,out nint result);
    [DllImport("user32.dll",SetLastError=true)] internal static extern nint SetParent(nint hwnd,nint parent);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)] internal static extern nint GetLong(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] internal static extern nint SetLong(nint hwnd,int index,nint value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out Rectangle rect);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint hwnd,ref Point point);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x,int y);
    [StructLayout(LayoutKind.Sequential)] internal struct RawDevice {public ushort Page,Usage;public uint Flags;public nint Target;}
    [StructLayout(LayoutKind.Explicit,Size=48)] internal struct RawMouse {
        [FieldOffset(0)]public uint Type;[FieldOffset(24)]public ushort Flags;
        [FieldOffset(36)]public int X;[FieldOffset(40)]public int Y;
    }
    [DllImport("user32.dll")] internal static extern bool RegisterRawInputDevices([In]RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll")] internal static extern uint GetRawInputData(nint input,uint command,out RawMouse data,ref uint size,uint headerSize);
    [DllImport("user32.dll")] internal static extern bool ClipCursor(ref Rectangle rect);
    [DllImport("user32.dll",EntryPoint="ClipCursor")] internal static extern bool UnclipCursor(nint none);
    [DllImport("user32.dll")] internal static extern bool GetClipCursor(out Rectangle rect);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32.dll")] internal static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetCapture();
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll",EntryPoint="LoadCursorW")] internal static extern nint LoadCursor(nint module,nint id);
    [DllImport("user32.dll")] internal static extern nint SetCursor(nint cursor);
    [StructLayout(LayoutKind.Sequential)] internal struct IconInfo {public int Icon;public uint X,Y;public nint Mask,Color;}
    [DllImport("user32.dll")] internal static extern bool GetIconInfo(nint icon,out IconInfo info);
    [DllImport("user32.dll")] internal static extern nint CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll")] internal static extern bool DestroyCursor(nint cursor);
    [DllImport("user32.dll")] internal static extern bool EnableWindow(nint hwnd,bool enabled);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("gdi32.dll")] internal static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd,nint region,bool redraw);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorProc callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(nint hwnd,ref Margins margins);
    internal static string Class(nint hwnd) { var s=new StringBuilder(256); GetClassName(hwnd,s,256); return s.ToString(); }
    internal static Rect Bounds(nint hwnd) { GetWindowRect(hwnd,out var r); return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top); }
}

internal record Display(string Id,Rect Work,double Scale,bool Primary)
{
    internal static List<Display> All()
    {
        var result=new List<Display>();
        Native.EnumDisplayMonitors(0,0,(m,dc,r,d) => {
            var info=new Native.MonitorInfo { Size=Marshal.SizeOf<Native.MonitorInfo>(),Device="" };
            if(Native.GetMonitorInfo(m,ref info)) {
                Native.GetDpiForMonitor(m,0,out var dpi,out _);
                result.Add(new(info.Device,new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top),dpi>0?dpi/96.0:1,(info.Flags&1)!=0));
            } return true;
        },0);
        return result;
    }
    internal static Display For(Rect r) => All().OrderByDescending(m => Math.Max(0,Math.Min(r.Right,m.Work.Right)-Math.Max(r.X,m.Work.X))*Math.Max(0,Math.Min(r.Bottom,m.Work.Bottom)-Math.Max(r.Y,m.Work.Y))).First();
}

internal sealed class DesktopHost
{
    private const int RoundedCorners=2; // DWMWCP_ROUND, the largest DWM preset.
    internal const double ClockCornerRadius=8;
    internal nint Parent { get; private set; }
    internal bool Attach(nint hwnd)
    {
        var progman=Native.FindWindow("Progman",null);
        if(progman==0) return false;
        Native.SendMessageTimeout(progman,0x052c,0,0,2,1000,out _);
        nint owner=0;
        // Locate the actual icon host instead of assuming a fixed WorkerW sibling order.
        Native.EnumWindows((w,d) => { if(Native.FindWindowEx(w,0,"SHELLDLL_DefView",null)!=0) owner=w; return true; },0);
        if(owner==0) { Log.Write("Desktop attach unavailable: no SHELLDLL_DefView host"); return false; }
        var bounds=Native.Bounds(hwnd);
        // Tear down top-level glass before changing the HWND's composition role.
        int none=1;
        Native.DwmSetWindowAttribute(hwnd,38,ref none,4);
        var noFrame=new Native.Margins();
        Native.DwmExtendFrameIntoClientArea(hwnd,ref noFrame);
        // Preserve WPF's top-level rendering surface. GWLP_HWNDPARENT sets
        // ownership for a popup; SetParent would convert it into a shell child.
        Native.SetLong(hwnd,-16,(nint)(((long)Native.GetLong(hwnd,-16)&~0x40000000L)|0x80000000L));
        var priorOwner=Native.SetLong(hwnd,-8,owner);
        int ownerError=Marshal.GetLastWin32Error();
        var assigned=Native.GetLong(hwnd,-8);
        if(assigned!=owner) { Log.Write($"Desktop owner assignment failed expected={owner:X}/{Native.Class(owner)} ownerParent={Native.GetParent(owner):X} ownerStyle=0x{Native.GetLong(owner,-16):X} actual={assigned:X}/{Native.Class(assigned)} prior={priorOwner:X} style=0x{Native.GetLong(hwnd,-16):X} setWin32={ownerError}"); return false; }
        Parent=owner;
        Move(hwnd,bounds);
        Log.Write($"Desktop owned popup owner={owner:X} class={Native.Class(owner)} hwnd={hwnd:X}");
        return true;
    }
    internal static void Move(nint hwnd,Rect bounds)
    {
        var p=new Native.Point { X=(int)Math.Round(bounds.X),Y=(int)Math.Round(bounds.Y) };
        var parent=Native.GetParent(hwnd);
        if(((long)Native.GetLong(hwnd,-16)&0x40000000L)!=0) Native.ScreenToClient(parent,ref p);
        Native.SetWindowPos(hwnd,parent!=0?(nint)1:0,p.X,p.Y,(int)Math.Round(bounds.Width),(int)Math.Round(bounds.Height),0x10|0x20);
    }
    internal static void Input(nint hwnd,bool edit)
    {
        long ex=(long)Native.GetLong(hwnd,-20);
        Native.SetLong(hwnd,-20,(nint)(ex|0x08000000|0x80));
        // Disabled native child HWND passes input to the desktop across process boundaries.
        Native.EnableWindow(hwnd,edit);
    }
    internal static void ConfigureGallery(nint hwnd)
    {
        // Only the editor is topmost. Desktop clocks retain their bottom Z-order.
        long ex=(long)Native.GetLong(hwnd,-20);
        Native.SetLong(hwnd,-20,(nint)((ex|0x80)&~0x40000L)); // TOOLWINDOW, no APPWINDOW
        Native.SetWindowPos(hwnd,-1,0,0,0,0,0x1|0x2|0x10|0x20);
    }
    internal static void RoundGallery(nint hwnd)
    {
        Native.SetWindowRgn(hwnd,0,true);
        int round=RoundedCorners,border=0x005D5D62;
        Native.DwmSetWindowAttribute(hwnd,33,ref round,4);
        Native.DwmSetWindowAttribute(hwnd,34,ref border,4);
    }
    internal static void GalleryBackdrop(nint hwnd)
    {
        SetAccent(hwnd,false,true);
        int none=1;Native.DwmSetWindowAttribute(hwnd,38,ref none,4);
        var margins=new Native.Margins();
        int hr=Native.DwmExtendFrameIntoClientArea(hwnd,ref margins);
        if(HwndSource.FromHwnd(hwnd)?.CompositionTarget is {} target)target.BackgroundColor=System.Windows.Media.Color.FromRgb(32,32,36);
        RoundGallery(hwnd);
        Log.Write($"Gallery opaque background; frame reset=0x{hr:X8}");
    }
    internal static void RoundClock(nint hwnd)
    {
        // Accent Acrylic did not follow the custom HRGN. Let DWM clip the
        // complete composited surface; WPF adds only a translucent outline.
        Native.SetWindowRgn(hwnd,0,true);
        int round=RoundedCorners,noBorder=unchecked((int)0xfffffffe);
        Native.DwmSetWindowAttribute(hwnd,33,ref round,4);
        Native.DwmSetWindowAttribute(hwnd,34,ref noBorder,4);
    }
    private static bool SetAccent(nint hwnd,bool enabled,bool dark,bool blur=true,byte alpha=112,uint? rgb=null,int? state=null)
    {
        // Undocumented WCA_ACCENT_POLICY, isolated here. No injection or layered HWND.
        var policy=new Native.AccentPolicy { State=enabled?(state??(blur?4:2)):0,Flags=enabled?2:0,
            Color=enabled?((uint)alpha<<24)|(rgb??(dark?0x00202020u:0x00F5F5F5u)):0 };
        nint buffer=Marshal.AllocHGlobal(Marshal.SizeOf<Native.AccentPolicy>());
        try {
            Marshal.StructureToPtr(policy,buffer,false);
            var data=new Native.CompositionData { Attribute=19,Data=buffer,Size=(nuint)Marshal.SizeOf<Native.AccentPolicy>() };
            return Native.SetWindowCompositionAttribute(hwnd,ref data);
        } catch(EntryPointNotFoundException) { return false; }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal static void ProbeAccent(nint hwnd,bool dark)
    {
        // One-shot API probe only. It does not establish visible blur or compare radii.
        foreach(int state in new[] {2,3,4})
            foreach(byte alpha in new byte[] {1,80,150,255}) {
                bool accepted=SetAccent(hwnd,true,dark,alpha:alpha,rgb:0x001E1E22,state:state);
                Log.Write($"ACCENT PROBE state={state} GradientColor=0x{((uint)alpha<<24)|0x001E1E22u:X8} accepted={accepted} hwnd={hwnd:X}");
            }
        SetAccent(hwnd,false,dark);
    }
    internal static bool Backdrop(nint hwnd,bool dark,bool enabled,bool editing,AppearanceSettings? appearance=null)
    {
        // DWM alone owns the outer contour: no HRGN and no second WPF radius.
        Native.SetWindowRgn(hwnd,0,true);
        int theme=dark?1:0, corner=RoundedCorners;
        int border=editing?0x00627DA7:(dark?0x005D5D62:0x00D0D0D0);
        Native.DwmSetWindowAttribute(hwnd,20,ref theme,4);
        Native.DwmSetWindowAttribute(hwnd,33,ref corner,4);
        Native.DwmSetWindowAttribute(hwnd,34,ref border,4);
        bool child=((long)Native.GetLong(hwnd,-16)&0x40000000L)!=0;
        bool transparency=(int?)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",1)!=0;
        enabled=enabled&&transparency&&!System.Windows.SystemParameters.HighContrast;
        // The two pipelines must never coexist. Clear the previous accent first.
        SetAccent(hwnd,false,dark);
        int acrylic=1; // Always disable SystemBackdrop: only native accent composition is used.
        var margins=enabled&&!child?new Native.Margins { Left=-1,Right=-1,Top=-1,Bottom=-1 }:new Native.Margins();
        int frame=Native.DwmExtendFrameIntoClientArea(hwnd,ref margins);
        int hr=Native.DwmSetWindowAttribute(hwnd,38,ref acrylic,4);
        // Widget opacity is drawn once by WPF above the live compositor surface.
        // Keep native tint minimal; Windows can ignore/clamp its alpha with Acrylic.
        bool accentAccepted=enabled&&!child&&SetAccent(hwnd,true,dark,appearance?.Blur??true,appearance!=null?(byte)1:(dark?(byte)112:(byte)136),appearance?.TintRgb);
        bool active=enabled&&!child&&hr>=0&&frame>=0&&accentAccepted;
        if(!active) {
            SetAccent(hwnd,false,dark);
            int none=1; Native.DwmSetWindowAttribute(hwnd,38,ref none,4);
            var zero=new Native.Margins(); Native.DwmExtendFrameIntoClientArea(hwnd,ref zero);
        }
        if(HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) {
            target.RenderMode=RenderMode.Default;
            target.BackgroundColor=active?System.Windows.Media.Colors.Transparent:
                dark?System.Windows.Media.Color.FromRgb(32,32,32):System.Windows.Media.Colors.WhiteSmoke;
        }
        Log.Write($"Backdrop backend=AccentPolicy requested={enabled} blur={appearance?.Blur??true} nativeAlpha={(appearance==null?112:1)} tintRgb=0x{appearance?.TintRgb:X6} wpfTintAlpha={appearance?.TintAlpha} accepted={active} owner={Native.GetParent(hwnd):X} frame=0x{frame:X8} acrylic=0x{hr:X8}; visual effect not verified.");
        return active;
    }
}

