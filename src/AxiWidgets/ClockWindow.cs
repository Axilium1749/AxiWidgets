using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AxiWidgets.Core;
using Rect = AxiWidgets.Core.Rect;

namespace AxiWidgets;

internal sealed class ClockWindow : Window
{
    internal readonly ClockState State;
    internal nint Handle;
    internal bool Editing;
    internal bool Dark;
    internal bool AcrylicAccepted;
    private readonly Manager manager;
    private readonly TextBlock time=new() { FontSize=54,FontWeight=FontWeights.Light,HorizontalAlignment=HorizontalAlignment.Center };
    private readonly TextBlock date=new() { FontSize=14,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,0) };
    private readonly Border card=new();
    private readonly Border outline=new() { CornerRadius=new CornerRadius(DesktopHost.ClockCornerRadius),BorderThickness=new Thickness(.6),IsHitTestVisible=false };
    private readonly Canvas guides=new() { IsHitTestVisible=false };
    private readonly DispatcherTimer timer=new();
    private readonly SnapEngine snap=new();
    private Native.Point pointer;
    private Rect start;
    private bool dragging;
    private readonly string mode;
    internal ClockWindow(Manager manager,ClockState state,string mode)
    {
        this.manager=manager; State=state; this.mode=mode;
        Title="AxiWidgets — Digital Clock"; Width=300; Height=160; WindowStyle=WindowStyle.None;
        ResizeMode=ResizeMode.NoResize; ShowInTaskbar=mode=="normal"; ShowActivated=mode=="normal";
        Background=Brushes.Transparent; FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        UseLayoutRounding=false;
        var stack=new StackPanel { VerticalAlignment=VerticalAlignment.Center }; stack.Children.Add(time); stack.Children.Add(date);
        var grid=new Grid(); grid.Children.Add(stack); grid.Children.Add(guides); card.Child=grid;
        var surface=new Grid();surface.Children.Add(card);surface.Children.Add(outline);Content=surface;
        SourceInitialized+=(_,_) => {
            Handle=new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(Handle)?.AddHook(Hook);
            if(mode!="normal") DesktopHost.Input(Handle,false);
            ApplyTheme(); Restore();
        };
        Loaded+=(_,_) => { if(mode=="desktop" && !new DesktopHost().Attach(Handle)) Hide(); ApplyTheme(); RefreshClock(); };
        SizeChanged+=(_,_)=> {if(Handle!=0)DesktopHost.RoundClock(Handle);};
        timer.Tick+=(_,_) => RefreshClock();
        MouseLeftButtonDown+=BeginDrag; MouseMove+=MoveDrag;
        MouseLeftButtonUp+=(_,_) => EndDrag();
        LostMouseCapture+=(_,_) => { if(dragging) EndDrag(); };
        ContextMenuOpening+=(_,e)=> {
            if(!Editing) {e.Handled=true;return;}
            UpdateContextMenu();
        };
        var menuResources=new ResourceDictionary { Source=new Uri("/AxiWidgets;component/WidgetMenu.xaml",UriKind.Relative) };
        ContextMenu=new ContextMenu { Style=(Style)menuResources["WidgetContextMenu"] };
        Closed+=(_,_) => { timer.Stop(); HwndSource.FromHwnd(Handle)?.RemoveHook(Hook); };
    }
    internal void ApplyTheme()
    {
        Dark=(int?)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1)==0;
        Foreground=Dark?Brushes.White:Brushes.Black;
        date.Opacity=.65;
        bool available=DesktopHost.Backdrop(Handle,Dark,true,Editing,manager.Appearance);
        AcrylicAccepted=available;
        DesktopHost.RoundClock(Handle);
        // Explicit solid fallback only on API failure; no simulated blur.
        byte tint=available?manager.Appearance.TintAlpha:(byte)255;
        uint rgb=manager.Appearance.TintRgb;
        card.Background=new SolidColorBrush(Color.FromArgb(tint,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb));
        Background=available?Brushes.Transparent:card.Background;
        InvalidateVisual();
        outline.BorderBrush=new SolidColorBrush(Editing?Color.FromArgb(100,130,190,255):Color.FromArgb(44,255,255,255));
    }
    internal void SetEdit(bool enabled)
    {
        if(!enabled && ContextMenu!=null) ContextMenu.IsOpen=false;
        EndDrag(); Editing=enabled; DesktopHost.Input(Handle,enabled); Cursor=enabled?Cursors.SizeAll:Cursors.Arrow; ApplyTheme();
    }
    internal void UpdateContextMenu()
    {
        var menu=ContextMenu!;
        menu.Items.Clear();
        void Toggle(string name,bool value,Action change) {
            var item=new MenuItem {Header=name,IsCheckable=true,IsChecked=value};
            item.Click+=(_,_)=> {change();RefreshClock();ApplyTheme();manager.Save();};
            menu.Items.Add(item);
        }
        Toggle("Show seconds",State.Seconds,()=>State.Seconds=!State.Seconds);
        Toggle("Show date",State.Date,()=>State.Date=!State.Date);
        menu.Items.Add(new Separator { Style=(Style)menu.FindResource("WidgetMenuSeparator") });
        var remove=new MenuItem {Header="Remove clock",Tag="Remove"};
        remove.Click+=(_,_)=>manager.RemoveClock(this); menu.Items.Add(remove);
    }
    internal void RefreshClock()
    {
        timer.Stop(); var now=DateTime.Now;
        var value=now.ToString(State.Seconds?"HH:mm:ss":"HH:mm",CultureInfo.CurrentCulture);
        if(time.Text!=value) time.Text=value;
        var day=now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture);
        if(date.Text!=day) date.Text=day;
        date.Visibility=State.Date?Visibility.Visible:Visibility.Collapsed;
        timer.Interval=ClockSchedule.Next(now,State.Seconds); timer.Start();
    }
    internal void Restore()
    {
        var displays=Display.All();
        var m=displays.Find(x=>x.Id==State.Monitor)??displays.Find(x=>x.Primary)??displays[0];
        var r=SnapEngine.Clamp(new(m.Work.X+State.X*m.Scale,m.Work.Y+State.Y*m.Scale,300*m.Scale,160*m.Scale),m.Work,8*m.Scale);
        DesktopHost.Move(Handle,r); Remember();
    }
    internal void Remember()
    {
        var r=Native.Bounds(Handle); var m=Display.For(r);
        State.Monitor=m.Id; State.X=(r.X-m.Work.X)/m.Scale; State.Y=(r.Y-m.Work.Y)/m.Scale;
    }
    private void BeginDrag(object sender,MouseButtonEventArgs e)
    {
        if(!Editing) return;
        Native.GetCursorPos(out pointer); start=Native.Bounds(Handle); snap.Reset(); dragging=CaptureMouse(); e.Handled=true;
    }
    private void MoveDrag(object sender,MouseEventArgs e)
    {
        if(!dragging) return;
        Native.GetCursorPos(out var p); var r=start with { X=start.X+p.X-pointer.X,Y=start.Y+p.Y-pointer.Y };
        var m=Display.For(r); r=r with { Width=300*m.Scale,Height=160*m.Scale };
        var result=snap.Snap(r,manager.Neighbors(this),m.Work,m.Scale);
        DesktopHost.Move(Handle,result.Bounds); DrawGuides(result,m.Scale);
    }
    private void DrawGuides(SnapResult result,double scale)
    {
        guides.Children.Clear();
        if(result.GuideX is double x) {
            double pos=Math.Clamp((x-result.Bounds.X)/scale,2,298);
            guides.Children.Add(new System.Windows.Shapes.Line { X1=pos,X2=pos,Y1=3,Y2=157,Stroke=Brushes.DeepSkyBlue,StrokeThickness=1 });
        }
        if(result.GuideY is double y) {
            double pos=Math.Clamp((y-result.Bounds.Y)/scale,2,158);
            guides.Children.Add(new System.Windows.Shapes.Line { X1=3,X2=297,Y1=pos,Y2=pos,Stroke=Brushes.DeepSkyBlue,StrokeThickness=1 });
        }
    }
    private void EndDrag()
    {
        if(!dragging) { guides.Children.Clear(); return; }
        dragging=false; ReleaseMouseCapture(); guides.Children.Clear(); Remember(); manager.Save();
    }
    private nint Hook(nint hwnd,int msg,nint w,nint l,ref bool handled)
    {
        if(msg==0x46 && mode=="desktop" && l!=0) {
            // WINDOWPOS.hwndInsertAfter: keep the owned popup at the bottom
            // whenever Windows requests a Z-order change; never activate it.
            int flags=System.Runtime.InteropServices.Marshal.ReadInt32(l,IntPtr.Size*2+16);
            if((flags&4)==0) System.Runtime.InteropServices.Marshal.WriteIntPtr(l,IntPtr.Size,(nint)1);
        }
        if(msg==0x21 && mode!="normal") { handled=true; return 3; } // MA_NOACTIVATE
        if(msg==0x7e || msg==0x2e0) Dispatcher.BeginInvoke(()=>Restore());
        if(msg==0x1a || msg==0x320) Dispatcher.BeginInvoke(()=>ApplyTheme());
        if(msg==0x1e) RefreshClock();
        return 0;
    }
}




