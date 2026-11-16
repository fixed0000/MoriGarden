using Godot;

namespace Mori;

internal enum DiscoveryPhase { Notice, Think, Travel, Inspect, Observe, React, CallPlayer, AwaitPlayer, Acknowledge, Settle }
internal sealed class DiscoverySession
{
    internal ExpeditionCompanion Pet { get; }
    internal DiscoveryInterest Item { get; }
    internal DiscoveryPlan Plan { get; }
    internal Vector3 Spot { get; }
    internal bool Novel { get; }
    internal bool Requested { get; }
    internal ExpeditionCompanion? Guide { get; set; }
    internal DiscoveryPhase Phase { get; private set; }
    internal float PhaseTime { get; private set; }
    internal float Age { get; private set; }
    internal bool Completed { get; private set; }
    internal bool Invited { get; set; }
    internal bool AttentionSent { get; private set; }
    internal Vector3? PeerLook { get; private set; }
    internal float PeerLookUntil { get; private set; }
    internal void NoticePeer(Vector3 point) { PeerLook=point;PeerLookUntil=Age+1.5f; }
    private int inviteAttempts;
    private float nextInvitation;
    internal DiscoverySession(ExpeditionCompanion pet,DiscoveryInterest item,DiscoveryPlan plan,Vector3 spot,bool novel,bool requested=false)
    {
        Pet=pet;Item=item;Plan=plan;Spot=spot;Novel=novel;Requested=requested;
        pet.Partner=null;pet.SetIntent(ExpeditionIntent.Investigate,pet.Body.Position,150);
        if(item.Interaction!=null)pet.TargetTolerance=.2f;
    }
    private void Enter(DiscoveryPhase phase)
    {
        DiscoveryPresentation.Clear(Pet);Phase=phase;PhaseTime=0;
        Pet.UpdateGoal(phase==DiscoveryPhase.Travel ? Spot : Pet.Body.Position);
    }
    // Returning true releases the intent back to the existing team planner.
    internal bool Advance(float dt,Vector3 player,DiscoverySystem system)
    {
        Age+=dt;if(Pet.Transitioning)return false;PhaseTime+=dt;
        if(Plan.InvitePeer && !Invited && inviteAttempts<2 && Age>=nextInvitation && Phase is DiscoveryPhase.React or DiscoveryPhase.CallPlayer or DiscoveryPhase.AwaitPlayer)
        { inviteAttempts++;nextInvitation=Age+6;Invited=system.Invite(this); }
        switch(Phase)
        {
            case DiscoveryPhase.Notice:
                if(PhaseTime>=1.5f)Enter(DiscoveryPhase.Think);break;
            case DiscoveryPhase.Think:
                if(PhaseTime>=Plan.ThoughtSeconds)Enter(Plan.Choice==DiscoveryChoice.Approach ? DiscoveryPhase.Travel : DiscoveryPhase.Observe);break;
            case DiscoveryPhase.Travel:
                if(ExpeditionCompanion.Flat(Pet.Body.Position,Spot)<(Item.Interaction==null ? .6f : .3f))
                {
                    if(!system.Perception.Visible(Pet,Item,false)){system.Cancel(Pet,"occluded");return false;}
                    Enter(DiscoveryPhase.Inspect);
                }
                else if(PhaseTime>60 || Pet.NavigationFailure>8)system.Cancel(Pet,"blocked");
                break;
            case DiscoveryPhase.Inspect:
                if(PhaseTime>=(Item.Interaction?.InspectSeconds(Pet.Pet.State) ?? 5))
                {
                    Completed=system.Complete(this);Enter(DiscoveryPhase.React);
                }
                break;
            case DiscoveryPhase.Observe:
                if(PhaseTime>=7)Enter(DiscoveryPhase.Settle);break;
            case DiscoveryPhase.React:
                if(PhaseTime>=2)
                {
                    if(Plan.SharePlayer && system.TryAttention(this))
                    { AttentionSent=true;Enter(DiscoveryPhase.CallPlayer);ChaoSounds.Play(Pet.Art,"voice"); }
                    else Enter(DiscoveryPhase.Settle);
                }
                break;
            case DiscoveryPhase.CallPlayer:
                if(PhaseTime>=2)Enter(DiscoveryPhase.AwaitPlayer);break;
            case DiscoveryPhase.AwaitPlayer:
                if(ExpeditionCompanion.Flat(player,Item.GlobalPosition)<3.5f && ExpeditionCompanion.Flat(player,Pet.Body.Position)<4){system.Acknowledge(this);Enter(DiscoveryPhase.Acknowledge);}
                else if(PhaseTime>=10)Enter(DiscoveryPhase.Settle);
                break;
            case DiscoveryPhase.Acknowledge:
                if(PhaseTime>=2)Enter(DiscoveryPhase.Settle);break;
            case DiscoveryPhase.Settle: return PhaseTime>=2;
        }
        return false;
    }
}
