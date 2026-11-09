using Godot;

namespace Mori;

public partial class ImportedChao : Node3D
{
    private float? sleepOffset;
    private readonly System.Collections.Generic.Dictionary<Node3D,Vector3> feet=new();
    private readonly System.Collections.Generic.Dictionary<Node3D,(Node3D Sleep,Node3D Happy,Node3D Squint)> eyeShapes=new();
    private readonly System.Collections.Generic.Dictionary<MeshInstance3D,(Material? Original,ShaderMaterial Half)> lids=new();
    private static readonly Shader LidShader=new() { Code="""
        shader_type spatial;
        render_mode cull_disabled;
        uniform sampler2D eye_texture : source_color, hint_default_white;
        uniform vec4 eye_color : source_color = vec4(1.0);
        uniform float midpoint;
        varying float eye_y;
        void vertex() { eye_y=VERTEX.y; }
        void fragment() {
            if (eye_y>midpoint) discard;
            ALBEDO=(texture(eye_texture,UV)*eye_color).rgb;
            ROUGHNESS=.82;
        }
        """ };
    private bool eyesClosed, eyesHappy, eyesHalf, eyesSquint;

    public static void CloseEyes(Node3D art,bool closed)
    {
        if (art is not ImportedChao model) return;
        if (model.eyesClosed==closed) return;
        model.eyesClosed=closed;
        model.RefreshEyes();
    }

    public static void SmileEyes(Node3D art,bool happy)
    {
        if (art is not ImportedChao model || model.eyesHappy==happy) return;
        model.eyesHappy=happy;
        model.RefreshEyes();
    }

    public static void HalfEyes(Node3D art,bool half)
    {
        if (art is not ImportedChao model || model.eyesHalf==half) return;
        model.eyesHalf=half;
        model.RefreshEyes();
    }

    private void RefreshEyes()
    {
        if ((eyesClosed || eyesHappy || eyesHalf || eyesSquint) && eyeShapes.Count==0)
        {
            foreach (var eye in FindChildren("*_Eye","MeshInstance3D",true,false))
            {
                if (eye is not MeshInstance3D mesh) continue;
                var bounds=mesh.GetAabb();
                var center=mesh.Transform*new Vector3(bounds.GetCenter().X,bounds.GetCenter().Y,bounds.End.Z+.006f);
                var parent=(Node3D)mesh.GetParent();
                AddLid(mesh,bounds.GetCenter().Y);
                foreach (var child in mesh.GetChildren())
                    if (child is MeshInstance3D rim)
                        AddLid(rim,(bounds.GetCenter().Y-rim.Position.Y)/rim.Scale.Y);
                eyeShapes[mesh]=(EyeLine(parent,center,false),EyeLine(parent,center,true),
                    EyeLine(parent,center,true,center.X<0 ? -1 : 1));
            }
        }
        bool half=!eyesClosed && eyesHalf && !eyesSquint;
        foreach (var (mesh,lid) in lids) mesh.MaterialOverride=half ? lid.Half : lid.Original;
        foreach (var (eye,shapes) in eyeShapes)
        {
            eye.Visible=!eyesClosed && (!eyesHappy || half) && !eyesSquint;
            shapes.Sleep.Visible=eyesClosed;
            shapes.Happy.Visible=!eyesClosed && eyesHappy && !half && !eyesSquint;
            shapes.Squint.Visible=!eyesClosed && eyesSquint;
        }
    }

    private void AddLid(MeshInstance3D eye,float midpoint)
    {
        var lid=new ShaderMaterial { Shader=LidShader };
        if (eye.GetActiveMaterial(0) is StandardMaterial3D material)
        {
            if (material.AlbedoTexture!=null) lid.SetShaderParameter("eye_texture",material.AlbedoTexture);
            lid.SetShaderParameter("eye_color",material.AlbedoColor);
        }
        lid.SetShaderParameter("midpoint",midpoint);
        lids.Add(eye,(eye.MaterialOverride,lid));
    }

    private static Node3D EyeLine(Node3D parent,Vector3 center,bool happy,int squint=0)
    {
        var line=new Node3D { Position=center };
        parent.AddChild(line);
        var ink=new Color("655649");
        const float radius=.004f;
        int segments=happy ? 2 : 8;
        Vector3 previous=Vector3.Zero;
        for (int i=0;i<=segments;i++)
        {
            float x=-1+2f*i/segments;
            float y=happy ? .016f-.030f*Mathf.Abs(x) : .016f*x*x-.010f;
            var point=squint==0 ? new Vector3(x*.029f,y,0) :
                new Vector3(squint*(Mathf.Abs(x)-.5f)*.045f,x*.026f,0);
            GardenArt.Ball(line,point,Vector3.One*(2*radius),ink);
            if (i>0)
            {
                var bar=GardenArt.Cylinder(line,(previous+point)*.5f,radius,previous.DistanceTo(point),ink);
                bar.Quaternion=new Quaternion(Vector3.Up,(point-previous).Normalized());
            }
            previous=point;
        }
        return line;
    }

