using Godot;

namespace Mori;

public partial class ToyCar : CharacterBody3D
{
    private static readonly Vector3 Home=new(10,0,5);
    private CharacterBody3D? player;
    private float playing, cooldown;
    private Vector3 playOrigin, playForward, velocity;
    private bool held, inFlight;
    public bool IsRattle { get; init; }
    public string ItemName => IsRattle ? "rattle" : "toy car";
    private int lastShake;
    public bool CanPickUp => !held && !inFlight && player==null;

    public override void _Ready()
    {
        Position=IsRattle ? Home+new Vector3(-3,0,.5f) : Home;
        CollisionLayer=0;
        AddChild(new CollisionShape3D { Position=Vector3.Up*.055f,
            Shape=new BoxShape3D { Size=new(.15f,.10f,.27f) } });
        if (IsRattle)
        {
            GardenArt.Ball(this,new(0,.069f,.065f),new(.14f,.14f,.14f),new("dfa648"));
            GardenArt.Ball(this,new(0,.038f,-.055f),new(.058f,.067f,.20f),new("70a9b2"));
            return;
        }
        GardenArt.Shape(this,new BoxMesh(),new(0,.062f,0),new(.14f,.071f,.26f),new("b35b42"));
        GardenArt.Shape(this,new BoxMesh(),new(0,.111f,-.028f),new(.116f,.05f,.107f),new("a1b8ba"));
        for (int side=-1;side<=1;side+=2)
            for (int axle=-1;axle<=1;axle+=2)
                GardenArt.Cylinder(this,new(side*.071f,.039f,axle*.077f),.039f,.027f,
                    new("33383a")).RotationDegrees=new(0,0,90);
    }

    public void Advance(float dt)
    {
        cooldown=Mathf.Max(0,cooldown-dt);
        if (inFlight)
        {
            inFlight=!ThrowMotion.Step(this,ref velocity,dt);
            return;
        }
        if (player==null) return;
        playing+=dt;
        if (playing>=4 || !IsInstanceValid(player) || player.Position.DistanceTo(playOrigin)>1)
        {
            player=null; cooldown=18;
        }
    }

    public void Hold(Vector3 at)
    {
        held=true;
        Position=at;
    }

    public void Toss(Vector3 impulse)
    {
        held=false;
        inFlight=true;
        velocity=impulse;
    }

    public bool BeginPlay(CharacterBody3D actor,PetState chao)
    {
        if ((!IsRattle && !chao.LikesToyCars) || chao.Fun>=100) return false;
        player=actor; playing=0; held=inFlight=false;
        lastShake=-1;
        playForward=Vector3.Back.Rotated(Vector3.Up,actor.Rotation.Y);
        playOrigin=actor.Position+playForward*.30f;
        chao.Fun=Mathf.Min(100,chao.Fun+36);
        Position=playOrigin;
        return true;
    }

    public bool PlayPose(CharacterBody3D actor,Node3D art)
    {
        if (player!=actor) return false;
        float wobble=Mathf.Sin(playing*7);
        if (IsRattle)
        {
            Position=actor.Position+playForward*.18f+Vector3.Up*(.17f+.029f*wobble);
            Rotation=new(.25f+wobble*.6f,actor.Rotation.Y,0);
            ImportedChao.ReceivePose(art,.72f+.14f*wobble);
            int shake=(int)(playing*4);
            if (shake!=lastShake) { ChaoSounds.Play(this,"rattle"); lastShake=shake; }
            return true;
        }
        Position=playOrigin+playForward*(.14f*(1+wobble));
        Rotation=new(0,actor.Rotation.Y,0);
        ImportedChao.EatPose(art,playing);
        art.Rotation=new(.35f+.11f*wobble,0,0);
        return true;
    }

    public bool Use(CharacterBody3D actor,Node3D art,PetState chao,out Vector3 target)
    {
        target=Position-Vector3.Back*.40f;
        if (!Available(actor,chao) || actor.Position.DistanceTo(target)>.25f) return false;
        actor.Rotation=Vector3.Zero;
        return BeginPlay(actor,chao) && PlayPose(actor,art);
    }

    public bool Available(CharacterBody3D actor,PetState chao) =>
        chao.LikesToyCars && chao.Fun<72 && cooldown<=0 && CanPickUp && actor.Position.DistanceTo(Position)<7;
}
