using System;

namespace Mori;

// Game rules independent of graphics and input.
public sealed partial class PetState
{
    public GardenSaveLocation? ManualLocation { get; set; }
    public ExpeditionProgress Expedition { get; set; }=new();
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Mori";
    public float Fullness { get; set; } = 72;
    public float Joy { get; set; } = 65;
    public float Energy { get; set; } = 90;
    public int Bond { get; set; }
    public const string CurrentHero = "player";
    public System.Collections.Generic.Dictionary<string,float> CharacterRelationships { get; set; } = new();

    public float RelationshipWith(string character = CurrentHero) =>
        CharacterRelationships.TryGetValue(character,out float value) ? value : 0;

    public void ChangeRelationship(float amount,string character = CurrentHero)
    {
        CharacterRelationships[character]=Math.Clamp(RelationshipWith(character)+amount,-100,100);
        if(Math.Abs(amount)<.2f || History.LastArea.Length==0)return;
        History.Record(amount>0 ? ChaoMemoryKind.Pleasant : ChaoMemoryKind.Unpleasant,"player-care",History.LastArea,
            amount>0 ? "Enjoyed spending time with you." : "Had an upsetting moment with you.",amount>0 ? .6f : -.6f,.45f,new[]{character});
    }

    public const int CallBondThreshold = 6;
    public bool RespondsToCall => Bond >= CallBondThreshold;
    public System.Collections.Generic.List<ChaoRelationship> SocialLinks { get; set; } = new();

    public ChaoRelationship? SocialLink(string first,string second,bool create=false)
    {
        if (first==second) return null;
        var link=SocialLinks.Find(entry=>entry.Matches(first,second));
        if (link==null && create)
        {
            link=new ChaoRelationship { First=first, Second=second };
            SocialLinks.Add(link);
        }
        return link;
    }

    public int Care { get; set; }
    public int Agility { get; set; }
    public int Stars { get; set; }
    public ChaoMemory History { get; set; }=new();
    public ChaoAthletics Athletics { get; set; }=new();
    public StadiumProgress Stadium { get; set; }=new();
    public WildlifeProgress Wildlife { get; set; }=new();
    public int BagFruit { get; set; }
    public System.Collections.Generic.Dictionary<string,int> SpecialFruitBag { get; set; } = new();
    public System.Collections.Generic.Dictionary<string,int> SeedBag { get; set; } = new();
    public System.Collections.Generic.List<GardenTreeState>? GardenTrees { get; set; }
    public int BagHeartFruit { get; set; }
    public float CourtshipReadiness { get; set; }
    public float BloomRemaining { get; set; }
    public System.Collections.Generic.List<GardenEggState> GardenEggs { get; set; } = new()
    {
        new GardenEggState { Color="White", X=-2, Z=6 }
    };
    public int BestScore { get; set; }
    public double GardenSeconds { get; set; }
    public double AgeSeconds { get; set; }
    public const int SecondsPerChaoYear = 192 * 60;
    public int AgeYears => (int)Math.Min(int.MaxValue,AgeSeconds/SecondsPerChaoYear);
    public bool LikesToyCars { get; set; } = true;
    public string FavoriteToy { get; set; } = "";
    public string FavoriteFruit { get; set; } = FruitKinds.NewFavorite();
    public string FavoriteInstrument { get; set; } = "Flute";

    public string PreferredToy => string.IsNullOrEmpty(FavoriteToy) ? (LikesToyCars ? "Toy Car" : "Rattle") : FavoriteToy;
    public bool EnjoysToyCars => LikesToyCars || FavoriteToy=="Toy Car";
    public bool Glutton { get; set; }
    public bool Crybaby { get; set; }
    public float Fun { get; set; } = 65;
    public string Form { get; set; } = "seed";

