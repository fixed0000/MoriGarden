using Godot;

namespace Mori;

public enum GardenZone { Garden, Lobby, Stadium }

public static class ZoneArt
{
    public static readonly Vector3 GardenLobbyGate = new(-18.45f,.43f,-16.85f);
    public static readonly Vector3 PlazaStadiumPad = new(6,0,-4);
    public static readonly Vector3 ChaoMachine = new(-23.25f,0,-16.0f);
    public static Vector3 MachineStep => ChaoMachine+Vector3.Back*1.42f;
    public static readonly Vector3 PlazaShop = new(-5,0,-2);
    public static string Title(GardenZone zone) => zone switch
    {
        GardenZone.Garden => "MORI GARDEN",
        GardenZone.Lobby => "GARDEN PLAZA",
        _ => "STAR STADIUM"
    };

    public static void Portal(Node3D root, Vector3 position, string title, Color color,float scale=1)
    {
        var portal = new Node3D { Position = position,Scale=Vector3.One*scale }; root.AddChild(portal);
        for (int side = -1; side <= 1; side += 2)
        {
            GardenArt.Shape(portal, new BoxMesh(), new(side * 1.65f, .95f, 0), new(.6f,1.9f,.9f), color);
            Solid(portal, new(side * 1.65f, .95f, 0), new(.6f,1.9f,.9f));
        }
        for (int i = 0; i <= 10; i++)
        {
            float angle = i * Mathf.Pi / 10;
            var stone = GardenArt.Shape(portal, new BoxMesh(),
                new(Mathf.Cos(angle)*1.65f,1.9f+Mathf.Sin(angle)*1.65f,0),
                new(.62f,.64f,.9f), color.Lerp(new Color("615e55"), i%2==0 ? .13f : 0));
            stone.Rotation = new(0,0,angle);
            stone.CreateConvexCollision();
        }
        var vault = GardenArt.Cylinder(portal,new(0,1.9f,-.32f),1.45f,.12f,new("202e32"));
        vault.RotationDegrees = new(90,0,0);
        GardenArt.Shape(portal, new BoxMesh(), new(0,.95f,-.32f), new(2.9f,1.9f,.12f), new("202e32")).CreateConvexCollision();
        GardenArt.Sign(portal, title, new(0,4.3f,0), GardenArt.Cream);
        GardenArt.Shape(portal, new BoxMesh(), new(0,.055f,.25f),new(3.25f,.09f,1.8f),new("9e8e70"));
    }

