using System;
using System.Text.Json.Serialization;

namespace Mori;

public sealed partial class PetState : IJsonOnDeserialized, IJsonOnSerializing
{
    public int PersonalitySchemaVersion { get; set; }
    public ChaoPersonality? Personality { get; set; }
    private int legacyCuriosity, legacyKindness;
    private bool legacyGlutton, legacyCrybaby;

    // Source adapters. JSON aliases below stage old data independently, so JSON
    // property order can never overwrite an existing canonical personality.
    [JsonIgnore] public int Curiosity
    { get => Personality?.Curious ?? legacyCuriosity; set { if (Personality is {} p) p.Curious=value; else legacyCuriosity=value; } }
    [JsonIgnore] public int Kindness
    { get => Personality?.Kindness ?? legacyKindness; set { if (Personality is {} p) p.Kindness=value; else legacyKindness=value; } }
    [JsonIgnore] public bool Glutton
    {
        get => Personality is {} p ? p.BigEater>54 : legacyGlutton;
        set { if (Personality is {} p) { if(value!=Glutton)p.BigEater=value ? 75 : 0; } else legacyGlutton=value; }
    }
    [JsonIgnore] public bool Crybaby
    {
        get => Personality is {} p ? p.Energetic< -54 : legacyCrybaby;
        set { if (Personality is {} p) { if(value!=Crybaby)p.Energetic=value ? -75 : 0; } else legacyCrybaby=value; }
    }
    [JsonPropertyName("Curiosity"), JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public int? SavedCuriosity { get => null; set => legacyCuriosity=value ?? throw new ArgumentException("Invalid legacy Curiosity"); }
    [JsonPropertyName("Kindness"), JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public int? SavedKindness { get => null; set => legacyKindness=value ?? throw new ArgumentException("Invalid legacy Kindness"); }
    [JsonPropertyName("Glutton"), JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public bool? SavedGlutton { get => null; set => legacyGlutton=value ?? throw new ArgumentException("Invalid legacy Glutton"); }
    [JsonPropertyName("Crybaby"), JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public bool? SavedCrybaby { get => null; set => legacyCrybaby=value ?? throw new ArgumentException("Invalid legacy Crybaby"); }

    public void EnsurePersonality()
    {
        if (PersonalitySchemaVersion is <0 or >1) throw new UnsupportedPersonalitySchemaException();
        if (Personality==null)
        {
            if (PersonalitySchemaVersion!=0) throw new ArgumentException("Missing personality records. The save was kept.");
            Personality=new ChaoPersonality { Curious=legacyCuriosity,Kindness=legacyKindness,
                BigEater=legacyGlutton ? 75 : 0,Energetic=legacyCrybaby ? -75 : 0 };
        }
        Personality.Validate(); PersonalitySchemaVersion=1;
        legacyCuriosity=legacyKindness=0;legacyGlutton=legacyCrybaby=false;
    }
    public void EnsurePersonalityTree()
    {
        EnsurePersonality(); if(GardenEggs!=null) foreach(var egg in GardenEggs) egg?.Chao?.EnsurePersonalityTree();
    }
    public void InitializeFreshPersonality(Random? random=null)
    {
        if(Personality!=null || PersonalitySchemaVersion!=0)throw new ArgumentException("This Chao already has a personality.");
        Personality=ChaoPersonality.CreateInitial(random ?? Random.Shared);PersonalitySchemaVersion=1;
        legacyCuriosity=legacyKindness=0;legacyGlutton=legacyCrybaby=false;
    }
    void IJsonOnDeserialized.OnDeserialized() => EnsurePersonality();
    void IJsonOnSerializing.OnSerializing() => EnsurePersonality();
}

public sealed class UnsupportedPersonalitySchemaException : ArgumentException
{
    public UnsupportedPersonalitySchemaException() : base("Unsupported personality schema. Use a compatible game version; the save was kept.") { }
}
