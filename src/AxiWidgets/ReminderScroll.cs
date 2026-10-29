using System;
using System.Diagnostics;
using System.Windows.Threading;
using AxiWidgets.Core;

namespace AxiWidgets;
internal sealed partial class ClockWindow
{
    private ScrollPhysics scrollPhysics=new();
    private readonly DispatcherTimer scrollTimer=new(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
    private readonly Stopwatch scrollClock=Stopwatch.StartNew();
    private double motionStarted,pointerY,pointerX;
    private const double MotionInterval=1.0/240;
    private System.Collections.Generic.List<ScrollPhysics>? motionFrames;
    private bool listPressed,listDragging,scrollTimerHooked;
    private bool pointerLocked,rawAbsoluteKnown;
    private Native.Point pointerOrigin,rawAbsolute;
    private Native.Rectangle previousClip;
    private double accumulatedX,accumulatedY;
    private int renderedBand=int.MinValue;
    private void ResizeScroll()=>scrollPhysics.Resize(Math.Max(0,manager.Reminders.Items.Count*52-208));
    private void StartScrollMotion()
    {
        if(closed||State.Kind!="Reminders"||!scrollPhysics.Moving)return;
        if(!scrollTimerHooked){scrollTimer.Tick+=ScrollFrame;scrollTimerHooked=true;}
        var model=scrollPhysics.Copy();motionFrames=new(){model.Copy()};
        var positions=new System.Collections.Generic.List<float>{(float)model.Position};
        for(int i=0;i<1200&&model.Moving;i++){model.Step(MotionInterval);motionFrames.Add(model.Copy());positions.Add((float)model.Position);}
        if(positions.Count<2)return;
        NativeCard.Check(NativeCard.AnimateScroll(Handle,positions.ToArray(),positions.Count,(float)MotionInterval));
        motionStarted=scrollClock.Elapsed.TotalSeconds;scrollTimer.Start();
    }
    private void SyncScrollMotion()
    {
        if(motionFrames==null)return;
        double elapsed=Math.Max(0,scrollClock.Elapsed.TotalSeconds-motionStarted);
        int index=Math.Min(motionFrames.Count-1,(int)(elapsed/MotionInterval));
        scrollPhysics=motionFrames[index].Copy();
        if(index<motionFrames.Count-1)scrollPhysics.Step(elapsed-index*MotionInterval);
        PresentScroll(false);
        if(index==motionFrames.Count-1){scrollTimer.Stop();motionFrames=null;}
    }
    private void StopScrollMotion()
    {
        SyncScrollMotion();scrollTimer.Stop();motionFrames=null;scrollPhysics.Cancel();
        if(!closed&&State.Kind=="Reminders")NativeCard.Check(NativeCard.SetScroll(Handle,(float)scrollPhysics.Position));
    }
    private void ScrollFrame(object? sender,EventArgs args)
    {
        // Only maintain virtualized rows. DWM plays every motion frame itself.
        if(motionFrames!=null)SyncScrollMotion();else scrollTimer.Stop();
    }
    private void PresentScroll(bool applyOffset=true)
    {
        scroll=scrollPhysics.Position;
        int band=(int)Math.Floor(scroll/52);
        if(band!=renderedBand){renderedBand=band;RefreshClock();}
        if(applyOffset)NativeCard.Check(NativeCard.SetScroll(Handle,(float)scroll));
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
        if(Native.GetCapture()!=Handle){listPressed=false;scrollPhysics.Cancel();return;}
        Native.GetCursorPos(out pointerOrigin);
        accumulatedX=0;accumulatedY=0;rawAbsoluteKnown=false;
        var device=new Native.RawDevice {Page=1,Usage=2,Flags=0x100,Target=Handle};
        // Raw input remains relative even while the screen pointer is confined.
        if(Native.GetClipCursor(out previousClip)&&Native.RegisterRawInputDevices([device],1,(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.RawDevice>())){
            var clip=new Native.Rectangle {Left=pointerOrigin.X,Top=pointerOrigin.Y,Right=pointerOrigin.X+1,Bottom=pointerOrigin.Y+1};
            if(Native.ClipCursor(ref clip)){pointerLocked=true;Native.SetCursor(0);}
            else Native.RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=1}],1,(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.RawDevice>());
        }
    }
    private void MoveListPointer()
    {
        if(!listPressed||pointerLocked)return;var(x,y)=LocalPointer();
        if(!listDragging&&Math.Abs(y-pointerY)<5&&Math.Abs(x-pointerX)<5)return;
        listDragging=true;scrollPhysics.Drag(pointerY-y,scrollClock.Elapsed.TotalSeconds);PresentScroll();
    }
    private void RawListPointer(nint inputHandle)
    {
        if(!listPressed||!pointerLocked)return;
        uint size=48;if(Native.GetRawInputData(inputHandle,0x10000003,out var mouse,ref size,24)==uint.MaxValue||mouse.Type!=0)return;
        double dx=mouse.X,dy=mouse.Y;
        if((mouse.Flags&1)!=0){
            bool virtualDesktop=(mouse.Flags&2)!=0;
            var absolute=new Native.Point {X=(int)(dx*Native.GetSystemMetrics(virtualDesktop?78:0)/65535),Y=(int)(dy*Native.GetSystemMetrics(virtualDesktop?79:1)/65535)};
            if(!rawAbsoluteKnown){rawAbsolute=absolute;rawAbsoluteKnown=true;return;}
            dx=absolute.X-rawAbsolute.X;dy=absolute.Y-rawAbsolute.Y;rawAbsolute=absolute;
        }
        RelativeListPointer(dx,dy);
    }
    private void RelativeListPointer(double dx,double dy)
    {
        if(!listPressed||!pointerLocked)return;
        double scale=Native.Bounds(Handle).Width/WidgetWidth;
        accumulatedX+=dx/scale;accumulatedY+=dy/scale;
        if(!listDragging&&Math.Abs(accumulatedX)<5&&Math.Abs(accumulatedY)<5)return;
        listDragging=true;scrollPhysics.Drag(-accumulatedY,scrollClock.Elapsed.TotalSeconds);PresentScroll();
    }
    private void RestoreListPointer()
    {
        if(!pointerLocked)return;pointerLocked=false;
        Native.RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=1}],1,(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.RawDevice>());
        Native.ClipCursor(ref previousClip);Native.SetCursorPos(pointerOrigin.X,pointerOrigin.Y);
        UpdatePointerCursor();
    }
    private void UpdatePointerCursor()
    {
        if(pointerLocked){Native.SetCursor(0);return;}
        var(x,y)=LocalPointer();int cursor=Editing?32646:
            State.Kind=="Reminders"&&x>=16&&x<=348&&y>=86&&y<=308?32649:
            State.Kind=="Reminders"&&x>=18&&x<=346&&y>=322&&y<=367?32513:32512;
        int row=(int)Math.Floor((y-92+scroll)/52);
        bool control=row>=0&&row<manager.Reminders.Items.Count&&y>=92&&y<300&&((x>=30&&x<=64)||(x>=306&&x<=336));
        bool grab=cursor==32649&&!control;
        Native.SetCursor(grab&&manager.GrabCursor!=0?manager.GrabCursor:Native.LoadCursor(0,cursor));
    }
    private void EndListPointer(bool click)
    {
        if(!listPressed)return;bool moved=listDragging;listPressed=false;listDragging=false;
        RestoreListPointer();
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
                Native.SetCursorPos(x,y);BeginListPointer();
                if(!pointerLocked)throw new InvalidOperationException("Cursor lock failed");
                RelativeListPointer(0,2*scale);
                if(listDragging)throw new InvalidOperationException("Click threshold failed");
                RelativeListPointer(0,58*scale);
                if(!listDragging||Native.GetCapture()!=Handle)throw new InvalidOperationException("List drag capture failed");
                if(count<=2&&scroll>=0)throw new InvalidOperationException("Short list did not stretch");
                if(Native.Bounds(Handle)!=original)throw new InvalidOperationException("Normal scroll moved widget");
                EndListPointer(true);Native.GetCursorPos(out var restored);
                Native.GetClipCursor(out var restoredClip);
                if(pointerLocked||Math.Abs(restored.X-x)>2||Math.Abs(restored.Y-y)>2||restoredClip.Left!=previousClip.Left||restoredClip.Right!=previousClip.Right||restoredClip.Top!=previousClip.Top||restoredClip.Bottom!=previousClip.Bottom)throw new InvalidOperationException("Cursor position/clip restore failed");
                scrollTimer.Stop();motionFrames=null;
                for(int step=0;step<1600&&scrollPhysics.Moving;step++){scrollPhysics.Step(1.0/120);PresentScroll();}
                ScrollFrame(null,EventArgs.Empty);
                if(scrollTimer.IsEnabled||scroll<0||scroll>scrollPhysics.Maximum)throw new InvalidOperationException("Spring/idle cleanup failed");
                if(count>4){double before=scroll;Scroll(-1);scrollTimer.Stop();motionFrames=null;scrollPhysics.Step(.008);PresentScroll();if(scroll==before)throw new InvalidOperationException("Fine wheel delta ignored");}
                Native.SetCursorPos(x,y);BeginListPointer();Native.ReleaseCapture();
                if(pointerLocked||listPressed)throw new InvalidOperationException("Capture loss left cursor locked");
                Native.SetCursorPos(x,y);BeginListPointer();SetEdit(true);
                if(pointerLocked||listPressed)throw new InvalidOperationException("Edit mode left cursor locked");SetEdit(false);
            }
        }finally{EndListPointer(false);StopScrollMotion();foreach(var id in added)manager.Reminders.Remove(id);SettleScroll();Native.SetCursorPos(saved.X,saved.Y);}
    }
}
