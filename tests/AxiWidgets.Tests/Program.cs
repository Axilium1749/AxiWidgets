using AxiWidgets.Core;

int passed=0;
void Check(string name,Action test) { try {test();Console.WriteLine($"PASS {name}");passed++;} catch(Exception e) {Console.Error.WriteLine($"FAIL {name}: {e.Message}");Environment.ExitCode=1;} }
void Equal(double expected,double actual) {if(Math.Abs(expected-actual)>.001)throw new Exception($"expected {expected}, got {actual}");}
var work=new Rect(0,0,1920,1080);
SnapResult Snap(Rect r,params Rect[] others)=>new SnapEngine().Snap(r,others,work);
Check("top left corner",()=> {var r=Snap(new(11,15,300,160)).Bounds;Equal(8,r.X);Equal(8,r.Y);});
Check("bottom right corner",()=> {var r=Snap(new(1605,906,300,160)).Bounds;Equal(1612,r.X);Equal(912,r.Y);});
Check("nearest edge",()=>Equal(410,Snap(new(407,400,300,160),new Rect(400,700,300,160),new Rect(410,800,300,160)).Bounds.X));
Check("left gap",()=>Equal(388,Snap(new(390,300,300,160),new Rect(700,300,300,160)).Bounds.X));
Check("right gap",()=>Equal(1012,Snap(new(1018,300,300,160),new Rect(700,300,300,160)).Bounds.X));
Check("above gap",()=>Equal(328,Snap(new(700,334,300,160),new Rect(700,500,300,160)).Bounds.Y));
Check("below gap",()=>Equal(672,Snap(new(700,677,300,160),new Rect(700,500,300,160)).Bounds.Y));
Check("center X",()=>Equal(750,Snap(new(754,200,300,160),new Rect(700,600,400,160)).Bounds.X));
Check("center Y",()=>Equal(520,Snap(new(1100,524,300,160),new Rect(700,500,300,200)).Bounds.Y));
Check("tie stable independent of order",()=> {var a=new Rect(400,800,300,160);var b=new Rect(420,800,300,160);Equal(400,Snap(new(410,300,300,160),a,b).Bounds.X);Equal(400,Snap(new(410,300,300,160),b,a).Bounds.X);});
Check("outside threshold",()=>Equal(100,Snap(new(100,100,300,160)).Bounds.X));
Check("hysteresis releases",()=> {var s=new SnapEngine();Equal(8,s.Snap(new(18,100,300,160),[],work).Bounds.X);Equal(8,s.Snap(new(24,100,300,160),[],work).Bounds.X);Equal(30,s.Snap(new(30,100,300,160),[],work).Bounds.X);});
Check("negative origin scaled monitor",()=> {var r=new SnapEngine().Snap(new(-1900,15,450,240),[],new Rect(-1920,0,1920,1080),1.5).Bounds;Equal(-1908,r.X);Equal(12,r.Y);});
Check("unplug recovery clamp",()=> {var r=SnapEngine.Clamp(new(-2000,2000,300,160),work);Equal(8,r.X);Equal(912,r.Y);});
Check("minute boundary",()=>Equal(29750,ClockSchedule.Next(new DateTime(2026,10,28,12,0,30,250),false).TotalMilliseconds));
Check("second boundary",()=>Equal(750,ClockSchedule.Next(new DateTime(2026,10,28,12,0,30,250),true).TotalMilliseconds));
Check("exact boundary sleeps full period",()=>Equal(60000,ClockSchedule.Next(new DateTime(2026,10,28,12,0,0),false).TotalMilliseconds));
Check("legacy layout keeps instance settings without System Acrylic",()=> {
    var clock=System.Text.Json.JsonSerializer.Deserialize<ClockState>("{\"Acrylic\":true,\"NativeAcrylic\":false,\"Seconds\":true,\"Date\":false,\"Monitor\":\"display-2\",\"X\":120,\"Y\":80}")!;
    if(!clock.Acrylic||!clock.Seconds||clock.Date||clock.Monitor!="display-2") throw new Exception("Legacy settings changed");
    Equal(120,clock.X); Equal(80,clock.Y);
    if(System.Text.Json.JsonSerializer.Serialize(clock).Contains("NativeAcrylic")) throw new Exception("Removed backend selector persisted");
});
Check("tint opacity bounds and invalid values",()=> {
    var a=new AppearanceSettings {TintOpacity=-50};a.Normalize();Equal(0,a.TintOpacity);Equal(0,a.TintAlpha);
    a.TintOpacity=150;a.Normalize();Equal(100,a.TintOpacity);Equal(255,a.TintAlpha);
    a.TintOpacity=double.NaN;a.Normalize();Equal(55,a.TintOpacity);
});
Check("independent tint color and opacity round trip",()=> {
    var a=System.Text.Json.JsonSerializer.Deserialize<AppearanceSettings>(System.Text.Json.JsonSerializer.Serialize(new AppearanceSettings {Blur=false,TintColor="#804020",TintOpacity=75}))!;
    if(a.Blur||a.TintColor!="#804020"||a.TintRgb!=0x804020)throw new Exception("Tint settings lost");
    Equal(75,a.TintOpacity);Equal(191,a.TintAlpha);a.Normalize();if(!a.Blur)throw new Exception("Native blur must stay enabled");
});
Check("reject malformed tint colors",()=> {
    foreach(var value in new string?[] {null,"red","#GGGGGG","#12345","#12345678"})
        if(AppearanceSettings.TryColor(value,out _))throw new Exception($"Accepted {value}");
    var a=new AppearanceSettings {TintColor="invalid"};a.Normalize();
    if(a.TintColor!="#1E1E22")throw new Exception("Wrong tint fallback");
});
Check("calendar all month lengths and Monday origin",()=>{
    var culture=System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
    for(int year=2024;year<=2027;year++)for(int month=1;month<=12;month++){
        var today=new DateTime(year,month,DateTime.DaysInMonth(year,month));var days=CalendarGrid.Build(today,culture);
        Equal(42,days.Length);if(days[0].Date.DayOfWeek!=DayOfWeek.Monday)throw new Exception("Week origin");
        Equal(DateTime.DaysInMonth(year,month),days.Count(x=>x.CurrentMonth));Equal(1,days.Count(x=>x.Today));
        for(int i=1;i<42;i++)if(days[i].Date!=days[i-1].Date.AddDays(1))throw new Exception("Calendar gap");
    }
});
Check("calendar year boundary and localized calendar",()=>{
    foreach(string name in new[]{"en-US","ru-RU","ar-SA"}){
        var days=CalendarGrid.Build(new DateTime(2026,1,1),System.Globalization.CultureInfo.GetCultureInfo(name));
        Equal(1,days.Count(x=>x.Today));if(!days.Any(x=>!x.CurrentMonth))throw new Exception("Missing adjacent dates");
    }
});
Check("reminders persistence order completion removal and empty input",()=>{
    var folder=Path.Combine(Path.GetTempPath(),"AxiWidgets-test-"+Guid.NewGuid());var path=Path.Combine(folder,"reminders.json");
    try{
        var store=new ReminderStore(path);if(store.Add("   "))throw new Exception("Empty task added");
        store.Add("  Первая задача  ");store.Add("Second");var id=store.Items[0].Id;store.Toggle(id);
        var loaded=new ReminderStore(path);Equal(2,loaded.Items.Count);if(loaded.Items[0].Text!="Первая задача"||!loaded.Items[0].Done||loaded.Items[0].Id!=id)throw new Exception("Round trip failed");
        loaded.Toggle(id);if(loaded.Items[0].Done)throw new Exception("Reactivate failed");loaded.Remove(loaded.Items[1].Id);
        loaded=new ReminderStore(path);Equal(1,loaded.Items.Count);if(loaded.Items[0].Id!=id)throw new Exception("Wrong task deleted");
    }finally{if(File.Exists(path))File.Delete(path);if(Directory.Exists(folder))Directory.Delete(folder);}
});
Check("widget kinds preserve old clock layouts",()=>{
    if(System.Text.Json.JsonSerializer.Deserialize<ClockState>("{}")!.Kind!="Clock")throw new Exception("Legacy kind");
    foreach(var kind in new[]{"Clock","Calendar","Reminders"}){var original=new ClockState{Kind=kind};var round=System.Text.Json.JsonSerializer.Deserialize<ClockState>(System.Text.Json.JsonSerializer.Serialize(original))!;if(round.Kind!=kind||round.Id!=original.Id)throw new Exception("Widget kind lost");}
});
Console.WriteLine($"{passed}/25 passed");
