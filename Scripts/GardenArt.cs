using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

// Вся графика прототипа строится из простых фигур — без платных моделей.
public static class GardenArt
{
    public static readonly Color Mint = new("8edac2");
    public static readonly Color Cream = new("fff2cc");
    public static readonly Color Ink = new("253e43");
    public static readonly Vector3 Pond = new(-10,0,-10);
    public const float GardenHalfWidth = 98;
    public const float GardenHalfDepth = 64;
    public const float RearBoundary=-26.5f;
    public static Vector3 RelocateFromRear(Vector3 at) => at.Z<RearBoundary+.5f ? new(at.X,0,19) : at;
    public const float PondRadius = 11.4f;
    public const float PondDepth = .30f;
    public static bool IsPondWater(Vector3 at) =>
        at.Y<.11f && new Vector2(at.X-Pond.X,at.Z-Pond.Z).LengthSquared()<PondRadius*PondRadius;
    public static readonly Vector3 SwimExit = Pond + new Vector3(9.25f,0,9.2f);
    public static readonly Vector3[] TreePositions = { new(-7,0,3), new(8,0,7), new(19,0,-7), new(-23,0,-4), new(7,0,-14), new(-17,0,7), new(17,0,2) };
    public static readonly (Vector3 Center, Vector3 Size)[] Ledges = {
        (new(-2,.4f,-10),new(2.8f,.8f,3.4f)),
        (new(-2,.8f,-12.7f),new(3.0f,1.6f,3.4f)),
        (new(-2.5f,1.2f,-15.3f),new(3.7f,2.4f,3.2f)),
        (new(-4.5f,1.6f,-17.2f),new(4.3f,3.2f,3.2f)),
        (new(-7,2,-18.3f),new(3.6f,4f,3.4f)),
        (new(-8.55f,2.73f,-20.0f),new(3.1f,5.46f,3.3f)),
    };
    public static readonly Vector3 Balcony = new(-11.72f,7.49f,-17.74f);
    public static readonly Vector3 BalconyRampStart=new(-8.90f,5.46f,-19.67f);
    public static readonly Vector3 BalconyRampEnd=new(-11.70f,7.49f,-19.67f);
    public static readonly Vector3 WaterfallExit=new(-9.15f,5.46f,-18.64f);
    public static Vector3 BalconyRail => Balcony+new Vector3(0,.55f,1.10f);
    public static StandardMaterial3D Material(Color color, bool glow = false) => new()
    {
        AlbedoColor = color, Roughness = .85f, MetallicSpecular = .15f,
        EmissionEnabled = glow, Emission = color, EmissionEnergyMultiplier = glow ? .45f : 0
    };

    public static MeshInstance3D Shape(Node3D parent, Mesh mesh, Vector3 position, Vector3 scale, Color color)
    {
        var node = new MeshInstance3D { Mesh = mesh, Position = position, Scale = scale, MaterialOverride = Material(color) };
        parent.AddChild(node);
        return node;
    }

    public static void StoneCollision(MeshInstance3D mesh,bool climbable=false)
    {
        mesh.CreateConvexCollision();
        foreach (var node in mesh.GetChildren())
            if (node is StaticBody3D body)
            {
                body.SetMeta("stone",true);
                if (!climbable) body.SetMeta("unclimbable",true);
            }
    }