    public static void Solid(Node3D root, Vector3 pos, Vector3 size,bool stone=false)
    {
        var body = new StaticBody3D { Position = pos };
        if (stone) body.SetMeta("stone",true);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); root.AddChild(body);
    }

    public static void Build(Node3D root, GardenZone zone)
    {
        if (zone != GardenZone.Garden) Solid(root,new(0,-.25f,0),new(76,.5f,76));
        if (zone == GardenZone.Garden)
        {
            GardenArt.BuildGarden(root);
            root.AddChild(new WateringCan { Name="WateringCan" });
            root.AddChild(new ToyCar { Name="ToyCar" });
            root.AddChild(new ToyCar { Name="Rattle",IsRattle=true });
            root.AddChild(new ToyCar { Name="Flute",IsFlute=true });
            GardenArt.Cylinder(root,MachineStep+Vector3.Up*.065f,.94f,.13f,new("91a99a")).CreateConvexCollision();
            GardenArt.Cylinder(root,ChaoMachine+Vector3.Up*.10f,1.0f,.20f,new("586c65")).CreateConvexCollision();
            var casing=GardenArt.Shape(root,new CapsuleMesh { Radius=.31f,Height=1.79f,RadialSegments=16,Rings=6 },
                ChaoMachine+new Vector3(0,1.10f,-.67f),new(1,1,.70f),new("647d77"));
            casing.RotationDegrees=new(0,0,90);
            casing.CreateConvexCollision();
            GardenArt.Shape(root,new BoxMesh(),ChaoMachine+new Vector3(0,.51f,-.70f),
                new(1.58f,.99f,.39f),new("647d77")).CreateConvexCollision();
            foreach(int side in new[]{-1,1})
                GardenArt.Shape(root,new BoxMesh(),ChaoMachine+new Vector3(side*.71f,.46f,-.21f),
                    new(.23f,.81f,.90f),new("647d77")).CreateConvexCollision();
            var hatch=new Node3D { Name="MachineHatch",Position=ChaoMachine+new Vector3(0,.25f,.39f) };
            root.AddChild(hatch);
            GardenArt.Shape(hatch,new BoxMesh(),new(0,.34f,0),new(1.20f,.68f,.10f),new("91b7a9"));
            GardenArt.Shape(root,new BoxMesh(),ChaoMachine+new Vector3(0,1.51f,-.61f),
                new(.15f,.49f,.18f),new("556357"));
            GardenArt.Shape(root,new BoxMesh(),ChaoMachine+new Vector3(0,1.89f,-.51f),
                new(1.75f,.38f,.13f),new("e4ba53"));
            root.AddChild(new Label3D { Text="CHAO MACHINE",Position=ChaoMachine+new Vector3(0,1.89f,-.438f),
                FontSize=48,PixelSize=.0020f,OutlineSize=0,Modulate=new Color("40372a") });
            root.AddChild(new TrainingAnimal { Skill=AnimalSkill.Swim, Home=new(-11,0,-2.2f), Position=new(-11,0,-2.2f) });
            root.AddChild(new TrainingAnimal { Skill=AnimalSkill.Fly, Home=new(5,0,-6), Position=new(5,0,-6) });
            root.AddChild(new TrainingAnimal { Skill=AnimalSkill.Run, Home=new(3,0,9), Position=new(3,0,9) });
            root.AddChild(new TrainingAnimal { Skill=AnimalSkill.Power, Home=new(-4,0,-7), Position=new(-4,0,-7) });
            Portal(root,GardenLobbyGate,"PLAZA",new("8f8c7e"),1.33f);
            for (int i=0;i<5;i++)
            {
                var at=new Vector3(-22.20f+i*.94f,.06f+i*.09f,-14.28f-i*.52f);
                var step=GardenArt.Shape(root,new CylinderMesh { TopRadius=1.16f,BottomRadius=1.26f,
                    Height=.72f,RadialSegments=7 },at+Vector3.Down*.36f,new(1.05f,1,.86f),
                    new(i%2==0 ? "858577" : "777b70"));
                step.RotationDegrees=new(0,12+i*17,0);
                GardenArt.StoneCollision(step);
            }
            GardenArt.StoneCollision(GardenArt.Shape(root,new BoxMesh(),
                GardenLobbyGate+new Vector3(0,-.24f,.08f),new(4.38f,.48f,1.82f),new("77796b")));
            return;
        }
        GardenArt.Cylinder(root, new(0,-.1f,0),14,.3f,new(zone == GardenZone.Lobby ? "c4cbd5" : "9bbd9a"));
        GardenArt.Cylinder(root, new(0,.06f,0),4,.05f,new(zone == GardenZone.Lobby ? "e4d5b2" : "d5ceaa"));
        for (int i=0;i<24;i++)
        {
            float a = i * Mathf.Tau / 24;
            Vector3 p = new(Mathf.Cos(a)*13,1.5f,Mathf.Sin(a)*13);
            GardenArt.Shape(root,new BoxMesh(),p,new(1.4f,3,1.4f),new(zone==GardenZone.Lobby?"7d91ad":"b4a591"));
            Solid(root,p,new(1.4f,3,1.4f));
        }
        Portal(root,new(0,0,-10),zone==GardenZone.Stadium ? "PLAZA" : "GARDEN",new("a9c99f"));
        if (zone == GardenZone.Lobby)
        {
            GardenArt.Cylinder(root,PlazaStadiumPad+Vector3.Up*.07f,1.35f,.13f,new("7d9b8f"));
            GardenArt.Sign(root,"STADIUM PAD",PlazaStadiumPad+Vector3.Up*1.4f,GardenArt.Cream);
            GardenArt.Shape(root,new BoxMesh(),PlazaShop+new Vector3(0,.47f,0),new(2.45f,.92f,1.16f),new("a28c6e"));
            GardenArt.Sign(root,"EGG SHOP",PlazaShop+Vector3.Up*1.65f,GardenArt.Cream);
            GardenArt.Sign(root,"MORI WORLD",new(0,5,-4),GardenArt.Cream);
            for (int i=0;i<8;i++)
                GardenArt.Cylinder(root,new(0,.11f,7-i*2),.75f,.07f,new("eee5cb"));
        }
        else
        {
            GardenArt.Cylinder(root,new(0,.13f,5),1.7f,.12f,new("f2d58c"));
            GardenArt.Sign(root,"E · START RACE",new(0,2.8f,5),GardenArt.Cream);
            for (int i=0;i<8;i++)
            {
                GardenArt.Shape(root,new BoxMesh(),new(-10,.2f,i*2-7),new(1,.3f,1),new("f0dcab"));
                GardenArt.Shape(root,new BoxMesh(),new(10,.2f,i*2-7),new(1,.3f,1),new("f0dcab"));
            }
        }
    }
}
