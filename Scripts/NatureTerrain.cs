using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

// Shared terrain and path tools. A region supplies relief, paths and material masks.
internal static class NatureTerrain
{
    internal static Vector2[] SmoothPath(params Vector2[] controls)
    {
        var result=new List<Vector2>();
        for(int i=0;i<controls.Length-1;i++)
        {
            var a=controls[Math.Max(0,i-1)]; var b=controls[i]; var c=controls[i+1]; var d=controls[Math.Min(i+2,controls.Length-1)];
            int count=Mathf.CeilToInt(b.DistanceTo(c)*1.5f);
            for(int j=0;j<count;j++)
            {
                float t=(float)j/count;
                result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
        }
        result.Add(controls[^1]); return result.ToArray();
    }
    internal static float PathDistance(Vector2 at,IEnumerable<Vector2[]> paths)
    {
        float best=float.MaxValue;
        foreach(var path in paths) for(int i=1;i<path.Length;i++)
        {
            var delta=path[i]-path[i-1];
            float t=Mathf.Clamp((at-path[i-1]).Dot(delta)/Mathf.Max(.0001f,delta.LengthSquared()),0,1);
            best=Mathf.Min(best,at.DistanceTo(path[i-1]+delta*t));
        }
        return best;
    }
    internal static MeshInstance3D Build(Node3D root,Func<float,float,float> height,Func<Vector2,Color> masks,ShaderMaterial? surfaceMaterial=null,string name="MeadowTerrain")
    {
        var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles); surface.SetSmoothGroup(0);
        Vector3 At(float x,float z)=>new(x,height(x,z),z);
        for(int x=-40;x<40;x++) for(int z=-32;z<32;z++)
            foreach(var p in new[]{At(x,z),At(x+1,z),At(x,z+1),At(x+1,z),At(x+1,z+1),At(x,z+1)}) surface.AddVertex(p);
        surface.Index(); surface.GenerateNormals();
        var image=Image.CreateEmpty(256,208,false,Image.Format.Rgba8);
        for(int x=0;x<256;x++) for(int y=0;y<208;y++) image.SetPixel(x,y,masks(new(-40+x*80f/255,-32+y*64f/207)));
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            uniform sampler2D masks : filter_linear, repeat_disable;
            varying vec3 world;
            float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
            float noise(vec2 p) {
                vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
                return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
            }
            void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
            void fragment() {
                vec4 mask=texture(masks,(world.xz+vec2(40,32))/vec2(80,64));
                float broad=noise(world.xz*.23), grain=noise(world.xz*16.0);
                vec3 grass=mix(vec3(.23,.31,.16),vec3(.38,.44,.24),broad);
                grass=mix(grass,vec3(.22,.24,.14),mask.g*.7);
                vec3 dirt=mix(vec3(.40,.32,.22),vec3(.56,.46,.32),broad);
                vec3 rock=mix(vec3(.34,.36,.31),vec3(.48,.47,.39),grain);
                vec3 base=mix(grass,dirt,smoothstep(.04,.97,mask.r));
                base=mix(base,rock,mask.b*.75);
                base=mix(base,vec3(.30,.27,.20),mask.a*.94);
                ALBEDO=base*(.94+grain*.12); ROUGHNESS=.97;
            }
            """ } };
        material=surfaceMaterial ?? material;
        material.SetShaderParameter("masks",ImageTexture.CreateFromImage(image));
        var terrain=new MeshInstance3D { Name=name,Mesh=surface.Commit(),MaterialOverride=material };
        root.AddChild(terrain); terrain.CreateTrimeshCollision(); return terrain;
    }
    internal static void DistantHills(Node3D root,Func<float,float,float> height)
    {
        var mesh=new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles); mesh.SetSmoothGroup(0);
        Vector3 At(float x,float z)
        {
            float outside=Mathf.Max(Mathf.Abs(x)-40,Mathf.Abs(z)-32);
            float ridge=Mathf.SmoothStep(0,18,outside)*(8+Mathf.Sin(x*.13f+z*.09f)*2.5f);
            return new(x,height(Mathf.Clamp(x,-40,40),Mathf.Clamp(z,-32,32))+ridge,z);
        }
        for(int x=-76;x<76;x++) for(int z=-66;z<66;z++)
        {
            if(x>=-40 && x<40 && z>=-32 && z<32)continue;
            foreach(var p in new[]{At(x,z),At(x+1,z),At(x,z+1),At(x+1,z),At(x+1,z+1),At(x,z+1)}) mesh.AddVertex(p);
        }
        mesh.Index(); mesh.GenerateNormals();
        root.AddChild(new MeshInstance3D { Name="DistantWoodedSlopes",Mesh=mesh.Commit(),MaterialOverride=GardenArt.Material(new("566c50")) });
    }
}
