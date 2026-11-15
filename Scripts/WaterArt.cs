using Godot;

namespace Mori;

// Every zone uses the garden's water palette, moving ripples and transparency.
internal static class WaterArt
{
    internal static NaturalWaterBody? Body(GardenZone zone)=>zone switch {
        GardenZone.Expedition=>WindfallMeadow.Water, GardenZone.CoastalCove=>CoastalCove.Water,_=>null };
    internal static float Level(GardenZone zone)=>Body(zone)?.Level ?? .004f;
    internal static bool PlayerWet(GardenZone zone,Vector3 at)=>zone switch {
        GardenZone.Garden=>GardenArt.IsPondWater(at),
        _=>Body(zone)?.Contains(at)==true };
    internal static ShaderMaterial Material(bool waterfall=false,bool transparent=true,bool gardenCutout=false)
    {
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            uniform bool waterfall=false;
            uniform bool transparent_pond=true;
            uniform bool garden_cutout=false;
            uniform bool natural_surface=false;
            uniform bool sea=false;
            uniform sampler2D depth_map : filter_linear, repeat_disable;
            uniform vec2 water_origin;
            uniform vec2 water_size=vec2(1.0);
            uniform vec2 pond_center=vec2(0.0);
            uniform float pond_radius=100000.0;
            varying vec3 world;
            void vertex() {
                world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
                if(natural_surface) VERTEX.y+=(sin(world.x*.9+TIME*.75)+sin(world.z*1.3-TIME*.55))*(sea ? .006 : .002)*smoothstep(.01,.20,COLOR.r);
            }
            void fragment() {
                float bands=sin(world.x*12.0+sin(world.x*2.8)*3.0);
                float streak=waterfall ? sin(world.y*1.8+TIME*9.0+bands*.45) : sin(world.z*7.0-TIME*2.0+bands*1.8);
                float whitewater=waterfall ? smoothstep(-.25,.92,bands*.78+streak*.22) : smoothstep(.3,1.0,streak*.65+bands*.35);
                ALBEDO=mix(vec3(.19,.38,.43),vec3(.67,.79,.79),whitewater*(waterfall ? .87 : .36));
                if(garden_cutout) {
            """+MountainInterior.VoidDiscard+"""
                }
                if(transparent_pond && distance(world.xz,pond_center)>pond_radius) discard;
                ALPHA=transparent_pond ? .39+whitewater*.13 : waterfall ? .61+whitewater*.25 : 1.0;
                ROUGHNESS=.35;
                if(natural_surface) {
                    float depth=texture(depth_map,(world.xz-water_origin)/water_size).r*8.0;
                    if(depth<.002) discard;
                    float ripple=sin(world.x*3.2+world.z*2.1+TIME*.7)*sin(world.z*4.7-TIME*.55);
                    float crest=smoothstep(.90,.99,sin(world.x*3.0+sin(world.z*1.6)*.65+TIME*.65))*(.5+.5*sin(world.z*2.7-TIME*.4));
                    vec3 shallow=vec3(.18,.34,.34), deep=sea ? vec3(.075,.20,.25) : vec3(.13,.27,.27);
                    ALBEDO=mix(shallow,deep,smoothstep(.15,sea ? 3.5 : .7,depth))+ripple*.014+crest*.035;
                    float foam=(1.0-smoothstep(.015,.12,depth))*smoothstep(.1,.65,sin(world.x*1.8+world.z*2.7-TIME*.7));
                    ALBEDO=mix(ALBEDO,vec3(.70,.73,.66),foam*(sea ? .35 : .10));
                    ALPHA=mix(.29,sea ? .87 : .54,smoothstep(0.0,sea ? 2.0 : .40,depth))*smoothstep(.002,.045,depth);
                    ROUGHNESS=.30; SPECULAR=.2;
                    NORMAL=normalize(NORMAL+vec3(ripple*.014,0.0,cos(world.x*3.2+TIME*.7)*.014));
                }
            }
            """ } };
        material.SetShaderParameter("waterfall",waterfall);
        material.SetShaderParameter("transparent_pond",transparent);
        material.SetShaderParameter("garden_cutout",gardenCutout);
        return material;
    }
    internal static MeshInstance3D Build(Node3D root,NaturalWaterBody body)
    {
        var mesh=new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector2 a,Vector2 b,Vector2 c)
        {
            var input=new[]{a,b,c}; var polygon=new System.Collections.Generic.List<Vector2>();
            for(int i=0;i<3;i++)
            {
                var p=input[i]; var q=input[(i+1)%3];
                float dp=body.SignedVertexDepth(p)-.001f,dq=body.SignedVertexDepth(q)-.001f;
                if(dp>=0) polygon.Add(p);
                if((dp>=0)!=(dq>=0))polygon.Add(p.Lerp(q,dp/(dp-dq)));
            }
            for(int i=1;i+1<polygon.Count;i++)foreach(var p in new[]{polygon[0],polygon[i],polygon[i+1]})
            {
                mesh.SetColor(new Color(Mathf.Clamp(body.Depth(p),0,1),0,0));
                mesh.AddVertex(new(p.X,body.Level,p.Y));
            }
        }
        for(float x=body.Bounds.Position.X;x<body.Bounds.End.X;x++)for(float z=body.Bounds.Position.Y;z<body.Bounds.End.Y;z++)
        { Triangle(new(x,z),new(x+1,z),new(x,z+1)); Triangle(new(x+1,z),new(x+1,z+1),new(x,z+1)); }
        mesh.GenerateNormals();
        var map=Image.CreateEmpty(body.Sea ? 384 : 256,body.Sea ? 384 : 256,false,Image.Format.Rf);
        for(int x=0;x<map.GetWidth();x++)for(int y=0;y<map.GetHeight();y++)
        {
            var p=body.Bounds.Position+body.Bounds.Size*new Vector2((float)x/(map.GetWidth()-1),(float)y/(map.GetHeight()-1));
            map.SetPixel(x,y,new Color(body.Depth(p)/8,0,0));
        }
        var material=Material(); material.SetShaderParameter("natural_surface",true); material.SetShaderParameter("sea",body.Sea);
        material.SetShaderParameter("water_origin",body.Bounds.Position); material.SetShaderParameter("water_size",body.Bounds.Size);
        material.SetShaderParameter("depth_map",ImageTexture.CreateFromImage(map));
        var water=new MeshInstance3D { Name=body.Id,Mesh=mesh.Commit(),MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(water); return water;
    }
    internal static MeshInstance3D Ellipse(Node3D root,Vector3 center,Vector2 radii,string name)
    {
        var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=0;i<96;i++)
            foreach(var p in new[]{Vector3.Zero,new Vector3(Mathf.Cos(i*Mathf.Tau/96)*radii.X,0,Mathf.Sin(i*Mathf.Tau/96)*radii.Y),
                new Vector3(Mathf.Cos((i+1)*Mathf.Tau/96)*radii.X,0,Mathf.Sin((i+1)*Mathf.Tau/96)*radii.Y)}) surface.AddVertex(p);
        surface.GenerateNormals();
        var water=new MeshInstance3D { Name=name,Position=center,Mesh=surface.Commit(),MaterialOverride=Material(),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(water); return water;
    }
}
