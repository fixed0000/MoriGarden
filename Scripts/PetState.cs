using System;

namespace Mori;

// Чистые правила игры: этот класс ничего не знает о графике и клавиатуре.
public sealed class PetState
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Mori";
    public float Fullness { get; set; } = 72;
    public float Joy { get; set; } = 65;
    public float Energy { get; set; } = 90;
    public int Bond { get; set; }
    public const int CallBondThreshold = 6;
    public bool RespondsToCall => Bond >= CallBondThreshold;
    public int Care { get; set; }
    public int Agility { get; set; }
    public int Stars { get; set; }
    public int BagFruit { get; set; }
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
    public bool Glutton { get; set; }
    public bool Crybaby { get; set; }
    public float Fun { get; set; } = 65;
    public string Form { get; set; } = "seed";

    public int Swim { get; set; }
    public int Fly { get; set; }
    public int Run { get; set; }
    public int Power { get; set; }
    public int Stamina { get; set; }
    public int Luck { get; set; }
    public int Intelligence { get; set; }
    public int Initiative { get; set; }
    public bool FindsFood => Intelligence>=80 && Initiative>=65;

    public bool IsEgg { get; set; }
    public float EggSeconds { get; set; }
    public float ChildSeconds { get; set; }
    public float CocoonSeconds { get; set; }
    public string? RecentAnimalSkill { get; set; }

    public bool InCocoon => CocoonSeconds > 0;
    public bool CanAct => !IsEgg && !InCocoon;
    public string LifeStage => IsEgg ? "Egg" : InCocoon ? "Cocoon" : Form == "seed" ? "Child" : "Adult";

    public static PetState NewEgg() => new() { IsEgg = true, EggSeconds = 28 };

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
        if (Form != "seed") return false;
        ChildSeconds = Math.Min(90,ChildSeconds+seconds);
        if (ChildSeconds < 90 || Growth < 100) return false;
        CocoonSeconds = 9;
        return true;
    }

    public int Growth => Math.Min(100, Care * 10 + Agility * 10);

    public void Tick(float seconds, bool sleeping)
    {
        Fullness = Math.Clamp(Fullness - seconds * .035f, 0, 100);
        Joy = Math.Clamp(Joy - seconds * .018f, 0, 100);
        Energy = Math.Clamp(Energy + seconds * (sleeping ? 5 : -.045f/(1+Stamina*.002f)), 0, 100);
        if (!sleeping) Fun=Math.Max(0,Fun-seconds*.07f);
    }

    public bool Feed()
    {
        if (!CanAct || Fullness >= 100) return false;
        Fullness = Math.Min(100, Fullness + 24);
        Care++;
        Bond++;
        Stamina = Math.Min(990, Stamina + 2);
        return true;
    }

    public void FeedBite(bool last)
    {
        Fullness=Math.Min(100,Fullness+4.8f);
        if (!last) return;
        Care++;
        Bond++;
        Stamina=Math.Min(990,Stamina+2);
    }

    public void Pet()
    {
        if (!CanAct) return;
        Joy = Math.Min(100, Joy + 12);
        Bond++;
        Care++;
    }

    public void TrainWithAnimal(string skill)
    {
        if (!CanAct) return;
        const int xp = 12;
        switch (skill)
        {
            case "Swim": Swim = Math.Min(990, Swim+xp); break;
            case "Fly": Fly = Math.Min(990, Fly+xp); break;
            case "Run": Run = Math.Min(990, Run+xp); break;
            case "Power": Power = Math.Min(990, Power+xp); break;
            default: throw new ArgumentException("Unknown animal skill", nameof(skill));
        }
        RecentAnimalSkill = skill;
        Joy = Math.Min(100,Joy+4);
    }

    public void CompleteRace(int score)
    {
        score = Math.Clamp(score, 0, 8);
        Stars += score;
        Run = Math.Min(990, Run + score);
        BestScore = Math.Max(BestScore, score);
        Agility += score >= 6 ? 3 : score >= 3 ? 2 : score > 0 ? 1 : 0;
        Joy = Math.Min(100, Joy + score * 2);
        Energy = Math.Max(0, Energy - 10);
    }

    public bool Evolve()
    {
        if (Form != "seed" || Growth < 100) return false;
        Form = RecentAnimalSkill switch
        {
            "Swim" => "tide",
            "Fly" => "comet",
            "Run" => "swift",
            "Power" => "stone",
            _ => Agility >= Care ? "comet" : "bloom"
        };
        return true;
    }

    public void Validate()
    {
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
        BagFruit = Math.Clamp(BagFruit,0,12);
        GardenEggs ??= new();
        if (GardenEggs.Count>12 || GardenEggs.Exists(egg=>egg==null || !float.IsFinite(egg.X) || !float.IsFinite(egg.Z)))
            throw new ArgumentException("Unsupported garden egg data");
        Swim = Math.Clamp(Swim, 0, 990);
        Fly = Math.Clamp(Fly, 0, 990);
        Run = Math.Clamp(Run, 0, 990);
        Power = Math.Clamp(Power, 0, 990);
        Stamina = Math.Clamp(Stamina, 0, 990);
        Luck = Math.Clamp(Luck, 0, 990);
        Intelligence = Math.Clamp(Intelligence, 0, 990);
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
    public float Z { get; set; }
    public bool Hatched { get; set; }
    public PetState? Chao { get; set; }
}
