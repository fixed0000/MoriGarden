using Godot;

namespace Mori;

internal sealed class FruitMeal
{
    private float elapsed;
    private readonly float[] bitesAt=new float[FruitTree.BiteCount];
    private readonly float[] animationsAt=new float[FruitTree.BiteCount];
    private int startBites;
    private int plannedBites;
    private bool fullAtStart;
    private string? giver;
    private float duration;
    private Vector3 from;
    public float Duration => duration;
    public float Remaining => Duration-elapsed;
    public bool Rejected { get; private set; }
    public bool RejectedForTaste { get; private set; }

    public void Begin(FruitTree fruit,PetState chao,Vector3 start,bool alreadyFull=false,string? givenBy=null)
    {
        elapsed=0;
        from=start;
        startBites=fruit.Bites;
        Rejected=false;
        giver=givenBy;
        RejectedForTaste=fruit.IsSpecial && fruit.Kind!=chao.FavoriteFruit;
        if (RejectedForTaste)
        {
            plannedBites=startBites;
            animationsAt[startBites]=0;
            bitesAt[startBites]=duration=chao.Fullness>=75 ? 3 : chao.Fullness<=25 || chao.Glutton ? 1 : 2;
            fullAtStart=false;
            return;
        }
        fullAtStart=!fruit.IsHeartFruit && (alreadyFull || chao.Fullness>=99.9f);
        if (fullAtStart)
        {
            plannedBites=startBites+(startBites<FruitTree.BiteCount-1 && GD.Randf()<.24f ? 1 : 0);
            animationsAt[startBites]=0;
            bitesAt[startBites]=duration=3;
            return;
        }
        bool hungry=chao.Fullness<=25;
        int quickBites=hungry && GD.Randf()<(chao.Glutton ? .65f : .32f) ? GD.RandRange(2,3) : 1;
        float time=0;
        plannedBites=bitesAt.Length;
        for (int i=startBites;i<plannedBites;i++)
        {
            float fullness=chao.Fullness+(i-startBites)*6;
            float chewing=fruit.IsHeartFruit ? .9f : fullness>=75 ? 3 : (hungry || chao.Glutton) ? 1 : 2;
            animationsAt[i]=time;
            bitesAt[i]=time+chewing;
            time=bitesAt[i]+(fruit.IsHeartFruit ? .5f : i-startBites+1<quickBites ? 0 : hungry ? 2 : 5);
        }
        duration=bitesAt[^1];
    }

    public void Advance(float dt,FruitTree fruit,PetState chao,Node3D art,Vector3 mouth)
    {
        elapsed=Mathf.Min(Duration,elapsed+dt);
        var forward=((Node3D)art.GetParent()).GlobalBasis.Z.Normalized();
        int next=Mathf.Min(fruit.Bites,bitesAt.Length-1);
        bool pause=elapsed<animationsAt[next];
        float elapsedPause=fruit.Bites>startBites ? elapsed-bitesAt[fruit.Bites-1] : 0;
        float chewTime=Mathf.Max(0,elapsed-animationsAt[next]);
        float lean=pause ? 0 : Mathf.SmoothStep(0,.42f,chewTime);
        var bite=mouth+forward*Mathf.Lerp(.175f,.112f,lean)+Vector3.Up*Mathf.Lerp(-.106f,-.079f,lean);
        float reach=Mathf.Clamp(elapsed/.28f,0,1);
        fruit.Hold(reach>=1 ? bite : from.Lerp(bite,reach)+forward*(Mathf.Sin(reach*Mathf.Pi)*.10f));
        while (fruit.Bites<plannedBites && elapsed>=bitesAt[fruit.Bites])
        {
            bool favoriteGift=giver!=null && fruit.IsSpecial && fruit.Kind==chao.FavoriteFruit && fruit.Bites==0;
            fruit.TakeBite();
            if (favoriteGift)
            {
                chao.Bond++;
                chao.ChangeRelationship(1,giver!);
                chao.Joy=Mathf.Min(100,chao.Joy+4);
            }
            if (!fullAtStart) chao.FeedBite(fruit.Bites==FruitTree.BiteCount);
            ChaoSounds.Play(art,"bite");
            if (fruit.IsHeartFruit && fruit.Bites==FruitTree.BiteCount)
                chao.CourtshipReadiness=100;
            if (!fruit.IsHeartFruit && chao.Fullness>=100 && fruit.Bites<bitesAt.Length)
            {
                Rejected=true;
                duration=elapsed;
                break;
            }
        }
        if ((fullAtStart || RejectedForTaste) && elapsed>=Duration) Rejected=true;
        ImportedChao.EatPose(art,pause ? 0 : chewTime);
        if (art.FindChild("HeadPivot",true,false) is Node3D head)
            head.Rotation=new(0,pause ? Mathf.Sin(elapsedPause*1.75f)*.33f : 0,0);
        if (art.FindChild("MoodOrb",true,false) is EmotionOrb orb)
        {
            orb.SetMealHappy(fruit.IsSpecial && fruit.Kind==chao.FavoriteFruit && !Rejected && fruit.Bites<FruitTree.BiteCount);
            orb.SetMealUpset(pause && chao.Joy<40 && elapsedPause>.35f && elapsedPause<1.24f);
        }
    }

    public void ThrowBehind(FruitTree fruit,CharacterBody3D chao,Node3D art)
    {
        ImportedChao.DiscardPose(art);
        var back=-chao.GlobalBasis.Z.Normalized();
        fruit.Hold(chao.Position+back*.24f+Vector3.Up*.38f);
        fruit.Release(back*2.8f+Vector3.Up*2.25f);
        if (RejectedForTaste)
            art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.ReactUpset(1);
    }
}
