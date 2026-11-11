using Godot;

namespace Mori;

public enum AnimalSkill { Swim, Fly, Run, Power }

public partial class TrainingAnimal : CharacterBody3D
{
    public AnimalSkill Skill { get; init; }
    public bool IsParrot { get; init; }
    public Vector3 Home { get; init; }
    private float respawn;
    private Vector3 habitat;
    private float roamAge;
    private Vector3 releaseFrom;
    private float releaseTime;
    private const float DropDuration = .32f;
    private bool inFlight;
    private Vector3 tossVelocity;
    private Node3D art = null!;
    private readonly Node3D[] feet = new Node3D[2];
    private readonly Node3D[] wings = new Node3D[2];
    public bool Available => respawn <= 0 && !inFlight;
    public string AnimalName => IsParrot ? "Parrot" : Skill switch
    {
        AnimalSkill.Swim => "Penguin",
        AnimalSkill.Fly => "Songbird",
        AnimalSkill.Run => "Rabbit",
        _ => "Bear cub"
    };

    public override void _Ready()
    {
        CollisionLayer = 0;
        AddChild(new CollisionShape3D { Position = new(0,.125f,0), Shape = new SphereShape3D { Radius=.105f } });
        habitat = Home;
        releaseTime = DropDuration;
        art = new Node3D { Scale = Vector3.One*(Skill == AnimalSkill.Run ? .39f : .46f) };
        AddChild(art);
        if (IsParrot) { BuildParrot(); return; }
        Color coat = new(Skill switch
        {
            AnimalSkill.Swim => "354959",
            AnimalSkill.Fly => "7c8caf",
            AnimalSkill.Run => "a28f76",
            _ => "725647"
        });
        float bodyWidth = Skill == AnimalSkill.Power ? .29f : Skill == AnimalSkill.Fly ? .20f : .24f;
        AvatarArt.Profile(art,Vector3.Zero,coat,(.06f,bodyWidth*.59f,.13f),
            (.21f,bodyWidth,.205f),(.36f,bodyWidth*.86f,.18f),(.48f,.13f,.12f));
        GardenArt.Ball(art,new(0,.24f,.15f),new(.33f,.38f,.19f),new("e0d5be"));
        for (int side=-1;side<=1;side+=2)
        {
            var foot = new Node3D { Position=new(side*.125f,.055f,.02f) };
            art.AddChild(foot);
            feet[(side+1)/2]=foot;
            GardenArt.Ball(foot,new(0,0,.1f),new(.19f,.10f,.24f),
                Skill is AnimalSkill.Swim or AnimalSkill.Fly ? new("c38b50") : coat);
        }
        AvatarArt.Profile(art,new(0,0,.075f),coat,(.42f,.13f,.12f),
            (.52f,.21f,.19f),(.65f,.20f,.17f),(.735f,.118f,.10f));
        for (int side=-1;side<=1;side+=2)
        {
            GardenArt.Ball(art,new(side*.11f,.61f,.27f),new(.055f,.07f,.044f),GardenArt.Ink);
            var detail = Skill == AnimalSkill.Run || Skill == AnimalSkill.Power
                ? new Vector3(side*.14f,.77f,0) : new Vector3(side*.26f,.30f,0);
            var size = Skill == AnimalSkill.Run ? new Vector3(.10f,.32f,.11f) : new Vector3(.18f,.16f,.16f);
            var ear = GardenArt.Ball(art,detail,size,coat);
            ear.Rotation = new(0,0,side*.22f);
            if (Skill is AnimalSkill.Run or AnimalSkill.Power)
                GardenArt.Ball(art,detail+new Vector3(0,0,.058f),size*.64f,new("c5a295"));
            GardenArt.Ball(art,new(side*.11f-.012f,.632f,.292f),new(.017f,.022f,.016f),Colors.White);
        }
        GardenArt.Ball(art,new(0,.52f,.29f),new(.14f,.09f,.08f),new("ceb18a"));
        if (Skill == AnimalSkill.Run)
            GardenArt.Ball(art,new(0,.21f,-.24f),new(.22f,.22f,.20f),new("e4d9c6"));
        if (Skill == AnimalSkill.Fly)
            GardenArt.Ball(art,new(0,.22f,-.24f),new(.18f,.12f,.34f),coat);

    }

