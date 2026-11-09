using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class FruitTree : Node3D
{
    private Node3D crown = null!;
    private MeshInstance3D trunk = null!;
    private CharacterBody3D fruit = null!;
    public bool IsFeeder { get; init; }
    public bool SingleUse { get; init; }
    public bool IsHeartFruit { get; init; }
    private FruitTree? host;
    private readonly List<FruitTree> extras=new();
    private MeshInstance3D fruitSkin=null!;
    private float ripening, ripeAfter=92;
    private float crownShake;
    public const float ShakeDuration=2.05f;
    public float Sway { get; private set; }
    public IEnumerable<FruitTree> Slots => new[]{this}.Concat(extras);
    private bool Ripe => IsFeeder || ripening>=ripeAfter;
    public bool CanShake => !IsFeeder && host==null && crownShake<=0 && Slots.Any(slot=>!slot.dropped && slot.Ripe);
    public bool IsBranchSlot => host!=null;
    private Vector3 branch = new(1.18f,4.02f,1.27f);
    private float elapsed;
    private bool dropped, settled;
    private float fallSpeed;
    private bool held, thrown;
    private Vector3 tossVelocity;
    public bool HasFruitOnGround => dropped && settled && !held;

    public void Hold(Vector3 position)
    {
        held = true;
        fruit.GlobalPosition = position;
    }

    public void Release(Vector3 velocity)
    {
        held = false;
        settled = false;
        thrown = true;
        tossVelocity = velocity;
    }
    public void Place(Vector3 position)
    {
        fruit.GlobalPosition=position;
        held=thrown=false;
        dropped=settled=true;
    }
    public bool CanInteract => CanShake || HasFruitOnGround;
    public const int BiteCount=4;
    public int Bites { get; private set; }
    public void TakeBite()
    {
        Bites++;
        fruit.Scale=Vector3.One*(1-.19f*Bites);
        fruit.Visible=Bites<BiteCount;
    }
    public Vector3 FruitPosition => fruit.GlobalPosition;

    public (float Growth, Vector3? GroundPosition, int Bites) CaptureGrowth() =>
        (dropped && !HasFruitOnGround ? 0 : ripening, HasFruitOnGround ? FruitPosition : null,Bites);

    public void RestoreGrowth(float growth, Vector3? groundPosition,int bites)
    {
        ripening=growth;
        RefreshGrowth();
        if (groundPosition is not Vector3 position) return;
        Place(position);
        while (Bites<bites) TakeBite();
    }

    public override void _Ready()
    {
        if (IsFeeder)
        {
            branch = new(0,.19f,0);
            dropped = settled = true;
        }
        else if (host==null)
        {
            ripening=ripeAfter;
            var bark=new Color("504639");
            trunk=GardenArt.Shape(this,new CylinderMesh { TopRadius=.28f,BottomRadius=.68f,Height=5.6f,RadialSegments=9 },
                new(0,2.8f,0),Vector3.One,bark);
            crown=new Node3D { Position=new(0,3.6f,0) };
            AddChild(crown);
            for (int i=0;i<7;i++)
            {
                float a=i*Mathf.Tau/7+.78f;
                var start=new Vector3(0,.09f+i%2*.28f,0);
                var tip=new Vector3(Mathf.Cos(a)*(i==0?1.72f:2.02f),i==0?.65f:1.15f+i%3*.36f,Mathf.Sin(a)*(i==0?1.82f:2.02f));
                var axis=tip-start;
                var limb=GardenArt.Shape(crown,new CylinderMesh { TopRadius=.08f,BottomRadius=.23f,Height=axis.Length(),RadialSegments=7 },
                    (start+tip)*.5f,Vector3.One,bark);
                limb.Quaternion=new Quaternion(Vector3.Up,axis.Normalized());
                GardenArt.Shape(crown,new SphereMesh { RadialSegments=10,Rings=5 },tip+Vector3.Up*.82f,
                    new(3.40f,2.25f,3.1f),new(i%2==0?"465e35":"5a7041"));
                if (i==0 || i==2 || i==4)
                {
                    Vector3 socket=tip+new Vector3(0,3.6f-.29f,0);
                    if (i==0) branch=socket;
                    else
                    {
                        var slot=new FruitTree { host=this,branch=socket,ripeAfter=92+i*9,ripening=i==2 ? 57 : 24 };
                        extras.Add(slot);
                        AddChild(slot);
                    }
                    GardenArt.Cylinder(crown,tip+Vector3.Down*.115f,.029f,.23f,bark);
                }
            }
        }
        fruit = new CharacterBody3D { Position = branch, CollisionLayer = 0, CollisionMask = 1 };
        fruit.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius=.18f } });
        AddChild(fruit);
        fruitSkin=IsHeartFruit
            ? GardenArt.Shape(fruit,HeartMesh(),Vector3.Zero,Vector3.One,new("df486f"))
            : GardenArt.Ball(fruit,Vector3.Zero,new(.30f,.28f,.29f),new("ad7540"));
        GardenArt.Ball(fruit,new(.05f,.15f,0),new(.12f,.035f,.07f),new("51663b"));
        RefreshGrowth();
    }

    private static Mesh HeartMesh()
    {
        var mesh=new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int i)
        {
            float a=i*Mathf.Tau/32;
            float x=Mathf.Sin(a);
            float y=13*Mathf.Cos(a)-5*Mathf.Cos(2*a)-2*Mathf.Cos(3*a)-Mathf.Cos(4*a);
            return new Vector3(16*x*x*x,y+2,0)*.0085f;
        }
        for (int i=0;i<32;i++)
        {
            var a=Point(i); var b=Point(i+1);
            mesh.AddVertex(Vector3.Back*.098f); mesh.AddVertex(a); mesh.AddVertex(b);
            mesh.AddVertex(Vector3.Forward*.098f); mesh.AddVertex(b); mesh.AddVertex(a);
        }
        mesh.GenerateNormals();
        return mesh.Commit();
    }

    public void Shake()
    {
        if (!CanShake) return;
        crownShake=ShakeDuration;
    }

    private void DropRipeFruit()
    {
        foreach(var slot in Slots)
        {
            if (slot.dropped || !slot.Ripe) continue;
            slot.dropped=true;
            slot.elapsed=0;
        }
    }

    private void RefreshGrowth()
    {
        if (IsFeeder || dropped) return;
        float size=Mathf.Clamp(ripening/ripeAfter,0,1);
        fruit.Visible=size>.08f;
        fruit.Scale=Vector3.One*Mathf.Lerp(.35f,1,size);
        if (fruitSkin.MaterialOverride is StandardMaterial3D paint)
            paint.AlbedoColor=new Color("6c864c").Lerp(new Color("ad7540"),Mathf.SmoothStep(.5f,1,size));
    }

    public void UpdateFruit(float delta)
    {
        if (!IsFeeder && host==null)
        {
            bool shaking=crownShake>0;
            crownShake=Mathf.Max(0,crownShake-delta);
            float progress=1-crownShake/ShakeDuration;
            Sway=shaking ? Mathf.Sin(progress*Mathf.Tau*4)*Mathf.Sin(progress*Mathf.Pi) : 0;
            var rotation=new Vector3(0,0,Sway*.045f);
            var bend=Basis.FromEuler(rotation);
            trunk.Rotation=crown.Rotation=rotation;
            trunk.Position=bend*(Vector3.Up*2.8f);
            crown.Position=bend*(Vector3.Up*3.6f);
            if (shaking && crownShake==0) DropRipeFruit();
        }
        if (held) return;
        if (thrown)
        {
            settled=ThrowMotion.Step(fruit,ref tossVelocity,delta);
            thrown=!settled;
            return;
        }
        if (!dropped)
        {
            ripening=Mathf.Min(ripeAfter,ripening+delta);
            var tree=host ?? this;
            fruit.Position=tree.crown.Transform*(branch-new Vector3(0,3.6f,0));
            RefreshGrowth();
            return;
        }
        if (settled) return;
        elapsed+=delta;
        if (elapsed<.17f)
        {
            var tree=host ?? this;
            fruit.Position=tree.crown.Transform*(branch-new Vector3(0,3.6f,0));
            return;
        }
        fallSpeed+=19*delta;
        if (fruit.MoveAndCollide(Vector3.Down*fallSpeed*delta)==null) return;
        if (fallSpeed>2) fallSpeed*=-.23f;
        else settled=true;
    }

    public void Regrow()
    {
        if (SingleUse) { QueueFree(); return; }
        dropped = IsFeeder;
        held = false;
        thrown = false;
        settled = IsFeeder;
        fallSpeed = 0;
        ripening=0;
        Bites=0;
        fruit.Visible=true;
        if (!IsFeeder && host==null) crown.Rotation = Vector3.Zero;
        fruit.Scale = Vector3.One;
        fruit.Position = branch;
        RefreshGrowth();
    }
}
