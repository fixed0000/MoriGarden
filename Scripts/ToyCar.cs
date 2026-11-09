using Godot;

namespace Mori;

public partial class ToyCar : CharacterBody3D
{
    private static readonly Vector3 Home=new(10,0,5);
    private CharacterBody3D? player;
    private PetState? playChao;
    private float playing, cooldown;
    private Vector3 placementFrom, placementTo, placementRotation;
    private Vector3 playOrigin, playForward, velocity;
    private bool held, inFlight;
    public bool IsRattle { get; init; }
    public bool IsFlute { get; init; }
    public string ItemName => IsFlute ? "flute" : IsRattle ? "rattle" : "toy car";
    private int lastShake;
    public bool CanPickUp => !held && !inFlight;

    public override void _Ready()
    {
        Position=IsFlute ? new Vector3(3.1f,0,1.7f) : IsRattle ? Home+new Vector3(-3,0,.5f) : Home;
        CollisionLayer=0;
        AddChild(new CollisionShape3D { Position=Vector3.Up*.055f,
            Shape=new BoxShape3D { Size=new(.15f,.10f,.27f) } });
        if (IsRattle)
        {
            GardenArt.Ball(this,new(0,.069f,.065f),new(.14f,.14f,.14f),new("dfa648"));
            GardenArt.Ball(this,new(0,.038f,-.055f),new(.058f,.067f,.20f),new("70a9b2"));
            return;
        }
        if (IsFlute)
        {
            var tube=GardenArt.Cylinder(this,new(0,.039f,0),.023f,.31f,new("d5bd81"));
            tube.RotationDegrees=new(0,0,90);
            for (int i=0;i<5;i++)
                GardenArt.Ball(this,new(-.066f+i*.034f,.060f,0),new(.013f,.003f,.013f),new("665540"));
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
        if (!IsInstanceValid(player) || player.Position.DistanceTo(playOrigin)>1)
        {
            EndPlay();
            return;
        }
        float before=playing;
        playing+=dt;
        if (playing<4) return;
        if (before<4)
        {
            placementFrom=Position;
            placementRotation=Rotation;
            placementTo=PlacementPoint(player);
        }
        float progress=Mathf.SmoothStep(0,1,Mathf.Clamp((playing-4)/.45f,0,1));
        Position=placementFrom.Lerp(placementTo,progress);
        Rotation=placementRotation.Lerp(new(0,player.Rotation.Y,0),progress);
        if (playing>=4.45f) EndPlay();
    }

    private Vector3 PlacementPoint(CharacterBody3D actor)
    {
        var space=GetWorld3D().DirectSpaceState;
        var origin=actor.Position+Vector3.Up*.21f;
        foreach (float angle in new[]{0f,.65f,-.65f})
        {
            var ahead=origin+playForward.Rotated(Vector3.Up,angle)*.78f;
            if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin,ahead,1)).Count>0) continue;
            var floor=space.IntersectRay(PhysicsRayQueryParameters3D.Create(ahead,ahead+Vector3.Down*.8f,1));
            if (floor.Count>0) return (Vector3)floor["position"]+Vector3.Up*.009f;
        }
        return actor.Position+playForward*.64f;
    }

    private void EndPlay()
    {
        player=null;
        playChao=null;
        cooldown=18;
    }

    public bool TakeFromPlay()
    {
        if (playChao==null) return false;
        playChao.ChangeRelationship(-.55f);
        EndPlay();
        return true;
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
        if ((!IsRattle && !IsFlute && !chao.LikesToyCars) || chao.Fun>=100) return false;
        player=actor; playChao=chao; playing=0; held=inFlight=false;
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
        if (playing>=4)
        {
            ImportedChao.ReceivePose(art,.55f);
            art.Rotation=new(.33f,0,0);
            return true;
        }
        float wobble=Mathf.Sin(playing*7);
        if (IsFlute)
        {
            Position=actor.Position+playForward*.16f+Vector3.Up*(.164f+.004f*wobble);
            Rotation=new(0,actor.Rotation.Y,.06f*wobble);
            ImportedChao.ReceivePose(art,.87f);
            int phrase=(int)(playing/1.24f);
            if (phrase!=lastShake) { ChaoSounds.Play(this,"flute"); lastShake=phrase; }
            return true;
        }
        if (IsRattle)
        {
            var grip=actor.Position+playForward*.14f+Vector3.Up*.19f;
            Rotation=new(-.8f+wobble*.55f,actor.Rotation.Y,0);
            Position=grip-Basis*new Vector3(0,.038f,-.091f);
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
        chao.LikesToyCars && chao.Fun<72 && cooldown<=0 && player==null && CanPickUp && actor.Position.DistanceTo(Position)<7;
}
