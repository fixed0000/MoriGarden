using System;
using System.Linq;

namespace Mori;

public sealed partial class PetState
{
    // Missing in old JSON -> independent empty model. Explicit null is corrupt.
    public ChaoLearnedPreferences LearnedPreferences { get; set; }=new();
    internal void ValidateLearnedPreferences()
    {if(LearnedPreferences==null)throw new ArgumentException("Missing learned preferences model");LearnedPreferences.Validate();}
    public object LearnedPreferenceDiagnostics()=>new
    {
        activeSeconds=History.Seconds,
        fixedFavorites=new{FavoriteToy,PreferredToy,LikesToyCars,FavoriteFruit,FavoriteInstrument},
        entries=Enum.GetValues<LearnedItemGroup>().SelectMany(group=>
            (group==LearnedItemGroup.Toy ? LearnedPreferenceIds.Toys : LearnedPreferenceIds.Fruits).Select(id=>
            {
                var records=group==LearnedItemGroup.Toy ? LearnedPreferences.ToyPreferences : LearnedPreferences.FruitPreferences;
                records.TryGetValue(id,out var entry);
                bool favorite=group==LearnedItemGroup.Fruit ? FavoriteFruit==id : LearnedPreferenceIds.FixedToy(PreferredToy)==id || LearnedPreferenceIds.FixedToy(FavoriteInstrument)==id;
                return new{group=group.ToString(),id,effectiveBias=LearnedPreferences.EffectiveBias(group,id,History.Seconds),
                    experienceCount=entry?.ExperienceCount ?? 0,lastCreditSeconds=(double?)entry?.LastCreditSeconds,fixedFavorite=favorite};
            })).ToArray()
    };
}
