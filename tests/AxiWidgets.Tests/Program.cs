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
Check("global transparency bounds and invalid values",()=> {
    var a=new AppearanceSettings {Transparency=-50}; a.Normalize(); Equal(0,a.Transparency); Equal(255,a.TintAlpha);
    a.Transparency=150; a.Normalize(); Equal(95,a.Transparency); Equal(13,a.TintAlpha);
    a.Transparency=double.NaN; a.Normalize(); Equal(56,a.Transparency);
});
Check("global appearance round trip",()=> {
    var a=System.Text.Json.JsonSerializer.Deserialize<AppearanceSettings>(System.Text.Json.JsonSerializer.Serialize(new AppearanceSettings {Blur=false,Transparency=75}))!;
    if(a.Blur) throw new Exception("Blur preference lost"); Equal(75,a.Transparency); Equal(64,a.TintAlpha);
});
Console.WriteLine($"{passed}/20 passed");