    private void BuildParrot()
    {
        var red=new Color("c65345"); var blue=new Color("477aa6"); var gold=new Color("d9b557");
        AvatarArt.Profile(art,Vector3.Zero,red,(.07f,.10f,.10f),(.22f,.21f,.17f),(.43f,.19f,.16f),(.52f,.12f,.11f));
        AvatarArt.Profile(art,new(0,0,.06f),red,(.44f,.12f,.11f),(.57f,.19f,.17f),(.70f,.18f,.15f),(.76f,.09f,.08f));
        for(int side=-1;side<=1;side+=2)
        {
            var foot=new Node3D { Position=new(side*.10f,.047f,.015f) }; art.AddChild(foot); feet[(side+1)/2]=foot;
            for(int toe=-1;toe<=1;toe++)
                GardenArt.Shape(foot,new BoxMesh(),new(toe*.035f,0,.055f),new(.027f,.032f,.15f),new("6a665c"));
            GardenArt.Ball(art,new(side*.13f,.62f,.18f),new(.14f,.14f,.13f),new("e7dfc9"));
            GardenArt.Ball(art,new(side*.155f,.635f,.227f),new(.047f,.052f,.03f),new("303a40"));
            GardenArt.Ball(art,new(side*.153f,.646f,.24f),new(.014f,.016f,.008f),Colors.White);
            var wing=new Node3D { Position=new(side*.16f,.35f,-.015f) }; art.AddChild(wing); wings[(side+1)/2]=wing;
            AvatarArt.Profile(wing,Vector3.Zero,gold,(-.14f,.045f,.09f),(0,.095f,.14f),(.15f,.055f,.08f));
            for(int feather=0;feather<3;feather++)
                AvatarArt.Patch(wing,blue,new(side*.05f,-.02f,-.01f),new(side*.09f,-.11f,-.07f-feather*.045f),
                    new(side*.04f,-.27f-feather*.025f,-.20f-feather*.025f));
        }
        AvatarArt.Profile(art,new(0,0,.25f),new("dfcba1"),(.53f,.06f,.08f),(.63f,.105f,.14f),(.69f,.06f,.07f));
        AvatarArt.Patch(art,new("3b4145"),new(-.065f,.60f,.37f),new(.065f,.60f,.37f),new(0,.50f,.33f));
        for(int i=-1;i<=1;i++)
            AvatarArt.Patch(art,i==0 ? red : blue,new(i*.042f-.035f,.22f,-.12f),new(i*.042f+.035f,.22f,-.12f),
                new(i*.065f,.025f,-.56f));
    }

    public void UpdateAnimal(float dt, float time, bool carried)
    {
        respawn = Mathf.Max(0,respawn-dt);
        Visible = respawn <= 0;
        if (inFlight)
        {
            if (ThrowMotion.Step(this,ref tossVelocity,dt))
            {
                inFlight = false;
                habitat = Position;
                roamAge = 0;
                releaseTime = DropDuration;
            }
            return;
        }
        if (!Available || carried)
        {
            art.Position=Vector3.Zero; art.Rotation=Vector3.Zero;
            foreach(var foot in feet) foot.Rotation=Vector3.Zero;
            return;
        }
        if (releaseTime < DropDuration)
        {
            releaseTime = Mathf.Min(DropDuration,releaseTime+dt);
            float t = releaseTime/DropDuration;
            Position = releaseFrom.Lerp(habitat,t*t);
            return;
        }
        roamAge += dt;
        float phase = roamAge*.65f + (int)Skill;
        float radius = Mathf.Min(.65f,roamAge*.2f);
        Position = habitat + new Vector3(Mathf.Sin(phase)*radius,0,Mathf.Cos(phase)*radius);
        Rotation = new(0,phase+Mathf.Pi/2,0);
        var fromPond=Position-GardenArt.Pond; fromPond.Y=0;
        bool swimming=fromPond.LengthSquared()<GardenArt.PondRadius*GardenArt.PondRadius;
        float stroke=Mathf.Sin(roamAge*(swimming ? 12 : 16));
        if (swimming) Position=new(Position.X,-.04f,Position.Z);
        art.Position=new(0,swimming ? Mathf.Sin(roamAge*5)*.017f : Mathf.Abs(stroke)*.025f,0);
        art.Rotation=new(swimming ? .33f : 0,0,swimming ? stroke*.09f : 0);
        feet[0].Rotation=new(stroke*(swimming ? .93f : .62f),0,0);
        feet[1].Rotation=new(-feet[0].Rotation.X,0,0);
        if (IsParrot)
            for(int side=0;side<2;side++) wings[side].Rotation=new(swimming ? stroke*.55f : .05f*Mathf.Sin(roamAge*3),0,(side==0 ? -1 : 1)*(swimming ? .42f : .08f));
    }

    internal void PushAside(Vector3 travel)
    {
        if (!Available) return;
        Vector3 before=Position;
        GardenContact.Move(this,travel);
        habitat+=Position-before;
        releaseFrom+=Position-before;
    }

    public void Throw(Vector3 velocity)
    {
        inFlight = true;
        tossVelocity = velocity;
    }

    public void ReleaseHere(float groundHeight)
    {
        releaseFrom = Position;
        habitat = new Vector3(Position.X,groundHeight,Position.Z);
        releaseTime = 0;
        roamAge = 0;
    }

    public void ReturnToHabitat()
    {
        respawn = 35;
        ReleaseHere(0);
        Hide();
    }
}
