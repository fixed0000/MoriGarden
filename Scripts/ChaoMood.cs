using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChaoEmotion { Neutral, Happy, Excited, Curious, Sad, Lonely, Frustrated, Afraid, Sleepy }
public sealed record ChaoEmotionReaction(ChaoEmotion Emotion,float Intensity,float Duration,string Source,int Priority,double ExpiresAt);
public sealed record ChaoMoodSnapshot(float Valence,float Arousal,ChaoEmotion DominantEmotion,float EmotionIntensity);

// Bounded, temporary reactions. Existing animation flags and action poses remain
// authoritative; this model does not write Joy, needs, personality or AI intents.
public sealed class ChaoMood
{
    private readonly PetState pet;
    private readonly List<ChaoEmotionReaction> reactions=new();
    private readonly Dictionary<string,double> cooldown=new(StringComparer.Ordinal);
    private bool? sleeping;
    public double Now { get; private set; }
    public bool Sleeping => sleeping==true;
    public const int ReactionCapacity=8, SourceCapacity=32;
    internal ChaoMood(PetState pet) => this.pet=pet;
    internal ChaoMood CopyFor(PetState owner)
    {
        var copy=new ChaoMood(owner){Now=Now,sleeping=sleeping};
        copy.reactions.AddRange(reactions);
        foreach(var item in cooldown)copy.cooldown[item.Key]=item.Value;
        return copy;
    }
    public IReadOnlyList<ChaoEmotionReaction> ActiveReactions => reactions.ToArray();
    public bool React(ChaoEmotion emotion,float intensity,float duration,string source,int priority)
    {
        if(!Enum.IsDefined(emotion) || !float.IsFinite(intensity) || intensity is <0 or >1 || !float.IsFinite(duration) || duration is <=0 or >120 ||
            string.IsNullOrWhiteSpace(source) || source.Length>160 || priority is <0 or >100)throw new ArgumentException("Invalid temporary emotional event.");
        if(reactions.Any(e=>e.Source==source) || cooldown.TryGetValue(source,out double until) && Now<until)return false;
        if(reactions.Count>=ReactionCapacity)
        {
            var weakest=reactions.OrderBy(e=>e.Priority).ThenBy(e=>e.Intensity).ThenBy(e=>e.ExpiresAt).First();
            if(weakest.Priority>priority || weakest.Priority==priority && weakest.Intensity>intensity)return false;
            reactions.Remove(weakest);
        }
        reactions.Add(new(emotion,intensity,duration,source,priority,Now+duration));
        if(cooldown.Count>=SourceCapacity && !cooldown.ContainsKey(source))cooldown.Remove(cooldown.MinBy(pair=>pair.Value).Key);
        cooldown[source]=Now+duration+8;
        return true;
    }
    internal void Advance(float seconds,bool isSleeping)
    {
        Now+=seconds;reactions.RemoveAll(e=>e.ExpiresAt<=Now);
        foreach(var source in cooldown.Where(e=>e.Value<=Now).Select(e=>e.Key).ToArray())cooldown.Remove(source);
        bool woke=sleeping==true && !isSleeping;sleeping=isSleeping;
        if(woke)React(ChaoEmotion.Happy,.25f,4,"wake-naturally",20);
    }
    internal void WakeDisturbed() { sleeping=false; }
    public ChaoMoodSnapshot Snapshot
    {
        get
        {
            if(!float.IsFinite(pet.Joy) || !float.IsFinite(pet.Energy) || !float.IsFinite(pet.Fullness) || !float.IsFinite(pet.SocialNeed))
                throw new ArgumentException("Invalid mood source meter");
            var emotion=ChaoEmotion.Neutral;float intensity=0;int priority=0;
            if(pet.Joy>=80){emotion=ChaoEmotion.Happy;intensity=(pet.Joy-65)/100;priority=5;}
            if(pet.Joy<25){emotion=ChaoEmotion.Sad;intensity=.35f+(25-pet.Joy)/100;priority=10;}
            if(pet.SocialNeed>=75){emotion=ChaoEmotion.Lonely;intensity=.35f+(pet.SocialNeed-75)/100;priority=15;}
            if(pet.Energy<=25){emotion=ChaoEmotion.Sleepy;intensity=.4f+(25-pet.Energy)/100;priority=pet.Energy<=10 ? 85 : 25;}
            if(pet.Fullness<=10 && pet.Energy>10){emotion=ChaoEmotion.Frustrated;intensity=.5f;priority=65;}
            var reaction=reactions.OrderByDescending(e=>e.Priority).ThenByDescending(e=>e.Intensity).ThenBy(e=>e.ExpiresAt).FirstOrDefault();
            if(reaction!=null && reaction.Priority>=priority){emotion=reaction.Emotion;intensity=reaction.Intensity;}
            if(Sleeping){emotion=ChaoEmotion.Sleepy;intensity=1;}
            float baseline=Math.Clamp((pet.Joy-50)/50,-1,1);
            float valence=emotion switch {ChaoEmotion.Happy=>.6f,ChaoEmotion.Excited=>.85f,ChaoEmotion.Curious=>.25f,ChaoEmotion.Sad=>-.65f,ChaoEmotion.Lonely=>-.45f,ChaoEmotion.Frustrated=>-.7f,ChaoEmotion.Afraid=>-.9f,ChaoEmotion.Sleepy=>-.1f,_=>baseline};
            float arousal=emotion switch {ChaoEmotion.Excited=>.9f,ChaoEmotion.Afraid=>.95f,ChaoEmotion.Frustrated=>.75f,ChaoEmotion.Curious=>.6f,ChaoEmotion.Sleepy=>.1f,_=>.15f+.55f*pet.Energy/100+.3f*pet.Joy/100};
            return new(Math.Clamp(baseline*(1-intensity)+valence*intensity,-1,1),Math.Clamp(arousal,0,1),emotion,Math.Clamp(intensity,0,1));
        }
    }
}