    public static void SleepPose(Node3D art)
    {
        const float roll=88;
        Gait(art,0,false,false);
        CloseEyes(art,true);
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

    public static void Gait(Node3D art,float phase,bool moving,bool swimming,bool sprinting=false,bool struggling=false)
    {
        float swing=moving ? Mathf.Sin(phase*(struggling ? 2.3f : 1)) : 0;
        for(int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "L" : "R";
            if (art is ImportedChao model && art.FindChild("Foot"+suffix,true,false) is Node3D foot)
            {
                if (!model.feet.TryGetValue(foot,out var rest)) model.feet[foot]=rest=foot.Position;
                float stride=side*swing;
                float angle=stride*(swimming ? (struggling ? 1.05f : .55f) : sprinting ? .92f : .70f);
                float heelClearance=swimming ? 0 : Mathf.Abs(Mathf.Sin(angle))*.077f;
                foot.Rotation=new(angle,0,0);
                foot.Position=rest+(swimming ? Vector3.Up*(struggling ? stride*.019f : 0) :
                    new Vector3(0,heelClearance+Mathf.Max(0,stride)*.025f,stride*(sprinting ? .043f : .032f)));
            }
            if (art.FindChild("Paddle"+suffix,true,false) is Node3D paddle)
                paddle.Rotation=new(sprinting ? -.98f+side*swing*.10f : -side*swing*(swimming ? (struggling ? 1.52f : 1.02f) : .49f),
                    swimming ? side*.32f*Mathf.Cos(phase) : 0,
                    side*(swimming ? .43f+.19f*side*swing : sprinting ? .18f : .04f));
        }
    }

    public static void MovePose(Node3D art,float phase,bool moving,bool water,bool swims,bool sprinting,bool airborne,bool flying)
    {
        CloseEyes(art,false);
        bool struggling=water && !swims;
        if (art is ImportedChao model && model.eyesSquint!=struggling)
        {
            model.eyesSquint=struggling;
            model.RefreshEyes();
        }
        float pitch=water ? (swims ? 85 : 12+Mathf.Sin(phase*2.6f)*11) : sprinting ? 30 : airborne ? 20 : 0;
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.RotationDegrees=new(water ? -pitch+12 : 0,0,0);
        Gait(art,phase,moving || water,water,sprinting,struggling);
        if (flying)
            foreach (var (suffix,side) in new[]{("L",-1),("R",1)})
                if (art.FindChild("Paddle"+suffix,true,false) is Node3D paddle)
                    paddle.Rotation=new(-.24f,0,side*(.90f+Mathf.Sin(phase)*.16f));
        art.RotationDegrees=new(pitch,0,struggling ? Mathf.Sin(phase*2.3f)*15 : 0);
        art.Position=Vector3.Up*(water ? (swims ? .19f : -.085f)+Mathf.Sin(phase*(struggling ? 2.1f : 1))*(struggling ? .032f : .012f) :
            airborne ? .025f : moving ? .008f+Mathf.Abs(Mathf.Sin(phase))*(sprinting ? .025f : .009f) : .008f);
    }

    public static void IslandNapPose(Node3D art)
    {
        Gait(art,0,false,false);
        CloseEyes(art,true);
        art.Rotation=new(.83f,0,0);
        art.Position=Vector3.Up*.027f;
        if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=new(-.20f,0,0);
        for (int side=-1;side<=1;side+=2)
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
                paw.Rotation=new(-.88f,0,side*.22f);
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

    public static void CryPose(Node3D art,float remaining)
    {
        ReceivePose(art,.88f);
        art.Rotation=new(.22f,0,.07f*Mathf.Sin(remaining*23));
        var tears=art.GetNodeOrNull<Node3D>("Tears");
        if (tears==null)
        {
            tears=new Node3D { Name="Tears" };
            art.AddChild(tears);
            foreach (int side in new[]{-1,1})
                GardenArt.Ball(tears,new(side*.052f,0,.131f),new(.019f,.038f,.019f),new("96dfff"));
        }
        tears.Visible=true;
        tears.Position=Vector3.Up*(.225f-Mathf.PosMod(remaining*-.27f,.13f));
    }

    public static void StopCrying(Node3D art) => art.GetNodeOrNull<Node3D>("Tears")?.Hide();

    public static void SetHappy(Node3D art,bool happy)
    {
        if (art.FindChild("MoodOrb",true,false) is EmotionOrb orb) orb.SetHappy(happy);
    }

    public static void PetAnimalPose(Node3D art,float remaining)
    {
        ReceivePose(art,.75f);
        if (art.FindChild("PaddleR",true,false) is Node3D paw)
            paw.Rotation=new(-1.18f+.38f*Mathf.Sin(remaining*17),-.25f,.21f);
        SetHappy(art,true);
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
        var orbColor=EmotionOrb.OrbColor;
        GardenArt.Ball(orb,Vector3.Zero,new(.062f,.062f,.062f),orbColor).MaterialOverride=GardenArt.Material(orbColor,true);
        return model;
    }
}
