using System;
using System.Collections.Generic;
using System.Linq;

namespace AxiWidgets;
internal sealed partial class ClockWindow
{
    private HashSet<Guid> renderedRows=[];
    private HashSet<Guid> knownTasks=[];
    private HashSet<Guid> insertedTasks=[];
    private bool tasksInitialized;
    private void ReminderDataChanged()
    {
        var ids=manager.Reminders.Items.Select(x=>x.Id).ToHashSet();
        insertedTasks=ids.Except(knownTasks).ToHashSet();
        ResizeScroll();
        if(tasksInitialized&&insertedTasks.Count>0){StopScrollMotion();scrollPhysics.Jump(0);scroll=0;}
        else if(scroll>scrollPhysics.Maximum)StartScrollMotion();
        RefreshClock();
    }
    private WidgetDrawing DrawReminders()
    {
        var items=manager.Reminders.Items;
        var drawing=WidgetDrawing.Reminders(items,scroll,input?.Text,input?.SelectionStart??0,renderedRows,insertedTasks);
        renderedRows=items.Skip(Math.Max(0,(int)(scroll/52)-6)).Take(scroll<312?(int)(scroll/52)+11:17).Select(x=>x.Id).ToHashSet();
        knownTasks=items.Select(x=>x.Id).ToHashSet();insertedTasks.Clear();tasksInitialized=true;
        return drawing;
    }
}
