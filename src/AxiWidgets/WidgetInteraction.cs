using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace AxiWidgets;
internal sealed partial class ClockWindow
{
    internal float WidgetWidth=>State.Kind=="Clock"?300:364;
    internal float WidgetHeight=>State.Kind=="Clock"?160:384;
    private int scroll;
    private System.Windows.Forms.TextBox? input;
    private InputMessages? inputMessages;
    private nint previousForeground;
    private (double X,double Y) LocalPointer(){Native.GetCursorPos(out var p);var r=Native.Bounds(Handle);var scale=r.Width/WidgetWidth;return((p.X-r.X)/scale,(p.Y-r.Y)/scale);}
    private bool InteractivePoint(){var(x,y)=LocalPointer();return input!=null||x>=16&&x<=348&&((y>=86&&y<=308)||(y>=322&&y<=367));}
    private void Scroll(short delta){if(Editing||State.Kind!="Reminders")return;scroll=Math.Clamp(scroll+(delta<0?1:-1),0,Math.Max(0,manager.Reminders.Items.Count-4));RefreshClock();}
    private void ReminderClick()
    {
        if(State.Kind!="Reminders")return;
        var(x,y)=LocalPointer();
        if(y>=322&&y<=367&&x>=18&&x<=346){BeginInput();return;}
        EndInput();
        if(y<92||y>=300)return;
        int index=scroll+(int)(y-92)/52;if(index>=manager.Reminders.Items.Count)return;
        var id=manager.Reminders.Items[index].Id;
        try{if(x>=30&&x<=64)manager.Reminders.Toggle(id);else if(x>=306&&x<=336)manager.Reminders.Remove(id);}
        catch(Exception e){Log.Write("Reminder save failed: "+e);MessageBox.Show("Could not save reminders. Please check the data folder.","AxiWidgets");}
    }
    private void BeginInput()
    {
        if(input!=null){Native.SetFocus(input.Handle);return;}
        previousForeground=Native.GetForegroundWindow();
        // A clipped native edit control supplies Windows keyboard/clipboard editing.
        // All visible text stays on the existing Composition surface.
        var field=new System.Windows.Forms.TextBox {MaxLength=200,TabStop=false};
        input=field;var h=field.Handle;Native.SetParent(h,Handle);
        Native.SetLong(h,-16,(nint)(((long)Native.GetLong(h,-16)&~0x80000000L)|0x40000000L));
        Native.SetWindowPos(h,0,-100,-100,1,1,4|0x10);Native.ShowWindow(h,5);
        inputMessages=new InputMessages(this);inputMessages.AssignHandle(h);
        field.TextChanged+=(_,_)=>RefreshClock();
        Native.SetLong(Handle,-20,(nint)((long)Native.GetLong(Handle,-20)&~0x08000000L));
        Native.SetForegroundWindow(Handle);Native.SetFocus(h);RefreshClock();
    }
    private void SubmitInput()
    {
        if(input==null)return;
        try{if(manager.Reminders.Add(input.Text))input.Clear();}
        catch(Exception e){Log.Write("Reminder save failed: "+e);}
    }
    private void EndInput()
    {
        if(input==null)return;var field=input;input=null;
        bool focused=Native.GetForegroundWindow()==Handle;
        inputMessages?.ReleaseHandle();inputMessages=null;field.Dispose();
        Native.SetLong(Handle,-20,(nint)((long)Native.GetLong(Handle,-20)|0x08000000L));
        RefreshClock();if(focused&&Native.IsWindow(previousForeground))Native.SetForegroundWindow(previousForeground);
    }
    private sealed class InputMessages(ClockWindow owner):System.Windows.Forms.NativeWindow
    {
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if(m.Msg==0x100&&(m.WParam==13||m.WParam==27)){
                if(m.WParam==13)owner.SubmitInput();else owner.EndInput();m.Result=0;return;
            }
            if(m.Msg==0x102&&(m.WParam==13||m.WParam==27)){m.Result=0;return;}
            base.WndProc(ref m);
            if(m.Msg==0x101)owner.RefreshClock();
        }
    }
    internal void SmokeReminderInput()
    {
        if(Editing)throw new InvalidOperationException("Input test requires normal mode");
        BeginInput();var field=input??throw new InvalidOperationException("No input editor");
        if(Native.GetFocus()!=field.Handle)throw new InvalidOperationException("Keyboard focus missing");
        int count=manager.Reminders.Items.Count;field.Text="Smoke reminder";
        Native.SendMessageTimeout(field.Handle,0x100,13,0,2,1000,out _);
        if(manager.Reminders.Items.Count!=count+1||field.Text.Length!=0)throw new InvalidOperationException("Enter did not submit and clear");
        field.Text="   ";SubmitInput();if(manager.Reminders.Items.Count!=count+1)throw new InvalidOperationException("Blank input added");
        Native.SendMessageTimeout(field.Handle,0x100,27,0,2,1000,out _);
        if(input!=null||Editing)throw new InvalidOperationException("Input cleanup failed");
        BeginInput();Native.GetCursorPos(out var pointer);var bounds=Native.Bounds(Handle);
        try{Native.SetCursorPos((int)bounds.X+80,(int)bounds.Y+30);ReminderClick();if(input!=null)throw new InvalidOperationException("Header click kept input active");}
        finally{EndInput();Native.SetCursorPos(pointer.X,pointer.Y);}
    }
    internal void SmokeDrag()
    {
        Native.GetCursorPos(out var saved);var before=Native.Bounds(Handle);
        try {
            SetEdit(true);Native.SetCursorPos((int)before.X+40,(int)before.Y+40);BeginDrag();
            if(Native.GetCapture()!=Handle)throw new InvalidOperationException("Drag capture failed");
            Native.SetCursorPos((int)before.X+100,(int)before.Y+80);MoveDrag();EndDrag();
            if(Native.Bounds(Handle)==before)throw new InvalidOperationException("Widget did not move");
        } finally {SetEdit(false);Native.SetCursorPos(saved.X,saved.Y);}
    }
    internal void SmokeReminderActions()
    {
        Native.GetCursorPos(out var saved);var r=Native.Bounds(Handle);double scale=r.Width/WidgetWidth;
        var added=new System.Collections.Generic.List<Guid>();
        try {
            for(int i=0;i<6;i++){manager.Reminders.Add("Scroll test "+i);added.Add(manager.Reminders.Items[^1].Id);}
            for(int i=0;i<manager.Reminders.Items.Count;i++)Scroll(-120);
            if(scroll!=manager.Reminders.Items.Count-4)throw new InvalidOperationException("Scroll range failed");
            var id=manager.Reminders.Items[scroll].Id;
            Native.SetCursorPos((int)(r.X+45*scale),(int)(r.Y+117*scale));ReminderClick();
            if(!manager.Reminders.Items.Find(x=>x.Id==id)!.Done)throw new InvalidOperationException("Checkbox hit failed");
            scroll=manager.Reminders.Items.FindIndex(x=>x.Id==id);RefreshClock();
            int row=manager.Reminders.Items.FindIndex(x=>x.Id==id)-scroll;
            Native.SetCursorPos((int)(r.X+45*scale),(int)(r.Y+(117+row*52)*scale));
            ReminderClick();if(manager.Reminders.Items.Find(x=>x.Id==id)!.Done)throw new InvalidOperationException("Checkbox reset failed");
            scroll=Math.Min(manager.Reminders.Items.FindIndex(x=>x.Id==id),Math.Max(0,manager.Reminders.Items.Count-4));RefreshClock();
            row=manager.Reminders.Items.FindIndex(x=>x.Id==id)-scroll;
            Native.SetCursorPos((int)(r.X+320*scale),(int)(r.Y+(117+row*52)*scale));ReminderClick();
            if(manager.Reminders.Items.Exists(x=>x.Id==id))throw new InvalidOperationException("Delete hit failed");
        }finally{foreach(var id in added)manager.Reminders.Remove(id);Native.SetCursorPos(saved.X,saved.Y);}
    }
}
