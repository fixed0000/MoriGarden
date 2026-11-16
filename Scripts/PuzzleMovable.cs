using System;
using Godot;

namespace Mori;

internal partial class PuzzleMovable : CharacterBody3D
{
    internal Func<PuzzleElementState> State { get; set; }=null!;
    internal Vector3 Initial { get; init; }
    internal Rect2 PlacementBounds { get; init; }=new(-100,-100,200,200);
    internal Func<float,float,float> FloorHeight { get; init; }=(_,_)=>0;
    internal string LastContact { get; private set; }="none";
    public override void _Ready()
    {
        CollisionLayer=1;CollisionMask=1;SetMeta("no_step",true);
        AddChild(new CollisionShape3D { Position=new(0,.38f,0),Shape=new BoxShape3D { Size=new(.85f,.75f,.85f) } });
        GreenhouseArt.Box(this,new(0,.38f,0),new(.8f,.75f,.8f),new("7c6747"));
        foreach(int side in new[]{-1,1})
        {
            GreenhouseArt.Box(this,new(side*.31f,.38f,.41f),new(.08f,.7f,.05f),new("4b5546"));
            GreenhouseArt.Box(this,new(.41f,.38f,side*.31f),new(.05f,.7f,.08f),new("4b5546"));
        }
        GreenhouseArt.Box(this,new(0,.46f,.425f),new(.65f,.18f,.025f),new("3b5148"));
        AddChild(new Label3D { Text="WEIGHT",Position=new(0,.46f,.445f),FontSize=22,PixelSize=.003f,Modulate=new("eee5cb"),OutlineSize=0 });
    }
    internal void Restore()
    {
        var state=State();Position=state.Positioned && PlacementBounds.HasPoint(new(state.X,state.Z)) ? new(state.X,FloorHeight(state.X,state.Z)+.01f,state.Z) : Initial;
        ResetPhysicsInterpolation();Capture();
    }
    internal bool Push(CharacterBody3D actor,Vector3 displacement)
    {
        displacement.Y=0;
        var delta=Position-actor.Position;delta.Y=0;
        if(Mathf.Abs(actor.Position.Y-Position.Y)>.55f || delta.Length()>1.05f || displacement.LengthSquared()<.00001f || delta.Normalized().Dot(displacement.Normalized())<.25f)return false;
        var hit=MoveAndCollide(displacement*.65f);
        if(hit!=null)LastContact=$"{(hit.GetCollider() as Node)?.GetPath()} {hit.GetNormal()}";
        if(hit!=null && hit.GetNormal().Y>.6f)MoveAndCollide(hit.GetRemainder().Slide(hit.GetNormal()));
        MoveAndCollide(Vector3.Down*.08f);Capture();return true;
    }
    internal void Capture(){var state=State();state.X=Position.X;state.Z=Position.Z;state.Positioned=true;}
}
