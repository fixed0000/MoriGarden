using System;
using System.Text.Json.Serialization;

namespace Mori;

public enum ChaoStateEvent { FoodBite, MealFinished, PlayStarted, PlayFinished, PlayerContact,
    PositiveSocial, UnpleasantContact, DiscoveryNoticed, DiscoveryFinished, WakeDisturbed, Frightened }

public sealed partial class PetState
{
    public float SocialNeed { get; set; }=20;
    private ChaoNeeds? needs;
    private ChaoMood? mood;
    [JsonIgnore] public ChaoNeeds Needs => needs ??=new(this);
    [JsonIgnore] public ChaoMood Mood => mood ??=new(this);

    internal void AdvanceCurrentState(float seconds,bool sleeping)
    {
        if(!float.IsFinite(seconds) || seconds<0)throw new ArgumentException("Invalid state update interval");
        Needs.Advance(seconds,sleeping);Mood.Advance(seconds,sleeping);
    }
    internal void KeepCurrentRuntimeFrom(PetState source)
    { needs=source.needs?.CopyFor(this);mood=source.mood?.CopyFor(this); }

    public bool ObserveEvent(ChaoStateEvent kind,string source)
    {
        if(string.IsNullOrWhiteSpace(source) || source.Length>128)throw new ArgumentException("Invalid Chao state event source");
        var rule=kind switch
        {
            ChaoStateEvent.FoodBite=>(ChaoEmotion.Happy,.35f,3f,30,0f),
            ChaoStateEvent.MealFinished=>(ChaoEmotion.Happy,.6f,8f,40,0f),
            ChaoStateEvent.PlayStarted=>(ChaoEmotion.Happy,.45f,6f,35,0f),
            ChaoStateEvent.PlayFinished=>(ChaoEmotion.Excited,.65f,8f,45,0f),
            ChaoStateEvent.PlayerContact=>(ChaoEmotion.Happy,.55f,6f,40,6f),
            ChaoStateEvent.PositiveSocial=>(ChaoEmotion.Happy,.6f,8f,42,8f),
            ChaoStateEvent.UnpleasantContact=>(ChaoEmotion.Frustrated,.65f,8f,75,0f),
            ChaoStateEvent.DiscoveryNoticed=>(ChaoEmotion.Curious,.55f,6f,35,0f),
            ChaoStateEvent.DiscoveryFinished=>(ChaoEmotion.Happy,.6f,8f,40,0f),
            ChaoStateEvent.WakeDisturbed=>(ChaoEmotion.Frustrated,.7f,60f,80,0f),
            ChaoStateEvent.Frightened=>(ChaoEmotion.Afraid,.8f,3f,95,0f),
            _=>throw new ArgumentException("Unknown Chao state event")
        };
        if(!Mood.React(rule.Item1,rule.Item2,rule.Item3,kind+":"+source,rule.Item4))return false;
        if(rule.Item5>0)Needs.PositiveContact(rule.Item5);
        if(kind==ChaoStateEvent.WakeDisturbed)Mood.WakeDisturbed();
        return true;
    }
}
