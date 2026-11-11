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
        foreach (var lead in props.Where(t=>t.CanJoin))
        {
            var musician=all.FirstOrDefault(c=>c.Actor==lead.PlayActor);
            if (musician==null) continue;
            foreach (var candidate in free)
            {
                if (candidate.Actor==musician.Actor || candidate.Pet.Fun>=100 || candidate.Pet.Energy<35 || candidate.Pet.Fullness<28 ||
                    candidate.Actor.Position.DistanceTo(musician.Actor.Position)>5 ||
                    Mathf.Abs(candidate.Actor.Position.Y-musician.Actor.Position.Y)>.15f ||
                    (root.SocialLink(candidate.Id,musician.Id)?.ScoreFrom(candidate.Id) ?? 0)<0 ||
                    !Clear(candidate.Actor,musician.Actor.Position)) continue;
                var prop=props.Where(t=>t.IsInstrument && t.CanPickUp && t.PlayActor==null &&
                    t.Position.DistanceTo(candidate.Actor.Position)<8 && !GardenArt.IsPondWater(t.Position))
                    .OrderBy(t=>t.Position.DistanceSquaredTo(candidate.Actor.Position)).FirstOrDefault();
                if (prop==null) continue;
                visitor=candidate; leader=musician; instrument=prop; leadInstrument=lead;
                prop.Hold(prop.Position); stage=0; time=0; lease=35; motion.Reset();
                stand=musician.Actor.Position+musician.Actor.Basis.X*(candidate.Actor.Position.X<musician.Actor.Position.X ? -.78f : .78f);
                return true;
            }
        }
        return false;
    }

    public void Advance(float dt,PetState root,IReadOnlyList<ChaoIntroductions.Candidate> all,
        IReadOnlyList<ChaoIntroductions.Candidate> free,IEnumerable<ToyCar> toys)
    {
        if (!Active)
        {
            scan-=dt;
            if (scan>0) return;
            scan=3;
            if (GD.Randf()>.38f || !TryJoin(root,all,free,toys)) return;
        }
        if (visitor==null || leader==null || instrument==null || leadInstrument==null) return;
        if (!GodotObject.IsInstanceValid(visitor.Art) || !GodotObject.IsInstanceValid(leader.Art) ||
            !leadInstrument.CanJoin || !visitor.Pet.CanAct || GardenArt.IsPondWater(visitor.Actor.Position) || (lease-=dt)<=0)
        { Cancel(); return; }
        time+=dt;
        var orb=visitor.Art.GetNodeOrNull<EmotionOrb>("MoodOrb");
        if (stage==0)
        {
            ImportedChao.IdeaPose(visitor.Art);
            orb?.SetIdea(time<4 ? "?" : time<4.5f ? "" : "!",time>=4.5f);
            if (time>=6.7f) { ImportedChao.EndThink(visitor.Art); orb?.SetIdea("",true); stage=1; time=0; }
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
        instrument=null; visitor=leader=null; leadInstrument=null; scan=4;
        actor.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea("",true);
        if (!propToPlay.BeginPlay(actor.Actor,actor.Pet)) propToPlay.Toss(Vector3.Zero);
        else { propToPlay.StartWith(conductor); propToPlay.PlayPose(actor.Actor,actor.Art); }
    }

    private static bool Clear(CharacterBody3D actor,Vector3 to) => actor.GetWorld3D().DirectSpaceState.IntersectRay(
        PhysicsRayQueryParameters3D.Create(actor.Position+Vector3.Up*.2f,to+Vector3.Up*.2f,1)).Count==0;
}
