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
    private Label3D? question;
    private bool thinking;
    private bool upset, distressed;
    private bool mealUpset;
    private float heartTime;
    private string symbol="?";
    private string displayed="";
    public void Celebrate() { heartTime=1.1f; Refresh(); }
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

    private void Refresh()
    {
        string next=distressed ? "spring" : thinking ? symbol : mealUpset ? "spring" : heartTime>0 ? "♥" : upset ? "spring" : "";
        if (next==displayed) return;
        displayed=next;
        if (GetParent() is Node3D chao)
        {
            ImportedChao.SmileEyes(chao,next=="♥");
            ImportedChao.HalfEyes(chao,next=="spring");
        }
        if (next.Length>0 && next is not ("spring" or "♥") && question==null)
        {
            question=new Label3D { Text="?", FontSize=76, PixelSize=.0024f,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled, Modulate=OrbColor,OutlineSize=10 };
            AddChild(question);
        }
        if (next=="spring" && spring==null) BuildSpring();
        if (next=="♥" && heart==null) BuildHeart();
        foreach (var node in GetChildren())
            if (node is MeshInstance3D mesh) mesh.Visible=mesh==spring ? next=="spring" : mesh==heart ? next=="♥" : next.Length==0;
        if (question!=null)
        {
            question.Visible=next.Length>0 && next is not ("spring" or "♥");
            if (question.Visible) question.Text=next;
        }
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

    private void BuildHeart()
    {
        var mesh=new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Edge(int step)
        {
            float t=step*Mathf.Tau/48;
            float x=Mathf.Sin(t);
            float y=13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t);
            return new Vector3(16*x*x*x,y+2,0)*.0049f;
        }
        for (int step=0;step<48;step++)
        {
            mesh.AddVertex(Vector3.Zero);
            mesh.AddVertex(Edge(step));
            mesh.AddVertex(Edge(step+1));
        }
        heart=new MeshInstance3D { Name="Heart", Mesh=mesh.Commit(),
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
            spring=GardenArt.Shape(this,mesh.Commit(),Vector3.Zero,Vector3.One*1.24f,OrbColor);
            ((StandardMaterial3D)spring.MaterialOverride).CullMode=BaseMaterial3D.CullModeEnum.Disabled;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
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
