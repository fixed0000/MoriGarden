using System;
using System.Linq;

namespace Mori;

internal static class ChaoExperience
{
    internal static string AreaTitle(string area)=>WorldCatalog.Default.Areas.TryGetValue(area,out var definition) ?
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(definition.Title.ToLowerInvariant()) : "a quiet place";
    internal static void Study(PetState pet,DiscoveryDefinition item,bool requested,string? guide=null)
    {
        string place=AreaTitle(item.AreaId);
        var kind=guide!=null ? ChaoMemoryKind.SharedStudy : requested ? ChaoMemoryKind.PlayerDiscovery : ChaoMemoryKind.IndependentStudy;
        string description=guide!=null ? $"Explored a discovery with {guide}'s guidance." : requested ? $"Studied something interesting in {place} with your encouragement." : $"Explored something interesting in {place} independently.";
        if(pet.History.Record(kind,item.Id,item.AreaId,description,.65f,.55f,place:item.Position))
            pet.History.Grow(pet,item.Id,"Curiosity",.18f);
    }
    internal static void Together(PetState owner,string first,PetState a,string second,PetState b,ChaoMemoryKind kind,string source,string area,bool relationship=true)
    {
        string verb=kind==ChaoMemoryKind.SharedRest ? "Rested" : kind==ChaoMemoryKind.Introduction ? "Became acquainted" : "Explored";
        bool added=a.History.Record(kind,source,area,$"{verb} with {b.Name}.",.6f,.65f,new[]{second});
        bool other=b.History.Record(kind,source,area,$"{verb} with {a.Name}.",.6f,.65f,new[]{first});
        if(added)a.History.Grow(a,source+"-"+second,"Sociability",.16f);
        if(other)b.History.Grow(b,source+"-"+first,"Sociability",.16f);
        if(!relationship || !added || !other)return;
        var link=owner.SocialLink(first,second);if(link?.Met!=true)return;
        void Credit(string id,string peer,PetState state)
        {
            int count=state.History.Events.Count(e=>e.Participants.Contains(peer) && e.Valence>.2f);
            string key="friendship-"+peer+"-"+count/5;
            if(count>=5 && state.History.GrowthSources.Count<128 && state.History.GrowthSources.Add(key))link.SetScoreFrom(id,Math.Min(100,link.ScoreFrom(id)+1));
        }
        Credit(first,second,a);Credit(second,first,b);
    }
}
