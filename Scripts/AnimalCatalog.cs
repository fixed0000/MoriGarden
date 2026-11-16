using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public sealed record AnimalSpecies(string Id,string Name,string Skill,string Habitat,float Speed,float Caution,float Curiosity,
    int Rarity,float RespawnSeconds,bool Obtainable=true,string? RequiredFood=null,bool RequiresFamiliarity=false);
public static class AnimalCatalog
{
    public static readonly AnimalSpecies[] All={
        new("rabbit","Rabbit","Run","Grassland",1.9f,.85f,.35f,1,150),
        new("songbird","Songbird","Fly","Woodland edge",1.1f,.60f,.75f,1,170),
        new("bear-cub","Bear cub","Power","Woodland",.65f,.25f,.65f,2,210),
        new("penguin","Penguin","Swim","Pond shore",.8f,.40f,.55f,2,190),
        new("parrot","Parrot","Fly","Warm woodland",1.1f,.45f,.8f,3,240),
        new("gorilla","Gorilla","Power","Dense woodland",.7f,.3f,.6f,3,250)
    };
    public static AnimalSpecies? Find(string id)=>All.FirstOrDefault(a=>a.Id==id);
}
public static class AnimalEncounterRules
{
    public static float CalmRate(AnimalSpecies species,float speed)=>speed>4.3f ? -(.24f+species.Caution*.3f) : .065f+species.Curiosity*.045f;
    public static float ChaoHelp(PetState pet)=>.22f+.12f*pet.Intelligence/990f+.1f*(pet.Kindness+100)/200f+.08f*pet.Sociability/100f;
    public static float Thought(PetState pet)=>4+(1-pet.Intelligence/990f)*2;
}