    public int Sociability { get; set; } = 50;
    public int ActivityPreference { get; set; } = 3;
    public bool Clumsy { get; set; }
    public bool Dexterous { get; set; }
    public static float BallMissChance(PetState p) => Math.Clamp(.12f+(p.Clumsy ? .40f : 0)+(3-p.ActivityPreference)*.065f-(p.Dexterous ? .09f : 0),.025f,.85f);
    public int Manners { get; set; } = 50;
    public int Kindness { get; set; }
    public int Aggressiveness { get; set; }
    public int Curiosity { get; set; }
    public int Initiative { get; set; }
    public bool FindsFood => IntelligenceAbility>=80 && Initiative>=65;

    public bool IsEgg { get; set; }
    public float EggSeconds { get; set; }
    public float ChildSeconds { get; set; }
    public float CocoonSeconds { get; set; }
    // Active garden time; food contributes equivalent development time.
    public const float FirstEvolutionSeconds=3*60*60;
    public const float SecondEvolutionSeconds=12*60*60;
    public const float FullSecondEvolutionSeconds=14.5f*60*60;
    public float AdultSeconds { get; set; }
    public float EvolutionFoodSeconds { get; set; }
    public float SecondEvolutionProgress => Math.Clamp((AdultSeconds+EvolutionFoodSeconds)/SecondEvolutionSeconds,0,1.2f);
    public string? RecentAnimalSkill { get; set; }

    public bool InCocoon => CocoonSeconds > 0;
    public bool CanAct => !IsEgg && !InCocoon;
    public string LifeStage => IsEgg ? "Egg" : InCocoon ? "Cocoon" : Form == "seed" ? "Child" : SecondEvolutionProgress>=1 ? "Second evolution" : "Adult";

    public static PetState NewEgg() { var pet=new PetState { IsEgg=true,EggSeconds=28 };pet.InitializeFreshStatistics();return pet; }

    public bool Hatch()
    {
        if (!IsEgg) return false;
        IsEgg = false;
        EggSeconds = 0;
        return true;
    }

    public bool AdvanceLife(float seconds)
    {
        if (IsEgg)
        {
            EggSeconds = Math.Max(0,EggSeconds-seconds);
            return EggSeconds == 0 && Hatch();
        }
        if (InCocoon)
        {
            CocoonSeconds = Math.Max(0,CocoonSeconds-seconds);
            return CocoonSeconds == 0 && Evolve();
        }
        if (Form != "seed")
        {
            bool developed=SecondEvolutionProgress>=1;
            AdultSeconds=Math.Min(FullSecondEvolutionSeconds,AdultSeconds+seconds);
            return !developed && SecondEvolutionProgress>=1;
        }
        ChildSeconds = Math.Min(FirstEvolutionSeconds,ChildSeconds+seconds);
        if (ChildSeconds+EvolutionFoodSeconds < FirstEvolutionSeconds) return false;
        EvolutionFoodSeconds=0;
        ChildSeconds=FirstEvolutionSeconds;
        CocoonSeconds = 9;
        return true;
    }

    public int Growth => Math.Min(100, Care * 10 + Agility * 10);

    public void Tick(float seconds, bool sleeping)
    {
        History.Advance(seconds);
        Fullness = Math.Clamp(Fullness - seconds * .035f, 0, 100);
        Joy = Math.Clamp(Joy - seconds * .018f, 0, 100);
        Energy = Math.Clamp(Energy + seconds * (sleeping ? 5 : -.045f/(1+StaminaAbility*.002f)), 0, 100);
        if (CanAct) CourtshipReadiness=Math.Min(100,CourtshipReadiness+seconds/72);
        if (!sleeping) Fun=Math.Max(0,Fun-seconds*.07f);
    }

    public bool Feed()
    {
        if (!CanAct || Fullness >= 100) return false;
        Fullness = Math.Min(100, Fullness + 24);
        Care++;
        Bond++;
        TrainProgress("Stamina",40);
        return true;
    }

    public void FeedBite(bool last,string kind="Garden Fruit")
    {
        EvolutionFoodSeconds+=Form=="seed" ? 52.5f : 54f;
        Fullness=Math.Min(100,Fullness+6);
        TrainProgress("Stamina",FruitTraining.Progress(kind,last));
        if (!last) return;
        Care++;
        Bond++;

    }

