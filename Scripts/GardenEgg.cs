using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenEgg : CharacterBody3D
{
    public static readonly string[] Colors = { "White","Red","Orange","Yellow","Green","Cyan","Blue","Purple","Pink","Brown","Gray","Black" };
    public GardenEggState Data { get; init; } = null!;
    public bool InFlight { get; private set; }
    public bool Cracked { get; private set; }
    public PetState? Chao => Data.Chao;
    public Node3D Art => visual;
    public void ShowUpset() => upset=Mathf.Max(upset,5);
    public float ShakeTime { get; private set; }
    private Vector3 velocity;
    private Node3D visual = null!;
    private Label3D? nameLabel;
    private float roam, gait, upset, sleepDelay=14, callRemaining, petAnimation, petCooldown;
    private Vector3 callTarget, foodStart;
    private FruitTree? meal;
    private readonly FruitMeal fruitMeal=new();
    private bool reserved;
    private readonly ChaoRest rest=new();
    private readonly FruitReaction fruitReaction=new();
    internal System.Collections.Generic.Dictionary<int,ChaoRest> BedSpaces { set => rest.Spaces=value; }
    internal void ConfuseOverFruit() => fruitReaction.Confuse();
    internal bool CanAcceptFruit => Chao!=null && fruitReaction.CanAcceptFruit(Chao);
    internal bool AnticipatesFruit => fruitReaction.AnticipatesFruit;
    internal void ClearUpset() => upset=0;
    private readonly ChaoMotion motion=new();
    private bool tossed;
    private float cliffDelay=38;
    private float napRemaining;
    private AudioStreamPlayer3D swimSound = null!;
    private float swimBeat;
    public bool Asleep { get; private set; }
    public bool CanReceive => !Asleep && !InFlight && !reserved && meal==null && !fruitReaction.IsConfused;
    public bool CanPet => CanReceive && Chao?.CanAct==true && petCooldown<=0;

    public static Color Pigment(string name) => new(name switch
    {
        "Red"=>"dd605b", "Orange"=>"e99b52", "Yellow"=>"e6ce61", "Green"=>"79b475",
        "Cyan"=>"78cbd4", "Blue"=>"668ce2", "Purple"=>"a481c7", "Pink"=>"eaa6ba",
        "Brown"=>"a37b61", "Gray"=>"a9afb0", "Black"=>"090a0c", _=>"eee5d1"
    });

    public override void _Ready()
    {
        CollisionLayer=0;
        AddChild(new CollisionShape3D { Position=new(0,.18f,0),Shape=new SphereShape3D { Radius=.17f } });
        swimSound=SwimAudio.Attach(this);
        if (Data.Hatched) MakeHatchling();
        else
        {
            visual=new Node3D(); AddChild(visual);
            DrawShell(visual,Pigment(Data.Color),new(.31f,.43f,.31f),.215f);
        }
    }

    public void Hold(Vector3 at,bool shaking,float dt)
    {
        if (Data.Hatched)
        {
            rest.Cancel();
            fruitReaction.Interrupt(visual);
            Asleep=false;
            if (meal!=null) { meal.Release(Vector3.Zero); meal=null; }
        }
        if (shaking) ShakeTime+=dt;
        Position=at;
        visual.Position=Vector3.Zero;
        visual.Rotation=new(0,0,shaking ? Mathf.Sin(ShakeTime*38)*.25f : 0);
    }

    public void Release(Vector3 impulse,bool thrown)
    {
        velocity=impulse;
        InFlight=true;
        motion.Reset();
        tossed=thrown;
        if (thrown && Data.Chao!=null)
        {
            upset=9; Data.Chao.Joy=Mathf.Max(0,Data.Chao.Joy-4);
            Data.Chao.Bond=Mathf.Max(0,Data.Chao.Bond-1);
        }
        Cracked=!Data.Hatched && !thrown && ShakeTime>5;
        visual.Rotation=Vector3.Zero;
        ShakeTime=0;
    }

    public void Call(Vector3 at)
    {
        if (Chao is not { CanAct:true, RespondsToCall:true } || InFlight || reserved || meal!=null ||
            Position.DistanceTo(at)>16) return;
        rest.Cancel(); Asleep=false;
        callTarget=at; callRemaining=11;
    }

    public void Pet()
    {
        if (!CanPet || Chao==null) return;
        Chao.Pet();
        (visual.FindChild("MoodOrb",true,false) as EmotionOrb)?.Celebrate();
        petAnimation=.93f;
        petCooldown=12;
    }

    public void ReserveForOffer() { reserved=true; rest.Cancel(); fruitReaction.Interrupt(visual); }
    public void FinishOffer() { reserved=false; roam=0; }
    public void EatPose(float remaining) => ImportedChao.EatPose(visual,remaining);

    public bool Advance(float dt,IEnumerable<FruitTree>? fruitSlots=null,ToyCar? toy=null,Vector3? fruitCarrier=null,ToyCar? rattle=null)
    {
        upset=Mathf.Max(0,upset-dt);
        petCooldown=Mathf.Max(0,petCooldown-dt);
        if (visual.FindChild("MoodOrb",true,false) is EmotionOrb orb) orb.SetUpset(upset>0);
        if (Data.Hatched && !InFlight) return AdvanceChao(dt,fruitSlots,toy,fruitCarrier,rattle);
        if (!InFlight) return false;
        bool changed=false;
        if (!ThrowMotion.Step(this,ref velocity,dt,hit=>
        {
            if (hit.GetCollider() is not GodotObject body || !body.HasMeta("stone")) return;
            if (!Data.Hatched) Cracked=true;
            else if (Data.Chao!=null && upset<10)
            { upset=12; Data.Chao.Joy=Mathf.Max(0,Data.Chao.Joy-8); changed=true; }
        }))
        {
            if (Data.Hatched) visual.Rotation=new(.25f,0,Mathf.Sin(roam*21)*.21f);
            roam+=dt;
            return changed;
        }
        InFlight=false;
        if (tossed && Data.Hatched) motion.Tumble();
        tossed=false;
        Data.X=Position.X; Data.Z=Position.Z;
        if (Cracked && !Data.Hatched) Hatch();
        roam=0;
        return true;
    }

    private bool AdvanceChao(float dt,IEnumerable<FruitTree>? fruitSlots,ToyCar? toy,Vector3? fruitCarrier,ToyCar? rattle)
    {
        var state=Data.Chao!;
        if (nameLabel!=null) nameLabel.Text=state.Name;
        if (reserved) return false;
        state.Tick(dt,Asleep);
        if (state.AdvanceLife(dt))
        {
            visual.QueueFree();
            if (nameLabel!=null) nameLabel.GetParent().QueueFree();
            MakeHatchling();
            return true;
        }
        if (!state.CanAct) return false;
        if (toy?.PlayPose(this,visual)==true || rattle?.PlayPose(this,visual)==true) return false;
        if (motion.Recover(visual,dt)) return false;
        if (petAnimation>0)
        {
            petAnimation=Mathf.Max(0,petAnimation-dt);
            visual.Rotation=new(0,0,Mathf.Sin(petAnimation*22)*.10f);
            return false;
        }
        if (Asleep)
        {
            ImportedChao.SleepPose(visual);
            napRemaining-=dt;
            if (napRemaining<=0 && state.Energy>=100) { Asleep=false; rest.Cancel(); sleepDelay=18; }
            return false;
        }
        if (meal!=null)
        {
            var mouth=Position+new Vector3(0,.225f,.135f).Rotated(Vector3.Up,Rotation.Y);
            fruitMeal.Advance(dt,meal,state,visual,mouth);
            if (meal.Bites<FruitTree.BiteCount) return false;
            meal.Regrow(); meal=null;
            return true;
        }
        if (fruitReaction.Step(this,visual,state,motion,fruitCarrier,dt)) return false;
        callRemaining=Mathf.Max(0,callRemaining-dt);
        bool called=callRemaining>0 && Position.DistanceTo(callTarget)>1.2f;
        Vector3 beforeRest=Position;
        if (!called && rest.Step(this,state,dt,out bool arrived))
        {
            var travel=Position-beforeRest; travel.Y=0;
            bool walking=!arrived && travel.Length()>dt*.075f;
            if (walking) gait+=dt*12;
            ImportedChao.Gait(visual,gait,walking,false);
            visual.Position=Vector3.Zero; visual.Rotation=Vector3.Zero;
            if (arrived) { Asleep=true; napRemaining=ChaoRest.MinimumSleep; ImportedChao.SleepPose(visual); }
            return false;
        }
        sleepDelay-=dt;
        if (!called && sleepDelay<=0 && state.Energy<95) rest.Request(Position);
        roam+=dt;
        float phase=Mathf.Floor(roam/4)*1.43f;
        Vector3 target=called ? callTarget : new(Data.X+Mathf.Sin(phase)*1.65f,0,Data.Z+Mathf.Cos(phase)*1.65f);
        cliffDelay-=dt;
        Vector3? cliff=called ? null : motion.CliffTarget(Position,dt,cliffDelay<=0);
        if (cliffDelay<=0) cliffDelay=65;
        if (cliff is Vector3 rockTarget) target=rockTarget;
        if (!called && cliff==null && state.Fullness>=76 && toy!=null && toy.Available(this,state))
        {
            if (toy.Use(this,visual,state,out var toyTarget)) return false;
            target=toyTarget;
        }
        if (!called && state.FindsFood && state.Fullness<76 && fruitSlots!=null)
        {
            var food=fruitSlots.Where(f=>!f.IsFeeder && f.HasFruitOnGround && f.FruitPosition.DistanceTo(Position)<12)
                .OrderBy(f=>f.FruitPosition.DistanceSquaredTo(Position)).FirstOrDefault();
            if (food!=null)
            {
                target=food.FruitPosition;
                if (target.DistanceTo(Position)<.55f)
                {
                    foodStart=food.FruitPosition; food.Hold(foodStart);
                    meal=food; fruitMeal.Begin(food,state,foodStart);
                    return false;
                }
            }
        }
        Vector3 offset=target-Position; offset.Y=0;
        Vector3 pondOffset=Position-GardenArt.Pond; pondOffset.Y=0;
        bool inWater=cliff==null && !motion.Airborne && Position.Y<.11f && pondOffset.Length()<GardenArt.PondRadius;
        bool swimming=inWater && state.Swim>=100;
        if (inWater && !swimming && !called) { offset=GardenArt.SwimExit-Position; offset.Y=0; }
        if (inWater) Position=new(Position.X,Mathf.MoveToward(Position.Y,0,dt*1.2f),Position.Z);
        bool moving=motion.Step(this,offset,state,false,inWater,dt,true,out bool hardLanding);
        if (moving) gait+=dt*Mathf.Clamp(motion.GroundSpeed*9,4,19);
        ImportedChao.MovePose(visual,gait,moving,inWater,swimming,motion.Sprinting,motion.Airborne,motion.Flying);
        SwimAudio.Paddle(swimSound,dt,inWater && moving,ref swimBeat);
        if (!hardLanding) return false;
        upset=9;
        state.Joy=Mathf.Max(0,state.Joy-5);
        return true;
    }

    private void Hatch()
    {
        Data.Hatched=true;
        ShellBurst(this,Pigment(Data.Color));
        visual.QueueFree();
        MakeHatchling();
    }

    public static void DrawShell(Node3D parent,Color color,Vector3 size,float height)
    {
        var shell=GardenArt.Ball(parent,new(0,height,0),size,Godot.Colors.White);
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            uniform vec4 shell_color : source_color;
            void fragment() {
                vec2 cell=UV*vec2(7.0,4.0);
                cell.x+=mod(floor(cell.y),2.0)*.5;
                float spot=1.0-smoothstep(.165,.19,length(fract(cell)-.5));
                float band=smoothstep(.14,.21,UV.y)*(1.0-smoothstep(.75,.88,UV.y));
                ALBEDO=mix(shell_color.rgb,shell_color.rgb*.64,spot*band);
                ROUGHNESS=.88;
            }
            """ } };
        material.SetShaderParameter("shell_color",color);
        shell.MaterialOverride=material;
    }

    public void Park(Vector3 at)
    {
        InFlight=false; Cracked=false; velocity=Vector3.Zero; roam=0;
        Position=at; Data.X=at.X; Data.Z=at.Z;
    }

    public static void ShellBurst(Node3D parent,Color color,bool sparkles=false)
    {
        var shell=new CpuParticles3D
        {
            Position=Vector3.Up*.18f, OneShot=true, Emitting=false, Explosiveness=1,
            Amount=18, Lifetime=.72f, Direction=Vector3.Up, Spread=74, Gravity=new(0,-5.5f,0),
            InitialVelocityMin=.7f,InitialVelocityMax=2.25f,
            Mesh=new BoxMesh { Size=sparkles ? new(.028f,.028f,.028f) : new(.052f,.024f,.073f),
                Material=GardenArt.Material(color,sparkles) }
        };
        parent.AddChild(shell);
        shell.Finished += shell.QueueFree;
        shell.Emitting=true;
    }

    public void RefreshArt()
    {
        RemoveChild(visual);
        visual.QueueFree();
        if (nameLabel!=null)
        {
            var label=nameLabel.GetParent();
            RemoveChild(label);
            label.QueueFree();
        }
        MakeHatchling();
    }

    private void MakeHatchling()
    {
        Data.Chao ??= new PetState { Name=Data.Color+" Chao",GardenEggs=new(),
            LikesToyCars=Data.Color is "Red" or "Yellow" or "White" or "Black",
            Glutton=GD.Randf()<.25f,Crybaby=GD.Randf()<.25f };
        visual=Data.Chao.CanAct ? ImportedChao.Build(this,Data.Chao.Form) :
            GardenArt.LifeShell(this,Data.Chao.IsEgg);
        nameLabel=GardenArt.Sign(this,Data.Chao.Name,new(0,.63f,0),GardenArt.Cream);
        var bodyPaint=GardenArt.Material(Pigment(Data.Color));
        if (Data.Color=="Black")
        {
            bodyPaint.EmissionEnabled=true;
            bodyPaint.Emission=new Color("343743");
            bodyPaint.EmissionEnergyMultiplier=.19f;
        }
        foreach (var mesh in visual.FindChildren("*","MeshInstance3D",true,false))
        {
            if (mesh is not MeshInstance3D part || part.GetParent() is EmotionOrb) continue;
            string name=part.Name;
            if (name.Contains("Eye"))
            {
                if (Data.Color=="Black") AddEyeRim(part);
                continue;
            }
            if (name!="Wings") part.MaterialOverride=bodyPaint;
        }
    }

    private static void AddEyeRim(MeshInstance3D eye)
    {
        var center=eye.GetAabb().GetCenter();
        var scale=new Vector3(1.16f,1.12f,1);
        var rim=new MeshInstance3D { Mesh=eye.Mesh, Scale=scale,
            Position=center-center*scale-Vector3.Back*.002f,
            MaterialOverride=GardenArt.Material(new Color("e0d7c6")) };
        eye.AddChild(rim);
    }
}
