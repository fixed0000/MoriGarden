using System;
using System.Collections.Generic;

namespace Mori;

public sealed record PersonalityContribution(string Trait,float Normalized,float Contribution);
public sealed record PersonalityActivityScore(float BaseScore,float Adjustment,float FinalScore,
    IReadOnlyList<PersonalityContribution> Contributions);

// Immutable normalized snapshot, never owns or mutates PetState. Unused scales
// remain explicit extension points, not speculative behavioral multipliers.
public sealed record NormalizedChaoPersonality(float Curious,float Kindness,float Energetic,float Naive,
    float Solitude,float Vitality,float BigEater,float Regain,float Skillful,float Charm,float Chatty,float Carefree,float Fickle);

public sealed class ChaoPersonalityActivityWeights
{
    public NormalizedChaoPersonality Traits { get; }
    private readonly Dictionary<ChaoActivity,(float Adjustment,IReadOnlyList<PersonalityContribution> Terms)> weights=new();
    public ChaoPersonalityActivityWeights(ChaoPersonality? personality)
    {
        personality?.Validate();var p=personality ?? new ChaoPersonality();
        Traits=new(p.Curious/100f,p.Kindness/100f,p.Energetic/100f,p.Naive/100f,p.Solitude/100f,
            p.Vitality/100f,p.BigEater/100f,p.Regain/100f,p.Skillful/100f,p.Charm/100f,p.Chatty/100f,p.Carefree/100f,p.Fickle/100f);
        foreach(var activity in Enum.GetValues<ChaoActivity>())weights[activity]=Build(activity);
    }
    public float MaximumVariation=>.03f*Math.Max(0,Traits.Fickle);
    public PersonalityActivityScore Evaluate(ChaoActivity activity,float baseScore,NeedUrgency urgency=NeedUrgency.Low)
    {
        if(!float.IsFinite(baseScore))throw new ArgumentException("Invalid base score");
        var (total,terms)=weights[activity];
        if(activity is ChaoActivity.Eat or ChaoActivity.Sleep)
            total=Math.Max(total,urgency==NeedUrgency.Critical ? 0 : urgency==NeedUrgency.High ? -.05f : -.20f);
        return new(baseScore,total,Math.Clamp(baseScore+total,0,1),terms);
    }
    private (float,IReadOnlyList<PersonalityContribution>) Build(ChaoActivity activity)
    {
        var terms=new List<PersonalityContribution>();float total=0;
        void Add(string trait,float value,float factor){terms.Add(new(trait,value,value*factor));total+=value*factor;}
        switch(activity)
        {
            case ChaoActivity.Eat:Add("BigEater",Traits.BigEater,.06f);break;
            case ChaoActivity.Play:
                Add("Energetic",Traits.Energetic,.08f);Add("Carefree",Traits.Carefree,.04f);Add("Vitality",Traits.Vitality,.03f);break;
            case ChaoActivity.Socialize:Add("Solitude",Traits.Solitude,-.10f);Add("Chatty",Traits.Chatty,.08f);break;
            case ChaoActivity.Leisure:
                Add("Curious",Traits.Curious,.12f);Add("Energetic",Traits.Energetic,.04f);
                Add("Carefree",Traits.Carefree,.06f);Add("Vitality",Traits.Vitality,.04f);break;
            case ChaoActivity.Wander:Add("Curious",Traits.Curious,.05f);Add("Energetic",Traits.Energetic,.03f);break;
        }
        return (Math.Clamp(total,-.20f,.20f),terms.AsReadOnly());
    }
}
