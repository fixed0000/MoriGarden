using Godot;

namespace Mori;

public partial class EmotionOrb : Node3D
{
    private Vector3 rest;
    private Vector3 velocity;
    private Vector3 previousTarget;
    private Vector3 simulatedPosition;
    private bool initialized;

    public override void _Ready() => rest = Position;

    private MeshInstance3D? spring;
    private Label3D? question;
    private bool thinking;
    private bool upset;
    public void SetThinking(bool value)
    {
        if (value && question==null)
        {
            question=new Label3D { Text="?", FontSize=76, PixelSize=.0019f,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled, Modulate=new Color("fff1ad"),OutlineSize=10 };
            AddChild(question);
        }
        thinking=value;
        SetUpset(upset);
    }
    public void SetUpset(bool upset)
    {
        this.upset=upset;
        if (upset && spring==null)
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
            spring=GardenArt.Shape(this,mesh.Commit(),Vector3.Zero,Vector3.One,new("d58a9b"));
            ((StandardMaterial3D)spring.MaterialOverride).CullMode=BaseMaterial3D.CullModeEnum.Disabled;
        }
        foreach (var node in GetChildren())
            if (node is MeshInstance3D meshNode) meshNode.Visible=!thinking && (meshNode==spring ? upset : !upset);
        if (question!=null) question.Visible=thinking;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
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
