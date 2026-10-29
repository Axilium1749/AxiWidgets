using System;
using System.Diagnostics;
using System.Windows.Threading;
using AxiWidgets.Core;

namespace AxiWidgets;
internal sealed partial class ClockWindow
{
    private readonly ScrollPhysics scrollPhysics=new();
    private readonly DispatcherTimer scrollTimer=new(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(8)};
    private readonly Stopwatch scrollClock=Stopwatch.StartNew();
    private double lastScrollFrame,pointerY,pointerX;
    private bool listPressed,listDragging,scrollTimerHooked;
    private int renderedBand=int.MinValue;
    private void ResizeScroll()=>scrollPhysics.Resize(Math.Max(0,manager.Reminders.Items.Count*52-208));
    private void StartScrollMotion()
    {
        if(closed||State.Kind!="Reminders")return;
        if(!scrollTimerHooked){scrollTimer.Tick+=ScrollFrame;scrollTimerHooked=true;}
        if(!scrollTimer.IsEnabled){lastScrollFrame=scrollClock.Elapsed.TotalSeconds;scrollTimer.Start();}
    }
    private void StopScrollMotion(){scrollTimer.Stop();scrollPhysics.Cancel();}
    private void ScrollFrame(object? sender,EventArgs args)
    {
        double now=scrollClock.Elapsed.TotalSeconds;scrollPhysics.Step(now-lastScrollFrame);lastScrollFrame=now;PresentScroll();
        if(!scrollPhysics.Moving)scrollTimer.Stop();
    }
    private void PresentScroll()
    {
        scroll=scrollPhysics.Position;
        int band=(int)Math.Floor(scroll/52);
        // Refill virtualized rows only on crossing a row boundary, not per frame.
        if(band!=renderedBand){renderedBand=band;RefreshClock();}
        NativeCard.Check(NativeCard.SetScroll(Handle,(float)scroll));
    }
    private void BeginListPointer()
    {
        if(State.Kind!="Reminders")return;
        var(x,y)=LocalPointer();
        if(x<16||x>348||y<86||y>308){ReminderClick();return;}
        EndInput();StopScrollMotion();ResizeScroll();
        listPressed=true;listDragging=false;pointerX=x;pointerY=y;
        renderedBand=(int)Math.Floor(scroll/52);
        scrollPhysics.Begin(scrollClock.Elapsed.TotalSeconds);Native.SetCapture(Handle);
    }
    private void MoveListPointer()
    {
        if(!listPressed)return;var(x,y)=LocalPointer();
        if(!listDragging&&Math.Abs(y-pointerY)<5&&Math.Abs(x-pointerX)<5)return;
        listDragging=true;scrollPhysics.Drag(pointerY-y,scrollClock.Elapsed.TotalSeconds);PresentScroll();
    }
    private void EndListPointer(bool click)
    {
        if(!listPressed)return;bool moved=listDragging;listPressed=false;listDragging=false;
        if(Native.GetCapture()==Handle)Native.ReleaseCapture();
        scrollPhysics.Release(scrollClock.Elapsed.TotalSeconds);
        if(click&&!moved)ReminderClick();
        if(!closed&&scrollPhysics.Moving)StartScrollMotion();
    }
    private void SettleScroll()
    {
        StopScrollMotion();ResizeScroll();scrollPhysics.Jump(scroll);PresentScroll();
    }
    internal void SmokePhysicalScroll()
    {
        Native.GetCursorPos(out var saved);var bounds=Native.Bounds(Handle);double scale=bounds.Width/WidgetWidth;
        var original=bounds;var added=new System.Collections.Generic.List<Guid>();
        try {
            foreach(int count in new[]{1,2,12}) {
                foreach(var id in added)manager.Reminders.Remove(id);added.Clear();
                for(int i=0;i<count;i++){manager.Reminders.Add("Physics "+i);added.Add(manager.Reminders.Items[0].Id);}
                ResizeScroll();scrollPhysics.Jump(count>4?scrollPhysics.Maximum/2:0);PresentScroll();
                int x=(int)(bounds.X+150*scale),y=(int)(bounds.Y+140*scale);
                Native.SetCursorPos(x,y);BeginListPointer();Native.SetCursorPos(x,y+2);MoveListPointer();
                if(listDragging)throw new InvalidOperationException("Click threshold failed");
                Native.SetCursorPos(x,y+60);MoveListPointer();
                if(!listDragging||Native.GetCapture()!=Handle)throw new InvalidOperationException("List drag capture failed");
                if(count<=2&&scroll>=0)throw new InvalidOperationException("Short list did not stretch");
                if(Native.Bounds(Handle)!=original)throw new InvalidOperationException("Normal scroll moved widget");
                EndListPointer(true);scrollTimer.Stop();
                for(int step=0;step<1600&&scrollPhysics.Moving;step++){scrollPhysics.Step(1.0/120);PresentScroll();}
                ScrollFrame(null,EventArgs.Empty);
                if(scrollTimer.IsEnabled||scroll<0||scroll>scrollPhysics.Maximum)throw new InvalidOperationException("Spring/idle cleanup failed");
                if(count>4){double before=scroll;Scroll(-1);scrollTimer.Stop();scrollPhysics.Step(.008);PresentScroll();if(scroll==before)throw new InvalidOperationException("Fine wheel delta ignored");}
            }
        }finally{EndListPointer(false);StopScrollMotion();foreach(var id in added)manager.Reminders.Remove(id);SettleScroll();Native.SetCursorPos(saved.X,saved.Y);}
    }
}
