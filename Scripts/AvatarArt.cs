using Godot;

namespace Mori;

internal static class AvatarArt
{
    public static void Build(Node3D root)
    {
        Color hair = new("262832"), jacket = new("25262d"), shirt = new("818294");
        Color pants = new("363640"), skin = new("e3c3b2");
        Profile(root,Vector3.Zero,jacket,(.83f,.29f,.19f),(1.2f,.34f,.23f),(1.54f,.44f,.22f),(1.63f,.29f,.17f));
        Profile(root,new(0,0,.19f),shirt,(.86f,.24f,.04f),(1.22f,.26f,.055f),(1.53f,.23f,.043f));
        Patch(root,new("383945"),new(-.29f,1.59f,.20f),new(-.15f,1.58f,.259f),new(-.08f,1.22f,.251f),new(-.25f,1.39f,.24f));
        Patch(root,new("444451"),new(.29f,1.59f,.20f),new(.25f,1.38f,.24f),new(.08f,1.22f,.251f),new(.15f,1.58f,.259f));
        Patch(root,new("30303b"),new(-.085f,1.58f,.262f),new(.10f,1.58f,.262f),new(.06f,1.32f,.252f),new(-.025f,1.21f,.257f));
        Profile(root,Vector3.Zero,skin,(1.53f,.105f,.11f),(1.76f,.12f,.12f));
        Profile(root,new(0,0,.035f),skin,(1.72f,.115f,.13f),(1.81f,.22f,.195f),(2.02f,.256f,.23f),(2.18f,.24f,.19f),(2.27f,.14f,.10f));
        Profile(root,new(0,0,-.047f),hair,(1.76f,.21f,.19f),(1.92f,.305f,.24f),(2.17f,.305f,.275f),(2.34f,.22f,.206f),(2.39f,.07f,.085f));
        Patch(root,hair,new(-.28f,2.16f,.18f),new(-.18f,2.32f,.19f),new(.07f,2.27f,.242f),
            new(-.02f,1.91f,.29f),new(-.23f,2.01f,.25f));
        Patch(root,new("353540"),new(.03f,2.31f,.24f),new(.24f,2.26f,.22f),new(.30f,1.87f,.13f),
            new(.18f,1.97f,.274f),new(.075f,2.10f,.27f));
        Patch(root,hair,new(-.29f,2.13f,.18f),new(-.23f,1.91f,.19f),new(-.28f,1.75f,.11f),new(-.32f,1.99f,.07f));
        for (int side=-1;side<=1;side+=2)
        {
            float x=side*.15f;
            Profile(root,new(x,0,0),pants,(.14f,.105f,.105f),(.45f,.12f,.13f),(.82f,.15f,.163f),(1.00f,.154f,.158f));
            Profile(root,new(x,0,.10f),jacket,(.015f,.131f,.227f),(.105f,.132f,.232f),(.22f,.109f,.127f));
            var arm = new Node3D { Name=side<0 ? "LeftArm" : "RightArm", Position=new(side*.39f,1.48f,0) };
            root.AddChild(arm);
            Profile(arm,Vector3.Zero,jacket,(-.64f,.07f,.08f),(-.34f,.09f,.095f),(-.12f,.102f,.11f),(.075f,.116f,.11f));
            Profile(arm,new(0,0,.01f),skin,(-.79f,.060f,.063f),(-.68f,.078f,.078f),(-.59f,.068f,.069f));
            Patch(root,new("3b3944"),new(side*.065f,1.99f,.254f),new(side*.17f,2.005f,.230f),new(side*.15f,1.969f,.233f),new(side*.077f,1.968f,.257f));
        }
        Patch(root,new("9a756f"),new(-.045f,1.833f,.217f),new(.043f,1.835f,.217f),new(.003f,1.821f,.223f));
    }

    internal static void Profile(Node3D root,Vector3 offset,Color color,params (float Y,float X,float Z)[] rings)
    {
        const int sides=8;
        var surface=new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(uint.MaxValue);
        Vector3 Point((float Y,float X,float Z) ring,int i)
        {
            float a=(i+.5f)*Mathf.Tau/sides;
            return new(Mathf.Cos(a)*ring.X,ring.Y,Mathf.Sin(a)*ring.Z);
        }
        for(int r=0;r<rings.Length-1;r++)
            for(int i=0;i<sides;i++)
            {
                var a=Point(rings[r],i); var b=Point(rings[r],i+1);
                var c=Point(rings[r+1],i); var d=Point(rings[r+1],i+1);
                surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(b);
                surface.AddVertex(b); surface.AddVertex(c); surface.AddVertex(d);
            }
        for(int i=0;i<sides;i++)
        {
            surface.AddVertex(new(0,rings[0].Y,0)); surface.AddVertex(Point(rings[0],i)); surface.AddVertex(Point(rings[0],i+1));
            surface.AddVertex(new(0,rings[^1].Y,0)); surface.AddVertex(Point(rings[^1],i+1)); surface.AddVertex(Point(rings[^1],i));
        }
        Finish(root,surface,offset,color);
    }

    internal static void Patch(Node3D root,Color color,params Vector3[] points)
    {
        var surface=new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(uint.MaxValue);
        for(int i=1;i<points.Length-1;i++)
        {
            surface.AddVertex(points[0]); surface.AddVertex(points[i]); surface.AddVertex(points[i+1]);
        }
        Finish(root,surface,Vector3.Zero,color);
    }

    private static void Finish(Node3D root,SurfaceTool surface,Vector3 offset,Color color)
    {
        surface.GenerateNormals();
        var mesh=GardenArt.Shape(root,surface.Commit(),offset,Vector3.One,color);
        ((StandardMaterial3D)mesh.MaterialOverride).CullMode=BaseMaterial3D.CullModeEnum.Disabled;
    }
}
