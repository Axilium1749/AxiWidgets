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
        if(tasksInitialized&&insertedTasks.Count>0)scroll=0;
        RefreshClock();
    }
    private WidgetDrawing DrawReminders()
    {
        var items=manager.Reminders.Items;
        var drawing=WidgetDrawing.Reminders(items,scroll,input?.Text,input?.SelectionStart??0,renderedRows,insertedTasks);
        renderedRows=items.Skip(Math.Max(0,scroll-6)).Take(scroll<6?scroll+11:17).Select(x=>x.Id).ToHashSet();
        knownTasks=items.Select(x=>x.Id).ToHashSet();insertedTasks.Clear();tasksInitialized=true;
        return drawing;
    }
}
