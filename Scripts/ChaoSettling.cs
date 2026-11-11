using Godot;

namespace Mori;

internal static class ChaoThought
{
    public const float Minimum=4;
    public const float IdeaEnd=Minimum+2.7f;
    public static string Glyph(float elapsed) => elapsed<Minimum ? "?" : elapsed<Minimum+.5f ? "" : "!";
    public static bool Inspired(float elapsed) => elapsed>=Minimum+.5f;
    public static float WithIntelligence(PetState chao) => Minimum+Mathf.Clamp((240-chao.Intelligence)/55f,0,3);
}

internal sealed class ChaoSettling
{
    private float time,from,to;
    public bool Active { get; private set; }
    public void Begin(CharacterBody3D actor)
    {
        Active=true; time=0; from=actor.Rotation.Y;
        to=from+Mathf.Lerp(-Mathf.Pi,Mathf.Pi,GD.Randf());
    }
    public void Cancel() { Active=false; time=0; }
    public bool Step(CharacterBody3D actor,Node3D? art,int manners,float dt)
    {
        if (!Active) Begin(actor);
        time+=dt;
        actor.Rotation=new(0,Mathf.LerpAngle(from,to,Mathf.SmoothStep(0,1,Mathf.Clamp(time/.9f,0,1))),0);
        if (art!=null)
        {
            if (time<2.2f)
            {
                ImportedChao.MovePose(art,time*8,time<.9f,false,false,false,false,false);
                if (art.FindChild("HeadPivot",true,false) is Node3D head)
                    head.Rotation=new(.45f,time<.9f ? 0 : Mathf.Sin((time-.9f)/1.3f*Mathf.Tau)*.55f,0);
            }
            else ImportedChao.LieDown(art,(time-2.2f)/1.3f,manners);
        }
        if (time<3.5f) return false;
        Active=false; return true;
    }
}