    public static MeshInstance3D Ball(Node3D parent, Vector3 pos, Vector3 scale, Color color) =>
        Shape(parent, new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 24, Rings = 12 }, pos, scale, color);

    public static MeshInstance3D Cylinder(Node3D parent, Vector3 pos, float radius, float height, Color color) =>
        Shape(parent, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 48 }, pos, Vector3.One, color);

    public static Label3D Sign(Node3D parent, string text, Vector3 pos, Color color)
    {
        var sign=new ProximitySign { Position=pos,Visible=false };
        parent.AddChild(sign);
        const int size=52;
        const float pixels=.0045f;
        float width=ThemeDB.FallbackFont.GetStringSize(text,fontSize:size).X*pixels+.095f;
        var plate = Shape(sign,new QuadMesh { Size=new(width,.26f) },Vector3.Zero,Vector3.One,Colors.White);
        plate.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        var background=new ShaderMaterial { RenderPriority=-1, Shader=new Shader { Code="""
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
            uniform vec2 card_size;
            void vertex() {
                MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1],
                    INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
            }
            void fragment() {
                vec2 p=abs((UV-.5)*card_size)-(card_size*.5-vec2(.037));
                float d=length(max(p,vec2(0)))+min(max(p.x,p.y),0.0)-.037;
                ALBEDO=vec3(.035,.055,.064);
                ALPHA=.52*(1.0-smoothstep(-.009,.002,d));
            }
            """ } };
        background.SetShaderParameter("card_size",new Vector2(width,.26f));
        plate.MaterialOverride=background;
        var label=new Label3D { Text=text,FontSize=size,PixelSize=pixels,Modulate=new("fbf4df"),
            OutlineSize=0,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,RenderPriority=1 };
        sign.AddChild(label);
        return label;
    }

    public static Node3D Creature(Node3D parent, bool player, string form = "seed")
    {
        if (!player) return ImportedChao.Build(parent,form);
        var root = new Node3D();
        parent.AddChild(root);
        AvatarArt.Build(root);
        return root;
    }

    public static Node3D LifeShell(Node3D parent, bool egg)
    {
        var root = new Node3D();
        parent.AddChild(root);
        if (egg) GardenEgg.DrawShell(root,GardenEgg.Pigment("White"),new(.42f,.55f,.42f),.275f);
        else
        {
            Ball(root,new(0,.72f,0),new(1.12f,1.48f,1.04f),new("b8d7c9"));
            Shape(root,new TorusMesh { InnerRadius=.46f,OuterRadius=.53f },new(0,.55f,0),Vector3.One,new("dcebd2"));
        }
        return root;
    }

    public static void BuildGarden(Node3D root,IReadOnlyList<GardenTreeState>? trees=null)
    {
        var ground = BuildPondTerrain(root);
        ground.MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            varying vec3 world;
            void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
            void fragment() {
                vec2 p = world.xz;
                float grain = fract(sin(dot(floor(p*13.0),vec2(127.1,311.7)))*43758.5453);
                float patch = sin(p.x*.7+sin(p.y*.8))*sin(p.y*.44);
                vec3 grass = mix(vec3(.16,.38,.08),vec3(.39,.57,.17), .46+.30*patch+.16*grain);
                float path = 1.0-smoothstep(.65,1.6,abs(p.x-sin(p.y*.29)*.85));
                path *= 1.0-smoothstep(6.0,11.0,p.y);
                float side_path = (1.0-smoothstep(.6,1.55,abs(p.y+4.0+p.x*.54))) * smoothstep(1.0,5.0,p.x);
                path = max(path,side_path);
                float cross_path = (1.0-smoothstep(.65,1.85,abs(p.y-5.5-sin(p.x*.095)*2.3))) *
                    smoothstep(9.0,18.0,abs(p.x)) * (1.0-smoothstep(90.0,110.0,abs(p.x)));
                float long_path=(1.0-smoothstep(.65,1.6,abs(p.x-2.0-sin(p.y*.11)*4.5)))*
                    smoothstep(13.0,26.0,abs(p.y));
                path=max(max(path,cross_path*.75),long_path*.77);
                ALBEDO = mix(grass,vec3(.48,.36,.20)+grain*.035,path*.88);
                ROUGHNESS = 1.0;
            }
            """ } };
        Vector3 pond = Pond;
        var water = Shape(root,new PlaneMesh { Size = new(PondRadius*2,PondRadius*2) },
            Pond+Vector3.Up*.004f,Vector3.One,Colors.White);
        var pondWater = FlowMaterial(false,true);
        pondWater.SetShaderParameter("pond_center",new Vector2(Pond.X,Pond.Z));
        pondWater.SetShaderParameter("pond_radius",PondRadius);
        water.MaterialOverride = pondWater;
        var gravel = new RandomNumberGenerator { Seed=802 };
        for (int i=0;i<58;i++)
        {
            float angle=gravel.Randf()*Mathf.Tau;
            float radius=Mathf.Sqrt(gravel.Randf())*(PondRadius-1.4f);
            var spot=Pond+new Vector3(Mathf.Cos(angle)*radius,-PondDepth+.023f,Mathf.Sin(angle)*radius);
            Ball(root,spot,new(gravel.RandfRange(.13f,.36f),.08f,gravel.RandfRange(.11f,.32f)),
                new(i%2==0?"797764":"aaa38b"));
        }
        for (int i=0;i<24;i++)
        {
            float angle = i*Mathf.Tau/24;
            if (angle is >.46f and <1.12f or >2.92f and <3.35f) continue;
            var stone=Shape(root,new CylinderMesh { RadialSegments=9,TopRadius=.13f,BottomRadius=.53f,
                Height=.27f+(i%3)*.022f },
                pond+new Vector3(Mathf.Cos(angle)*(PondRadius+.15f),.035f,Mathf.Sin(angle)*(PondRadius+.15f)),
                new(1+(i%3)*.17f,1,.81f),new("777b6d"));
            stone.RotationDegrees=new(0,i*19,0);
            StoneCollision(stone);
        }
        var strata = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            varying vec3 world;
            void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
            void fragment() {
                vec2 gate=vec2(world.x+18.45,world.y-.43);
                float arch=max(0.0,gate.y-2.53);
                if (world.z>-17.65 && world.y<5.03 && gate.y>0.0 && length(vec2(gate.x,arch))<2.25) discard;
                float layer = sin(world.y*10.0 + world.x*.85 + sin(world.z*3.0)*.65);
                float flecks = fract(sin(dot(floor(world*19.0),vec3(12.99,78.23,45.61)))*43758.5453);
                float seam = smoothstep(.78, .98, layer);
                ALBEDO = vec3(.31,.33,.31) + flecks*.065 - seam*.075;
                ROUGHNESS = .94;
            }
            """ } };
        for (int i=0;i<8;i++)
        {
            var rock = Shape(root,new SphereMesh { RadialSegments=7,Rings=4 },
                new(-17+i*1.65f,3.0f,i>=4 ? -24.5f : i==3 ? -23.55f : i<2 ? -21.6f : -19.60f),new(4.8f,8.4f+(i%3)*.9f,5.15f),
                new(i%2==0?"565d61":"71766f"));
            rock.RotationDegrees = new(0,i*39,0);
            rock.MaterialOverride = strata;
            StoneCollision(rock);

        }
        // One continuous stone mass behind the fall and its doorway.
        for (int i=0;i<6;i++)
        {
            var backWall=Shape(root,new SphereMesh { RadialSegments=5,Rings=3 },
                new(-23.2f+i*3.8f,2.8f+(i%3)*.43f,(i>=3 ? -25.2f : -22.1f)+(i%2)*.42f),
                new(7.9f,10.35f+(i%3)*1.2f,6.6f),new("626d64"));
            backWall.RotationDegrees=new(4-i*2,19+i*23,i%2==0 ? -11 : 14);
            backWall.MaterialOverride=strata;
            StoneCollision(backWall);
        }
        var doorLintel=Shape(root,new SphereMesh { RadialSegments=7,Rings=4 },
            ZoneArt.GardenLobbyGate+new Vector3(0,5.95f,-2.65f),new(7.2f,3.6f,4.4f),new("6c776c"));
        doorLintel.MaterialOverride=strata;
        StoneCollision(doorLintel);
        void BalconyStone(Vector3 top,Vector3 size)
        {
            var slab=Shape(root,new BoxMesh(),top-Vector3.Up*size.Y*.5f,size,new("777c70"));
            slab.MaterialOverride=strata;
            StoneCollision(slab,climbable:true);
        }
        BalconyStone(Balcony,new(4.58f,.37f,2.84f));
        BalconyStone(BalconyRampEnd+new Vector3(-.70f,0,.10f),new(1.42f,.36f,1.65f));
        Vector3 rampAxis=BalconyRampEnd-BalconyRampStart;
        var ramp=Shape(root,new BoxMesh(),(BalconyRampEnd+BalconyRampStart)*.5f,
            new(1.93f,.24f,rampAxis.Length()),new("777c70"));
        ramp.LookAt(ramp.Position+rampAxis);
        ramp.Position-=ramp.Basis.Orthonormalized().Y*.12f;
        ramp.MaterialOverride=strata;
        StoneCollision(ramp);
        // The rear edges of the upper terraces are embedded in the mountain.
        for (int i=Ledges.Length-3;i<Ledges.Length;i++)
        {
            var (center,size)=Ledges[i];
            float rear=center.Z-size.Z*.5f;
            float top=center.Y+size.Y*.5f;
            float wallHeight=top+5.4f+(i%2)*.6f;
            var face=Shape(root,new BoxMesh(),new(center.X,wallHeight*.5f,rear-1.09f),
                new(size.X,wallHeight,2.26f),new("626d64"));
            face.MaterialOverride=strata;
            StoneCollision(face,climbable:false);
            var crown=Shape(root,new SphereMesh { RadialSegments=6,Rings=4 },
                new(center.X-.25f,wallHeight-.40f,rear-1.65f),
                new(size.X+.62f,2.6f+(i%2)*.55f,2.48f),new("626d64"));
            crown.RotationDegrees=new(0,13+i*17,0);
            crown.MaterialOverride=strata;
            StoneCollision(crown,climbable:false);
        }
        BalconyStone(BalconyRail,new(4.08f,.14f,.61f));
        for (int i=-2;i<=2;i++)
            BalconyStone(BalconyRail+new Vector3(i*.94f,-.11f,0),new(.19f,.46f,.24f));
        BalconyStone(Balcony+new Vector3(-2.01f,.55f,0),new(.50f,.55f,1.76f));
        for (int i=0;i<3;i++)
        {
            var support=Shape(root,new PrismMesh { LeftToRight=.25f+i*.2f },
                Balcony+new Vector3(-1.42f+i*1.42f,-.68f,-.79f),new(1.1f,1.05f,1.36f),new("6b7368"));
            support.MaterialOverride=strata;
        }
        var fallBack=Shape(root,new BoxMesh(),new(-11,3.54f,-17.11f),
            new(4.65f,7.1f,.57f),new("526158"));
        fallBack.MaterialOverride=strata;
        StoneCollision(fallBack);
        // A split rock shelf feeds the visible sheet from behind the cliff.
        var crest = Shape(root,new BoxMesh(),new(-11,6.88f,-17.48f),new(5.05f,.46f,2.23f),new("657065"));
        crest.MaterialOverride=strata;
        StoneCollision(crest);
        var inlet=Shape(root,new PlaneMesh { Size=new(4.32f,1.78f) },
            new(-11,7.13f,-17.48f),Vector3.One,Colors.White);
        inlet.MaterialOverride=FlowMaterial(false);
        WaterfallArt.Build(root,new(-11,0,-16.64f),FlowMaterial(true));
        foreach (var (center,size) in Ledges)
        {
            var terrace=Shape(root,TerraceMesh(size),center,Vector3.One,new("74796e"));
            terrace.MaterialOverride=strata;
            StoneCollision(terrace,climbable:true);
            for (int i=0;i<3;i++)
                Shape(root,new SphereMesh { RadialSegments=7,Rings=3 },
                    center+new Vector3((i-1)*size.X*.32f,-size.Y*.15f,size.Z*.47f),
                    new(size.X*.43f,size.Y*.76f,.57f),new(i%2==0?"616861":"838579"));
        }
        var orchard=trees ?? TreePositions.Select(position=>new GardenTreeState { X=position.X,Z=position.Z }).ToArray();
        foreach (var tree in orchard)
            root.AddChild(new FruitTree { Position=new(tree.X,0,tree.Z),FruitKind=tree.FruitKind,TreeState=tree });
        GardenLandscape.Build(root);
        var details = new RandomNumberGenerator { Seed = 731 };
        for (int i=0;i<28;i++)
        {
            float angle = details.RandfRange(0,Mathf.Tau);
            if (Mathf.Sin(angle)<-.65f || Mathf.Cos(angle)>.74f) continue;
            var shore = Pond + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*details.RandfRange(PondRadius+.05f,PondRadius+1.05f);
            for (int reed=0;reed<4;reed++)
            {
                float height = details.RandfRange(.28f,.83f);
                var blade = Shape(root,new CylinderMesh { TopRadius=.005f,BottomRadius=.035f,Height=height,RadialSegments=4 },
                    shore+new Vector3(reed*.11f,height*.5f,details.RandfRange(-.2f,.2f)),
                    Vector3.One,new(reed%2==0?"506247":"697755"));
                blade.RotationDegrees = new(0,0,details.RandfRange(-23,23));
            }
        }
        // A worn trail marker at the fork.
        Shape(root,new BoxMesh(),new(2,.72f,-5.15f),new(.15f,1.44f,.15f),new("514638"));
        Shape(root,new BoxMesh(),new(2,1.40f,-5),new(.98f,.38f,.13f),new("72634e"));
        var boardText=new Label3D { Text="MORI GARDEN",Position=new(2,1.4f,-4.927f),
            FontSize=54,PixelSize=.0018f,Modulate=Cream,OutlineSize=0 };
        root.AddChild(boardText);
        ZoneArt.Solid(root,new(2,.72f,-5.15f),new(.15f,1.44f,.15f));
        // Кормушка слева спереди.
        Cylinder(root, new(-5, .13f, 4), 1, .26f, new("736956"));
        Cylinder(root, new(-5, .31f, 4), 1.12f, .13f, new("a29878"));
        var feeder=new StaticBody3D { Position=new(-5,.1875f,4) };
        feeder.SetMeta("no_step",true); feeder.SetMeta("jumpable",true);
        feeder.AddChild(new CollisionShape3D { Shape=new CylinderShape3D { Radius=1.10f,Height=.375f } });
        root.AddChild(feeder);

        // Мягкая постель питомца.
        Cylinder(root, new(5, .12f, 4), 1.6f, .23f, new("706e5f")).CreateConvexCollision();
        Ball(root, new(5, .26f, 4), new(2.8f, .35f, 2.4f), new("9a9277")).CreateConvexCollision();
        Ball(root, new(5, .4f, 3.3f), new(1.3f, .25f, .65f), new("ada78c")).CreateConvexCollision();

    }
    private static Mesh TerraceMesh(Vector3 size)
    {
        var half=size*.5f;
        Vector3[] edge={
            new(-half.X,half.Y,-half.Z),new(half.X,half.Y,-half.Z),
            new(half.X,half.Y,half.Z-.26f),new(half.X-.46f,half.Y,half.Z),
            new(-half.X+.34f,half.Y,half.Z-.06f),new(-half.X,half.Y,half.Z-.42f)
        };
        var mesh=new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        var depth=Vector3.Down*size.Y;
        for (int i=0;i<edge.Length;i++)
        {
            var a=edge[i]; var b=edge[(i+1)%edge.Length];
            mesh.AddVertex(Vector3.Up*half.Y); mesh.AddVertex(a); mesh.AddVertex(b);
            mesh.AddVertex(a); mesh.AddVertex(a+depth); mesh.AddVertex(b);
            mesh.AddVertex(b); mesh.AddVertex(a+depth); mesh.AddVertex(b+depth);
        }
        mesh.GenerateNormals();
        return mesh.Commit();
    }

    private static MeshInstance3D BuildPondTerrain(Node3D root)
    {
        const int sides=144;
        Vector3 Ring(int i,int ring)
        {
            float a=i*Mathf.Tau/sides;
            var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            if (ring == 2) return Pond+direction*(PondRadius-1.15f)-Vector3.Up*PondDepth;
            if (ring == 1) return Pond+direction*(PondRadius+.55f);
            float toX=(direction.X>=0 ? GardenHalfWidth+4-Pond.X : GardenHalfWidth+4+Pond.X)/Mathf.Max(.0001f,Mathf.Abs(direction.X));
            float toZ=(direction.Z>=0 ? GardenHalfDepth+4-Pond.Z : GardenHalfDepth+4+Pond.Z)/Mathf.Max(.0001f,Mathf.Abs(direction.Z));
            return Pond+direction*Mathf.Min(toX,toZ);
        }

        var turf=new SurfaceTool();
        turf.Begin(Mesh.PrimitiveType.Triangles);
        var bed=new SurfaceTool();
        bed.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=0;i<sides;i++)
        {
            for(int ring=0;ring<2;ring++)
            {
                var surface=ring==0 ? turf : bed;
                var a=Ring(i,ring); var b=Ring(i+1,ring);
                var c=Ring(i,ring+1); var d=Ring(i+1,ring+1);
                surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
                surface.AddVertex(b); surface.AddVertex(d); surface.AddVertex(c);
            }
            bed.AddVertex(Ring(i,2)); bed.AddVertex(Ring(i+1,2));
            bed.AddVertex(Pond-Vector3.Up*PondDepth);
        }
        turf.GenerateNormals();
        bed.GenerateNormals();
        var grass=Shape(root,turf.Commit(),Vector3.Zero,Vector3.One,Colors.White);
        grass.CreateTrimeshCollision();
        var bottom=Shape(root,bed.Commit(),Vector3.Zero,Vector3.One,new("a49b7d"));
        bottom.CreateTrimeshCollision();
        return grass;
    }

    private static ShaderMaterial FlowMaterial(bool waterfall,bool transparentPond=false)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            uniform bool waterfall = false;
            uniform bool transparent_pond = false;
            uniform vec2 pond_center;
            uniform float pond_radius;
            varying vec3 world;
            void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
            void fragment() {
                float bands = sin(world.x*12.0 + sin(world.x*2.8)*3.0);
                float streak = waterfall ? sin(world.y*1.8+TIME*9.0+bands*.45) : sin(world.z*7.0-TIME*2.0+bands*1.8);
                float whitewater = waterfall ? smoothstep(-.25,.92,bands*.78+streak*.22) : smoothstep(.3,1.0,streak*.65+bands*.35);
                ALBEDO = mix(vec3(.19,.38,.43),vec3(.67,.79,.79),whitewater*(waterfall ? .87 : .36));
                if (transparent_pond && distance(world.xz,pond_center)>pond_radius) { discard; }
                ALPHA = transparent_pond ? .39+whitewater*.13 : waterfall ? .61+whitewater*.25 : 1.0;
                ROUGHNESS = .35;
            }
            """ } };
        material.SetShaderParameter("waterfall",waterfall);
        material.SetShaderParameter("transparent_pond",transparentPond);
        return material;
    }

}
