using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal enum ExpeditionOrder { Follow, Wait }
internal sealed class ExpeditionTeam
{
    internal List<ExpeditionCompanion> Members { get; }=new();
    internal ExpeditionOrder Order { get; private set; }
    internal string Message { get; private set; }="Following · Free to explore";
    private PetState owner;
    private readonly ExpeditionNavigation nav;
    private Vector3 anchor,lastPlayer;
    private float playerSpeed;
    internal ExpeditionTeam(Node3D world,PetState owner,Vector3 player)
    {
        this.owner=owner; nav=new(world); lastPlayer=anchor=player;
        var selected=ExpeditionRoster.Resolve(owner,owner.Expedition.LastTeamIds);
        foreach(var id in selected)
        {
            var entry=ExpeditionRoster.Available(owner).First(p=>p.Id==id);
            var spawn=Spawn(player,Members.Count,Members.Select(m=>m.Body.Position));
            Members.Add(new(world,entry,spawn,Members.Count));
        }
    }
    private Vector3 Spawn(Vector3 center,int slot,IEnumerable<Vector3> occupied)
    {
        var placed=occupied.ToArray();
        for(int attempt=0;attempt<80;attempt++)
        {
            float angle=slot*Mathf.Tau/3+attempt*.63f,radius=1.8f+attempt/12*.8f;
            var candidate=nav.Nearest(center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius);
            if(placed.All(p=>ExpeditionCompanion.Flat(p,candidate)>.75f))return candidate;
        }
        return nav.Nearest(center+Vector3.Left*(4+slot*2));
    }
    internal void RefreshStates(PetState current)
    {
        // The editor replaces a validated state object. Rebind views immediately, without moving actors.
        owner=current;
        foreach(var member in Members)
        {
            if(member.Pet.Id=="main")member.Rebind(new("main","Default",current));
            else if(current.GardenEggs.FirstOrDefault(e=>e.Id==member.Pet.Id && e.Hatched)?.Chao is {} state)
            {
                var egg=current.GardenEggs.First(e=>e.Id==member.Pet.Id);
                member.Rebind(new(egg.Id,egg.Color,state));
            }
        }
    }
    internal void Command(string command,Vector3 player)
    {
        if(command=="Wait") { Order=ExpeditionOrder.Wait;anchor=nav.Nearest(player);Message="Wait here · Exploring nearby"; }
        else if(command=="Follow") { Order=ExpeditionOrder.Follow;Message="Follow · Travelling together"; }
        else { Message="Come here · Calling the team";if(Order==ExpeditionOrder.Wait)anchor=nav.Nearest(player); }
        int heard=0;
        foreach(var member in Members)
        {
            if(command=="Call" && (!member.Pet.State.RespondsToCall || member.Sleeping && member.Pet.State.Energy<12))continue;
            if(command=="Call")heard++;
            member.Partner=null;
            if(command=="Wait")member.SetIntent(ExpeditionIntent.Observe,member.Body.Position,3);
            else
            {
                member.SetIntent(ExpeditionIntent.Follow,Formation(member,player),command=="Call" ? 14 : 5);
                member.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.Celebrate();
                ChaoSounds.Play(member.Art,"voice");
            }
        }
        if(command=="Call")Message=$"Come here · {heard}/{Members.Count} responded";
    }
    private Vector3 Formation(ExpeditionCompanion m,Vector3 player)
    {
        int i=Members.IndexOf(m);float angle=i*Mathf.Tau/3+.5f;
        float radius=1.2f+(m.Pet.State.Curiosity+100)*.004f+(100-Mathf.Clamp(m.Pet.State.Bond,0,100))*.009f;
        return nav.Nearest(player+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius);
    }
    internal void Step(float dt,Vector3 player,Camera3D camera)
    {
        playerSpeed=Mathf.Lerp(playerSpeed,ExpeditionCompanion.Flat(player,lastPlayer)/Mathf.Max(dt,.001f),1-Mathf.Exp(-4*dt));lastPlayer=player;
        foreach(var m in Members)
            if(m.Partner is {} other && other.Partner==m && m.Intent==ExpeditionIntent.Social && other.Intent==ExpeditionIntent.Social && !m.WalkedTogether)
            {
                m.SocialApproach+=dt;
                // Allow time for turning, thought and slow crawlers; a failed approach still times out.
                if(m.SocialApproach<55)m.HoldSocial();
            }
        foreach(var m in Members)
        {
            if(!m.Pet.State.CanAct)continue;
            float distance=ExpeditionCompanion.Flat(m.Body.Position,player);
            bool mustRegroup=Order==ExpeditionOrder.Follow && distance>9+(m.Pet.State.Curiosity+100)*.012f;
            if(mustRegroup && !m.Busy && m.Intent!=ExpeditionIntent.Follow)
            { m.Partner=null;m.SetIntent(ExpeditionIntent.Follow,Formation(m,player),10); }
            if(m.Intent==ExpeditionIntent.Follow && Order==ExpeditionOrder.Follow)m.UpdateGoal(Formation(m,player));
            if(m.Remaining<=0 && !m.Busy)Decide(m,player);
            if(m.Partner is {} partner && (partner.Partner!=m || partner.Remaining<=0))m.Partner=null;
            m.Step(dt,nav,camera,player);
        }
        ChaoContacts.Resolve(Members.Select(m=>new ChaoContacts.Member(m.Body,false,m.Sleeping || m.Intent==ExpeditionIntent.Rest ? .28f : .17f)).ToList());
        foreach(var m in Members)
        {
            if(m.Partner is not {} other || string.CompareOrdinal(m.Pet.Id,other.Pet.Id)>=0 ||
                m.Intent!=ExpeditionIntent.Social || other.Intent!=ExpeditionIntent.Social || m.WalkedTogether)continue;
            if(ExpeditionCompanion.Flat(m.Body.Position,m.Goal)>.5f || ExpeditionCompanion.Flat(other.Body.Position,other.Goal)>.5f)continue;
            m.SocialContact+=dt;
            if(m.SocialContact<3)continue;
            var center=(m.Body.Position+other.Body.Position)*.5f;
            var axis=new Vector3(Mathf.Sin((float)m.Random.NextDouble()*Mathf.Tau),0,1).Normalized();
            var target=center+axis*3;
            if(Order==ExpeditionOrder.Wait)target=anchor+(target-anchor).LimitLength(4);
            var side=axis.Cross(Vector3.Up)*.6f;
            m.UpdateGoal(nav.Nearest(target-side));other.UpdateGoal(nav.Nearest(target+side));
            m.WalkedTogether=other.WalkedTogether=true;
        }
    }
    internal void Decide(ExpeditionCompanion m,Vector3 player)
    {
        m.Partner=null;
        var friends=Members.Where(n=>n!=m && !n.Busy && n.Partner==null && ExpeditionCompanion.Flat(n.Body.Position,m.Body.Position)<9 &&
            owner.SocialLink(m.Pet.Id,n.Pet.Id) is { Met:true } link && link.ScoreFrom(m.Pet.Id)>=0 && link.ScoreFrom(n.Pet.Id)>=0).ToArray();
        var intent=ExpeditionDecisions.Choose(m.Pet.State,friends.Length>0,m.Random);
        var center=Order==ExpeditionOrder.Wait ? anchor : player;
        if(Order==ExpeditionOrder.Follow && playerSpeed>1.2f)intent=ExpeditionIntent.Follow;
        if(intent==ExpeditionIntent.Social && friends.Length>0)
        {
            var other=friends[m.Random.Next(friends.Length)];
            bool resting=m.Pet.State.Energy<50 && other.Pet.State.Energy<65;
            var point=nav.Nearest((m.Body.Position+other.Body.Position)*.5f);
            var occupied=Members.Where(n=>n!=m && n!=other).Select(n=>n.Body.Position).ToArray();
            if(occupied.Any(p=>ExpeditionCompanion.Flat(p,point)<1.1f))point=Spawn(point,0,occupied);
            var beside=nav.Nearest(point+Vector3.Right*1.2f);
            if(ExpeditionCompanion.Flat(point,beside)<.75f || occupied.Any(p=>ExpeditionCompanion.Flat(p,beside)<1.1f))
                beside=Spawn(point,1,occupied.Append(point));
            float Pace(PetState s)=>s.Run<50 ? .38f+s.Run*.003f : .95f+s.Run*.0042f;
            float duration=25+ExpeditionCompanion.Flat(m.Body.Position,other.Body.Position)/Mathf.Min(Pace(m.Pet.State),Pace(other.Pet.State));
            m.SetIntent(resting ? ExpeditionIntent.Rest : intent,point,duration,true);
            other.SetIntent(resting ? ExpeditionIntent.Rest : intent,beside,duration,true);
            m.Partner=other;other.Partner=m;
            ChaoSounds.Play(m.Art,"voice");return;
        }
        if(intent==ExpeditionIntent.Follow && Order==ExpeditionOrder.Wait)intent=ExpeditionIntent.Observe;
        Vector3 goal=m.Body.Position;
        if(intent==ExpeditionIntent.Explore)
        {
            float radius=Order==ExpeditionOrder.Wait ? 4 : 3+(m.Pet.State.Curiosity+100)*.035f;
            float angle=(float)m.Random.NextDouble()*Mathf.Tau;
            goal=nav.Nearest(center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius);
            var landmarks=WindfallMeadow.ObservationSpots.Select(p=>nav.Nearest(new(p.X,0,p.Y)))
                .Where(p=>ExpeditionCompanion.Flat(p,center)<radius && ExpeditionCompanion.Flat(p,m.Body.Position)>1).ToArray();
            if(landmarks.Length>0 && m.Random.NextDouble()<.35+(m.Pet.State.Curiosity+100)/400d)
                goal=landmarks[m.Random.Next(landmarks.Length)];
        }
        else if(intent==ExpeditionIntent.Follow)goal=Formation(m,player);
        float seconds=intent==ExpeditionIntent.Sleep ? 30 : intent==ExpeditionIntent.Rest ? 22 : 9+(float)m.Random.NextDouble()*13;
        if(intent==ExpeditionIntent.Observe)seconds+=(100-m.Pet.State.Curiosity)*.035f;
        m.SetIntent(intent,goal,seconds,intent==ExpeditionIntent.Explore);
    }
    internal void PushPlayer(CharacterBody3D player)
    {
        var list=Members.Select(m=>new ChaoContacts.Member(m.Body,false,m.Sleeping || m.Intent==ExpeditionIntent.Rest ? .28f : .17f)).ToList();
        list.Add(new(player,true,.26f));ChaoContacts.Resolve(list);
    }
    internal void Reposition(Vector3 player)
    {
        lastPlayer=player;
        var placed=new List<Vector3>();
        foreach(var m in Members) { m.Body.Position=Spawn(player,placed.Count,placed);placed.Add(m.Body.Position);m.Body.ResetPhysicsInterpolation();m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,3); }
    }
}