    public void Pet()
    {
        if (!CanAct) return;
        ChangeRelationship(.38f);
        Joy = Math.Min(100, Joy + 12);
        Bond++;
        Care++;
    }

    public void TrainWithAnimal(string skill)
    {
        if (!CanAct) return;
        TrainWithSpecies(skill switch { "Swim"=>"penguin","Fly"=>"songbird","Run"=>"rabbit","Power"=>"bear-cub",_=>throw new ArgumentException("Unknown animal skill") });
    }

    public void CompleteRace(int score)
    {
        score = Math.Clamp(score, 0, 8);
        Stars += score;
        TrainProgress("Run",score);
        BestScore = Math.Max(BestScore, score);
        Agility += score >= 6 ? 3 : score >= 3 ? 2 : score > 0 ? 1 : 0;
        Joy = Math.Min(100, Joy + score * 2);
        Energy = Math.Max(0, Energy - 10);
    }

    public bool Evolve()
    {
        if (Form != "seed" || Growth<100 && ChildSeconds<FirstEvolutionSeconds) return false;
        EnsureStatistics();
        Form = RecentAnimalSkill switch
        {
            "Swim" => "tide",
            "Fly" => "comet",
            "Run" => "swift",
            "Power" => "stone",
            _ => Agility > Care ? "comet" : "bloom"
        };
        Statistics!.Promote(Form switch { "tide"=>"Swim","comet"=>"Fly","swift"=>"Run","stone"=>"Power",_=>"Stamina" });
        return true;
    }

    public void EnsureFavoriteFruit()
    {
        if (!FruitKinds.IsSpecial(FavoriteFruit)) FavoriteFruit=FruitKinds.NewFavorite();
    }

    public int FruitCount(string kind) => kind==FruitKinds.Garden ? BagFruit : kind==FruitKinds.Heart ? BagHeartFruit :
        SpecialFruitBag.TryGetValue(kind,out int count) ? count : 0;

    public void AddFruit(string kind,int amount)
    {
        if (kind==FruitKinds.Garden) BagFruit=Math.Clamp(BagFruit+amount,0,12);
        else if (kind==FruitKinds.Heart) BagHeartFruit=Math.Clamp(BagHeartFruit+amount,0,12);
        else if (FruitKinds.CanBag(kind)) SpecialFruitBag[kind]=Math.Clamp(FruitCount(kind)+amount,0,12);
    }

    public int SeedCount(string kind) => SeedBag.TryGetValue(kind,out int count) ? count : 0;

