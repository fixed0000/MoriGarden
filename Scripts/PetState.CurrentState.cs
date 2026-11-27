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
    private long contactSequence;
    private ChaoVocalSignals vocalSignals=new();
    internal ChaoVocalSignals VocalSignals=>vocalSignals;
    private readonly System.Collections.Generic.Queue<string> contactOrder=new();
    private readonly System.Collections.Generic.HashSet<string> contacts=new(StringComparer.Ordinal);
    [JsonIgnore] public ChaoActivityCoordinator? GardenActivity { get; internal set; }
    [JsonIgnore] public GardenRhythmContext? DailyRhythm { get; internal set; }
    [JsonIgnore] public string GardenSleepAvailability { get; internal set; }="Not probed in an active garden";
    [JsonIgnore] public ChaoNeeds Needs => needs ??=new(this);
    [JsonIgnore] public ChaoMood Mood => mood ??=new(this);

    internal void AdvanceCurrentState(float seconds,bool sleeping)
    {
        if(!float.IsFinite(seconds) || seconds<0)throw new ArgumentException("Invalid state update interval");
        Needs.Advance(seconds,sleeping);Mood.Advance(seconds,sleeping);
    }
    internal void KeepCurrentRuntimeFrom(PetState source)
    {
        LearnedPreferences.KeepRuntimeFrom(source.LearnedPreferences);LeisureSelection=source.LeisureSelection;SocialSelection=source.SocialSelection;
        ActivityHabits.KeepRuntimeFrom(source.ActivityHabits);
        vocalSignals=source.vocalSignals;
        needs=source.needs?.CopyFor(this);mood=source.mood?.CopyFor(this);GardenActivity=source.GardenActivity;DailyRhythm=source.DailyRhythm;GardenSleepAvailability=source.GardenSleepAvailability;contactSequence=source.contactSequence;
        foreach(var id in source.contactOrder){contactOrder.Enqueue(id);contacts.Add(id);}
    }
    public string NewContactId(string source)=>source+":"+(++contactSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool ObserveEvent(ChaoStateEvent kind,string source,string? contactId=null)
    {
        if(string.IsNullOrWhiteSpace(source) || source.Length>128)throw new ArgumentException("Invalid Chao state event source");
        if(contactId!=null && (string.IsNullOrWhiteSpace(contactId) || contactId.Length>256))throw new ArgumentException("Invalid completed contact identity");
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
        bool physical=false;
        if(rule.Item5>0)
        {
            // Explicit completion IDs are independent of the emotion source's cooldown.
            // Legacy callers receive frame-level idempotency, never one relief per call.
            string id=kind+":"+(contactId ?? source+"@"+Mood.Now.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            if(id.Length>300)throw new ArgumentException("Invalid contact identity");
            if(contacts.Add(id))
            {
                contactOrder.Enqueue(id);while(contactOrder.Count>256)contacts.Remove(contactOrder.Dequeue());
                Needs.PositiveContact(rule.Item5);physical=true;
            }
        }
        bool emotion=Mood.React(rule.Item1,rule.Item2,rule.Item3,kind+":"+source,rule.Item4);
        if(emotion)vocalSignals.Accept(kind,source,contactId,Mood.Now);
        if(kind==ChaoStateEvent.WakeDisturbed)Mood.WakeDisturbed();
        return physical || emotion;
    }
}
