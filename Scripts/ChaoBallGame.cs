using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

// Owns both participants only while a ball interaction is in progress.
internal sealed class ChaoBallGame
{
    private enum Phase { Consider, Approach, Offer, Answer, Refuse, Sad, Separate, Toss, Hit, Fetch, Return, Solo, Finish }
    private ChaoIntroductions.Candidate? a,b;
    private ToyCar? ball;
    private Phase phase;
    private float time,total,voice,arc;
    private Vector3 start,end,spotA,spotB;
    private bool towardB=true,miss,scold,polite;
    private int passes;
    internal bool Active => a!=null;
    internal string Stage => phase.ToString();
    public bool Uses(CharacterBody3D actor) => a?.Actor==actor || b?.Actor==actor;

    public void Cancel(CharacterBody3D? actor=null)
    {
        if (actor!=null && !Uses(actor)) return;
        foreach (var member in new[]{a,b})
            if (member!=null && GodotObject.IsInstanceValid(member.Art))
            {
                ImportedChao.ClearBallReaction(member.Art);
                Orb(member)?.SetIdea(null,false);
            }
        if (ball!=null && GodotObject.IsInstanceValid(ball)) ball.FinishBall();
        a=b=null; ball=null;
    }

    public void Advance(float dt,PetState owner,IReadOnlyList<ChaoIntroductions.Candidate> all,
        IReadOnlyList<ChaoIntroductions.Candidate> available,IEnumerable<ToyCar> toys)
    {
        if (a==null)
        {
            ball=toys.FirstOrDefault(t=>t.IsBeachBall && t.PlayActor!=null);
            if (ball==null) return;
            a=all.FirstOrDefault(c=>c.Actor==ball.PlayActor);
            if (a==null) { ball.FinishBall(); ball=null; return; }
            time=total=voice=0; passes=0; towardB=true; scold=polite=false;
            phase=a.Pet.Sociability>50 ? Phase.Consider : Phase.Solo;
        }
        if (!Valid(a) || ball==null || !GodotObject.IsInstanceValid(ball) || ball.PlayActor!=a.Actor ||
            b!=null && !Valid(b)) { Cancel(); return; }
        time+=dt; total+=dt; voice-=dt;
        if (total>70 || (a.Pet.Energy<8 || b?.Pet.Energy<8)) { Place(); Cancel(); return; }
        switch (phase)
        {
            case Phase.Consider:
                Carry(a); ImportedChao.IdeaPose(a.Art); Orb(a)?.SetIdea("?",true);
                if (time<ChaoThought.Minimum) break;
                b=available.Where(c=>c.Actor!=a.Actor && c.Pet.CanAct && c.Pet.Fun<100 &&
                    owner.SocialLink(a.Id,c.Id) is { Met:true } link && link.ScoreFrom(a.Id)>=0 &&
                    c.Actor.Position.DistanceTo(a.Actor.Position)<5 &&
                    Mathf.Abs(c.Actor.Position.Y-a.Actor.Position.Y)<.16f && ClearPath(a.Actor,c.Actor.Position))
                    .OrderBy(c=>c.Actor.Position.DistanceSquaredTo(a.Actor.Position)).FirstOrDefault();
                Orb(a)?.SetIdea("",true); ImportedChao.EndThink(a.Art);
                Next(b==null ? Phase.Solo : Phase.Approach);
                break;
            case Phase.Approach:
                Face(a,b!.Actor.Position,dt); Carry(a); Normal(b);
                if (Walk(a,b.Actor.Position,.48f,dt)) Next(Phase.Offer);
                else if (time>9) { Place(); Cancel(); }
                break;
            case Phase.Offer:
                Face(a,b!.Actor.Position,dt); Face(b,a.Actor.Position,dt);
                Carry(a); ImportedChao.ReceivePose(a.Art,1); Happy(a);
                if (voice<=0) { ChaoSounds.Play(a.Art,"voice"); voice=.9f; }
                ImportedChao.IdeaPose(b.Art); LookAtPeer(b);
                Orb(b)?.SetIdea("?",false);
                if (time>=ChaoThought.Minimum)
                {
                    var relation=owner.SocialLink(a.Id,b.Id)!;
                    scold=relation.ScoreFrom(b.Id)<0;
                    bool tired=b.Pet.Energy<35,bad=b.Pet.Joy<30;
                    polite=!scold && tired && bad && b.Pet.Manners>50;
                    Orb(b)?.SetIdea("",false); ImportedChao.EndThink(b.Art);
                    Next(scold || tired || bad ? Phase.Refuse : Phase.Answer);
                }
                break;
            case Phase.Answer:
                Carry(a); Happy(a); Normal(b!); Happy(b!);
                if (b!.Art.FindChild("HeadPivot",true,false) is Node3D head)
                    head.Rotation=new(Mathf.Sin(time*9)*.22f,0,0);
                if (voice<=0) { ChaoSounds.Play(b.Art,"voice"); voice=.8f; }
                if (time>=2)
                {
                    var axis=(b.Actor.Position-a.Actor.Position).Normalized(); axis.Y=0;
                    spotA=a.Actor.Position-axis*.65f; spotB=b.Actor.Position+axis*.65f;
                    if (!ClearPath(a.Actor,spotA) || !ClearPath(b.Actor,spotB)) { Place(); Cancel(); break; }
                    Next(Phase.Separate);
                }
                break;
            case Phase.Refuse:
                Carry(a); Orb(a)?.SetIdea("",false);
                Orb(b!)?.SetIdea(polite ? "" : "spring",false);
                // The orb uses the original spring via its timed upset reaction.
                if (!polite) Orb(b!)?.ReactUpset(.12f);
                ImportedChao.BallReaction(b!.Art,time,scold,polite,false);
                if (voice<=0) { ChaoSounds.Play(b.Art,scold ? "angry" : "voice"); voice=.8f; }
                if (time>2.2f) b.Actor.MoveAndCollide((b.Actor.Position-a.Actor.Position).Normalized()*dt*.3f);
                if (time>=3)
                {
                    a.Pet.Joy=Mathf.Max(0,a.Pet.Joy-(scold ? 12 : polite ? 3 : 5));
                    ImportedChao.ClearBallReaction(b.Art); Orb(b)?.SetIdea(null,false);
                    b=null; Place(); Next(Phase.Sad);
                }
                break;
            case Phase.Sad:
                Orb(a)?.SetIdea("",false);
                ImportedChao.BallReaction(a.Art,time,false,false,scold);
                if (scold && voice<=0) { ChaoSounds.Play(a.Art,"sniff"); voice=1.4f; }
                if (time>=(scold ? 6 : 2)) Cancel();
                break;
            case Phase.Separate:
                Carry(a); Happy(a); Happy(b!);
                bool readyA=Walk(a,spotA,.04f,dt),readyB=Walk(b!,spotB,.04f,dt);
                Face(a,b!.Actor.Position,dt); Face(b,a.Actor.Position,dt);
                if (readyA && readyB) BeginToss();
                else if (time>5) { Place(); Cancel(); }
                break;
            case Phase.Toss:
                Face(a,b!.Actor.Position,dt); Face(b,a.Actor.Position,dt);
                var receiver=towardB ? b : a;
                Normal(a); Normal(b); Happy(a); Happy(b);
                float u=Mathf.Clamp(time/1.1f,0,1);
                bool athletic=receiver!.Pet.Dexterous && receiver.Pet.ActivityPreference>=4;
                ball.Position=start.Lerp(end,u)+Vector3.Up*(Mathf.Sin(u*Mathf.Pi)*arc);
                ball.RotateY(dt*3);
                ImportedChao.ReceivePose(receiver.Art,.85f);
                var sender=towardB ? a : b;
                if (time<.3f) ImportedChao.BallHitPose(sender!.Art,start,time/.3f);
                if (athletic) receiver.Art.Position+=Vector3.Up*(Mathf.Sin(Mathf.Clamp((u-.6f)/.4f,0,1)*Mathf.Pi)*.17f);
                if (voice<=0) { ChaoSounds.Play(receiver.Art,"voice"); voice=3.1f; }
                if (u>=1)
                {
                    if (miss)
                    {
                        ChaoSounds.Play(ball,"ball"); start=end;
                        var rolled=end+Vector3.Right*.28f;
                        if (ClearPath(receiver.Actor,rolled)) end=rolled;
                        Next(Phase.Fetch);
                    }
                    else Next(Phase.Hit);
                }
                break;
            case Phase.Hit:
                receiver=towardB ? b! : a;
                Normal(a); Normal(b!); Happy(a); Happy(b!);
                ball.Position=receiver!.Actor.Position+Forward(receiver)*.18f+Vector3.Up*.23f;
                ImportedChao.BallHitPose(receiver.Art,ball.GlobalPosition,time/.32f);
                if (time>=.32f) { ChaoSounds.Play(ball,"ball"); CompletePass(); }
                break;
            case Phase.Fetch:
                receiver=towardB ? b! : a;
                Normal(towardB ? a : b!); Happy(a); Happy(b!);
                float roll=Mathf.Clamp(time/.7f,0,1);
                ball.Position=start.Lerp(end,Mathf.Sin(roll*Mathf.Pi*.5f));
                ball.RotateX(dt*(1-roll)*6);
                if (roll>=1 && Walk(receiver!,end,.20f,dt)) { Next(Phase.Return); }
                else if (time>7) { Place(); Cancel(); }
                break;
            case Phase.Return:
                receiver=towardB ? b! : a;
                Carry(receiver!);
                if (Walk(receiver!,towardB ? spotB : spotA,.05f,dt)) CompletePass();
                else if (time>7) { Place(); Cancel(); }
                break;
            case Phase.Solo:
                a.Pet.Fun=Mathf.Min(100,a.Pet.Fun+dt*4);
                Normal(a); Happy(a); ImportedChao.ReceivePose(a.Art,.7f);
                ball.Position=a.Actor.Position+Forward(a)*.22f+Vector3.Up*(.025f+Mathf.Abs(Mathf.Sin(time*3))*.20f);
                if (voice<=0) { ChaoSounds.Play(ball,"ball"); voice=1.05f; }
                if (time>=8) { Place(); Cancel(); }
                break;
            case Phase.Finish:
                if (time>.6f) { Place(); Cancel(); }
                break;
        }
    }