    public void Validate()
    {
        Expedition??=new(); Expedition.Validate();
        if (Version != 1 || !float.IsFinite(Fullness) || !float.IsFinite(Joy) || !float.IsFinite(Energy)
            || !double.IsFinite(AgeSeconds) || AgeSeconds<0 || !double.IsFinite(GardenSeconds) || GardenSeconds<0 || !float.IsFinite(Fun)
            || Care < 0 || Agility < 0 || Care > 100000 || Agility > 100000 || Bond < 0 || Stars < 0
            || BestScore < 0 || BestScore > 8 || Form is not ("seed" or "comet" or "bloom" or "tide" or "swift" or "stone"))
            throw new ArgumentException("Unsupported or corrupt save");
        if (!float.IsFinite(EggSeconds) || !float.IsFinite(ChildSeconds) || !float.IsFinite(CocoonSeconds)
            || EggSeconds < 0 || ChildSeconds < 0 || CocoonSeconds < 0
            || (Form != "seed" && (IsEgg || InCocoon)) || (IsEgg && InCocoon)
            || RecentAnimalSkill is not (null or "Swim" or "Fly" or "Run" or "Power"))
            throw new ArgumentException("Unsupported or corrupt life stage");
        Name = string.IsNullOrWhiteSpace(Name) ? "Mori" : Name.Trim();
        if (Name.Length>18) Name=Name[..18];
        FavoriteToy=FavoriteToy is "Toy Car" or "Rattle" or "Beach Ball" ? FavoriteToy : "";
        EnsureFavoriteFruit();
        FavoriteInstrument=FavoriteInstrument is "None" or "Drums" or "Electric Guitar" or "Synthesizer" ? FavoriteInstrument : "Flute";
        ActivityPreference=Math.Clamp(ActivityPreference,1,5);
        if (!float.IsFinite(AdultSeconds) || AdultSeconds<0 || !float.IsFinite(EvolutionFoodSeconds) || EvolutionFoodSeconds<0) throw new ArgumentException("Invalid evolution time");
        History ??=new();History.Validate();
        Athletics ??=new();Athletics.Validate();Stadium ??=new();Stadium.Validate();
        Wildlife ??=new();Wildlife.Validate();
        SpecialFruitBag ??= new();
        SeedBag ??= new();
        foreach (var key in new System.Collections.Generic.List<string>(SpecialFruitBag.Keys))
        {
            if (!FruitKinds.CanBag(key)) SpecialFruitBag.Remove(key);
            else SpecialFruitBag[key]=Math.Clamp(SpecialFruitBag[key],0,12);
        }
        foreach (var key in new System.Collections.Generic.List<string>(SeedBag.Keys))
        {
            if (!FruitKinds.CanGrow(key)) SeedBag.Remove(key);
            else SeedBag[key]=Math.Clamp(SeedBag[key],0,12);
        }
        if (GardenTrees!=null && (GardenTrees.Count>7 || GardenTrees.Exists(tree=>tree==null ||
            !FruitKinds.CanGrow(tree.FruitKind) || !float.IsFinite(tree.X) || !float.IsFinite(tree.Z) ||
            !float.IsFinite(tree.GrowthSeconds) || tree.GrowthSeconds<0)))
            throw new ArgumentException("Unsupported garden tree layout");
        if (GardenTrees!=null)
            foreach (var tree in GardenTrees)
                if (tree.Fruits!=null && (tree.Fruits.Length>3 ||
                    Array.Exists(tree.Fruits,fruit=>fruit==null || !float.IsFinite(fruit.Growth) ||
                        fruit.Bites<0 || fruit.Bites>4 ||
                        fruit.X.HasValue && !float.IsFinite(fruit.X.Value) ||
                        fruit.Y.HasValue && !float.IsFinite(fruit.Y.Value) ||
                        fruit.Z.HasValue && !float.IsFinite(fruit.Z.Value))))
                    throw new ArgumentException("Unsupported garden tree fruit state");
        BagFruit = Math.Clamp(BagFruit,0,12);
        BagHeartFruit=Math.Clamp(BagHeartFruit,0,12);
        CourtshipReadiness=float.IsFinite(CourtshipReadiness) ? Math.Clamp(CourtshipReadiness,0,100) : 0;
        BloomRemaining=float.IsFinite(BloomRemaining) ? Math.Clamp(BloomRemaining,0,300) : 0;
        SocialLinks ??= new();
        var known=new System.Collections.Generic.HashSet<(string,string)>();
        SocialLinks.RemoveAll(link=>link==null || string.IsNullOrWhiteSpace(link.First) ||
            string.IsNullOrWhiteSpace(link.Second) || link.First==link.Second ||
            !known.Add(string.CompareOrdinal(link.First,link.Second)<0 ? (link.First,link.Second) : (link.Second,link.First)));
        foreach (var link in SocialLinks)
        {
            link.Score=Math.Clamp(link.Score,-100,100);
            if (link.ReverseScore is int reverse) link.ReverseScore=Math.Clamp(reverse,-100,100);
            link.Introduced|=link.Met;
        }
        CharacterRelationships ??= new();
        foreach (var value in CharacterRelationships.Values)
            if (!float.IsFinite(value) || value is < -100 or > 100)
                throw new ArgumentException("Unsupported character relationship");
        GardenEggs ??= new();
        if (GardenEggs.Count>12 || GardenEggs.Exists(egg=>egg==null || !float.IsFinite(egg.X) || !float.IsFinite(egg.Y) || !float.IsFinite(egg.Z)))
            throw new ArgumentException("Unsupported garden egg data");
        foreach (var egg in GardenEggs)
            if(egg.Chao is {} resident){resident.EnsureFavoriteFruit();resident.History ??=new();resident.History.Validate();resident.Athletics ??=new();resident.Athletics.Validate();}
        EnsureStatisticsTree();
        Sociability=Math.Clamp(Sociability,0,100);
        Manners = Math.Clamp(Manners,0,100);
        Kindness = Math.Clamp(Kindness,-100,100);
        Aggressiveness = Math.Clamp(Aggressiveness,-100,100);
        Curiosity = Math.Clamp(Curiosity,-100,100);
        Initiative = Math.Clamp(Initiative,0,990);
        Fullness = Math.Clamp(Fullness, 0, 100);
        Joy = Math.Clamp(Joy, 0, 100);
        Energy = Math.Clamp(Energy, 0, 100);
        Fun = Math.Clamp(Fun,0,100);
    }
}

