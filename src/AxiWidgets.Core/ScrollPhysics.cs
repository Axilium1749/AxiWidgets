namespace AxiWidgets.Core;

public sealed record ScrollParameters(double Resistance=0.55,double RubberLength=72,double Friction=9,
    double SpringStiffness=210,double SpringDamping=27,double MaxVelocity=1800,double WheelImpulse=5,
    double WheelCooldown=.3,double WheelReturnFraction=.65);

// Coordinates and velocity are DIP and DIP/second; no UI or platform dependencies.
public sealed class ScrollPhysics(ScrollParameters? parameters=null)
{
    public ScrollParameters Parameters {get;}=parameters??new();
    public double Position {get;private set;}
    public double Velocity {get;private set;}
    public double Maximum {get;private set;}
    public bool Dragging {get;private set;}
    public bool Moving=>!Dragging&&(Math.Abs(Velocity)>.5||Math.Abs(Position-(springTarget??Math.Clamp(Position,0,Maximum)))>.05);
    private double? springTarget;
    private double origin,lastTime;
    private double modelTime,lastWheelOverscroll=double.NegativeInfinity,wheelPeak;
    private int wheelDirection;
    private bool wheelMotion,wheelArmOnArrival;
    public void Resize(double maximum){Maximum=Math.Max(0,maximum);if(springTarget.HasValue)springTarget=Math.Clamp(springTarget.Value,0,Maximum);}
    public void Jump(double position){Position=Math.Clamp(position,0,Maximum);Velocity=0;Dragging=false;springTarget=null;wheelMotion=false;}
    public void Begin(double time){Dragging=true;wheelMotion=false;springTarget=null;double bound=Math.Clamp(Position,0,Maximum),over=Position-bound;origin=bound+Math.Sign(over)*Math.Abs(over)*Parameters.RubberLength/Math.Max(.1,Parameters.RubberLength-Math.Abs(over))/Parameters.Resistance;Velocity=0;lastTime=time;}
    public void Drag(double displacement,double time)
    {
        double raw=origin+displacement,bound=Math.Clamp(raw,0,Maximum),over=raw-bound;
        double next=bound+Math.Sign(over)*Parameters.RubberLength*(Math.Abs(over)*Parameters.Resistance)/(Parameters.RubberLength+Math.Abs(over)*Parameters.Resistance);
        double dt=time-lastTime;if(dt>0){double sample=(next-Position)/Math.Max(dt,.004);Velocity=Velocity*.3+sample*.7;}
        Position=next;lastTime=time;
    }
    public void Release(double time){Dragging=false;if(time-lastTime>.1)Velocity=0;Velocity=Math.Clamp(Velocity,-Parameters.MaxVelocity,Parameters.MaxVelocity);}
    public void Cancel(){Dragging=false;Velocity=0;springTarget=null;}
    public ScrollPhysics Copy()=>(ScrollPhysics)MemberwiseClone();
    public bool Wheel(double delta,double? time=null)
    {
        if(time.HasValue)modelTime=time.Value;
        if(delta==0||Dragging)return false;
        int direction=Math.Sign(-delta);
        bool outward=Position<0?direction<0:Position>Maximum?direction>0:
            (direction<0&&(Position<=0||springTarget==0))||(direction>0&&(Position>=Maximum||springTarget==Maximum));
        if(outward){
            double stretch=direction<0?Math.Max(0,-Position):Math.Max(0,Position-Maximum);
            if(wheelDirection==direction&&(modelTime-lastWheelOverscroll<Parameters.WheelCooldown
                ||stretch>wheelPeak*Parameters.WheelReturnFraction||direction*Velocity>.5))return false;
            wheelDirection=direction;lastWheelOverscroll=modelTime;wheelPeak=stretch;
        }
        wheelMotion=true;wheelArmOnArrival=!outward;springTarget=null;Velocity=Math.Clamp(Velocity-delta*Parameters.WheelImpulse,-Parameters.MaxVelocity,Parameters.MaxVelocity);return true;
    }
    public void Step(double elapsed)
    {
        if(Dragging)return;
        double remaining=Math.Clamp(elapsed,0,.05);
        while(remaining>0){double dt=Math.Min(remaining,1.0/240);remaining-=dt;
            modelTime+=dt;
            double bound=Math.Clamp(Position,0,Maximum);
            if(wheelMotion&&Position!=bound){
                int direction=Math.Sign(Position-bound);
                if(wheelArmOnArrival||wheelDirection!=direction){wheelDirection=direction;lastWheelOverscroll=modelTime;wheelPeak=0;wheelArmOnArrival=false;}
                wheelPeak=Math.Max(wheelPeak,Math.Abs(Position-bound));
            }
            if(!springTarget.HasValue&&Position!=bound)springTarget=bound;
            if(springTarget.HasValue)Velocity+=(-Parameters.SpringStiffness*(Position-springTarget.Value)-Parameters.SpringDamping*Velocity)*dt;
            else Velocity*=Math.Exp(-Parameters.Friction*dt);
            Position+=Velocity*dt;
        }
        if(!Moving){Position=springTarget??Math.Clamp(Position,0,Maximum);Velocity=0;springTarget=null;}
    }
}
