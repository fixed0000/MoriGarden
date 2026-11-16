using Godot;

namespace Mori;

internal static class DiscoveryPresentation
{
    internal static void Clear(ExpeditionCompanion pet)
    {
        ImportedChao.EndThink(pet.Art);pet.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(null,false);
    }
    internal static void Look(ExpeditionCompanion pet,Vector3 target,float dt)
    {
        pet.Face(target,dt);
        ImportedChao.Attend(pet.Art,target,80);
    }
    internal static void Pose(DiscoverySession session,Vector3 player,float dt)
    {
        var pet=session.Pet;var item=session.Item;
        if(!GodotObject.IsInstanceValid(item))return;
        var orb=pet.Art.GetNodeOrNull<EmotionOrb>("MoodOrb");
        ImportedChao.Express(pet.Art,session.Phase switch { DiscoveryPhase.Notice=>session.Novel ? ChaoExpression.Surprised : ChaoExpression.Curious,
            DiscoveryPhase.Think=>ChaoExpression.Curious,DiscoveryPhase.Inspect=>ChaoExpression.Focused,DiscoveryPhase.React or DiscoveryPhase.Acknowledge=>ChaoExpression.Delighted,
            _=>ChaoExpression.Neutral },.3f);
        if(session.Phase==DiscoveryPhase.Travel)return;
        var target=session.Phase==DiscoveryPhase.CallPlayer ? player+Vector3.Up : session.PeerLook is {} peer && session.Age<session.PeerLookUntil ? peer : item.LookPoint;
        Look(pet,target,dt);
        switch(session.Phase)
        {
            case DiscoveryPhase.Notice: orb?.SetIdea(session.Novel ? "!" : null,false);break;
            case DiscoveryPhase.Think: ImportedChao.ThinkPose(pet.Art,session.Plan.ThoughtSeconds-session.PhaseTime);break;
            case DiscoveryPhase.Inspect:
                if(item.Interaction!=null)item.Interaction.Pose(pet,session.PhaseTime);
                else if((item.Definition.Reactions & DiscoveryReaction.Touch)!=0)
                    ImportedChao.ReceivePose(pet.Art,Mathf.Sin(Mathf.Min(session.PhaseTime/4,1)*Mathf.Pi)*.7f);
                break;
            case DiscoveryPhase.React: orb?.SetIdea(session.Novel ? "!" : null,true);break;
            case DiscoveryPhase.CallPlayer: orb?.SetIdea("!",true);break;
            case DiscoveryPhase.AwaitPlayer:
                if(session.PhaseTime%6>3)Look(pet,player+Vector3.Up,dt);
                break;
            case DiscoveryPhase.Acknowledge: orb?.SetIdea("!",true);break;
        }
    }
}
