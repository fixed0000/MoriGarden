using Godot;

namespace Mori;

public partial class ImportedChao : Node3D
{
    private float? sleepOffset;
    private readonly System.Collections.Generic.Dictionary<Node3D,Vector3> feet=new();

    public static void SleepPose(Node3D art)
    {
        const float roll=88;
        Gait(art,0,false,false);
        art.RotationDegrees=new(0,0,roll);
        if (art is ImportedChao model)
        {
            if (model.sleepOffset==null)
            {
                float bottom=0;
                var inverse=art.GlobalTransform.AffineInverse();
                var pose=new Basis(Vector3.Back,Mathf.DegToRad(roll)).Scaled(art.Scale);
                foreach (var node in art.GetChild(0).FindChildren("*","MeshInstance3D",true,false))
                {
                    if (node is not MeshInstance3D mesh || mesh.Mesh==null) continue;
                    var relative=inverse*mesh.GlobalTransform;
                    for (int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                        foreach (var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                            bottom=Mathf.Min(bottom,(pose*(relative*vertex)).Y);
                }
                model.sleepOffset=-bottom-.008f;
            }
            art.Position=Vector3.Up*model.sleepOffset.Value;
        }
    }

    public static void Gait(Node3D art,float phase,bool moving,bool swimming,bool sprinting=false)
    {
        float swing=moving ? Mathf.Sin(phase) : 0;
        for(int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "L" : "R";
            if (art is ImportedChao model && art.FindChild("Foot"+suffix,true,false) is Node3D foot)
            {
                if (!model.feet.TryGetValue(foot,out var rest)) model.feet[foot]=rest=foot.Position;
                float stride=side*swing;
                foot.Rotation=new(stride*(swimming ? .55f : sprinting ? .92f : .70f),0,0);
                foot.Position=rest+(swimming ? Vector3.Zero :
                    new Vector3(0,Mathf.Max(0,stride)*.025f,stride*(sprinting ? .043f : .032f)));
            }
            if (art.FindChild("Paddle"+suffix,true,false) is Node3D paddle)
                paddle.Rotation=new(sprinting ? -.98f+side*swing*.10f : -side*swing*(swimming ? 1.02f : .35f),
                    0,side*(swimming ? .32f : sprinting ? .18f : .04f));
        }
    }

    public static void MovePose(Node3D art,float phase,bool moving,bool water,bool swims,bool sprinting,bool airborne,bool flying)
    {
        Gait(art,phase,moving,water,sprinting);
        if (flying)
            foreach (var (suffix,side) in new[]{("L",-1),("R",1)})
                if (art.FindChild("Paddle"+suffix,true,false) is Node3D paddle)
                    paddle.Rotation=new(-.24f,0,side*(.90f+Mathf.Sin(phase)*.16f));
        art.RotationDegrees=new(water ? (swims ? 76 : 18) : sprinting ? 30 : airborne ? 20 : 0,0,
            water && !swims ? Mathf.Sin(phase*2.3f)*17 : 0);
        art.Position=Vector3.Up*(water ? (swims ? .19f : -.025f)+Mathf.Sin(phase)*.012f :
            airborne ? .025f : moving ? Mathf.Abs(Mathf.Sin(phase))*(sprinting ? .025f : .009f) : 0);
    }

    public static void EatPose(Node3D art,float remaining)
    {
        art.Rotation=new(.09f*Mathf.Sin(remaining*23),0,0);
        for(int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "L" : "R";
            if (art.FindChild("Paddle"+suffix,true,false) is Node3D paddle)
                paddle.Rotation=new(-.98f+.14f*Mathf.Sin(remaining*23),0,side*.29f);
        }
    }

    public static void ThinkPose(Node3D art,float remaining)
    {
        Gait(art,0,false,false);
        art.Rotation=Vector3.Zero;
        art.Position=Vector3.Zero;
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.Rotation=new(-.28f,.18f*Mathf.Sin(remaining*2.4f),.08f);
        if (art.FindChild("PaddleR",true,false) is Node3D hand)
            hand.Rotation=new(-1.42f,-.44f,.47f);
        if (art.FindChild("MoodOrb",true,false) is EmotionOrb orb) orb.SetThinking(true);
    }

    public static void ReceivePose(Node3D art,float reach)
    {
        art.Rotation=new(.14f*reach,0,0);
        for (int side=-1;side<=1;side+=2)
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
                paw.Rotation=new(-1.14f*reach,0,side*.30f*reach);
    }

    public static void EndThink(Node3D art)
    {
        if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=Vector3.Zero;
        if (art.FindChild("MoodOrb",true,false) is EmotionOrb orb) orb.SetThinking(false);
    }

    public static ImportedChao Build(Node3D parent,string form)
    {
        var model=new ImportedChao { Scale=Vector3.One*(form=="seed" ? .91f : 1.06f) };
        parent.AddChild(model);
        var document=new GltfDocument();
        var state=new GltfState();
        if (document.AppendFromFile(ProjectSettings.GlobalizePath("res://Assets/baby-chao.glb"),state)!=Error.Ok)
            throw new System.InvalidOperationException("Could not load the supplied baby Chao model");
        var scene=document.GenerateScene(state);
        model.AddChild(scene);
        var head=new Node3D { Name="HeadPivot", Position=new(0,.235f,0) };
        scene.AddChild(head);
        foreach (var child in scene.GetChildren())
        {
            string name=child.Name;
            if (child!=head && (name.StartsWith("Head_") || name.Contains("Eye") || name.Contains("Mouth")))
                child.Reparent(head,true);
        }
        var orb=new EmotionOrb { Name="MoodOrb", Position=new(0,.508f,-.015f) };
        model.AddChild(orb);
        var orbColor=new Color("f3cd59");
        GardenArt.Ball(orb,Vector3.Zero,new(.062f,.062f,.062f),orbColor).MaterialOverride=GardenArt.Material(orbColor,true);
        return model;
    }
}