public sealed class GardenEggState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Color { get; set; } = "White";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool Hatched { get; set; }
    public PetState? Chao { get; set; }
}


public sealed record GardenSaveLocation(int Zone,int MoriZone,float PlayerX,float PlayerY,float PlayerZ,
    float MoriX,float MoriY,float MoriZ,GardenFruitSave[]? Trees=null,string? AreaId=null);

public sealed record GardenFruitSave(float X,float Z,GardenFruitSlotSave[] Slots);
public sealed record GardenFruitSlotSave(float Growth,int Bites,float? X,float? Y,float? Z);


public sealed class ChaoRelationship
{
    public string First { get; set; } = "";
    public string Second { get; set; } = "";
    public int Score { get; set; }
    public int? ReverseScore { get; set; }
    public int ScoreFrom(string id) => id==Second ? ReverseScore ?? Score : Score;
    public void SetScoreFrom(string id,int score)
    {
        score=Math.Clamp(score,-100,100);
        if (id==Second) ReverseScore=score;
        else { ReverseScore ??= Score; Score=score; }
    }
    public bool Met { get; set; }
    public bool Introduced { get; set; }
    public bool Partner { get; set; }

    public bool Matches(string first,string second) =>
        First==first && Second==second || First==second && Second==first;

    public string Status => StatusFrom(First);
    public string StatusFrom(string id) => Partner ? "Partner" : ScoreFrom(id)<=-60 ? "Enemy" : ScoreFrom(id)<=-25 ? "Rival" :
        ScoreFrom(id)>=30 && Met ? "Friend" : Met ? "Acquaintance" : "Stranger";
}


public static class FruitKinds
{
    public const string Garden="Garden Fruit";
    public const string Heart="Heart Fruit";
    public const string Blue="Blue Square Fruit";
    public const string Yellow="Yellow Triangle Fruit";
    public const string Red="Red Round Fruit";
    public const string Green="Green Diamond Fruit";
    public static readonly string[] Special={Blue,Yellow,Red,Green};
    public const string Moonblossom="Moonblossom Fruit";
    public static readonly string[] ShopSeeds={Garden,Blue,Yellow,Red,Green};
    public static readonly string[] Growable={Garden,Blue,Yellow,Red,Green,Moonblossom};
    public static readonly string[] BagKinds={Blue,Yellow,Red,Green,Moonblossom};
    public static bool CanBag(string kind)=>IsSpecial(kind) || kind==Moonblossom;
    public static bool IsSpecial(string kind) => Array.IndexOf(Special,kind)>=0;
    public static bool CanGrow(string kind) => Array.IndexOf(Growable,kind)>=0;
    public static string NewFavorite() => Special[Random.Shared.Next(Special.Length)];
}

public sealed class GardenTreeState
{
    public string FruitKind { get; set; } = FruitKinds.Garden;
    public float X { get; set; }
    public float Z { get; set; }
    public float GrowthSeconds { get; set; } = 180;
    public GardenFruitSlotSave[]? Fruits { get; set; }
}
