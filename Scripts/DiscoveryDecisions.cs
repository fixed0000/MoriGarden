using System;
using System.Collections.Generic;

namespace Mori;

public enum DiscoveryChoice { Ignore, Observe, Approach }
public readonly record struct DiscoveryPlan(DiscoveryChoice Choice,float ThoughtSeconds,bool SharePlayer,bool InvitePeer);
public static class DiscoveryDecisions
{
    public static float Curiosity(PetState pet)=>(pet.Curiosity+100)/200f;
    public static float DetectionRadius(DiscoveryDefinition item,PetState pet)=>item.Radius*(.65f+.45f*Curiosity(pet)+.1f*pet.Initiative/990f)*(.7f+.3f*pet.Energy/100);
    public static float Appeal(DiscoveryDefinition item,PetState pet,DiscoveryMemory memory,double now)
    {
        if(!memory.CanConsider(item,now))return 0;
        var known=memory.Knowledge(item.Id,now);
        float novelty=known==DiscoveryKnowledge.Unknown ? 1 : known==DiscoveryKnowledge.Noticed ? .65f : known==DiscoveryKnowledge.Familiar ? .16f : .3f;
        return item.Appeal*novelty*(.35f+.65f*Curiosity(pet))*(.55f+.45f*pet.Initiative/990f)*(.2f+.8f*pet.Energy/100)*(1+pet.History.ObjectBias(item.Id,item.AreaId))*
            (1+.35f*memory.Affinity.GetValueOrDefault(item.Kind));
    }
    public static DiscoveryPlan Choose(DiscoveryDefinition item,PetState pet,DiscoveryMemory memory,double now,Random random,bool invited=false)
    {
        float c=Curiosity(pet),i=pet.Initiative/990f,e=pet.Energy/100,s=pet.Sociability/100f;
        float familiarity=memory.Find(item.Id)?.Visits/15f ?? 0;
        float ignore=.2f+(1-c)*.8f+(1-e)*1.5f;
        float observe=.45f+(1-i)*1.6f+(1-c)*.5f;
        // Aggressiveness contributes only when curiosity and initiative already motivate an approach.
        float experience=pet.History.ObjectBias(item.Id,item.AreaId)+pet.History.Confidence;
        float approach=(.1f+c*i*3+i*.4f+familiarity*.4f+Math.Max(0,pet.Aggressiveness)/100f*c*i*.2f)*e*Math.Clamp(1+experience,.65f,1.4f);
        double draw=random.NextDouble()*(ignore+observe+approach);
        bool firstStudyInvitation=invited && memory.Find(item.Id)?.Visits==0;
        var choice=pet.Energy<20 || !firstStudyInvitation && Appeal(item,pet,memory,now)<=0 ? DiscoveryChoice.Ignore : draw<ignore ? DiscoveryChoice.Ignore : draw<ignore+observe ? DiscoveryChoice.Observe : DiscoveryChoice.Approach;
        bool novel=memory.Find(item.Id)==null;
        bool share=novel && pet.Bond>=PetState.CallBondThreshold && (item.Reactions & DiscoveryReaction.Share)!=0 &&
            random.NextDouble()<Math.Clamp(pet.Bond/100f,0,1)*.7f+s*.12f;
        bool invite=random.NextDouble()<s*(.25f+.55f*c)*(1-Math.Max(0,pet.Aggressiveness)/200f);
        return new(choice,4+(1-c)*(1-i)*3+(1-e)*1.5f-familiarity*.5f+Math.Max(0,-experience)*4,share,invite);
    }
}
