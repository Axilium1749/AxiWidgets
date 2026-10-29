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
    private Window? input;
    private nint previousForeground;
    private (double X,double Y) LocalPointer(){Native.GetCursorPos(out var p);var r=Native.Bounds(Handle);var scale=r.Width/WidgetWidth;return((p.X-r.X)/scale,(p.Y-r.Y)/scale);}
    private bool InteractivePoint(){var(x,y)=LocalPointer();return x>=16&&x<=348&&((y>=86&&y<=308)||(y>=322&&y<=367));}
    private void Scroll(short delta){if(Editing||State.Kind!="Reminders")return;scroll=Math.Clamp(scroll+(delta<0?1:-1),0,Math.Max(0,manager.Reminders.Items.Count-4));RefreshClock();}
    private void ReminderClick()
    {
        if(State.Kind!="Reminders")return;
        var(x,y)=LocalPointer();
        if(y>=322&&y<=367&&x>=18&&x<=346){BeginInput();return;}
        if(y<92||y>=300)return;
        int index=scroll+(int)(y-92)/52;if(index>=manager.Reminders.Items.Count)return;
        var id=manager.Reminders.Items[index].Id;
        try{if(x>=30&&x<=64)manager.Reminders.Toggle(id);else if(x>=306&&x<=336)manager.Reminders.Remove(id);}
        catch(Exception e){Log.Write("Reminder save failed: "+e);MessageBox.Show("Could not save reminders. Please check the data folder.","AxiWidgets");}
    }
    private void BeginInput()
    {
        if(input!=null){input.Activate();return;}
        previousForeground=Native.GetForegroundWindow();
        var field=new TextBox {MaxLength=200,FontSize=15,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromRgb(40,40,44)),BorderThickness=new Thickness(0),Padding=new Thickness(8,4,8,4),VerticalContentAlignment=VerticalAlignment.Center};
        var editor=new Window {Title="Add a reminder",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,SizeToContent=SizeToContent.Manual,Width=250,Height=35,Content=field,Background=field.Background};
        input=editor;new WindowInteropHelper(editor).Owner=Handle;
        editor.SourceInitialized+=(_,_)=>{
            var h=new WindowInteropHelper(editor).Handle;Native.SetLong(h,-20,(nint)(((long)Native.GetLong(h,-20)|0x80)&~0x40000L));
        };
        editor.Loaded+=(_,_)=>{
            var r=Native.Bounds(Handle);double scale=r.Width/WidgetWidth;
            Native.SetWindowPos(new WindowInteropHelper(editor).Handle,0,(int)(r.X+80*scale),(int)(r.Y+327*scale),(int)(254*scale),(int)(35*scale),4);
            field.Focus();Keyboard.Focus(field);
        };
        field.KeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){e.Handled=true;EndInput();}
            else if(e.Key==Key.Enter){e.Handled=true;try{if(manager.Reminders.Add(field.Text))field.Clear();}catch(Exception ex){Log.Write("Reminder save failed: "+ex);field.ToolTip="Could not save reminder";}}
        };
        editor.Deactivated+=(_,_)=>EndInput();
        editor.Closed+=(_,_)=>{if(input==editor)input=null;};
        editor.Show();editor.Activate();
    }
    private void EndInput()
    {
        if(input==null)return;var editor=input;input=null;
        bool focused=Native.GetForegroundWindow()==new WindowInteropHelper(editor).Handle;
        editor.Close();if(focused&&Native.IsWindow(previousForeground))Native.SetForegroundWindow(previousForeground);
    }
    internal void SmokeReminderInput()
    {
        if(Editing)throw new InvalidOperationException("Input test requires normal mode");
        BeginInput();var editor=input??throw new InvalidOperationException("No input editor");editor.UpdateLayout();
        var field=(TextBox)editor.Content;int count=manager.Reminders.Items.Count;
        field.Text="Smoke reminder";
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(field),0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
        if(manager.Reminders.Items.Count!=count+1||field.Text.Length!=0)throw new InvalidOperationException("Enter did not submit and clear");
        field.Text="   ";field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(field),0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
        if(manager.Reminders.Items.Count!=count+1)throw new InvalidOperationException("Blank input added");
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(field),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});
        if(input!=null||Editing)throw new InvalidOperationException("Escape/input mode cleanup failed");
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
            ReminderClick();if(manager.Reminders.Items.Find(x=>x.Id==id)!.Done)throw new InvalidOperationException("Checkbox reset failed");
            Native.SetCursorPos((int)(r.X+320*scale),(int)(r.Y+117*scale));ReminderClick();
            if(manager.Reminders.Items.Exists(x=>x.Id==id))throw new InvalidOperationException("Delete hit failed");
        }finally{foreach(var id in added)manager.Reminders.Remove(id);Native.SetCursorPos(saved.X,saved.Y);}
    }
}
