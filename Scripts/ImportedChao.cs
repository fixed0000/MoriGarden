using Godot;

namespace Mori;

public partial class ImportedChao : Node3D
{
    private readonly System.Collections.Generic.Dictionary<int,float> sleepOffsets=new();
    private Node3D? softBody;
    private Vector3 lastPosition, lastVelocity, softness, softnessVelocity;
    private bool tracked, alignSoles;
    private float discardTime, afterAction;
    internal static bool Pausing(Node3D art) => art is ImportedChao model && model.afterAction>0;
    public static void PauseAfterAction(Node3D art) { if (art is ImportedChao model) model.afterAction=2; }
    public static void ClearActionPause(Node3D art) { if (art is ImportedChao model) model.afterAction=0; }
    public static bool StepActionPause(Node3D art,float dt)
    {
        if (art is not ImportedChao model || model.afterAction<=0) return false;
        model.afterAction=Mathf.Max(0,model.afterAction-dt);
        MovePose(art,0,false,false,false,false,false,false); return true;
    }
    internal float LowestSole()
    {
        float bottom=float.MaxValue;
        foreach(var (mesh,vertices) in soleVertices)
            foreach(var vertex in vertices) bottom=Mathf.Min(bottom,(mesh.GlobalTransform*vertex).Y);
        return bottom;
    }
    private readonly System.Collections.Generic.Dictionary<MeshInstance3D,Vector3[]> soleVertices=new();
    public static void DiscardPose(Node3D art) { if (art is ImportedChao model) model.discardTime=.55f; }
    private Vector3 development=Vector3.One;
    public static void ApplyDevelopment(Node3D art,PetState state)
    {
        if (art is not ImportedChao model) return;
        float progress=state.Form=="seed" ? 0 : state.SecondEvolutionProgress;
        model.development=new(1+progress*(state.Form=="stone" ? .12f : .025f),
            1+progress*(state.Form=="comet" ? .10f : .035f),1+progress*.025f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (softBody==null || GetParent() is not Node3D actor) return;
        float dt=(float)delta;
        if (dt<=0) return;
        Vector3 at=actor.GlobalPosition;
        Vector3 shift=at-lastPosition;
        Vector3 velocity=tracked && shift.Length()<.5f ? shift/dt : Vector3.Zero;
        softnessVelocity+=actor.GlobalBasis.Inverse()*(lastVelocity-velocity).LimitLength(4)*.34f;
        softnessVelocity+=(-softness*75-softnessVelocity*13)*dt;
        softness=(softness+softnessVelocity*dt).LimitLength(.13f);
        softBody.Rotation=new(softness.Z,0,-softness.X);
        float stretch=Mathf.Clamp(softness.Y*.6f,-.065f,.065f);
        softBody.Scale=new Vector3(1-stretch*.45f,1+stretch,1-stretch*.45f)*development;
        lastPosition=at; lastVelocity=velocity; tracked=true;
        if (discardTime>0)
        {
            discardTime=Mathf.Max(0,discardTime-dt);
            if (FindChild("PaddleR",true,false) is Node3D paw)
                paw.Rotation=new(-2.9f+(.55f-discardTime)*6,0,.20f);
        }
        if (alignSoles && Mathf.Abs(Rotation.X)<.6f)
        {
            if (soleVertices.Count==0)
                foreach (string name in new[]{"FootL","FootR"})
                {
                    if (FindChild(name,true,false) is not Node3D foot) continue;
                    var meshes=new System.Collections.Generic.List<MeshInstance3D>();
                    if (foot is MeshInstance3D own) meshes.Add(own);
                    foreach (var child in foot.FindChildren("*","MeshInstance3D",true,false)) meshes.Add((MeshInstance3D)child);
                    foreach (var mesh in meshes)
                    {
                        var vertices=new System.Collections.Generic.List<Vector3>();
                        for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                            vertices.AddRange(mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array());
                        soleVertices[mesh]=vertices.ToArray();
                    }
                }
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.30f,at-Vector3.Up*.16f,1));
            if (hit.Count>0 && ((Vector3)hit["normal"]).Y>.70f)
            {
                float bottom=float.MaxValue;
                foreach(var (mesh,vertices) in soleVertices)
                    foreach(var vertex in vertices) bottom=Mathf.Min(bottom,(mesh.GlobalTransform*vertex).Y);
                if (bottom<float.MaxValue)
                    GlobalPosition+=Vector3.Up*Mathf.Clamp(((Vector3)hit["position"]).Y+.003f-bottom,-.12f,.12f);
            }
        }
    }
    private readonly System.Collections.Generic.Dictionary<Node3D,Vector3> feet=new();
    private readonly System.Collections.Generic.Dictionary<Node3D,(Vector3 Position,Vector3 Scale)> paddleRest=new();
    private readonly System.Collections.Generic.Dictionary<Node3D,(Node3D Sleep,Node3D Happy,Node3D Squint)> eyeShapes=new();
    private readonly System.Collections.Generic.Dictionary<MeshInstance3D,(Material? Original,ShaderMaterial Half)> lids=new();
    private static readonly Shader LidShader=new() { Code="""
        shader_type spatial;
        render_mode cull_disabled;
        uniform sampler2D eye_texture : source_color, hint_default_white;
        uniform vec4 eye_color : source_color = vec4(1.0);
        uniform float midpoint;
        uniform float slant=0.0;
        uniform float eye_mid_x=0.0;
        varying float eye_x;
        varying float eye_y;
        void vertex() { eye_y=VERTEX.y; eye_x=VERTEX.x; }
        void fragment() {
            if (eye_y>midpoint+slant*(eye_x-eye_mid_x)) discard;
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
        lid.SetShaderParameter("eye_mid_x",eye.GetAabb().GetCenter().X);
        lids.Add(eye,(eye.MaterialOverride,lid));
    }

    private static Node3D EyeLine(Node3D parent,Vector3 center,bool happy,int squint=0)
    {
        var line=new Node3D { Position=center };
        parent.AddChild(line);
        var ink=new Color("655649");
        if (happy && squint==0)
        {
            var triangles=new System.Collections.Generic.List<(Vector3 A,Vector3 B,Vector3 C)>();
            foreach(var node in parent.FindChildren("Head_*","MeshInstance3D",true,false))
            {
                var mesh=(MeshInstance3D)node;
                var relative=parent.GlobalTransform.AffineInverse()*mesh.GlobalTransform;
                for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                {
                    var arrays=mesh.Mesh.SurfaceGetArrays(surface);
                    var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                    for(int i=0;i+2<indices.Length;i+=3)
                        triangles.Add((relative*vertices[indices[i]],relative*vertices[indices[i+1]],relative*vertices[indices[i+2]]));
                }
            }
            Vector3 Surface(Vector2 point)
            {
                float front=float.NegativeInfinity;
                foreach(var (v0,v1,v2) in triangles)
                {
                    float d=(v1.Y-v2.Y)*(v0.X-v2.X)+(v2.X-v1.X)*(v0.Y-v2.Y);
                    if (Mathf.Abs(d)<.00000001f) continue;
                    float u=((v1.Y-v2.Y)*(point.X-v2.X)+(v2.X-v1.X)*(point.Y-v2.Y))/d;
                    float v=((v2.Y-v0.Y)*(point.X-v2.X)+(v0.X-v2.X)*(point.Y-v2.Y))/d;
                    float w=1-u-v;
                    if (Mathf.Min(u,Mathf.Min(v,w))>=-.0001f) front=Mathf.Max(front,u*v0.Z+v*v1.Z+w*v2.Z);
                }
                return new(point.X-center.X,point.Y-center.Y,(float.IsNegativeInfinity(front) ? center.Z-.005f : front+.0008f)-center.Z);
            }
            var ribbon=new SurfaceTool(); ribbon.Begin(Mesh.PrimitiveType.Triangles);
            var points=new[]{new Vector2(center.X-.026f,center.Y-.012f),new Vector2(center.X,center.Y+.014f),new Vector2(center.X+.026f,center.Y-.012f)};
            for(int segment=0;segment<2;segment++) for(int sample=0;sample<12;sample++)
            {
                var p0=points[segment].Lerp(points[segment+1],sample/12f);
                var p1=points[segment].Lerp(points[segment+1],(sample+1)/12f);
                var across=(p1-p0).Orthogonal().Normalized()*.0018f;
                foreach(var point in new[]{p0-across,p1-across,p1+across,p0-across,p1+across,p0+across}) ribbon.AddVertex(Surface(point));
            }
            ribbon.GenerateNormals();
            line.AddChild(new MeshInstance3D { Name="FlatSmile",Mesh=ribbon.Commit(),MaterialOverride=new StandardMaterial3D {
                AlbedoColor=ink,CullMode=BaseMaterial3D.CullModeEnum.Disabled,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded } });
            return line;
        }

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

    public static void SleepPose(Node3D art,float elapsed=0,int manners=50)
    {
        if (art is ImportedChao modelPose) modelPose.alignSoles=false;
        if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=Vector3.Zero;
        float phase=Mathf.PosMod(elapsed,33);
        float back=Mathf.SmoothStep(0,1,Mathf.Clamp((phase-6)/1.4f,0,1));
        float otherSide=Mathf.SmoothStep(0,1,Mathf.Clamp((phase-18)/1.5f,0,1));
        float repeat=Mathf.SmoothStep(0,1,Mathf.Clamp((phase-30)/2.9f,0,1));
        float roll=Mathf.Lerp(Mathf.Lerp(88*back,-88,otherSide),0,repeat);
        Gait(art,0,false,false);
        CloseEyes(art,true);
        var scale=art.Scale;
        Basis LyingBasis(float angle) => new Basis(Vector3.Back,Mathf.DegToRad(angle))*
            new Basis(Vector3.Right,-Mathf.Pi/2);
        art.Basis=LyingBasis(roll).Scaled(scale);
        if (art is ImportedChao model)
        {
            int key=(int)Mathf.Round(roll/5)*5;
            if (!model.sleepOffsets.TryGetValue(key,out float offset))
            {
                float bottom=0;
                var inverse=art.GlobalTransform.AffineInverse();
                var pose=LyingBasis(key).Scaled(scale);
                foreach (var node in art.GetChild(0).FindChildren("*","MeshInstance3D",true,false))
                {
                    if (node is not MeshInstance3D mesh || mesh.Mesh==null) continue;
                    var relative=inverse*mesh.GlobalTransform;
                    for (int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                        foreach (var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                            bottom=Mathf.Min(bottom,(pose*(relative*vertex)).Y);
                }
                model.sleepOffsets[key]=offset=-bottom+.005f;
            }
            art.Position=Vector3.Up*offset;
        }
        float sideWeight=Mathf.Abs(roll)/88;
        foreach (var side in new[]{-1,1})
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
            {
                bool under=side*roll<0;
                var comfortable=under ? new Vector3(-1.75f,side*.35f,side*.70f) : new Vector3(-.85f,-side*.40f,side*.27f);
                paw.Rotation=new Vector3(-.23f,0,side*.10f).Lerp(comfortable,sideWeight);
            }
        if (manners>=50) return;
        float stretch=Mathf.SmoothStep(0,1,Mathf.Clamp((phase-9)/1.3f,0,1))*
            (1-Mathf.SmoothStep(0,1,Mathf.Clamp((phase-14)/1.2f,0,1)));
        float scratch=phase is >22 and <24.5f ? Mathf.Sin(Mathf.Pi*(phase-22)/2.5f) : 0;
        for (int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "L" : "R";
            if (art.FindChild("Foot"+suffix,true,false) is Node3D foot)
                foot.Rotation=new(-.28f*stretch,0,side*.57f*stretch);
            if (art.FindChild("Paddle"+suffix,true,false) is not Node3D paw) continue;
            var loose=paw.Rotation.Lerp(new Vector3(-.40f*stretch,0,side*(.06f+.88f*stretch)),stretch);
            var behindBack=new Vector3(1.35f+.25f*Mathf.Sin(phase*23),-.35f,-.82f);
            paw.Rotation=side==1 ? loose.Lerp(behindBack,scratch) : loose;
        }
    }

    public static void CloudPose(Node3D art,float elapsed,float weight=1)
    {
        if (art is ImportedChao model) model.alignSoles=false;
        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp(weight,0,1));
        Gait(art,0,false,false); CloseEyes(art,false);
        art.Quaternion=Quaternion.Identity.Slerp(new Quaternion(Vector3.Right,-1.30f),blend);
        art.Position=Vector3.Up*Mathf.Lerp(.008f,.075f,blend);
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.Rotation=new(-.23f*blend,CloudLook(elapsed)*blend,0);
        foreach (int side in new[]{-1,1})
        {
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
            {
                paw.Position+=new Vector3(side*.035f,-.035f,-.11f)*blend;
                paw.Quaternion=paw.Quaternion.Slerp(new Quaternion(Vector3.Down,art.Basis.Inverse()*Vector3.Down),blend);
                GroundRestLimb(paw,art,blend);
            }
            if (art.FindChild(side<0 ? "FootL" : "FootR",true,false) is Node3D foot)
            { foot.Position+=new Vector3(side*.014f,-.008f,-.105f)*blend; foot.Rotation=new(-.35f*blend,0,side*.10f*blend); GroundRestLimb(foot,art,blend); }
        }
    }

    private static float CloudLook(float elapsed)
    {
        float phase=Mathf.PosMod(elapsed,18);
        if (phase<3.4f) return -.46f;
        if (phase<4.5f) return Mathf.Lerp(-.46f,.42f,Mathf.SmoothStep(0,1,(phase-3.4f)/1.1f));
        if (phase<9) return .42f;
        if (phase<9.85f) return Mathf.Lerp(.42f,0,Mathf.SmoothStep(0,1,(phase-9)/.85f));
        if (phase<14.8f) return 0;
        if (phase<16.1f) return Mathf.Lerp(0,-.46f,Mathf.SmoothStep(0,1,(phase-14.8f)/1.3f));
        return -.46f;
    }

    public static void StandUpPose(Node3D art,Quaternion from,Vector3 fromHeight,float progress)
    {
        if (art is ImportedChao model) model.alignSoles=false;
        float t=Mathf.Clamp(progress,0,1);
        Gait(art,0,false,false); CloseEyes(art,false);
        float push=Mathf.SmoothStep(0,1,Mathf.Clamp((t-.28f)/.65f,0,1));
        art.Quaternion=from.Slerp(Quaternion.Identity,push);
        art.Position=fromHeight.Lerp(Vector3.Up*.008f,push);
        if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=new(.18f*(1-push),0,0);
        foreach(int side in new[]{-1,1})
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
            {
                float brace=1-Mathf.SmoothStep(.70f,.97f,t);
                paw.Position+=new Vector3(side*.055f,-.035f,-.10f)*brace;
                paw.Quaternion=paw.Quaternion.Slerp(new Quaternion(Vector3.Down,art.Basis.Inverse()*Vector3.Down),brace);
                GroundRestLimb(paw,art,brace);
            }
    }

    private static void GroundRestLimb(Node3D limb,Node3D art,float blend)
    {
        var meshes=new System.Collections.Generic.List<MeshInstance3D>();
        if (limb is MeshInstance3D own) meshes.Add(own);
        foreach(var child in limb.FindChildren("*","MeshInstance3D",true,false)) meshes.Add((MeshInstance3D)child);
        float bottom=float.MaxValue;
        foreach(var mesh in meshes)
        {
            var bounds=mesh.GetAabb();
            for(int i=0;i<8;i++) bottom=Mathf.Min(bottom,(mesh.GlobalTransform*bounds.GetEndpoint(i)).Y);
        }
        if (bottom==float.MaxValue || art.GetParent() is not Node3D actor) return;
        var floor=art.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(
            actor.GlobalPosition+Vector3.Up*.3f,actor.GlobalPosition-Vector3.Up*.2f,1));
        if (floor.Count>0) limb.GlobalPosition+=Vector3.Up*(((Vector3)floor["position"]).Y+.004f-bottom)*blend;
    }

    public static void BallHitPose(Node3D art,Vector3 ball,float progress)
    {
        float reach=Mathf.Sin(Mathf.Clamp(progress,0,1)*Mathf.Pi);
        HandshakePose(art,ball,reach);
        if (art.FindChild("PaddleR",true,false) is Node3D paw) paw.RotateX(-.3f*reach);
    }

    public static void LieDown(Node3D art,float progress,int manners=50)
    {
        float t=Mathf.SmoothStep(0,1,Mathf.Clamp(progress,0,1));
        SleepPose(art,0,manners);
        var rotation=art.Quaternion;
        var height=art.Position;
        art.Quaternion=Quaternion.Identity.Slerp(rotation,t);
        art.Position=(Vector3.Up*.034f).Lerp(height,t);
        CloseEyes(art,t>.7f);
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
            {
                if (art is ImportedChao imported)
                {
                    if (!imported.paddleRest.TryGetValue(paddle,out var restPaddle))
                        imported.paddleRest[paddle]=restPaddle=(paddle.Position,paddle.Scale);
                    paddle.Position=restPaddle.Position;
                    paddle.Scale=restPaddle.Scale;
                }
                paddle.Rotation=new(sprinting ? -.98f+side*swing*.10f : -side*swing*(swimming ? (struggling ? 1.52f : 1.02f) : .49f),
                    swimming ? side*.32f*Mathf.Cos(phase) : 0,
                    side*(swimming ? .43f+.19f*side*swing : sprinting ? .18f : .04f));
            }
        }
    }

    public static void MovePose(Node3D art,float phase,bool moving,bool water,bool swims,bool sprinting,bool airborne,bool flying)
    {
        if (art is ImportedChao feetModel) feetModel.alignSoles=!water && !airborne;
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
            airborne ? .025f : .008f);
    }

    public static void ClimbPose(Node3D art,float elapsed)
    {
        CloseEyes(art,false);
        Gait(art,elapsed*11,true,false);
        art.Position=Vector3.Up*.014f;
        art.Rotation=new(.19f,0,0);
        for (int side=-1;side<=1;side+=2)
            if (art.FindChild(side<0 ? "PaddleL" : "PaddleR",true,false) is Node3D paw)
                paw.Rotation=new(-1.62f+side*.58f*Mathf.Sin(elapsed*11),0,side*.22f);
    }

    public static void CourtshipPose(Node3D art,float rub,float jump)
    {
        CloseEyes(art,false);
        Gait(art,0,false,false);
        art.Position=Vector3.Up*(jump*.28f-.034f);
        art.Rotation=new(-.14f*(1-jump),0,0);
        if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=new(.09f,rub,0);
        foreach (var (suffix,side) in new[]{("L",-1),("R",1)})
        {
            if (art.FindChild("Foot"+suffix,true,false) is Node3D foot)
                foot.Position+=new Vector3(side*.012f,0,.040f*(1-jump));
            if (art.FindChild("Paddle"+suffix,true,false) is Node3D paw)
                paw.Rotation=new(-2.75f*jump,0,side*(.12f+.28f*jump));
        }
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

    public static void HandshakePose(Node3D art,Vector3 grip,float weight=1)
    {
        MovePose(art,0,false,false,false,false,false,false);
        if (art.FindChild("PaddleR",true,false) is not Node3D paw || paw.GetParent() is not Node3D shoulder)
            return;
        var direction=shoulder.ToLocal(grip)-paw.Position;
        float length=direction.Length();
        if (length<=.01f) return;
        var reach=direction/length;
        float advance=Mathf.Clamp(length-.106f,0,.078f);
        paw.Quaternion=paw.Quaternion.Slerp(new Quaternion(Vector3.Down,reach),weight);
        paw.Position+=reach*advance*weight;
        paw.Scale*=Vector3.One.Lerp(new Vector3(.88f,Mathf.Clamp((length-advance)/.054f,1,2.8f),.88f),weight);
    }

    public static void IdeaPose(Node3D art,float weight=1)
    {
        Gait(art,0,false,false);
        CloseEyes(art,false);
        art.Rotation=Vector3.Zero;
        art.Position=Vector3.Zero;
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.Rotation=new Vector3(-.40f,-.43f,-.10f)*weight;
        if (art.FindChild("PaddleR",true,false) is Node3D paw)
            paw.Rotation=new Vector3(-1.38f,-.44f,.47f)*weight;
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

    public static void InstrumentHands(Node3D art,Vector3 leftGrip,Vector3 rightGrip)
    {
        foreach(var (suffix,grip) in new[]{("L",leftGrip),("R",rightGrip)})
        {
            if (art.FindChild("Paddle"+suffix,true,false) is not Node3D paw || paw.GetParent() is not Node3D shoulder) continue;
            var delta=shoulder.ToLocal(grip)-paw.Position;
            float distance=delta.Length(); if (distance<.01f) continue;
            var direction=delta/distance;
            float shift=Mathf.Clamp(distance-.10f,0,.11f);
            paw.Quaternion=new Quaternion(Vector3.Down,direction);
            paw.Position+=direction*shift;
            paw.Scale*=new Vector3(.9f,Mathf.Clamp((distance-shift)/.054f,1,2.8f),.9f);
        }
    }

    public static void InstrumentPose(Node3D art,float time,bool guitar)
    {
        MovePose(art,0,false,false,false,false,false,false);
        ReceivePose(art,.85f);
        if (art.FindChild("PaddleR",true,false) is Node3D paw)
            paw.Rotation=new(-1.02f+.22f*Mathf.Sin(time*(guitar ? 27 : 11)),0,.22f);
        if (art.FindChild("PaddleL",true,false) is Node3D left)
            left.Rotation=new(-1.10f+(guitar ? 0 : .17f*Mathf.Sin(time*13)),0,-.34f);
    }

    public static void BallReaction(Node3D art,float time,bool scold,bool polite,bool crying)
    {
        MovePose(art,0,false,false,false,false,false,false);
        if (scold)
        {
            bool crossed=time>1;
            HalfEyes(art,!crossed);
            if (art is ImportedChao model)
            {
                model.eyesSquint=crossed;
                foreach (var (eye,lid) in model.lids) lid.Half.SetShaderParameter("slant",
                    model.ToLocal(eye.ToGlobal(eye.GetAabb().GetCenter())).X>0 ? .35f : -.35f);
                model.RefreshEyes();
            }
            art.Rotation=new(.20f+Mathf.Sin(time*19)*.19f,0,0);
            foreach (var side in new[]{"L","R"})
                if (art.FindChild("Paddle"+side,true,false) is Node3D hand)
                    hand.Rotation=new(.5f+.6f*Mathf.Sin(time*19),0,side=="L" ? -.28f : .28f);
        }
        else if (polite)
        {
            CloseEyes(art,true);
            art.Rotation=new(Mathf.Sin(Mathf.Clamp(time/3,0,1)*Mathf.Pi)*.75f,0,0);
            ReceivePose(art,.62f);
            foreach (var side in new[]{"L","R"})
                if (art.FindChild("Paddle"+side,true,false) is Node3D hand)
                    hand.Rotation=new(-1.06f,side=="L" ? .65f : -.65f,side=="L" ? -.42f : .42f);
            art.Rotation=new(Mathf.Sin(Mathf.Clamp(time/3,0,1)*Mathf.Pi)*.75f,0,0);
        }
        else if (crying)
        {
            CryPose(art,time);
            CloseEyes(art,true);
            art.Position=Vector3.Up*-.025f;
            art.Rotation=new(.19f,0,0);
            if (art.FindChild("PaddleR",true,false) is Node3D hand)
                hand.Rotation=new(-1.85f,.25f,.20f+.25f*Mathf.Sin(time*4));
        }
        else HalfEyes(art,true);
    }

    public static void ClearBallReaction(Node3D art)
    {
        StopCrying(art); CloseEyes(art,false); HalfEyes(art,false);
        if (art is ImportedChao model)
        {
            model.eyesSquint=false;
            foreach (var (_,lid) in model.lids) lid.Half.SetShaderParameter("slant",0f);
            model.RefreshEyes();
        }
        EndThink(art);
        MovePose(art,0,false,false,false,false,false,false);
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
        model.softBody=(Node3D)scene;
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
