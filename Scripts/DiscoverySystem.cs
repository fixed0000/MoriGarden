using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal readonly record struct DiscoveryEvent(double Time,string Pet,string Object,string Action);
internal sealed class DiscoverySystem
{
    internal DiscoveryPerception Perception { get; }
    internal DiscoveryReservations Reservations { get; }=new();
    internal IReadOnlyCollection<DiscoverySession> Sessions=>sessions.Values;
    internal List<DiscoveryEvent> Events { get; }=new();
    internal bool Enabled { get; set; }=true;
    internal bool AutoScan { get; set; }=true;
    private readonly Func<PetState> owner;
    private readonly ExpeditionNavigation nav;
    private readonly IReadOnlyList<ExpeditionCompanion> team;
    private readonly Action<ExpeditionCompanion> resume;
    private readonly Dictionary<string,DiscoverySession> sessions=new();
    private readonly Dictionary<string,double> scanAt=new(),deferred=new();
    private readonly Dictionary<(string,string),double> occluded=new();
    private double lastAttention=-1000,nextIndex;
    private double Now=>owner().Expedition.ExplorationSeconds;
    private DiscoveryMemory Memory(ExpeditionCompanion pet)=>owner().Expedition.Memory(pet.Pet.Id);
    internal DiscoverySystem(Node3D world,Func<PetState> owner,ExpeditionNavigation nav,IReadOnlyList<ExpeditionCompanion> team,Action<ExpeditionCompanion> resume)
    { this.owner=owner;this.nav=nav;this.team=team;this.resume=resume;Perception=new(world); }
    private void Log(ExpeditionCompanion pet,DiscoveryInterest item,string action)
    {
        if(Events.Count>=128)Events.RemoveAt(0);Events.Add(new(Now,pet.Pet.Id,item.Definition.Id,action));
    }
    internal void Step(float dt,Vector3 player,float playerSpeed,Vector3? waitAnchor)
    {
        if(!Enabled)return;
        owner().Expedition.ExplorationSeconds+=dt;
        if(Now>=nextIndex){Perception.Reindex();nextIndex=Now+2;}
        foreach(var session in sessions.Values.ToArray())
        {
            var pet=session.Pet;
            if(!sessions.TryGetValue(pet.Pet.Id,out var current) || !ReferenceEquals(current,session))continue;
            if(pet.Intent!=ExpeditionIntent.Investigate || !pet.Pet.State.CanAct) { Cancel(pet,"priority",false);continue; }
            if(!GodotObject.IsInstanceValid(session.Item) || !session.Item.Usable(owner())) { Cancel(pet,"unavailable");continue; }
            if(pet.Pet.State.Energy<15 || ExpeditionCompanion.Flat(pet.Body.Position,player)>19 || session.Age>110)
            { Cancel(pet,pet.Pet.State.Energy<15 ? "tired" : "group left");continue; }
            if(session.Advance(dt,player,this))Finish(pet);
        }
        if(!AutoScan || playerSpeed>1.6f)return;
        for(int index=0;index<team.Count;index++)
        {
            var pet=team[index];string id=pet.Pet.Id;
            if(!scanAt.ContainsKey(id)){scanAt[id]=Now+.65+index*.37;continue;}
            if(Now<scanAt[id] || Now<deferred.GetValueOrDefault(id))continue;
            scanAt[id]=Now+1.1+index*.17;
            if(sessions.ContainsKey(id) || pet.Busy || !pet.ReadyForDiscovery || !pet.Pet.State.CanAct || pet.Pet.State.Energy<20 ||
                pet.Intent==ExpeditionIntent.Follow && ExpeditionCompanion.Flat(pet.Body.Position,player)>5 || Now-Memory(pet).LastFinished<45)continue;
            Scan(pet,waitAnchor);
        }
    }
    internal void Scan(ExpeditionCompanion pet,Vector3? waitAnchor=null)
    {
        var memory=Memory(pet);
        var candidates=Perception.Nearby(pet.Body.Position,Perception.SearchRadius).Where(item=>item.Usable(owner()) &&
            ExpeditionCompanion.Flat(pet.Body.Position,item.GlobalPosition)<=DiscoveryDecisions.DetectionRadius(item.Definition,pet.Pet.State) &&
            (waitAnchor==null || ExpeditionCompanion.Flat(waitAnchor.Value,item.GlobalPosition)<4) &&
            Now>=occluded.GetValueOrDefault((pet.Pet.Id,item.Definition.Id)) && memory.CanConsider(item.Definition,Now))
            .Select(item=>(Item:item,Score:DiscoveryDecisions.Appeal(item.Definition,pet.Pet.State,memory,Now)/(1+.035f*pet.Body.Position.DistanceTo(item.GlobalPosition))))
            .Where(p=>p.Score>.06f).OrderByDescending(p=>p.Score).ToArray();
        if(candidates.Length==0)return;
        var target=candidates[0].Item;
        if(!Perception.Visible(pet,target)) { occluded[(pet.Pet.Id,target.Definition.Id)]=Now+6;return; }
        var plan=DiscoveryDecisions.Choose(target.Definition,pet.Pet.State,memory,Now,pet.Random);
        bool novel=memory.Find(target.Definition.Id)==null;
        if(plan.Choice==DiscoveryChoice.Ignore){memory.Notice(target.Definition.Id,Now);Log(pet,target,"ignored");deferred[pet.Pet.Id]=Now+8;return;}
        Start(pet,target,plan,novel);
    }
    internal bool Start(ExpeditionCompanion pet,DiscoveryInterest item,DiscoveryPlan plan,bool novel,bool invited=false)
    {
        bool firstStudyInvitation=invited && Memory(pet).Find(item.Definition.Id)?.Visits==0;
        if(sessions.ContainsKey(pet.Pet.Id) || pet.Busy || !pet.Pet.State.CanAct || !item.Usable(owner()) ||
            !firstStudyInvitation && !Memory(pet).CanConsider(item.Definition,Now) || plan.Choice==DiscoveryChoice.Ignore)return false;
        var point=plan.Choice==DiscoveryChoice.Approach ? Reservations.Reserve(item,pet,nav,team) : pet.Body.Position;
        if(point==null)return false;
        Memory(pet).Notice(item.Definition.Id,Now);
        sessions[pet.Pet.Id]=new(pet,item,plan,point.Value,novel);Log(pet,item,plan.Choice==DiscoveryChoice.Approach ? "approach" : "observe");return true;
    }
    internal void Present(ExpeditionCompanion pet,Vector3 player,float dt)
    { if(!pet.Transitioning && sessions.TryGetValue(pet.Pet.Id,out var session))DiscoveryPresentation.Pose(session,player,dt); }
    internal bool Complete(DiscoverySession session)
    {
        var pet=session.Pet;var item=session.Item.Definition;
        bool completed=Memory(pet).Complete(item,pet.Pet.State,Now);
        if(!completed)return false;
        Log(pet,session.Item,"studied");
        foreach(var peer in team.Where(p=>p!=pet && ExpeditionCompanion.Flat(p.Body.Position,pet.Body.Position)<5))
        {
            var record=Memory(peer).Find(item.Id);var relation=owner().SocialLink(pet.Pet.Id,peer.Pet.Id);
            if(record?.Visits>0 && Now-record.StudiedAt<90 && relation?.Met==true && relation.ScoreFrom(pet.Pet.Id)>=0 && relation.ScoreFrom(peer.Pet.Id)>=0)
            {
                relation.SetScoreFrom(pet.Pet.Id,relation.ScoreFrom(pet.Pet.Id)+Memory(pet).ShareSuccess(item.Id,peer.Pet.Id));
                relation.SetScoreFrom(peer.Pet.Id,relation.ScoreFrom(peer.Pet.Id)+Memory(peer).ShareSuccess(item.Id,pet.Pet.Id));
            }
        }
        return true;
    }
    internal bool TryAttention(DiscoverySession session)
    {
        var memory=Memory(session.Pet);
        if(!session.Completed || Now-lastAttention<60 || Now-memory.LastAttention<120)return false;
        lastAttention=memory.LastAttention=Now;Log(session.Pet,session.Item,"called player");return true;
    }
    internal bool Invite(DiscoverySession source)
    {
        bool Watching(ExpeditionCompanion p)=>sessions.TryGetValue(p.Pet.Id,out var s) && s.Item==source.Item && s.Plan.Choice==DiscoveryChoice.Observe && s.Phase==DiscoveryPhase.Observe;
        foreach(var peer in team.Where(p=>p!=source.Pet && (!p.Busy && !sessions.ContainsKey(p.Pet.Id) || Watching(p)) && p.ReadyForDiscovery &&
            p.Pet.State.Energy>30 && ExpeditionCompanion.Flat(p.Body.Position,source.Pet.Body.Position)<7)
            .OrderByDescending(p=>p.Pet.State.Sociability+p.Pet.State.Curiosity))
        {
            var relation=owner().SocialLink(source.Pet.Pet.Id,peer.Pet.Id);
            if(relation?.Met!=true || relation.ScoreFrom(source.Pet.Pet.Id)<0 || relation.ScoreFrom(peer.Pet.Id)<0 ||
                Now-Memory(peer).LastInvitation<90 || !(Memory(peer).Find(source.Item.Definition.Id)?.Visits==0 || Memory(peer).CanConsider(source.Item.Definition,Now)) || !Perception.Visible(peer,source.Item,false))continue;
            Memory(peer).LastInvitation=Now;
            var plan=DiscoveryDecisions.Choose(source.Item.Definition,peer.Pet.State,Memory(peer),Now,peer.Random,true) with { SharePlayer=false,InvitePeer=false };
            bool novel=Memory(peer).Find(source.Item.Definition.Id)==null;
            if(plan.Choice==DiscoveryChoice.Ignore){Memory(peer).Notice(source.Item.Definition.Id,Now);Log(peer,source.Item,"declined invitation");return true;}
            source.NoticePeer(peer.Body.Position+Vector3.Up*.3f);
            if(Watching(peer))
            {
                if(plan.Choice==DiscoveryChoice.Observe)
                {
                    sessions[peer.Pet.Id].NoticePeer(source.Pet.Body.Position+Vector3.Up*.3f);
                    Log(source.Pet,source.Item,"invited peer");ChaoSounds.Play(source.Pet.Art,"voice");return true;
                }
                Cancel(peer,"joining peer");
            }
            if(Start(peer,source.Item,plan,novel,true)) { Log(source.Pet,source.Item,"invited peer");ChaoSounds.Play(source.Pet.Art,"voice");return true; }
        }
        return false;
    }
    internal void Cancel(ExpeditionCompanion pet,string reason,bool restore=true)
    {
        if(!sessions.Remove(pet.Pet.Id,out var session))return;
        Reservations.Release(pet.Pet.Id);DiscoveryPresentation.Clear(pet);Log(pet,session.Item,"cancelled: "+reason);deferred[pet.Pet.Id]=Now+12;
        if(restore)resume(pet);
    }
    internal void CancelAll(string reason)
    {
        foreach(var session in sessions.Values.ToArray())Cancel(session.Pet,reason);
        foreach(var pet in team)deferred[pet.Pet.Id]=Now+12;
    }
    private void Finish(ExpeditionCompanion pet)
    {
        var session=sessions[pet.Pet.Id];Reservations.Release(pet.Pet.Id);sessions.Remove(pet.Pet.Id);
        DiscoveryPresentation.Clear(pet);Log(pet,session.Item,"finished");deferred[pet.Pet.Id]=Now+45;resume(pet);
    }
}
