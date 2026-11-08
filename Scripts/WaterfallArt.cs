using Godot;

namespace Mori;

internal static class WaterfallArt
{
    public static void Build(Node3D root, Vector3 pool, ShaderMaterial water)
    {
        const float height = 7.1f;
        const float width = 4.3f;
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int lane, int row)
        {
            float x = lane/12f-.5f;
            float t = row/14f;
            return new(x*width*(1+.12f*t*t),height*(1-t),
                .10f*Mathf.Sin(lane*1.7f+row*.45f)+.24f*t+.42f*t*t*t);
        }
        for (int lane=0;lane<12;lane++)
            for (int row=0;row<14;row++)
            {
                var a=Point(lane,row); var b=Point(lane+1,row);
                var c=Point(lane,row+1); var d=Point(lane+1,row+1);
                mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
                mesh.AddVertex(b); mesh.AddVertex(d); mesh.AddVertex(c);
            }
        mesh.GenerateNormals();
        var sheet = GardenArt.Shape(root,mesh.Commit(),pool,Vector3.One,Colors.White);
        sheet.MaterialOverride = water;
        sheet.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        var drops = new CpuParticles3D
        {
            Position=pool+new Vector3(0,height-.02f,.09f),
            Amount=360, Lifetime=.87, Preprocess=.9,
            EmissionShape=CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents=new(width*.45f,.04f,.15f),
            Direction=new(0,-1,.07f), Spread=3, Gravity=new(0,-17,0),
            InitialVelocityMin=1.0f, InitialVelocityMax=2.0f,
            Mesh=new BoxMesh { Size=new(.032f,.30f,.035f),
                Material=GardenArt.Material(new Color("a8c9cf")) }
        };
        root.AddChild(drops);

        var splash = new CpuParticles3D
        {
            Position=pool+new Vector3(0,.055f,.68f),
            Amount=175, Lifetime=.47, Preprocess=.5,
            EmissionShape=CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents=new(width*.48f,.025f,.36f),
            Direction=new(0,1,.55f), Spread=56, Gravity=new(0,-6,0),
            InitialVelocityMin=.65f, InitialVelocityMax=2.3f,
            ScaleAmountMin=.65f, ScaleAmountMax=1.45f,
            Mesh=new SphereMesh { Radius=.043f, Height=.086f, RadialSegments=6, Rings=3,
                Material=GardenArt.Material(new Color("c4dadc")) }
        };
        root.AddChild(splash);

        var sound = AudioStreamWav.LoadFromFile(ProjectSettings.GlobalizePath("res://Audio/waterfall.wav"));
        if (sound == null) return;
        sound.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        sound.LoopEnd = Mathf.RoundToInt((float)sound.GetLength()*sound.MixRate);
        root.AddChild(new AudioStreamPlayer3D
        {
            Name="WaterfallSound", Position=pool+new Vector3(0,1.15f,.62f), Stream=sound,
            Autoplay=true, VolumeDb=-16, UnitSize=5, MaxDistance=35
        });
    }
}
