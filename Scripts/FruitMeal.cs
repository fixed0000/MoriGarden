using Godot;

namespace Mori;

internal sealed class FruitMeal
{
    private float elapsed, interval;
    private int startBites;
    private Vector3 from;
    public float Duration => .28f+(FruitTree.BiteCount-startBites)*interval;
    public float Remaining => Duration-elapsed;

    public void Begin(FruitTree fruit,PetState chao,Vector3 start)
    {
        elapsed=0;
        from=start;
        startBites=fruit.Bites;
        interval=Mathf.Lerp(.89f,1.55f,chao.Fullness/100);
        if (chao.Glutton) interval*=.78f;
    }

    public void Advance(float dt,FruitTree fruit,PetState chao,Node3D art,Vector3 mouth)
    {
        elapsed=Mathf.Min(Duration,elapsed+dt);
        float chewing=Mathf.Max(0,elapsed-.28f);
        float beat=Mathf.PosMod(chewing,interval)/interval;
        var forward=((Node3D)art.GetParent()).GlobalBasis.Z.Normalized();
        bool pause=fruit.Bites>startBites && beat<.66f && elapsed<Duration;
        var bite=mouth+forward*(pause ? .205f : .175f)+Vector3.Up*(pause ? -.062f : .012f);
        float reach=Mathf.Clamp(elapsed/.28f,0,1);
        fruit.Hold(reach>=1 ? bite : from.Lerp(bite,reach)+forward*(Mathf.Sin(reach*Mathf.Pi)*.10f));
        int bites=elapsed>=Duration ? FruitTree.BiteCount : startBites+Mathf.FloorToInt(chewing/interval);
        while (fruit.Bites<bites)
        {
            fruit.TakeBite();
            chao.FeedBite(fruit.Bites==FruitTree.BiteCount);
            ChaoSounds.Play(art,"bite");
        }
        ImportedChao.EatPose(art,beat*.24f);
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.Rotation=new(0,pause ? Mathf.Sin(beat*9+fruit.Bites)*.33f : 0,0);
        if (art.FindChild("MoodOrb",true,false) is EmotionOrb orb)
            orb.SetMealUpset(pause && chao.Joy<40 && beat<.41f);
    }
}
