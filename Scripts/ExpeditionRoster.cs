using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

// References live states. Names and list positions are never identities.
public sealed record ExpeditionPet(string Id, string Color, PetState State);
public static class ExpeditionRoster
{
    public static IReadOnlyList<ExpeditionPet> Available(PetState owner)
    {
        var result=new List<ExpeditionPet>();
        if(owner.CanAct) result.Add(new("main","Default",owner));
        var ids=new HashSet<string>(StringComparer.Ordinal) { "main" };
        foreach(var egg in owner.GardenEggs)
            if(egg.Hatched && egg.Chao?.CanAct==true && !string.IsNullOrWhiteSpace(egg.Id) && ids.Add(egg.Id))
                result.Add(new(egg.Id,egg.Color,egg.Chao));
        return result;
    }
    public static List<string> Resolve(PetState owner,IEnumerable<string>? ids) =>
        (ids ?? Array.Empty<string>()).Where(id=>Available(owner).Any(p=>p.Id==id)).Distinct(StringComparer.Ordinal).Take(3).ToList();
}

public enum ExpeditionIntent { Observe, Explore, Rest, Sleep, Social, Follow }
public static class ExpeditionDecisions
{
    // Continuous parameters and a weighted draw, followed by a committed action window.
    public static double Weight(ExpeditionIntent action,PetState pet,bool hasCompany)
    {
        double curiosity=(pet.Curiosity+100)/200d, initiative=pet.Initiative/990d;
        return action switch {
            ExpeditionIntent.Explore=>.5+curiosity*3+initiative*2,
            ExpeditionIntent.Observe=>1.2+(1-curiosity)*2,
            ExpeditionIntent.Rest=>.35+(100-pet.Energy)/22d,
            ExpeditionIntent.Sleep=>pet.Energy<40 ? (40-pet.Energy)/5d : 0,
            ExpeditionIntent.Social=>hasCompany ? .1+pet.Sociability/22d+(pet.Kindness+100)/160d-(pet.Aggressiveness+100)/300d : 0,
            _=>.6+Math.Clamp(pet.Bond,0,100)/35d+(1-initiative)
        };
    }
    public static ExpeditionIntent Choose(PetState pet,bool company,Random random)
    {
        var actions=Enum.GetValues<ExpeditionIntent>();
        double total=actions.Sum(a=>Math.Max(0,Weight(a,pet,company))), draw=random.NextDouble()*total;
        foreach(var action in actions) { draw-=Math.Max(0,Weight(action,pet,company)); if(draw<=0)return action; }
        return ExpeditionIntent.Observe;
    }
}
