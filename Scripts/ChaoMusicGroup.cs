using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class ChaoMusicGroup
{
    private ChaoIntroductions.Candidate? visitor,leader;
    private ToyCar? instrument,leadInstrument;
    private int stage;
    private float time,scan=3,lease;
    private readonly ChaoMotion motion=new();
    private readonly ChaoWander steer=new();
    private Vector3 stand;
    public bool Uses(CharacterBody3D actor) => visitor?.Actor==actor;
    internal bool Active => visitor!=null;
    internal string? LeaderId => leader?.Id;
    internal string? VisitorId => visitor?.Id;
    internal void RebindPlayer(CharacterBody3D actor,PetState state,Node3D art)
    {if(visitor?.Actor==actor)visitor=visitor with{Pet=state,Art=art};if(leader?.Actor==actor)leader=leader with{Pet=state,Art=art};}
    private static bool Compatible(PetState root,ChaoIntroductions.Candidate musician,ChaoIntroductions.Candidate candidate)=>
        GodotObject.IsInstanceValid(candidate.Actor) && GodotObject.IsInstanceValid(musician.Actor) &&
        candidate.Actor.IsInsideTree() && musician.Actor.IsInsideTree() && !candidate.Actor.IsQueuedForDeletion() && !musician.Actor.IsQueuedForDeletion() &&
        candidate.Pet.CanAct && musician.Pet.CanAct && candidate.Actor!=musician.Actor && candidate.Pet.Fun<100 && candidate.Pet.Energy>=35 && candidate.Pet.Fullness>=28 &&
        candidate.Actor.Position.DistanceTo(musician.Actor.Position)<=5 && Mathf.Abs(candidate.Actor.Position.Y-musician.Actor.Position.Y)<=.15f &&
        (root.SocialLink(candidate.Id,musician.Id)?.ScoreFrom(candidate.Id) ?? 0)>=0 && Clear(candidate.Actor,musician.Actor.Position);
    internal int JoinTargetCount(PetState root,ChaoIntroductions.Candidate candidate,IReadOnlyList<ChaoIntroductions.Candidate> all,IEnumerable<ToyCar> toys)
    {
        if(Active)return 0;var props=toys.ToArray();
        return Options(root,candidate,all,props).Count;
    }
    private sealed record JoinOption(ChaoIntroductions.Candidate Musician,ToyCar Lead,ToyCar Prop);
    private static bool FreeInstrument(ToyCar prop,ChaoIntroductions.Candidate candidate)=>
        GodotObject.IsInstanceValid(prop) && prop.IsInstrument && prop.CanPickUp && prop.PlayActor==null &&
        prop.Position.DistanceTo(candidate.Actor.Position)<8 && !GardenArt.IsPondWater(prop.Position) && Clear(candidate.Actor,prop.Position);
    private static List<JoinOption> Options(PetState root,ChaoIntroductions.Candidate candidate,IReadOnlyList<ChaoIntroductions.Candidate> all,ToyCar[] props)
    {
        var options=new List<JoinOption>();
        foreach(var lead in props.Where(t=>t.CanJoin))
        {
            var musician=all.FirstOrDefault(c=>c.Actor==lead.PlayActor && c.Pet.CanAct);
            if(musician==null || !Compatible(root,musician,candidate))continue;
            var prop=props.Where(p=>FreeInstrument(p,candidate)).OrderBy(p=>p.Position.DistanceSquaredTo(candidate.Actor.Position)).FirstOrDefault();
            if(prop!=null)options.Add(new(musician,lead,prop));
        }
        return options;
    }

    public void Cancel(CharacterBody3D? actor=null)
    {
        if (actor!=null && !Uses(actor)) return;
        if (visitor!=null && GodotObject.IsInstanceValid(visitor.Art))
        {
            ImportedChao.EndThink(visitor.Art);
            visitor.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(null,false);
            ImportedChao.MovePose(visitor.Art,0,false,false,false,false,false,false);
        }
        if (instrument!=null && GodotObject.IsInstanceValid(instrument)) instrument.Toss(Vector3.Zero);
        visitor=leader=null; instrument=leadInstrument=null; scan=4;
    }

    internal bool TryJoin(PetState root,IReadOnlyList<ChaoIntroductions.Candidate> all,
        IReadOnlyList<ChaoIntroductions.Candidate> free,IEnumerable<ToyCar> toys)
    {
        if (Active) return false;
        var props=toys.ToArray();
        foreach (var candidate in free)
        {
            if(!all.Any(c=>c.Id==candidate.Id && c.Actor==candidate.Actor))continue;
            var options=Options(root,candidate,all,props);
            while(options.Count>0)
            {
                var id=candidate.Pet.SocialSelection.Select(root,candidate.Id,candidate.Pet,
                    options.Select(o=>new SocialPartnerCandidate(o.Musician.Id,o.Musician.Actor.Position.DistanceTo(candidate.Actor.Position))),"Music");
                var option=options.FirstOrDefault(o=>o.Musician.Id==id);if(option==null)break;
                options.RemoveAll(o=>o.Musician.Id==id);
                if(!option.Lead.CanJoin || !Compatible(root,option.Musician,candidate) || !FreeInstrument(option.Prop,candidate))continue;
                visitor=candidate; leader=option.Musician; instrument=option.Prop; leadInstrument=option.Lead;
                instrument.Hold(instrument.Position); stage=0; time=0; lease=90; motion.Reset();
                stand=leader.Actor.Position+leader.Actor.Basis.X*(candidate.Actor.Position.X<leader.Actor.Position.X ? -.78f : .78f);
                return true;
            }
        }
        return false;
    }

    public void Advance(float dt,PetState root,IReadOnlyList<ChaoIntroductions.Candidate> all,
        IReadOnlyList<ChaoIntroductions.Candidate> free,IEnumerable<ToyCar> toys,System.Action<ChaoIntroductions.Candidate>? reserve=null,bool allowStart=true)
    {
        if (!Active)
        {
            if(!allowStart)return;
            scan-=dt;
            if (scan>0) return;
            scan=3;
            if (GD.Randf()>.38f || !TryJoin(root,all,free,toys)) return;
            reserve?.Invoke(visitor!);
        }
        if (visitor==null || leader==null || instrument==null || leadInstrument==null) return;
        if (!GodotObject.IsInstanceValid(visitor.Art) || !GodotObject.IsInstanceValid(leader.Art) ||
            !all.Any(c=>c.Id==leader.Id && c.Actor==leader.Actor) || !all.Any(c=>c.Id==visitor.Id && c.Actor==visitor.Actor) ||
            !leadInstrument.Performing || !visitor.Pet.CanAct || !leader.Pet.CanAct || GardenArt.IsPondWater(visitor.Actor.Position) || (lease-=dt)<=0)
        { Cancel(); return; }
        time+=dt;
        var orb=visitor.Art.GetNodeOrNull<EmotionOrb>("MoodOrb");
        if (stage==0)
        {
            ImportedChao.IdeaPose(visitor.Art);
            orb?.SetIdea(ChaoThought.Glyph(time),ChaoThought.Inspired(time));
            if (time>=ChaoThought.IdeaEnd) { ImportedChao.EndThink(visitor.Art); orb?.SetIdea("",true); stage=1; time=0; }
            return;
        }
        var target=stage==1 ? instrument.Position : stand;
        var delta=target-visitor.Actor.Position; delta.Y=0;
        bool arrived=delta.Length()<(stage==1 ? .30f : .19f);
        if (!arrived)
        {
            var heading=steer.Steer(visitor.Actor,delta,dt);
            bool moved=motion.Step(visitor.Actor,heading,visitor.Pet,false,false,dt,false,out _);
            ImportedChao.MovePose(visitor.Art,time*10,moved,false,false,false,motion.Airborne,false);
        }
        if (stage>=2)
        {
            instrument.Hold(visitor.Actor.Position+visitor.Actor.Basis.Z*.16f+Vector3.Up*.11f);
            ImportedChao.ReceivePose(visitor.Art,.8f);
        }
        if (!arrived) return;
        if (stage==1) { stage=2; time=0; return; }
        visitor.Actor.Rotation=new(0,leader.Actor.Rotation.Y,0);
        var propToPlay=instrument;
        var conductor=leadInstrument;
        var actor=visitor;
        var musicianId=leader.Id;
        instrument=null; visitor=leader=null; leadInstrument=null; scan=4;
        actor.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea("",true);
        if (!propToPlay.BeginPlay(actor.Actor,actor.Pet)) propToPlay.Toss(Vector3.Zero);
        else { propToPlay.StartWith(conductor,actor.Id,musicianId); propToPlay.PlayPose(actor.Actor,actor.Art); }
    }

    private static bool Clear(CharacterBody3D actor,Vector3 to) => actor.GetWorld3D().DirectSpaceState.IntersectRay(
        PhysicsRayQueryParameters3D.Create(actor.Position+Vector3.Up*.2f,to+Vector3.Up*.2f,1)).Count==0;
}
