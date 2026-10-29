using System.Globalization;
using System.Text.Json;

namespace AxiWidgets.Core;

public record CalendarDay(DateTime Date,bool CurrentMonth,bool Today);
public static class CalendarGrid
{
    public static CalendarDay[] Build(DateTime today,CultureInfo culture)
    {
        var calendar=culture.DateTimeFormat.Calendar;
        var first=today.Date.AddDays(1-calendar.GetDayOfMonth(today));
        var start=first.AddDays(-((int)first.DayOfWeek+6)%7);
        return Enumerable.Range(0,42).Select(i=> {var d=start.AddDays(i);return new CalendarDay(d,calendar.GetMonth(d)==calendar.GetMonth(today)&&calendar.GetYear(d)==calendar.GetYear(today),d==today.Date);}).ToArray();
    }
}
public sealed class ReminderItem
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Text {get;set;}="";
    public bool Done {get;set;}
}
public sealed class ReminderStore
{
    private readonly string path;
    public List<ReminderItem> Items {get;private set;}=[];
    public event Action? Changed;
    public ReminderStore(string path){this.path=path;if(File.Exists(path))Items=JsonSerializer.Deserialize<List<ReminderItem>>(File.ReadAllText(path))??[];}
    public bool Add(string text){text=text.Trim();if(text.Length==0)return false;Items.Add(new(){Text=text.Length>200?text[..200]:text});Save();return true;}
    public void Toggle(Guid id){var item=Items.Find(x=>x.Id==id);if(item==null)return;item.Done=!item.Done;Save();}
    public void Remove(Guid id){if(Items.RemoveAll(x=>x.Id==id)>0)Save();}
    private void Save(){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Items));File.Move(path+".tmp",path,true);Changed?.Invoke();}
}
