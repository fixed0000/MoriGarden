using Godot;

namespace Mori;

// Every zone uses the garden's water palette, moving ripples and transparency.
internal static class WaterArt
{
    internal static float Level(GardenZone zone)=>zone==GardenZone.Expedition ? WindfallMeadow.WaterLevel : zone==GardenZone.CoastalCove ? .022f : .004f;
    internal static bool PlayerWet(GardenZone zone,Vector3 at)=>zone switch {
        GardenZone.Garden=>GardenArt.IsPondWater(at),
        GardenZone.Expedition=>WindfallMeadow.Wet(at),
        GardenZone.CoastalCove=>at.Y<.09f && ((new Vector2(at.X,at.Z)-new Vector2(-16,2))/new Vector2(14.56f,17.16f)).Length()<1,
        _=>false };
    internal static ShaderMaterial Material(bool waterfall=false,bool transparent=true,bool gardenCutout=false)
    {
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            uniform bool waterfall=false;
            uniform bool transparent_pond=true;
            uniform bool garden_cutout=false;
            uniform vec2 pond_center=vec2(0.0);
            uniform float pond_radius=100000.0;
            varying vec3 world;
            void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
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
            }
            """ } };
        material.SetShaderParameter("waterfall",waterfall);
        material.SetShaderParameter("transparent_pond",transparent);
        material.SetShaderParameter("garden_cutout",gardenCutout);
        return material;
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
