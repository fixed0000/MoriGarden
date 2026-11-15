using Godot;

namespace Mori;

internal sealed class ChaoGreeting
{
    private readonly ChaoRising rise=new();
    private float age;
    public void Begin(Node3D art)
    {
        age=0; rise.Cancel();
        if (art.Basis.Y.Normalized().Dot(Vector3.Up)<.8f) rise.Begin(art);
        else ImportedChao.StandForAction(art);
    }
    public bool Step(CharacterBody3D actor,Node3D art,Vector3 player,float dt)
    {
        age+=dt;
        if (rise.Active) rise.Step(art,dt);
        else ImportedChao.MovePose(art,0,false,false,false,false,false,false);
        var delta=player-actor.Position;
        float target=Mathf.Atan2(delta.X,delta.Z);
        actor.Rotation=new(0,Mathf.RotateToward(actor.Rotation.Y,target,dt*2.6f),0);
        if (rise.Active || age<.30f || Mathf.Abs(Mathf.AngleDifference(actor.Rotation.Y,target))>.06f) return false;
        ImportedChao.ClearActionPause(art);
        return true;
    }
}
