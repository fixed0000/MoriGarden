using System;
using System.Linq;
using System.Text.Json;
using Mori;

internal static class SocialPartnerPreferenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var root=new PetState();root.EnsurePersonalityTree();root.EnsureStatisticsTree();
        var a=new PetState();a.EnsurePersonality();a.EnsureStatistics();var b=new PetState();
        SocialPartnerScore Score(string id,float distance=0)=>ChaoSocialPartnerScorer.Evaluate(root,"a",a,id,distance);
        check(Score("b").PartnerWeight==1 && Score("c").PartnerWeight==1,"Partner A: unfamiliar partners start neutral");
        var link=root.SocialLink("a","b",true)!;link.SetScoreFrom("a",50);link.SetScoreFrom("b",-50);
        check(Math.Abs(Score("b").PartnerWeight-1.10f)<.00001f,"Partner B: outgoing +50 gives 1.10");
        a.History.Record(ChaoMemoryKind.Pleasant,"old-shared","garden","A shared experience.",1,1,new[]{"b"});
        check(Score("b").MemoryContribution>0 && Score("b").PartnerWeight>1.10f,"Partner C: existing personal experience increases outgoing preference");
        var reverse=ChaoSocialPartnerScorer.Evaluate(root,"b",b,"a");
        check(Math.Abs(reverse.PartnerWeight-.90f)<.00001f && link.ScoreFrom("b")==-50,"Partner D: negative score reduces weight without editing the relation");
        check(Score("b").PartnerWeight>reverse.PartnerWeight && b.History.Events.Count==0,"Partner E: attitudes and memories remain directional");
        int links=root.SocialLinks.Count;string untouched=JsonSerializer.Serialize(a),rootUntouched=JsonSerializer.Serialize(root);
        for(int i=0;i<1000;i++)Score("unknown");
        check(root.SocialLinks.Count==links && rootUntouched==JsonSerializer.Serialize(root),"Partner F: evaluation never creates a relationship");
        check(Score("b").PartnerBias==a.History.PartnerBias("b") && Score("b").MemoryContribution==.25f*a.History.PartnerBias("b"),"Partner G: reuses existing decaying PartnerBias exactly");
        for(int i=0;i<6;i++)a.History.Record(ChaoMemoryKind.Pleasant,"experience-"+i,"garden","Pleasant shared experience.",1,1,new[]{"b"});
        link.SetScoreFrom("a",100);
        check(Math.Abs(Score("b").PartnerWeight-1.30f)<.00001f,"Partner H: maximum positive influence is bounded at 1.30");
        float high=Score("b").PartnerWeight;a.History.Advance(19800);
        check(Score("b").PartnerWeight<high && Score("b").PartnerWeight>=1.2f,"Partner I: preference decays with existing memory influence");
        var inputs=new[]{new SocialPartnerCandidate("b",2,false),new SocialPartnerCandidate("c",2)};
        var scores=ChaoSocialPartnerScorer.Candidates(root,"a",a,inputs);
        check(Enumerable.Range(0,1000).All(seed=>ChaoSocialPartnerScorer.Choose(scores,new Random(seed))!.Id=="c"),"Partner J: an unavailable favorite has zero chance");
        check(ChaoSocialPartnerScorer.Choose(scores,new Random(12))==ChaoSocialPartnerScorer.Choose(scores,new Random(12)),"Partner K: fixed seeds repeat exactly");
        a.History=new();for(int i=0;i<6;i++)a.History.Record(ChaoMemoryKind.Pleasant,"experience-"+i,"garden","Shared experience.",1,1,new[]{"b"});
        var equal=new[]{new SocialPartnerCandidate("b",2),new SocialPartnerCandidate("c",2)};
        int preferred=0,neutral=0;const int trials=20000;
        for(int seed=0;seed<trials;seed++)
        {
            if(ChaoSocialPartnerScorer.Choose(ChaoSocialPartnerScorer.Candidates(root,"a",a,equal),new Random(seed+1623))!.Id=="b")preferred++;
            if(ChaoSocialPartnerScorer.Choose(ChaoSocialPartnerScorer.Candidates(new PetState(),"a",new PetState(),equal),new Random(seed+1623))!.Id=="b")neutral++;
        }
        Console.WriteLine($"PARTNER_DISTRIBUTION: independent seeds={trials}; preferred={preferred}; neutral={neutral}");
        check(Math.Abs(preferred/(double)trials-1.30/2.30)<.02 && preferred<trials && preferred>neutral,"Partner L: 20000 independent trials match 56.52 percent without monopolization");
        check(Math.Abs(neutral/(double)trials-.5)<.02,"Partner M: equal neutral partners remain near 50 percent");
        a.SocialSelection.LastCompletedPartner="b";scores=ChaoSocialPartnerScorer.Candidates(root,"a",a,equal);
        check(scores[0].RecentMultiplier==.85f && scores[1].RecentMultiplier==1,"Partner N: last successful partner receives temporary repeat penalty");
        check(ChaoSocialPartnerScorer.Candidates(root,"a",a,equal.Take(1))[0].RecentMultiplier==1 &&
            ChaoSocialPartnerScorer.Candidates(root,"a",a,equal,repeatable:false)[0].RecentMultiplier==1,"Partner O: single choice and introductions have no repeat penalty");
        var innate=a.Personality!.Copy();float[] needs={a.Fullness,a.Energy,a.Fun,a.Joy,a.SocialNeed};
        untouched=JsonSerializer.Serialize(a);rootUntouched=JsonSerializer.Serialize(root);
        for(int i=0;i<1000;i++)a.SocialSelection.Select(root,"a",a,equal,"Ball");
        check(a.Personality.Equals(innate),"Partner P: all thirteen innate parameters remain unchanged");
        check(needs.SequenceEqual(new[]{a.Fullness,a.Energy,a.Fun,a.Joy,a.SocialNeed}),"Partner Q: choice does not relieve any need");
        check(untouched==JsonSerializer.Serialize(a) && rootUntouched==JsonSerializer.Serialize(root),"Partner R: selection leaves serialized state byte-equivalent");
        check(ChaoSocialPartnerScorer.Evaluate(root,"a",a,"b",float.NaN).FinalWeight==0 && ChaoSocialPartnerScorer.Choose(Array.Empty<SocialPartnerScore>(),new Random(1))==null,"Invalid distances and no candidates fail safely");
        var pairs=new[]{("a",a),("b",b)};float needA=a.SocialNeed,needB=b.SocialNeed;int scoreBefore=link.ScoreFrom("a");
        ChaoSocialPartnerScorer.CompleteShared("shared-ball",pairs,"completed-ball");
        check(a.History.Events.Single(e=>e.Source=="shared-ball").Participants.SequenceEqual(new[]{"b"}) && b.History.Events.Single(e=>e.Source=="shared-ball").Participants.SequenceEqual(new[]{"a"}),"Completed shared experience records stable reciprocal personal memories");
        ChaoSocialPartnerScorer.CompleteShared("shared-ball",pairs,"completed-ball");
        check(a.History.Events.Single(e=>e.Source=="shared-ball").Occurrences==1 && b.History.Events.Single(e=>e.Source=="shared-ball").Occurrences==1,"Shared credit retains the existing 120-second memory cooldown");
        check(a.SocialNeed==needA && b.SocialNeed==needB && link.ScoreFrom("a")==scoreBefore,"Memory completion does not duplicate SocialNeed or relationship rewards");
        ChaoSocialPartnerScorer.CompleteShared("music-ensemble",new[]{("a",a),("c",new PetState())},"completed-music");
        check(a.SocialSelection.LastCompletedPartner=="c","Completing another shared partner removes the previous repeat penalty");
        var restored=JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(a))!;
        check(restored.History.Events.Any(e=>e.Source=="shared-ball") && restored.SocialSelection.LastCompletedPartner==null,"Existing memory format persists while runtime partner state does not");
        var replacement=SaveStore.Snapshot(a);replacement.KeepCurrentRuntimeFrom(a);
        check(ReferenceEquals(replacement.SocialSelection,a.SocialSelection),"Editor rebind preserves the initiating actor's RNG and last completed partner");
        link.SetScoreFrom("a",-100);a.History=new();
        for(int i=0;i<6;i++)a.History.Record(ChaoMemoryKind.Unpleasant,"bad-"+i,"garden","Unpleasant experience.",-1,1,new[]{"b"});
        check(Math.Abs(Score("b").PartnerWeight-.725f)<.00001f,"Minimum combined relationship and memory follows the prescribed .725 formula");
    }
}
