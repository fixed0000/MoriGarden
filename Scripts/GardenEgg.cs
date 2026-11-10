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
    private Vector3 callTarget;
    private readonly ChaoInterests interests=new();
    private readonly ChaoPillarVisit pillar=new();
    private readonly ChaoWander wander=new();
    private Vector3 wanderTarget;
    private bool reserved;
    private readonly ChaoRest rest=new();
    private readonly IslandNap islandNap=new();
    private readonly ChaoVocal voice=new();
    private readonly FruitReaction fruitReaction=new();
    internal System.Collections.Generic.Dictionary<int,ChaoRest> BedSpaces { set => rest.Spaces=value; }
    internal void ConfuseOverOffer() => fruitReaction.Confuse();
    internal bool CanAcceptFruit => Chao!=null && fruitReaction.CanAcceptFruit(Chao);
    internal bool AnticipatesFruit => fruitReaction.AnticipatesFruit;
    internal void ClearUpset() => upset=0;
    private readonly ChaoMotion motion=new();
    private readonly ChaoDistress distress=new();
    private bool held;
    internal GardenCourtship? Courtship { get; set; }
    private bool tossed;
    private float cliffDelay=38;
    private float napRemaining;
    private AudioStreamPlayer3D swimSound = null!;
    private float swimBeat;
    public bool Asleep { get; private set; }
    public bool CanReceive => Courtship?.IsBlooming(this)!=true && !Asleep && !islandNap.IsNapping && !InFlight && !distress.Busy && !reserved && !interests.HasMeal;
    internal bool CanSocialize => Chao?.CanAct==true && CanReceive && !held && !islandNap.IsVisiting &&
        !motion.Airborne && !motion.Recovering && !motion.LeavingPond && !motion.OnCliffRoute &&
        !rest.Requested && !interests.Busy && !pillar.Active && !fruitReaction.Busy &&
        callRemaining<=0 && petAnimation<=0 && !GardenArt.IsPondWater(Position);
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
        AddToGroup("GardenChao");
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
            if (!held)
            {
                GetParent().GetNodeOrNull<WateringCan>("WateringCan")?.Cancel(this);
                Courtship?.Suspend(this);
                interests.Cancel(this,visual);
                pillar.Cancel();
                distress.PickUp(visual,Asleep || islandNap.IsNapping);
            }
            held=true;
            distress.Tick(dt);
            (visual.FindChild("MoodOrb",true,false) as EmotionOrb)?.SetDistressed(distress.Upset);
            rest.Cancel();
            islandNap.Cancel(visual);
            ImportedChao.CloseEyes(visual,false);
            fruitReaction.Interrupt(visual);
            Asleep=false;
        }
        if (shaking) ShakeTime+=dt;
        Position=at;
        visual.Position=Vector3.Zero;
        visual.Rotation=new(0,0,shaking ? Mathf.Sin(ShakeTime*38)*.25f : 0);
    }

    public void Release(Vector3 impulse,bool thrown)
    {
        held=false;
        velocity=impulse;
        InFlight=true;
        motion.Reset();
        tossed=thrown;
        if (thrown && Data.Chao!=null)
        {
            distress.BeginThrow();
            Data.Chao.ChangeRelationship(-1.25f);
            Data.Chao.Joy=Mathf.Max(0,Data.Chao.Joy-4);
            Data.Chao.Bond=Mathf.Max(0,Data.Chao.Bond-1);
        }
        Cracked=!Data.Hatched && !thrown && ShakeTime>5;
        visual.Rotation=Vector3.Zero;
        ShakeTime=0;
    }

    public void Call(Vector3 at)
    {
        if (Chao is not { CanAct:true, RespondsToCall:true } || InFlight || reserved || interests.HasMeal ||
            Position.DistanceTo(at)>16) return;
        GetParent().GetNodeOrNull<WateringCan>("WateringCan")?.Cancel(this);
        interests.Cancel(this,visual);
        pillar.Cancel();
        motion.StopCliffRoute();
        if (Asleep) motion.LeapFromBed(this,Chao);
        rest.Cancel(); islandNap.Cancel(visual); ImportedChao.CloseEyes(visual,false); Asleep=false;
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

    public bool HasMeal => interests.HasMeal;
    public FruitTree? TakeMeal()
    {
        var fruit=interests.TakeMeal(visual,Chao!);
        if (fruit!=null) ShowUpset();
        return fruit;
    }

    public void ReserveForOffer()
    {
        reserved=true;
        rest.Cancel();
        interests.Cancel(this,visual);
        fruitReaction.Interrupt(visual);
    }
    public void FinishOffer() { reserved=false; roam=0; }
    public void EatPose(float remaining) => ImportedChao.EatPose(visual,remaining);

    public bool Advance(float dt,IEnumerable<FruitTree>? fruitSlots=null,ToyCar? toy=null,Vector3? fruitCarrier=null,
        ToyCar? rattle=null,ToyCar? flute=null,WateringCan? wateringCan=null)
    {
        upset=Mathf.Max(0,upset-dt);
        petCooldown=Mathf.Max(0,petCooldown-dt);
        distress.Tick(dt);
        if (visual.FindChild("MoodOrb",true,false) is EmotionOrb orb)
        {
            orb.SetUpset(upset>0);
            orb.SetDistressed(distress.Upset);
        }
        if (Data.Hatched && !InFlight) return AdvanceChao(dt,fruitSlots,toy,fruitCarrier,rattle,flute,wateringCan);
        if (!InFlight) return false;
        if (tossed && Chao?.CanAct==true)
        {
            if (!distress.ThrowStep(this,visual,ref velocity,dt,hasPond:true)) return false;
            tossed=InFlight=false;
            Data.X=Position.X; Data.Y=Position.Y; Data.Z=Position.Z;
            roam=0;
            return true;
        }
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
        if (!tossed && Data.Hatched)
        {
            distress.PutDown();
            Courtship?.PutDown(this);
        }
        tossed=false;
        Data.X=Position.X; Data.Y=Position.Y; Data.Z=Position.Z;
        if (Cracked && !Data.Hatched) Hatch();
        roam=0;
        return true;
    }

    private bool AdvanceChao(float dt,IEnumerable<FruitTree>? fruitSlots,ToyCar? toy,Vector3? fruitCarrier,ToyCar? rattle,ToyCar? flute,WateringCan? wateringCan)
    {
        var state=Data.Chao!;
        if (nameLabel!=null) nameLabel.Text=state.Name;
        if (reserved || Courtship?.IsPaired(this)==true) return false;
        state.Tick(dt,Asleep || islandNap.IsNapping);
        if (Courtship?.IsBlooming(this)!=true && state.AdvanceLife(dt))
        {
            interests.Cancel(this,visual);
            visual.QueueFree();
            if (nameLabel!=null) nameLabel.GetParent().QueueFree();
            MakeHatchling();
            return true;
        }
        if (!state.CanAct || distress.HoldPose(visual,state,dt)) return false;
        voice.Step(visual,state,Asleep || islandNap.IsNapping,dt);
        bool inWater=motion.IsSwimmingAt(Position);
        if (toy?.PlayPose(this,visual)==true || rattle?.PlayPose(this,visual)==true ||
            flute?.PlayPose(this,visual)==true) return false;
        if (!inWater && motion.Recover(visual,dt)) return false;
        if (petAnimation>0)
        {
            petAnimation=Mathf.Max(0,petAnimation-dt);
            visual.Rotation=new(0,0,Mathf.Sin(petAnimation*22)*.10f);
            return false;
        }
        if (Asleep)
        {
            ImportedChao.SleepPose(visual,ChaoRest.MinimumSleep-napRemaining,state.Manners);
            napRemaining-=dt;
            if (napRemaining<=0 && state.Energy>=100)
            {
                Asleep=false; rest.Cancel(); sleepDelay=18;
                motion.LeapFromBed(this,state);
            }
            return false;
        }
        if (!motion.Airborne && !islandNap.IsVisiting && !pillar.Active && Courtship?.Sit(this,visual,state,Data.Color,(Node3D)GetParent(),dt)==true)
        {
            rest.Cancel();
            return false;
        }
        cliffDelay=Mathf.Max(0,cliffDelay-dt);
        bool startCliff=cliffDelay<=0 && !motion.OnCliffRoute && state.Power>=100 && callRemaining<=0 && !inWater && !motion.Airborne &&
            !motion.LeavingPond && !islandNap.IsVisiting && !rest.Requested && !pillar.Active && state.Energy>=58;
        Vector3? cliff=callRemaining>0 ? null : motion.CliffTarget(Position,dt,startCliff);
        if (startCliff && cliff!=null)
        {
            cliffDelay=70;
            interests.Cancel(this,visual);
            wateringCan?.Cancel(this);
        }
        if (cliff==null && wateringCan?.Step(this,visual,state,motion,dt,callRemaining<=0 && !rest.Requested && !islandNap.IsVisiting)==true)
            return false;
        if (pillar.Active && pillar.Step(this,visual,state,motion,dt,false)) return false;
        bool canChoose=!motion.Airborne && !motion.LeavingPond && !inWater && callRemaining<=0 &&
            !rest.Requested && !islandNap.IsVisiting && fruitCarrier==null;
        if (cliff==null && interests.Step(this,visual,state,motion,dt,canChoose,fruitSlots ?? Enumerable.Empty<FruitTree>(),
            new[]{toy,rattle,flute}.OfType<ToyCar>(),out bool fed))
            return fed;
        if (cliff==null && !motion.LeavingPond && fruitReaction.Step(this,visual,state,motion,fruitCarrier,dt)) return false;
        callRemaining=Mathf.Max(0,callRemaining-dt);
        bool called=callRemaining>0 && Position.DistanceTo(callTarget)>1.2f;
        if (cliff==null && (islandNap.IsVisiting || (!motion.LeavingPond && !called && !rest.Requested)) && islandNap.Step(this,visual,state,motion,dt)) return false;
        if (cliff==null && pillar.Step(this,visual,state,motion,dt,canChoose && state.Energy>=58)) return false;
        Vector3 beforeRest=Position;
        if (cliff==null && !motion.Airborne && !motion.LeavingPond && !inWater && !called && rest.Step(this,state,dt,out bool arrived))
        {
            var travel=Position-beforeRest; travel.Y=0;
            bool walking=!arrived && travel.Length()>dt*.075f;
            if (walking) gait+=dt*12;
            ImportedChao.Gait(visual,gait,walking,false);
            visual.Position=Vector3.Up*.034f; visual.Rotation=Vector3.Zero;
            if (arrived) { Asleep=true; napRemaining=ChaoRest.MinimumSleep; ImportedChao.SleepPose(visual,manners:state.Manners); }
            return false;
        }
        sleepDelay-=dt;
        if (!called && sleepDelay<=0 && state.Energy<58) rest.Request(Position);
        roam-=dt;
        if (roam<=0 || (!inWater && !motion.LeavingPond && motion.BlockedTime>.75f))
        {
            wanderTarget=ChaoWander.PickGoal(this,new(Data.X,Position.Y,Data.Z),4.7f);
            motion.ClearBlocked();
            roam=4.6f+GD.Randf()*2;
        }
        Vector3 target=called ? callTarget : wanderTarget;
        if (cliff is Vector3 rockTarget) target=rockTarget;
        target=motion.RouteOffUpperPlatform(Position,target);
        Vector3 offset=target-Position; offset.Y=0;
        bool swimming=state.Swim>=100;
        if (cliff==null && !inWater)
        {
            if (!called && !motion.LeavingPond && !motion.Airborne)
            {
                var steered=wander.Steer(this,offset,dt);
                if (steered.IsZeroApprox() && !offset.IsZeroApprox()) roam=0;
                offset=steered;
            }
            offset=ChaoMotion.AvoidNeighbors(this,offset);
        }
        bool moving=motion.Step(this,offset,state,false,inWater,dt,true,out bool hardLanding,allowIslandDrift:!called);
        inWater=motion.IsSwimmingAt(Position);
        if (moving || inWater) gait+=dt*(inWater && !swimming ? 5.2f : Mathf.Clamp(motion.GroundSpeed*9,4,19));
        ImportedChao.MovePose(visual,gait,moving,inWater,swimming,motion.Sprinting,motion.Airborne,motion.Flying);
        SwimAudio.Paddle(swimSound,dt,inWater && (moving || !swimming),ref swimBeat);
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
        interests.Cancel(this,visual);
        interests.Reconsider();
        rest.Cancel();
        islandNap.Cancel(visual);
        pillar.Cancel();
        motion.Reset();
        Asleep=false;
        InFlight=false; Cracked=false; velocity=Vector3.Zero; roam=0;
        Position=at; Data.X=at.X; Data.Y=at.Y; Data.Z=at.Z;
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
