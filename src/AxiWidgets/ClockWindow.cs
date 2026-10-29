using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using AxiWidgets.Core;
using Forms=System.Windows.Forms;
using Rect=AxiWidgets.Core.Rect;

namespace AxiWidgets;

internal sealed class ClockWindow
{
    internal readonly ClockState State;
    internal readonly nint Handle;
    internal bool Editing;
    internal bool AcrylicAccepted;
    internal bool IsVisible=>Native.IsWindowVisible(Handle);
    internal ContextMenu ContextMenu {get;}
    internal event EventHandler? Closed;
    private readonly Manager manager;
    private readonly string mode;
    private readonly Messages messages;
    private readonly DispatcherTimer timer=new();
    private readonly SnapEngine snap=new();
    private Native.Point pointer;
    private Rect start;
    private bool dragging,closed;

    internal ClockWindow(Manager manager,ClockState state,string mode)
    {
        this.manager=manager;State=state;this.mode=mode;
        NativeCard.Check(NativeCard.Create(out Handle));
        AcrylicAccepted=true;
        messages=new Messages(this);messages.AssignHandle(Handle);
        var resources=new ResourceDictionary {Source=new Uri("/AxiWidgets;component/WidgetMenu.xaml",UriKind.Relative)};
        ContextMenu=new ContextMenu {Style=(Style)resources["WidgetContextMenu"]};
        timer.Tick+=(_,_)=>RefreshClock();
    }
    internal void Show()
    {
        if(mode=="desktop"&&!new DesktopHost().Attach(Handle)){Log.Write("Native card hidden: desktop attachment failed");return;}
        Restore();ApplyTheme();SetEdit(false);RefreshClock();Native.ShowWindow(Handle,4);
    }
    internal void Close()
    {
        if(closed)return;
        EndDrag();closed=true;timer.Stop();ContextMenu.IsOpen=false;
        messages.ReleaseHandle();NativeCard.Check(NativeCard.Destroy(Handle));Closed?.Invoke(this,EventArgs.Empty);
    }
    internal void ApplyTheme()
    {
        if(closed)return;
        NativeCard.Check(NativeCard.SetStyle(Handle,manager.Appearance.TintAlpha,manager.Appearance.TintRgb,Editing));
    }
    internal void SetEdit(bool enabled)
    {
        if(closed)return;
        if(!enabled)ContextMenu.IsOpen=false;
        EndDrag();Editing=enabled;DesktopHost.Input(Handle,enabled);ApplyTheme();
    }
    internal void UpdateContextMenu()
    {
        ContextMenu.Items.Clear();
        void Toggle(string label,bool value,Action change){
            var item=new MenuItem {Header=label,IsCheckable=true,IsChecked=value};
            item.Click+=(_,_)=>{change();RefreshClock();manager.Save();};ContextMenu.Items.Add(item);
        }
        Toggle("Show seconds",State.Seconds,()=>State.Seconds=!State.Seconds);
        Toggle("Show date",State.Date,()=>State.Date=!State.Date);
        ContextMenu.Items.Add(new Separator {Style=(Style)ContextMenu.FindResource("WidgetMenuSeparator")});
        var remove=new MenuItem {Header="Remove clock",Tag="Remove"};remove.Click+=(_,_)=>manager.RemoveClock(this);ContextMenu.Items.Add(remove);
    }
    internal void OpenMenu()
    {
        if(!Editing||closed)return;
        EndDrag();UpdateContextMenu();Native.GetCursorPos(out var cursor);
        var display=Display.For(new(cursor.X,cursor.Y,1,1));
        ContextMenu.Placement=PlacementMode.AbsolutePoint;
        ContextMenu.HorizontalOffset=cursor.X/display.Scale;ContextMenu.VerticalOffset=cursor.Y/display.Scale;
        ContextMenu.IsOpen=true;ContextMenu.ApplyTemplate();ContextMenu.UpdateLayout();
        if(PresentationSource.FromVisual(ContextMenu) is HwndSource source){
            var bounds=Native.Bounds(source.Handle);
            int x=(int)Math.Clamp(cursor.X,display.Work.X,Math.Max(display.Work.X,display.Work.Right-bounds.Width));
            int y=(int)Math.Clamp(cursor.Y,display.Work.Y,Math.Max(display.Work.Y,display.Work.Bottom-bounds.Height));
            Native.SetWindowPos(source.Handle,-1,x,y,0,0,1|0x10);
        }
    }
    internal void RefreshClock()
    {
        if(closed)return;
        timer.Stop();var now=DateTime.Now;
        NativeCard.Check(NativeCard.SetText(Handle,now.ToString(State.Seconds?"HH:mm:ss":"HH:mm",CultureInfo.CurrentCulture),
            State.Date?now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture):""));
        timer.Interval=ClockSchedule.Next(now,State.Seconds);timer.Start();
    }
    internal void Restore()
    {
        var displays=Display.All();
        var m=displays.Find(x=>x.Id==State.Monitor)??displays.Find(x=>x.Primary)??displays[0];
        var r=SnapEngine.Clamp(new(m.Work.X+State.X*m.Scale,m.Work.Y+State.Y*m.Scale,300*m.Scale,160*m.Scale),m.Work,8*m.Scale);
        NativeCard.Check(NativeCard.SetDpi(Handle,(float)m.Scale));DesktopHost.Move(Handle,r);Remember();
    }
    internal void Remember()
    {
        var r=Native.Bounds(Handle);var m=Display.For(r);State.Monitor=m.Id;State.X=(r.X-m.Work.X)/m.Scale;State.Y=(r.Y-m.Work.Y)/m.Scale;
    }
    private void BeginDrag()
    {
        if(!Editing)return;
        ContextMenu.IsOpen=false;Native.GetCursorPos(out pointer);start=Native.Bounds(Handle);snap.Reset();Native.SetCapture(Handle);dragging=Native.GetCapture()==Handle;
    }
    private void MoveDrag()
    {
        if(!dragging)return;
        Native.GetCursorPos(out var p);var r=start with {X=start.X+p.X-pointer.X,Y=start.Y+p.Y-pointer.Y};var m=Display.For(r);
        r=r with {Width=300*m.Scale,Height=160*m.Scale};var result=snap.Snap(r,manager.Neighbors(this),m.Work,m.Scale);
        NativeCard.Check(NativeCard.SetDpi(Handle,(float)m.Scale));DesktopHost.Move(Handle,result.Bounds);
        float x=result.GuideX is double gx?(float)Math.Clamp((gx-result.Bounds.X)/m.Scale,2,298):-1;
        float y=result.GuideY is double gy?(float)Math.Clamp((gy-result.Bounds.Y)/m.Scale,2,158):-1;
        NativeCard.Check(NativeCard.SetGuides(Handle,x,y));
    }
    private void EndDrag()
    {
        if(closed)return;
        bool moved=dragging;dragging=false;
        if(Native.GetCapture()==Handle)Native.ReleaseCapture();
        NativeCard.SetGuides(Handle,-1,-1);
        if(moved){Remember();manager.Save();}
    }
    private sealed class Messages(ClockWindow owner) : Forms.NativeWindow
    {
        protected override void WndProc(ref Forms.Message message)
        {
            try {
                if(message.Msg==0x46&&owner.mode=="desktop"&&message.LParam!=0){
                    int flags=Marshal.ReadInt32(message.LParam,IntPtr.Size*2+16);
                    if((flags&4)==0)Marshal.WriteIntPtr(message.LParam,IntPtr.Size,1);
                }
                switch(message.Msg){
                case 0x201:owner.BeginDrag();break;
                case 0x200:owner.MoveDrag();break;
                case 0x202:case 0x215:owner.EndDrag();break;
                case 0x7B:owner.OpenMenu();message.Result=0;return;
                case 0x20:Native.SetCursor(Native.LoadCursor(0,owner.Editing?32646:32512));message.Result=1;return;
                case 0x21:message.Result=3;return;
                case 0x7E:case 0x2E0:Application.Current.Dispatcher.BeginInvoke(()=>{if(!owner.closed&&!owner.dragging)owner.Restore();});break;
                case 0x1A:case 0x320:Application.Current.Dispatcher.BeginInvoke(()=>owner.ApplyTheme());break;
                case 0x1E:owner.RefreshClock();break;
                case 0x82:Application.Current.Dispatcher.BeginInvoke(()=>owner.Close());break;
                }
                base.WndProc(ref message);
            }catch(Exception e){Log.Write("Native card message failed: "+e);}
        }
    }
}
