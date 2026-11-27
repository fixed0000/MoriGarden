using System;
using System.Linq;
using System.Text.Json;
using Mori;

internal static class ChaoExpressionIntegrationChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var expected=new[]{ChaoExpression.Neutral,ChaoExpression.Happy,ChaoExpression.Delighted,ChaoExpression.Curious,ChaoExpression.Sad,ChaoExpression.Sad,ChaoExpression.Annoyed,ChaoExpression.Afraid,ChaoExpression.Sleepy};
        foreach(var emotion in Enum.GetValues<ChaoEmotion>())
            check(ChaoExpressiveness.FromMood(new(0,.5f,emotion,.7f))==expected[(int)emotion],"Expression A: mood mapping "+emotion);
        ChaoMoodSnapshot Mood(ChaoEmotion e,float intensity=.7f)=>new(0,.5f,e,intensity);
        ChaoExpressionDecision Eval(ChaoPoseKind pose,ChaoEmotion e,bool closed=false,bool squint=false,bool half=false,bool happy=false,
            ChaoExpression? forced=null,ChaoPoseKind owner=ChaoPoseKind.Idle)=>ChaoExpressionLayer.Evaluate(pose,closed,squint,half,happy,false,Mood(e),new(),forced,1,owner);
        check(Eval(ChaoPoseKind.Idle,ChaoEmotion.Happy).Expression==ChaoExpression.Happy && Eval(ChaoPoseKind.Idle,ChaoEmotion.Happy).Source==ChaoExpressionSource.Mood,"Expression B: Idle uses current mood");
        check(Eval(ChaoPoseKind.Walk,ChaoEmotion.Happy).Expression==ChaoExpression.Happy,"Expression C: Walk uses same mood source");
        check(Eval(ChaoPoseKind.Walk,ChaoEmotion.Sad).Expression==ChaoExpression.Sad,"Expression D: walking sadness survives movement");
        check(Eval(ChaoPoseKind.Sleep,ChaoEmotion.Happy,true,forced:ChaoExpression.Delighted).Expression==ChaoExpression.Sleepy,"Expression E: physical sleep closure wins over mood and forced joy");
        check(Eval(ChaoPoseKind.Distress,ChaoEmotion.Happy,squint:true).Expression==ChaoExpression.Annoyed && Eval(ChaoPoseKind.Water,ChaoEmotion.Happy,squint:true).Expression==ChaoExpression.Afraid,"Expression F: protective distress/water squint wins");
        check(Eval(ChaoPoseKind.Walk,ChaoEmotion.Sad,forced:ChaoExpression.Happy).Source==ChaoExpressionSource.Forced && Eval(ChaoPoseKind.Walk,ChaoEmotion.Sad).Expression==ChaoExpression.Sad,"Expression G/L: free temporary request wins; after expiry uses current mood");
        check(Eval(ChaoPoseKind.Thought,ChaoEmotion.Happy,forced:ChaoExpression.Delighted).Expression==ChaoExpression.Curious &&
            Eval(ChaoPoseKind.Grip,ChaoEmotion.Sad,happy:true,forced:ChaoExpression.Afraid).Expression==ChaoExpression.Happy,"Expression: action pose and smile flag outrank unrelated free-pose lease");
        check(Eval(ChaoPoseKind.Distress,ChaoEmotion.Happy,forced:ChaoExpression.Sad,owner:ChaoPoseKind.Distress).Source==ChaoExpressionSource.Action &&
            Eval(ChaoPoseKind.Walk,ChaoEmotion.Happy,forced:ChaoExpression.Sad,owner:ChaoPoseKind.Distress).Expression==ChaoExpression.Happy,"Expression: action-owned face does not leak after pose ends");
        foreach(var pose in Enum.GetValues<ChaoPoseKind>().Where(p=>!ChaoExpressionLayer.IsFree(p)))
            check(Eval(pose,ChaoEmotion.Happy).ProtectedAnimation && Eval(pose,ChaoEmotion.Happy).Source==ChaoExpressionSource.Action,"Expression: protected "+pose+" suppresses automatic mood");
        foreach(var (expression,trait,strength) in new[]{(ChaoExpression.Happy,"Energetic",.12f),(ChaoExpression.Delighted,"Energetic",.12f),(ChaoExpression.Curious,"Curious",.10f)})
        {
            var low=new ChaoPersonality();var high=new ChaoPersonality();typeof(ChaoPersonality).GetProperty(trait)!.SetValue(low,-100);typeof(ChaoPersonality).GetProperty(trait)!.SetValue(high,100);
            check(Math.Abs(ChaoExpressiveness.PersonalityMultiplier(expression,low)-(1-strength))<.000001f && Math.Abs(ChaoExpressiveness.PersonalityMultiplier(expression,high)-(1+strength))<.000001f &&
                ChaoExpressiveness.VisualIntensity(.7f,expression,low)<ChaoExpressiveness.VisualIntensity(.7f,expression,high),"Expression H/I: exact visual-only range "+expression);
        }
        check(new[]{.2f,.6f,1f}.Select(i=>ChaoExpressiveness.VisualIntensity(i,ChaoExpression.Happy,new())).SequenceEqual(new[]{.2f,.6f,1f}) && ChaoExpressiveness.VisualIntensity(1,ChaoExpression.Happy,new(){Energetic=100})==1,"Expression: real mood intensity is graded and clamped");
        var pet=new PetState{Personality=new(){Energetic=100,Curious=-100},PersonalitySchemaVersion=1};pet.EnsureStatistics();pet.Validate();
        var mood=pet.Mood.Snapshot;string original=JsonSerializer.Serialize(pet);
        for(int i=0;i<1000;i++)ChaoExpressionLayer.Evaluate(ChaoPoseKind.Walk,false,false,false,false,false,pet.Mood.Snapshot,pet.Personality);
        check(original==JsonSerializer.Serialize(pet) && mood==pet.Mood.Snapshot,"Expression J/K/P: 1000 reads preserve needs, all traits, exact mood and complete save data");
        var other=new ChaoPersonality{Energetic=100,Curious=100};
        foreach(var property in typeof(ChaoPersonality).GetProperties().Where(p=>p.Name is not ("Energetic" or "Curious")))property.SetValue(other,100);
        check(Enum.GetValues<ChaoExpression>().All(e=>ChaoExpressiveness.PersonalityMultiplier(e,other)==ChaoExpressiveness.PersonalityMultiplier(e,new(){Energetic=100,Curious=100})),"Expression: other eleven traits add no visual effects");
        check(Enumerable.Range(0,100).All(_=>Eval(ChaoPoseKind.Walk,ChaoEmotion.Happy).Expression==ChaoExpression.Happy),"Expression M: unchanged walking mood never alternates Happy/Neutral");
        // Blink clocks, exact rendered weights, real editor rebind and save/load are
        // additionally exercised on three actual ImportedChao in the native check.
    }
}
