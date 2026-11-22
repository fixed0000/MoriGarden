using System;

namespace Mori;

public enum ChaoExpression { Neutral,Happy,Delighted,Curious,Surprised,Sad,Afraid,Annoyed,Sleepy,Focused }
internal enum ChaoPoseKind { Idle,Walk,Crawl,Water,Air,Rest,Sleep,Rise,Thought,Interaction,Grip,Distress }
public static class ChaoExpressiveness
{
    // Only a fallback for free idle poses; existing face/pose flags keep priority.
    public static ChaoExpression FromMood(ChaoMoodSnapshot mood) => mood.DominantEmotion switch
    {
        ChaoEmotion.Happy=>ChaoExpression.Happy,ChaoEmotion.Excited=>ChaoExpression.Delighted,
        ChaoEmotion.Curious=>ChaoExpression.Curious,ChaoEmotion.Sad or ChaoEmotion.Lonely=>ChaoExpression.Sad,
        ChaoEmotion.Frustrated=>ChaoExpression.Annoyed,ChaoEmotion.Afraid=>ChaoExpression.Afraid,
        ChaoEmotion.Sleepy=>ChaoExpression.Sleepy,_=>ChaoExpression.Neutral
    };
    public static float Intensity(PetState state)=>.7f+state.Sociability*.002f+state.Initiative/990f*.12f;
    public static float Response(PetState state)=>5+state.Initiative/990f*3;
    public static float BlinkDelay(Random random,PetState? state)=>2.7f+(float)random.NextDouble()*4.4f-(state?.Energy<25 ? .8f : 0);
    public static ChaoExpression Resolve(bool closed,bool squint,bool upset,bool happy,float energy,ChaoExpression request)
        =>closed ? ChaoExpression.Sleepy : squint ? ChaoExpression.Afraid : upset ? ChaoExpression.Annoyed : request is ChaoExpression.Afraid or ChaoExpression.Sad ? request :
            happy ? request==ChaoExpression.Delighted ? request : ChaoExpression.Happy : request!=ChaoExpression.Neutral ? request : energy<22 ? ChaoExpression.Sleepy : ChaoExpression.Neutral;
}
