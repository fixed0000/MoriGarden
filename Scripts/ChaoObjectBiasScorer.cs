using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public sealed record ChaoObjectBiasScore(float Success,float Failure,float Bias);

// Shared pure calculation: gameplay and diagnostics use the same arithmetic.
public static class ChaoObjectBiasScorer
{
    public static ChaoObjectBiasScore Evaluate(IEnumerable<ChaoMemoryEvent> events,double now,string source,string area)
    {
        float success=events.Where(e=>e.Source==source && e.Area==area && e.Valence>0).Sum(e=>e.Influence(now)*(1+Math.Min(e.Occurrences,4)*.15f));
        float failure=events.Where(e=>e.Source==source && e.Area==area && e.Valence<0).Sum(e=>e.Influence(now));
        return new(success,failure,Math.Clamp(success*.10f-failure*.24f/(1+success),-.25f,.25f));
    }
}
