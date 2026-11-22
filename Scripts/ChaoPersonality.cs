using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Mori;

// Signed SA2B-style scales. The name Naive does NOT reverse its original scale:
// negative values mean naive. No needs, abilities or relationships live here.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChaoPersonality : IEquatable<ChaoPersonality>, IJsonOnDeserialized, IJsonOnSerializing
{
    [JsonRequired] public int Curious { get; set; }
    [JsonRequired] public int Kindness { get; set; }
    [JsonRequired] public int Energetic { get; set; }
    [JsonRequired] public int Naive { get; set; }
    [JsonRequired] public int Solitude { get; set; }
    [JsonRequired] public int Vitality { get; set; }
    [JsonRequired] public int BigEater { get; set; }
    [JsonRequired] public int Regain { get; set; }
    [JsonRequired] public int Skillful { get; set; }
    [JsonRequired] public int Charm { get; set; }
    [JsonRequired] public int Chatty { get; set; }
    [JsonRequired] public int Carefree { get; set; }
    [JsonRequired] public int Fickle { get; set; }

    private static readonly PropertyInfo[] Scales = typeof(ChaoPersonality).GetProperties();
    public void Validate()
    {
        foreach (var scale in Scales)
            if ((int)scale.GetValue(this)! is < -100 or > 100)
                throw new ArgumentException("Personality." + scale.Name + " must be between -100 and 100. The save was kept.");
    }
    public ChaoPersonality Copy() => (ChaoPersonality)MemberwiseClone();
    void IJsonOnDeserialized.OnDeserialized() => Validate();
    void IJsonOnSerializing.OnSerializing() => Validate();
    public bool Equals(ChaoPersonality? other) => other != null && Scales.All(p => Equals(p.GetValue(this), p.GetValue(other)));
    public override bool Equals(object? other) => other is ChaoPersonality p && Equals(p);
    public override int GetHashCode()
    {
        var hash = new HashCode(); foreach (var scale in Scales) hash.Add((int)scale.GetValue(this)!); return hash.ToHashCode();
    }
    public static ChaoPersonality CreateInitial(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var result = new ChaoPersonality();
        // MoriGarden distribution, not a claim about original SA2B genetics.
        // Sum of two independent uniforms: triangular, centered on zero, [-80,80].
        foreach (var scale in Scales) scale.SetValue(result, random.Next(-40, 41) + random.Next(-40, 41));
        return result;
    }
}
