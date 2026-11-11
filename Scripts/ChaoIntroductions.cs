using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

internal sealed class ChaoIntroductions
{
    internal sealed record Candidate(string Id,CharacterBody3D Actor,Node3D Art,PetState Pet);
    private enum Phase { Look, Think, Alert, Approach, Talk, Raise, Shake, Lower, Wait }
    private sealed class Meeting(Candidate a,Candidate b)
    {
        public Candidate A=a, B=b;
        public Phase Stage;
        public float Time;
        public int Sounds;
    }

    private Meeting? meeting;
    private float scan;

    public bool Active => meeting!=null;

    public bool Uses(CharacterBody3D actor) => meeting!=null && (meeting.A.Actor==actor || meeting.B.Actor==actor);

    public void Cancel(CharacterBody3D? actor=null)
    {
        if (meeting==null || actor!=null && !Uses(actor)) return;
        ResetPose(meeting.A);
        ResetPose(meeting.B);
        meeting=null;
        scan=2.5f;
    }

    public void Advance(float dt,PetState owner,Func<IReadOnlyList<Candidate>> available,Action save)
    {
        if (meeting==null)
        {
            scan-=dt;
            if (scan>0) return;
            scan=.45f;
            var candidates=available();
            for (int i=0;i<candidates.Count && meeting==null;i++)
                for (int j=i+1;j<candidates.Count;j++)
                {
                    var a=candidates[i]; var b=candidates[j];
                    if (owner.SocialLink(a.Id,b.Id)?.Introduced==true || !CanSee(a.Actor,b.Actor)) continue;
                    meeting=new(a,b);
                    break;
                }
        }
        if (meeting is not { } pair) return;
        if (!GodotObject.IsInstanceValid(pair.A.Art) || !GodotObject.IsInstanceValid(pair.B.Art) ||
            (!pair.A.Pet.CanAct || !pair.B.Pet.CanAct))
        {
            Cancel();
            return;
        }
        var aBody=pair.A.Actor;
        var bBody=pair.B.Actor;
        var between=bBody.Position-aBody.Position;
        between.Y=0;
        float distance=between.Length();
        if (distance>2.8f || Mathf.Abs(aBody.Position.Y-bBody.Position.Y)>.26f) { Cancel(); return; }
        var toward=distance>.001f ? between/distance : Vector3.Back;
        Face(aBody,toward,dt);
        Face(bBody,-toward,dt);
        pair.Time+=dt;
        switch (pair.Stage)
        {
            case Phase.Look:
                Normal(pair.A); Normal(pair.B);
                if (pair.Time>=3) Next(pair,Phase.Think);
                break;
            case Phase.Think:
                ImportedChao.IdeaPose(pair.A.Art); ImportedChao.IdeaPose(pair.B.Art);
                Mood(pair.A,"?"); Mood(pair.B,"?");
                if (pair.Time>=4) Next(pair,Phase.Alert);
                break;
            case Phase.Alert:
                ImportedChao.IdeaPose(pair.A.Art); ImportedChao.IdeaPose(pair.B.Art);
                Mood(pair.A,"!"); Mood(pair.B,"!");
                if (pair.Time>=1.4f)
                {
                    Mood(pair.A,""); Mood(pair.B,"");
                    Normal(pair.A); Normal(pair.B);
                    Next(pair,Phase.Approach);
                }
                break;
            case Phase.Approach:
                if (distance<=.282f)
                {
                    Normal(pair.A); Normal(pair.B);
                    Next(pair,Phase.Talk);
                    break;
                }
                var step=toward*Mathf.Min(dt*.68f,(distance-.275f)*.5f);
                aBody.MoveAndCollide(step);
                bBody.MoveAndCollide(-step);
                ImportedChao.MovePose(pair.A.Art,pair.Time*12,true,false,false,false,false,false);
                ImportedChao.MovePose(pair.B.Art,pair.Time*12,true,false,false,false,false,false);
                if (pair.Time>2.6f) Cancel();
                break;
            case Phase.Talk:
                int voice=Mathf.Min(2,(int)(pair.Time/.35f));
                if (voice>=pair.Sounds)
                {
                    ChaoSounds.Play(voice%2==0 ? pair.A.Art : pair.B.Art,"voice");
                    pair.Sounds++;
                }
                if (pair.Time>=1.05f) Next(pair,Phase.Raise);
                break;
            case Phase.Raise:
            case Phase.Lower:
                float weight=Mathf.SmoothStep(0,1,Mathf.Clamp(pair.Time/.5f,0,1));
                if (pair.Stage==Phase.Lower) weight=1-weight;
                var handPoint=(aBody.GlobalPosition+bBody.GlobalPosition)*.5f+Vector3.Up*.145f;
                ImportedChao.HandshakePose(pair.A.Art,handPoint,weight);
                ImportedChao.HandshakePose(pair.B.Art,handPoint,weight);
                if (pair.Time>=.5f)
                {
                    if (pair.Stage==Phase.Raise) Next(pair,Phase.Shake);
                    else { Mood(pair.A,"",true); Mood(pair.B,"",true); Next(pair,Phase.Wait); }
                }
                break;
            case Phase.Shake:
                var grip=(aBody.GlobalPosition+bBody.GlobalPosition)*.5f+
                    Vector3.Up*(.145f+Mathf.Sin(pair.Time*23)*.026f);
                ImportedChao.HandshakePose(pair.A.Art,grip);
                ImportedChao.HandshakePose(pair.B.Art,grip);
                if (pair.Time>=2.6f)
                {
                    var link=owner.SocialLink(pair.A.Id,pair.B.Id,create:true)!;
                    if (!link.Introduced)
                    {
                        link.Met=link.Introduced=true;
                        link.Score=Math.Clamp(link.Score+4+(pair.A.Pet.Sociability+pair.B.Pet.Sociability)/38,-100,100);
                        save();
                    }
                    Mood(pair.A,"",true);
                    Mood(pair.B,"",true);
                    Next(pair,Phase.Lower);
                }
                break;
            case Phase.Wait:
                if (pair.Time>=2) Cancel();
                break;
        }
    }

    private static bool CanSee(CharacterBody3D a,CharacterBody3D b)
    {
        if (a.Position.DistanceTo(b.Position)>1.95f || Mathf.Abs(a.Position.Y-b.Position.Y)>.16f) return false;
        var ray=PhysicsRayQueryParameters3D.Create(a.GlobalPosition+Vector3.Up*.21f,b.GlobalPosition+Vector3.Up*.21f,1);
        return a.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count==0;
    }

    private static void Face(CharacterBody3D actor,Vector3 toward,float dt)
    {
        float target=Mathf.Atan2(toward.X,toward.Z);
        actor.Rotation=new(0,Mathf.LerpAngle(actor.Rotation.Y,target,1-Mathf.Exp(-11*dt)),0);
    }

    private static void Next(Meeting pair,Phase stage) { pair.Stage=stage; pair.Time=0; }
    private static void Mood(Candidate member,string? glyph,bool happy=false) =>
        member.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(glyph,happy);
    private static void Normal(Candidate member) =>
        ImportedChao.MovePose(member.Art,0,false,false,false,false,false,false);

    private static void ResetPose(Candidate member)
    {
        if (!GodotObject.IsInstanceValid(member.Art)) return;
        Mood(member,null);
        Normal(member);
    }
}