    private void BeginToss()
    {
        var sender=towardB ? a! : b!; var receiver=towardB ? b! : a!;
        arc=sender.Pet.Dexterous && sender.Pet.ActivityPreference>=4 ? .85f : .42f;
        start=sender.Actor.Position+Forward(sender)*.18f+Vector3.Up*.23f;
        miss=GD.Randf()<PetState.BallMissChance(receiver.Pet);
        end=receiver.Actor.Position+Vector3.Up*.23f;
        if (miss)
        {
            end=receiver.Actor.Position+receiver.Actor.Basis.X*(GD.Randf()<.5f ? -.42f : .42f);
            var hit=receiver.Actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(end+Vector3.Up*.5f,end+Vector3.Down*.6f,1));
            if (hit.Count==0 || !ClearPath(receiver.Actor,end)) { miss=false; end=receiver.Actor.Position+Vector3.Up*.23f; }
            else end=(Vector3)hit["position"];
        }
        ChaoSounds.Play(ball!,"ball"); Next(Phase.Toss);
    }
    private void CompletePass()
    {
        a!.Pet.Fun=Mathf.Min(100,a.Pet.Fun+3); b!.Pet.Fun=Mathf.Min(100,b.Pet.Fun+3);
        towardB=!towardB;
        if (++passes>=10) Next(Phase.Finish); else BeginToss();
    }
    private void Place()
    {
        if (ball==null || a==null) return;
        var point=a.Actor.Position+Forward(a)*.72f;
        var hit=a.Actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point+Vector3.Up*.6f,point+Vector3.Down*.7f,1));
        ball.Position=hit.Count>0 ? (Vector3)hit["position"] : a.Actor.Position+Forward(a)*.35f;
    }
    private void Carry(ChaoIntroductions.Candidate c)
    {
        Normal(c); ImportedChao.ReceivePose(c.Art,.85f);
        ball!.Position=c.Actor.Position+Forward(c)*.20f+Vector3.Up*.06f;
    }
    private static bool Walk(ChaoIntroductions.Candidate c,Vector3 target,float reach,float dt)
    {
        var delta=target-c.Actor.Position; delta.Y=0;
        if (delta.Length()<=reach+.002f) { Normal(c); return true; }
        Face(c,target,dt);
        c.Actor.MoveAndCollide(delta.Normalized()*Mathf.Min(delta.Length()-reach,dt*(.36f+Mathf.Sqrt(c.Pet.Run)*.039f)));
        ImportedChao.MovePose(c.Art,(float)Time.GetTicksMsec()*.008f,true,false,false,false,false,false);
        return false;
    }
    private static bool ClearPath(CharacterBody3D body,Vector3 to) => body.GetWorld3D().DirectSpaceState.IntersectRay(
        PhysicsRayQueryParameters3D.Create(body.GlobalPosition+Vector3.Up*.2f,to+Vector3.Up*.2f,1)).Count==0;
    private static void Face(ChaoIntroductions.Candidate c,Vector3 at,float dt)
    {
        var delta=at-c.Actor.Position;
        c.Actor.Rotation=new(0,Mathf.LerpAngle(c.Actor.Rotation.Y,Mathf.Atan2(delta.X,delta.Z),1-Mathf.Exp(-10*dt)),0);
    }
    private static void LookAtPeer(ChaoIntroductions.Candidate c)
    {
        if (c.Art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=new(-.08f,0,0);
    }
    private static Vector3 Forward(ChaoIntroductions.Candidate c) => Vector3.Back.Rotated(Vector3.Up,c.Actor.Rotation.Y);
    private static EmotionOrb? Orb(ChaoIntroductions.Candidate c) => c.Art.GetNodeOrNull<EmotionOrb>("MoodOrb");
    private static bool Valid(ChaoIntroductions.Candidate c) => GodotObject.IsInstanceValid(c.Actor) && GodotObject.IsInstanceValid(c.Art) && c.Pet.CanAct;
    private static void Happy(ChaoIntroductions.Candidate c) => Orb(c)?.SetIdea("",true);
    private static void Normal(ChaoIntroductions.Candidate c) => ImportedChao.MovePose(c.Art,0,false,false,false,false,false,false);
    private void Next(Phase next) { phase=next; time=0; voice=0; }
}
