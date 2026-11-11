using Godot;

namespace Mori;

internal sealed class ChaoRising
{
    private Quaternion from;
    private Vector3 height;
    private float time;
    public bool Active { get; private set; }
    public void Begin(Node3D art) { if (Active) return; from=art.Quaternion; height=art.Position; time=0; Active=true; }
    public void Cancel() { Active=false; time=0; }
    public bool Step(Node3D art,float dt)
    {
        if (!Active) return false;
        time+=dt;
        ImportedChao.StandUpPose(art,from,height,time/1.5f);
        if (time<1.5f) return true;
        Active=false;
        ImportedChao.MovePose(art,0,false,false,false,false,false,false);
        ImportedChao.PauseAfterAction(art);
        return true;
    }
}

internal sealed class ChaoTouch
{
    private Node3D? currentArt;
    private float elapsed;
    private bool upset, celebrated;
    public bool Active { get; private set; }
    public bool Held { get; set; }
    public void Begin(PetState pet,Node3D art,bool eating)
    {
        elapsed=0; upset=eating; celebrated=false; Active=true;
        currentArt=art;
        ImportedChao.ClearActionPause(art);
        if (eating)
        {
            pet.Joy=Mathf.Max(0,pet.Joy-4); pet.ChangeRelationship(-.25f); pet.Bond=System.Math.Max(0,pet.Bond-1);
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetMealHappy(false);
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetHappy(false);
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.ReactUpset(8);
        }
        else { pet.Pet(); art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.WaitForPetting(); }
    }
    public void Cancel()
    {
        Active=false; Held=false;
        if (GodotObject.IsInstanceValid(currentArt)) currentArt!.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetMealHappy(false);
        currentArt=null;
    }
    public bool Step(Node3D art,float dt)
    {
        if (!Active) return false;
        elapsed+=dt;
        if (!upset && elapsed>=2 && !celebrated)
        {
            celebrated=true;
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.Celebrate();
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetMealHappy(true);
        }
        if (elapsed>=5 || !Held && elapsed>=(upset ? .95f : 3.1f))
        {
            Active=false; Held=false;
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetMealHappy(false);
            ImportedChao.MovePose(art,0,false,false,false,false,false,false);
            ImportedChao.PauseAfterAction(art);
            return true;
        }
        ImportedChao.MovePose(art,0,false,false,false,false,false,false);
        if (!upset && elapsed>=2) art.Rotation=new(0,0,Mathf.Sin(elapsed*8)*.035f);
        return true;
    }
}
