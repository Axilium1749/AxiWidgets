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

internal sealed partial class ClockWindow
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
        NativeCard.Check(NativeCard.CreateWidget(out Handle,WidgetWidth,WidgetHeight,State.Kind=="System"?WidgetDrawing.SystemRadius:30));
        if(State.Kind=="Reminders")manager.Reminders.Changed+=ReminderDataChanged;
        if(State.Kind=="System")SystemMonitor.Subscribe(RefreshClock);
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
        if(State.Kind=="System")SystemMonitor.Unsubscribe(RefreshClock);
        EndListPointer(false);StopScrollMotion();EndInput();manager.Reminders.Changed-=ReminderDataChanged;EndDrag();closed=true;timer.Stop();ContextMenu.IsOpen=false;
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
        EndListPointer(false);if(State.Kind=="Reminders")SettleScroll();EndInput();EndDrag();Editing=enabled;DesktopHost.Input(Handle,enabled||State.Kind=="Reminders");ApplyTheme();
    }
    internal void UpdateContextMenu()
    {
        ContextMenu.Items.Clear();
        void Toggle(string label,bool value,Action change){
            var item=new MenuItem {Header=label,IsCheckable=true,IsChecked=value};
            item.Click+=(_,_)=>{change();RefreshClock();manager.Save();};ContextMenu.Items.Add(item);
        }
        if(State.Kind=="Clock") {
        Toggle("Show seconds",State.Seconds,()=>State.Seconds=!State.Seconds);
        Toggle("Show date",State.Date,()=>State.Date=!State.Date);
        }
        ContextMenu.Items.Add(new Separator {Style=(Style)ContextMenu.FindResource("WidgetMenuSeparator")});
        var remove=new MenuItem {Header="Remove "+State.Kind.ToLowerInvariant(),Tag="Remove"};remove.Click+=(_,_)=>manager.RemoveClock(this);ContextMenu.Items.Add(remove);
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
        if(State.Kind!="Clock") {
            if(State.Kind=="Reminders")ResizeScroll();
            var drawing=State.Kind=="System"?WidgetDrawing.SystemCard(SystemMonitor.Current,SystemMonitor.CpuHistory):State.Kind=="Calendar"?WidgetDrawing.Calendar(now):DrawReminders();
            NativeCard.Check(NativeCard.SetContent(Handle,drawing.Commands.ToArray(),drawing.Commands.Count));
            if(State.Kind=="Calendar"){timer.Interval=new DateTimeOffset(now.Date.AddDays(1))-new DateTimeOffset(now);timer.Start();}return;
        }
        NativeCard.Check(NativeCard.SetText(Handle,now.ToString(State.Seconds?"HH:mm:ss":"HH:mm",CultureInfo.CurrentCulture),
            State.Date?now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture):""));
        timer.Interval=ClockSchedule.Next(now,State.Seconds);timer.Start();
    }
    internal void Restore()
    {
        var displays=Display.All();
        var m=displays.Find(x=>x.Id==State.Monitor)??displays.Find(x=>x.Primary)??displays[0];
        var r=SnapEngine.Clamp(new(m.Work.X+State.X*m.Scale,m.Work.Y+State.Y*m.Scale,WidgetWidth*m.Scale,WidgetHeight*m.Scale),m.Work,8*m.Scale);
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
        r=r with {Width=WidgetWidth*m.Scale,Height=WidgetHeight*m.Scale};var result=snap.Snap(r,manager.Neighbors(this),m.Work,m.Scale);
        NativeCard.Check(NativeCard.SetDpi(Handle,(float)m.Scale));DesktopHost.Move(Handle,result.Bounds);
        float x=result.GuideX is double gx?(float)Math.Clamp((gx-result.Bounds.X)/m.Scale,2,WidgetWidth-2):-1;
        float y=result.GuideY is double gy?(float)Math.Clamp((gy-result.Bounds.Y)/m.Scale,2,WidgetHeight-2):-1;
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
                case 0x111:if(owner.input!=null&&message.LParam==owner.input.Handle){Native.SendMessageTimeout(message.LParam,0x2111,message.WParam,message.LParam,2,1000,out _);message.Result=0;return;}break;
                case 0x6:if(((long)message.WParam&0xffff)==0){owner.EndListPointer(false);owner.EndInput();}break;
                case 0xFF:owner.RawListPointer(message.LParam);break;
                case 0x201:if(owner.Editing)owner.BeginDrag();else owner.BeginListPointer();break;
                case 0x20A:owner.Scroll(unchecked((short)((long)message.WParam>>16)));message.Result=0;return;
                case 0x84:if(!owner.Editing&&owner.State.Kind=="Reminders"&&!owner.InteractivePoint()){message.Result=-1;return;}break;
                case 0x200:owner.MoveListPointer();owner.MoveDrag();break;
                case 0x202:owner.EndListPointer(true);owner.EndDrag();break;
                case 0x215:owner.EndListPointer(false);owner.EndDrag();break;
                case 0x1F:owner.EndListPointer(false);break;
                case 0x7B:owner.OpenMenu();message.Result=0;return;
                case 0x20:owner.UpdatePointerCursor();message.Result=1;return;
                case 0x21:message.Result=3;return;
                case 0x7E:case 0x2E0:Application.Current.Dispatcher.BeginInvoke(()=>{if(!owner.closed&&!owner.dragging)owner.Restore();});break;
                case 0x1A:case 0x320:Application.Current.Dispatcher.BeginInvoke(()=>{owner.ApplyTheme();owner.RefreshClock();});break;
                case 0x1E:owner.RefreshClock();break;
                case 0x82:Application.Current.Dispatcher.BeginInvoke(()=>owner.Close());break;
                }
                base.WndProc(ref message);
            }catch(Exception e){try{owner.EndListPointer(false);}catch{}Log.Write("Native card message failed: "+e);}
        }
    }
}
