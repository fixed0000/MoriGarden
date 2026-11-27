using System;

namespace Mori;

public enum ChaoExpression { Neutral,Happy,Delighted,Curious,Surprised,Sad,Afraid,Annoyed,Sleepy,Focused }
internal enum ChaoPoseKind { Idle,Walk,Crawl,Water,Air,Rest,Sleep,Rise,Thought,Interaction,Grip,Distress }
public static class ChaoExpressiveness
{
    // Mood mapping is shared by free Idle and Walk; action/physical faces keep priority.
    public static ChaoExpression FromMood(ChaoMoodSnapshot mood) => mood.DominantEmotion switch
    {
        ChaoEmotion.Happy=>ChaoExpression.Happy,ChaoEmotion.Excited=>ChaoExpression.Delighted,
        ChaoEmotion.Curious=>ChaoExpression.Curious,ChaoEmotion.Sad or ChaoEmotion.Lonely=>ChaoExpression.Sad,
        ChaoEmotion.Frustrated=>ChaoExpression.Annoyed,ChaoEmotion.Afraid=>ChaoExpression.Afraid,
        ChaoEmotion.Sleepy=>ChaoExpression.Sleepy,_=>ChaoExpression.Neutral
    };
    public static float PersonalityMultiplier(ChaoExpression expression,ChaoPersonality? personality)=>expression switch
    {
        ChaoExpression.Happy or ChaoExpression.Delighted=>1+.12f*Math.Clamp(personality?.Energetic ?? 0,-100,100)/100f,
        ChaoExpression.Curious=>1+.10f*Math.Clamp(personality?.Curious ?? 0,-100,100)/100f,
        _=>1
    };
    public static float VisualIntensity(float moodIntensity,ChaoExpression expression,ChaoPersonality? personality)
        =>Math.Clamp(moodIntensity*PersonalityMultiplier(expression,personality),0,1);
    // Retained compatibility helpers; free expression amplitude uses VisualIntensity.
    public static float Intensity(PetState state)=>.7f+state.Sociability*.002f+state.Initiative/990f*.12f;
    public static float Response(PetState state)=>5+state.Initiative/990f*3;
    public static float BlinkDelay(Random random,PetState? state)=>2.7f+(float)random.NextDouble()*4.4f-(state?.Energy<25 ? .8f : 0);
    public static ChaoExpression Resolve(bool closed,bool squint,bool upset,bool happy,float energy,ChaoExpression request)
        =>closed ? ChaoExpression.Sleepy : squint ? ChaoExpression.Afraid : upset ? ChaoExpression.Annoyed : request is ChaoExpression.Afraid or ChaoExpression.Sad ? request :
            happy ? request==ChaoExpression.Delighted ? request : ChaoExpression.Happy : request!=ChaoExpression.Neutral ? request : energy<22 ? ChaoExpression.Sleepy : ChaoExpression.Neutral;
}

internal enum ChaoExpressionSource { Forced, Action, Mood, Neutral }
internal sealed record ChaoExpressionDecision(ChaoExpression Expression,ChaoExpressionSource Source,
    float PersonalityMultiplier,float VisualIntensity,bool ProtectedAnimation,string SuppressionReason);
public sealed record ChaoExpressionDiagnostic(ChaoEmotion? DominantEmotion,float EmotionIntensity,float Valence,float Arousal,
    string CurrentExpression,string CurrentPose,string Source,float AppliedPersonalityMultiplier,
    float FinalVisualIntensity,float SmoothedVisualIntensity,float BlinkAmount,bool ProtectedAnimation,string SuppressionReason,IdleGestureDiagnostic? IdleGestures=null);

// Pure arbitration only. No needs, mood writes, movement, random source or saved state.
internal static class ChaoExpressionLayer
{
    internal static bool IsFree(ChaoPoseKind pose)=>pose is ChaoPoseKind.Idle or ChaoPoseKind.Walk;
    internal static ChaoExpressionDecision Evaluate(ChaoPoseKind pose,bool closed,bool squint,bool half,bool happy,bool held,
        ChaoMoodSnapshot? mood,ChaoPersonality? personality,ChaoExpression? explicitExpression=null,
        float explicitIntensity=1,ChaoPoseKind explicitPose=ChaoPoseKind.Idle)
    {
        bool protectedPose=!IsFree(pose) || held;
        ChaoExpressionDecision Fixed(ChaoExpression e,ChaoExpressionSource source,string reason,float intensity=1)=>new(e,source,1,intensity,protectedPose,reason);
        if(closed)return Fixed(pose==ChaoPoseKind.Distress ? ChaoExpression.Sad : ChaoExpression.Sleepy,ChaoExpressionSource.Forced,"Physical closed eyes");
        if(squint)return Fixed(pose==ChaoPoseKind.Distress ? ChaoExpression.Annoyed : ChaoExpression.Afraid,ChaoExpressionSource.Forced,"Protective squint");
        if(half)return Fixed(ChaoExpression.Annoyed,ChaoExpressionSource.Action,"Action eyelid flag");
        if(happy)return Fixed(explicitExpression==ChaoExpression.Delighted ? ChaoExpression.Delighted : ChaoExpression.Happy,ChaoExpressionSource.Action,"Action smile flag");
        if(protectedPose)
        {
            // An expression authored by this same action remains its face channel;
            // a free-pose lease cannot leak into a later protected animation.
            if(explicitExpression is {} action && explicitPose==pose && pose is not (ChaoPoseKind.Sleep or ChaoPoseKind.Rise))
                return Fixed(action,ChaoExpressionSource.Action,"Action-owned expression",Math.Clamp(explicitIntensity,0,1));
            return Fixed(pose switch{ChaoPoseKind.Thought=>ChaoExpression.Curious,ChaoPoseKind.Interaction or ChaoPoseKind.Grip=>ChaoExpression.Focused,
                ChaoPoseKind.Sleep=>ChaoExpression.Sleepy,_=>ChaoExpression.Neutral},ChaoExpressionSource.Action,"Protected "+pose+(held ? " / held" : ""));
        }
        ChaoExpression expression;float intensity;ChaoExpressionSource source;
        if(explicitExpression is {} forced && IsFree(explicitPose)){expression=forced;intensity=explicitIntensity;source=ChaoExpressionSource.Forced;}
        else if(mood!=null){expression=ChaoExpressiveness.FromMood(mood);intensity=mood.EmotionIntensity;source=expression==ChaoExpression.Neutral ? ChaoExpressionSource.Neutral : ChaoExpressionSource.Mood;}
        else return Fixed(ChaoExpression.Neutral,ChaoExpressionSource.Neutral,"No bound mood",0);
        return new(expression,source,ChaoExpressiveness.PersonalityMultiplier(expression,personality),
            expression==ChaoExpression.Neutral ? 0 : ChaoExpressiveness.VisualIntensity(intensity,expression,personality),false,source==ChaoExpressionSource.Forced ? "Temporary free-pose Express lease" : "");
    }
}
