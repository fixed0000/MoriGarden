using Godot;

namespace Mori;

public partial class EmotionOrb : Node3D
{
    public static readonly Color OrbColor=new("f3cd59");
    private Vector3 rest;
    private Vector3 velocity;
    private Vector3 previousTarget;
    private Vector3 simulatedPosition;
    private bool initialized;

    public override void _Ready() => rest = Position;

    private MeshInstance3D? spring, heart;
    private MeshInstance3D? mark;
    internal string CurrentGlyph => displayed;
    private bool thinking;
    private bool upset, distressed;
    private bool mealUpset, mealHappy, ideaJoy;
    private string? ideaSymbol;
    private float heartTime, reactionTime;
    private string symbol="?";
    private string displayed="";
    public void ReactUpset(float duration) { reactionTime=Mathf.Max(reactionTime,duration); Refresh(); }
    public void Celebrate() { heartTime=1.1f; Refresh(); }
    public void SetMealHappy(bool happy) { if (mealHappy==happy) return; mealHappy=happy; Refresh(); }
    public void SetMealUpset(bool value) { mealUpset=value; Refresh(); }
    public void SetHappy(bool value)
    {
        SetSymbol(value,"♥");
    }
    public void SetAlert(bool value)
    {
        SetSymbol(value,"!");
    }
    public void SetThinking(bool value) => SetSymbol(value,"?");

    private void SetSymbol(bool value,string glyph)
    {
        thinking=value;
        symbol=glyph;
        Refresh();
    }

    public void SetIdea(string? glyph,bool happy)
    {
        if (ideaSymbol==glyph && ideaJoy==happy) return;
        ideaSymbol=glyph;
        ideaJoy=happy;
        Refresh();
    }

    private void Refresh()
    {
        string next=distressed || reactionTime>0 ? "spring" : ideaSymbol ?? (thinking ? symbol : mealUpset ? "spring" : heartTime>0 ? "♥" : upset ? "spring" : "");
        if (GetParent() is Node3D chao)
        {
            ImportedChao.SmileEyes(chao,((ideaJoy || mealHappy) && !distressed && reactionTime<=0) || next=="♥");
            ImportedChao.HalfEyes(chao,next=="spring" && (!ideaJoy || distressed || reactionTime>0));
        }
        if (next==displayed) return;
        displayed=next;
        bool punctuation=next is "?" or "!";
        if (punctuation && mark==null) BuildMark();
        if (next=="spring" && spring==null) BuildSpring();
        if (next=="♥" && heart==null) BuildHeart();
        foreach (var node in GetChildren())
            if (node is MeshInstance3D mesh)
                mesh.Visible=mesh==spring ? next=="spring" : mesh==heart ? next=="♥" :
                    mesh==mark ? punctuation : next.Length==0 || punctuation;
        if (punctuation)
            ((ShaderMaterial)mark!.MaterialOverride).SetShaderParameter("question",next=="?");
        if (next.Length>0) ChaoSounds.Play(this,"mood");
    }
    public void SetDistressed(bool value)
    {
        if (distressed==value) return;
        distressed=value;
        Refresh();
    }

    public void SetUpset(bool upset)
    {
        this.upset=upset;
        Refresh();
    }

    private void BuildMark()
    {
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never;
            uniform bool question=true;
            uniform vec4 ink : source_color;
            void vertex() {
                MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],
                    INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
            }
            float segment(vec2 p,vec2 a,vec2 b) {
                vec2 v=b-a;
                return length(p-a-v*clamp(dot(p-a,v)/dot(v,v),0.0,1.0));
            }
            void fragment() {
                vec2 p=vec2(UV.x,1.0-UV.y);
                float d=segment(p,vec2(.5,.16),vec2(.5,.85));
                if (question) {
                    d=segment(p,vec2(.27,.72),vec2(.36,.88));
                    d=min(d,segment(p,vec2(.36,.88),vec2(.59,.91)));
                    d=min(d,segment(p,vec2(.59,.91),vec2(.76,.80)));
                    d=min(d,segment(p,vec2(.76,.80),vec2(.77,.64)));
                    d=min(d,segment(p,vec2(.77,.64),vec2(.65,.52)));
                    d=min(d,segment(p,vec2(.65,.52),vec2(.51,.42)));
                    d=min(d,segment(p,vec2(.51,.42),vec2(.49,.20)));
                }
                ALBEDO=ink.rgb;
                ALPHA=1.0-smoothstep(.085,.101,d);
            }
            """ } };
        material.SetShaderParameter("ink",OrbColor);
        mark=new MeshInstance3D { Mesh=new QuadMesh { Size=new(.20f,.185f) },Position=Vector3.Up*.138f,
            MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mark);
    }

    private void BuildHeart()
    {
        var mesh=new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Edge(int step)
        {
            float t=step*Mathf.Tau/48;
            float x=Mathf.Sin(t);
            float y=13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t);
            return new Vector3(16*x*x*x*.0068f,(y+2)*.0088f,0);
        }
        for (int step=0;step<48;step++)
        {
            mesh.AddVertex(Vector3.Zero);
            mesh.AddVertex(Edge(step));
            mesh.AddVertex(Edge(step+1));
        }
        heart=new MeshInstance3D { Name="Heart", Position=Vector3.Up*.103f, Mesh=mesh.Commit(),
            MaterialOverride=new StandardMaterial3D
            {
                AlbedoColor=OrbColor, BillboardMode=BaseMaterial3D.BillboardModeEnum.Enabled,
                CullMode=BaseMaterial3D.CullModeEnum.Disabled, ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded
            } };
        AddChild(heart);
    }

    private void BuildSpring()
    {
        {
            var mesh=new SurfaceTool();
            mesh.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 Point(int i,int k)
            {
                float t=i/48f;
                float a=t*Mathf.Tau*3;
                float b=k*Mathf.Tau/6;
                float radius=.047f+.0075f*Mathf.Cos(b);
                return new(radius*Mathf.Cos(a),-.052f+t*.104f+.0075f*Mathf.Sin(b),radius*Mathf.Sin(a));
            }
            for(int i=0;i<48;i++)
                for(int k=0;k<6;k++)
                {
                    var a=Point(i,k); var b=Point(i+1,k); var c=Point(i,k+1); var d=Point(i+1,k+1);
                    mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
                    mesh.AddVertex(c); mesh.AddVertex(b); mesh.AddVertex(d);
                }
            mesh.GenerateNormals();
            spring=GardenArt.Shape(this,mesh.Commit(),Vector3.Up*.10f,new(1.36f,2.18f,1.36f),OrbColor);
            ((StandardMaterial3D)spring.MaterialOverride).CullMode=BaseMaterial3D.CullModeEnum.Disabled;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (reactionTime>0) { reactionTime=Mathf.Max(0,reactionTime-dt); if (reactionTime==0) Refresh(); }
        if (heartTime>0) { heartTime=Mathf.Max(0,heartTime-dt); if (heartTime==0) Refresh(); }
        Vector3 target = ((Node3D)GetParent()).ToGlobal(rest);
        if (!initialized || previousTarget.DistanceTo(target) > .75f)
        {
            initialized = true;
            simulatedPosition = target;
            velocity = Vector3.Zero;
        }
        previousTarget = target;
        velocity += ((target-simulatedPosition)*112 - velocity*13)*dt;
        Vector3 offset = simulatedPosition+velocity*dt-target;
        simulatedPosition = target+offset.LimitLength(.19f);
        GlobalPosition = simulatedPosition;
    }
}
