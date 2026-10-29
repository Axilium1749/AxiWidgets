namespace AxiWidgets.Core;

public sealed record ScrollParameters(double Resistance=0.55,double RubberLength=72,double Friction=9,
    double SpringStiffness=210,double SpringDamping=27,double MaxVelocity=1800,double WheelImpulse=5,
    double WheelOverscrollImpulse=0,double WheelCoastTime=.24,double WheelCoastDamping=12,
    double WheelOverscrollScale=.55);

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
    private double modelTime,wheelReturnStarted;
    private bool wheelMotion,wheelArmOnArrival,freeReturn;
    public void Resize(double maximum){Maximum=Math.Max(0,maximum);if(springTarget.HasValue)springTarget=Math.Clamp(springTarget.Value,0,Maximum);}
    public void Jump(double position){Position=Math.Clamp(position,0,Maximum);Velocity=0;Dragging=false;springTarget=null;wheelMotion=false;freeReturn=false;}
    public void Begin(double time){Dragging=true;wheelMotion=false;freeReturn=false;springTarget=null;double bound=Math.Clamp(Position,0,Maximum),over=Position-bound;origin=bound+Math.Sign(over)*Math.Abs(over)*Parameters.RubberLength/Math.Max(.1,Parameters.RubberLength-Math.Abs(over))/Parameters.Resistance;Velocity=0;lastTime=time;}
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
        double stretch=Math.Abs(Position-Math.Clamp(Position,0,Maximum));
        if(!outward&&(stretch>0||springTarget.HasValue)){
            // New inward input releases the old boundary target. Once back in
            // range, momentum must continue through the list rather than snap back.
            freeReturn=true;
            if(direction*Velocity<0)Velocity=0;
        }else if(outward)freeReturn=false;
        double boost=outward?direction*Parameters.WheelOverscrollImpulse*Math.Min(1,Math.Abs(delta)/120):0;
        double impulse=-delta*Parameters.WheelImpulse+boost;
        if(outward){wheelReturnStarted=modelTime;impulse*=Parameters.WheelOverscrollScale/(1+Math.Pow(stretch/Parameters.RubberLength,2));}
        wheelMotion=true;wheelArmOnArrival=!outward;springTarget=null;
        Velocity=Math.Clamp(Velocity+impulse,-Parameters.MaxVelocity,Parameters.MaxVelocity);return true;
    }
    public void Step(double elapsed)
    {
        if(Dragging)return;
        double remaining=Math.Clamp(elapsed,0,.05);
        while(remaining>0){double dt=Math.Min(remaining,1.0/240);remaining-=dt;
            modelTime+=dt;
            double bound=Math.Clamp(Position,0,Maximum);
            if(wheelMotion&&Position!=bound&&wheelArmOnArrival){wheelReturnStarted=modelTime;wheelArmOnArrival=false;}
            if(freeReturn&&Position==bound)freeReturn=false;
            if(!springTarget.HasValue&&Position!=bound&&!freeReturn)springTarget=bound;
            if(springTarget.HasValue||(freeReturn&&Position!=bound)){
                double ramp=wheelMotion?Math.Clamp((modelTime-wheelReturnStarted)/Parameters.WheelCoastTime,0,1):1;
                ramp=ramp*ramp*(3-2*ramp);
                double stiffness=Parameters.SpringStiffness*(wheelMotion?.25+.75*ramp:1);
                double damping=wheelMotion?Parameters.WheelCoastDamping+(Parameters.SpringDamping-Parameters.WheelCoastDamping)*ramp:Parameters.SpringDamping;
                Velocity+=(-stiffness*(Position-(springTarget??bound))-damping*Velocity)*dt;
            }
            else Velocity*=Math.Exp(-Parameters.Friction*dt);
            if(wheelMotion){
                // Approach the same rubber-band extent as drag without hitting a
                // hard position clamp. Only outward velocity is constrained.
                double rate=Math.Sqrt(Parameters.SpringStiffness);
                if(Velocity>0&&Position+Velocity*dt>Maximum)
                    Velocity=Math.Min(Velocity,Math.Max(0,Maximum+Parameters.RubberLength-Position)*rate);
                else if(Velocity<0&&Position+Velocity*dt<0)
                    Velocity=Math.Max(Velocity,-Math.Max(0,Position+Parameters.RubberLength)*rate);
            }
            Position+=Velocity*dt;
        }
        if(!Moving){Position=springTarget??Math.Clamp(Position,0,Maximum);Velocity=0;springTarget=null;}
    }
}
