using System;
using System.Linq;
using System.Text;

namespace Mori;

public static class ChaoStateDiagnostics
{
    public static object Capture(PetState pet) => new
    {
        meters=new {pet.Fullness,Hunger=pet.Needs.Hunger,pet.Energy,Fatigue=pet.Needs.Fatigue,
            pet.Fun,Boredom=pet.Needs.Boredom,pet.Joy,pet.SocialNeed},
        mood=pet.Mood.Snapshot,
        priorities=pet.Needs.Evaluate(NeedActionContext.LongOptional).Priorities,
        activity=pet.GardenActivity==null ? null : new{pet.GardenActivity.SelectedActivity,pet.GardenActivity.StartedAt,pet.GardenActivity.MinimumRemaining,pet.GardenActivity.LastFailureReason,pet.GardenActivity.CurrentExecutor,pet.GardenActivity.Scores,pet.GardenActivity.History},
        reactions=pet.Mood.ActiveReactions.Select(r=>new{r.Emotion,r.Intensity,r.Source,r.Priority,
            r.Duration,r.ExpiresAt,Remaining=Math.Max(0,r.ExpiresAt-pet.Mood.Now)}).ToArray()
    };
    public static string Describe(PetState pet)
    {
        var mood=pet.Mood.Snapshot;var text=new StringBuilder();
        text.AppendLine($"Fullness {pet.Fullness:F1} / Hunger {pet.Needs.Hunger:F1}");
        text.AppendLine($"Energy {pet.Energy:F1} / Fatigue {pet.Needs.Fatigue:F1}");
        text.AppendLine($"Fun {pet.Fun:F1} / Boredom {pet.Needs.Boredom:F1}");
        text.AppendLine($"Joy {pet.Joy:F1} / SocialNeed {pet.SocialNeed:F1}\n");
        text.AppendLine($"Emotion: {mood.DominantEmotion} ({mood.EmotionIntensity:F2})");
        text.AppendLine($"Valence {mood.Valence:F2} / Arousal {mood.Arousal:F2}\n");
        text.AppendLine("Need urgency and interruption eligibility");
        foreach(var need in pet.Needs.Evaluate(NeedActionContext.LongOptional).Priorities)
            text.AppendLine($"{need.Need}: {need.Level} / {need.Weight:F2}\n  {need.LevelSeconds:F0}s; interrupt long optional: {need.CanInterruptCurrentAction}");
        text.AppendLine("\nActive temporary reactions");
        foreach(var r in pet.Mood.ActiveReactions)
            text.AppendLine($"{r.Emotion} {r.Intensity:F2} / priority {r.Priority}\n  {Math.Max(0,r.ExpiresAt-pet.Mood.Now):F1}s left / {r.Source}");
        if(pet.Mood.ActiveReactions.Count==0)text.AppendLine("None");
        if(pet.GardenActivity!=null)text.AppendLine(pet.GardenActivity.Describe());
        return text.ToString();
    }
}
