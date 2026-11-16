using System;
using System.Numerics;

namespace Mori;

public enum KarateAction { Wait,Approach,Attack,Guard,Dodge,Retreat,Meditate,Hit,Knockdown,Rise,Recover }
public sealed class KarateFighter
{
    public string Id {get;}
    public string Color {get;}
    public PetState Pet {get;}
    public Vector2 Position {get;internal set;}
    public Vector2 Facing {get;internal set;}
    public float MaxHealth=>55+Pet.Stamina*.10f;
    public float Health {get;internal set;}
    public const float MaxZeal=100;
    public float Zeal {get;internal set;}
    public KarateAction Action {get;internal set;}
    public float ActionTime {get;internal set;}
    public float Duration {get;internal set;}=.7f;
    public float Swing=>Action==KarateAction.Attack ? ActionTime/Math.Max(.01f,Duration) : 0;
    public int AttackKind {get;internal set;}
    public int Hits {get;internal set;}
    public int Misses {get;internal set;}
    public int Blocks {get;internal set;}
    public int Dodges {get;internal set;}
    public int Knockdowns {get;internal set;}
    public int Meditations {get;internal set;}
    public float Travelled {get;internal set;}
    public float CheerReaction {get;internal set;}
    public int Impact {get;internal set;}
    public Vector2 ImpactPoint {get;internal set;}
    public Vector2 StrikeOrigin {get;internal set;}
    public Vector2 StrikeFacing {get;internal set;}
    public int StrikeKind {get;internal set;}
    public bool StrikeMissed {get;internal set;}
    public float StrikeAge {get;internal set;}=100;
    public float OutsideHeight {get;internal set;}
    public float FallVelocity {get;internal set;}
    public bool RingFalling {get;internal set;}
    public Vector2 Knockback=>Push;
    internal bool Struck,NeedsMeditation;
    internal int AttackSerial,ObservedAttack,PreviousMove=-1,MoveRepeats;
    internal Vector2 Push;
    internal Random Random;
    public KarateFighter(string id,string color,PetState pet,Vector2 at,int seed)
    {Id=id;Color=color;Pet=pet;Position=at;Facing=at.LengthSquared()>.0001f ? Vector2.Normalize(-at) : Vector2.UnitX;Health=MaxHealth;Zeal=60+Math.Clamp(pet.Energy,0,100)*.4f;Random=new(seed);Duration=.5f+(float)Random.NextDouble()*.3f;}
}
public sealed class ChaoKarate
{
    public const float HalfExtent=1.2f,ContactRange=.43f;
    public KarateFighter A {get;}
    public KarateFighter B {get;}
    public float Time {get;private set;}
    public float Countdown=>Math.Max(0,3-Time);
    public float Remaining=>Math.Max(0,90-Math.Max(0,Time-3));
    public bool Finished {get;private set;}
    public string? Winner {get;private set;}
    public string Method {get;private set;}="";
    public float CheerCooldown {get;private set;}
    public ChaoKarate(KarateFighter a,KarateFighter b){A=a;B=b;}
    public static float AttackDuration(PetState pet)=>1.55f-pet.Run/990f*.85f;
    public static float ZealDrain(PetState pet)=>1.6f*(1-pet.Intelligence/990f*.70f);
    public static float MissCost(PetState pet)=>9*(1-pet.Stamina/990f*.70f);
    public static float Defence(PetState pet)=>1+pet.Swim/990f*1.8f;
    public static float CheerRecovery(PetState pet)=>4+pet.Luck/990f*4;
    public bool Cheer(string id)
    {
        var fighter=A.Id==id ? A : B.Id==id ? B : null;
        if(fighter==null || Finished || Countdown>0 || CheerCooldown>0 || fighter.Zeal>=KarateFighter.MaxZeal)return false;
        fighter.CheerReaction=.65f;fighter.Zeal=Math.Min(KarateFighter.MaxZeal,fighter.Zeal+CheerRecovery(fighter.Pet));CheerCooldown=.12f;return true;
    }
    public static double Weight(KarateAction action,KarateFighter self,KarateFighter other)
    {
        float smallPersonality=(self.Pet.Aggressiveness/100f+self.Pet.Initiative/990f-.5f)*.08f;
        bool danger=other.Action==KarateAction.Attack && other.Swing<.46f;
        return action switch{
            KarateAction.Attack=>4.5+smallPersonality,
            KarateAction.Guard=>.45+self.Pet.Swim/990f*.6+(danger ? 1.4 : 0),
            KarateAction.Dodge=>.15+self.Pet.Fly/990f*.65+(danger ? .9 : 0),
            KarateAction.Retreat=>.08,
            _=>.18};
    }
    private static void Begin(KarateFighter f,KarateAction action,float duration)
    {
        f.Action=action;f.ActionTime=0;f.Duration=duration;f.Struck=false;
        if(action==KarateAction.Attack){f.AttackSerial++;int moves=f.Pet.Power>=650 ? 5 : f.Pet.Power>=300 ? 4 : 3;f.AttackKind=f.Random.Next(moves);if(f.AttackKind==f.PreviousMove && f.MoveRepeats>=2)f.AttackKind=(f.AttackKind+1)%moves;f.MoveRepeats=f.AttackKind==f.PreviousMove ? f.MoveRepeats+1 : 1;f.PreviousMove=f.AttackKind;}
        if(action==KarateAction.Meditate)f.Meditations++;
    }
    public void Step(float dt)
    {
        if(!float.IsFinite(dt) || dt<=0)return;
        if(Finished){Settle(A,dt);Settle(B,dt);return;}
        while(dt>0){float step=Math.Min(dt,1f/120);Advance(step);dt-=step;if(Finished)break;}
    }
    private void Advance(float dt)
    {
        Time+=dt;CheerCooldown=Math.Max(0,CheerCooldown-dt);if(Countdown>0)return;
        var oldA=A.Position;var oldB=B.Position;Act(A,B,dt);Act(B,A,dt);A.Travelled+=Vector2.Distance(oldA,A.Position);B.Travelled+=Vector2.Distance(oldB,B.Position);
        var delta=B.Position-A.Position;float distance=delta.Length();
        if(distance<.31f){var normal=distance<.001f ? Vector2.UnitX : delta/distance;A.Position-=normal*(.31f-distance)*.5f;B.Position+=normal*(.31f-distance)*.5f;}
        if((int)(Time*120)%2==0){Strike(A,B);Strike(B,A);}else{Strike(B,A);Strike(A,B);}
        if(A.Health<=0 || B.Health<=0){End(A.Health>B.Health ? A.Id : B.Health>A.Health ? B.Id : null,"Knockout");return;}
        bool Out(KarateFighter f)=>Math.Abs(f.Position.X)>HalfExtent || Math.Abs(f.Position.Y)>HalfExtent;
        bool outA=Out(A),outB=Out(B);if(outA || outB){End(outA && outB ? null : outA ? B.Id : A.Id,"Ring Out");return;}
        if(Remaining<=0)End(Math.Abs(A.Health-B.Health)<.0001f ? null : A.Health>B.Health ? A.Id : B.Id,"Time Up");
    }
    private void End(string? winner,string method)
    {
        Finished=true;Winner=winner;Method=winner==null ? "Draw" : method;
        if(method=="Ring Out")foreach(var fighter in new[]{A,B})fighter.RingFalling=Math.Abs(fighter.Position.X)>HalfExtent || Math.Abs(fighter.Position.Y)>HalfExtent;
    }
    private static void Settle(KarateFighter fighter,float dt)
    {
        fighter.StrikeAge+=dt;
        if(!fighter.RingFalling)return;
        // Continue the actual impact velocity through the edge, then gravity.
        while(dt>0){float step=Math.Min(dt,1f/120);dt-=step;fighter.Position+=fighter.Push*step;fighter.Push*=MathF.Exp(-3*step);
            if(Math.Abs(fighter.Position.X)>HalfExtent+.07f || Math.Abs(fighter.Position.Y)>HalfExtent+.07f){fighter.FallVelocity-=3.8f*step;fighter.OutsideHeight=Math.Max(-.35f,fighter.OutsideHeight+fighter.FallVelocity*step);}}
    }
    private static void Act(KarateFighter f,KarateFighter other,float dt)
    {
        f.StrikeAge+=dt;f.CheerReaction=Math.Max(0,f.CheerReaction-dt);f.ActionTime+=dt;f.Position+=f.Push*dt;f.Push*=MathF.Exp(-5*dt);
        f.Zeal=Math.Max(0,f.Zeal-ZealDrain(f.Pet)*dt);if(f.Zeal<=0)f.NeedsMeditation=true;
        Vector2 toward=other.Position-f.Position;float distance=toward.Length();toward=distance>.001f ? toward/distance : f.Facing;
        if(f.Action is not (KarateAction.Knockdown or KarateAction.Rise or KarateAction.Hit) && (f.Action!=KarateAction.Attack || f.Swing<.25f))f.Facing=Vector2.Normalize(Vector2.Lerp(f.Facing,toward,Math.Clamp(dt*8,0,1)));
        bool recovering=f.Action is KarateAction.Knockdown or KarateAction.Rise or KarateAction.Hit;
        if(f.NeedsMeditation && !recovering)
        {
            if(f.Action!=KarateAction.Meditate)Begin(f,KarateAction.Meditate,100);
            f.Zeal=Math.Min(KarateFighter.MaxZeal,f.Zeal+dt*(6+f.Pet.Luck/990f*10));
            if(f.Zeal<28)return;f.NeedsMeditation=false;Begin(f,KarateAction.Wait,.25f);
        }
        if(other.Action==KarateAction.Attack && other.AttackSerial!=f.ObservedAttack && other.Swing<.3f)
        {
            f.ObservedAttack=other.AttackSerial;
            if(f.Action is KarateAction.Wait or KarateAction.Guard && f.Random.NextDouble()<.05+f.Pet.Fly/990f*.50)Begin(f,KarateAction.Dodge,.7f);
        }
        if(f.ActionTime>=f.Duration)
        {
            if(f.Action==KarateAction.Knockdown){Begin(f,KarateAction.Rise,1.2f);return;}
            if(f.Action==KarateAction.Attack){Begin(f,KarateAction.Recover,.18f+.12f*(1-f.Pet.Run/990f));return;}
            if(f.Action==KarateAction.Hit){Begin(f,KarateAction.Recover,.16f);return;}
            if(other.Action is KarateAction.Knockdown or KarateAction.Rise)Begin(f,KarateAction.Wait,.35f);
            else if(distance>.39f)Begin(f,KarateAction.Approach,.4f);
            else
            {
                var choices=new[]{KarateAction.Attack,KarateAction.Guard,KarateAction.Dodge,KarateAction.Retreat,KarateAction.Wait};
                double total=0;foreach(var c in choices)total+=Weight(c,f,other);double draw=f.Random.NextDouble()*total;var selected=KarateAction.Wait;
                foreach(var c in choices){draw-=Weight(c,f,other);if(draw<=0){selected=c;break;}}
                Begin(f,selected,selected==KarateAction.Attack ? AttackDuration(f.Pet) : selected==KarateAction.Wait ? .30f : .65f);
            }
        }
        float speed=(.42f+f.Pet.Run*.0006f)*(.9f+.1f*f.Pet.Energy/100);
        if(f.Action==KarateAction.Approach && distance>.35f)f.Position+=toward*Math.Min(distance-.35f,speed*dt);
        else if(f.Action==KarateAction.Retreat)f.Position+=(-toward*.28f-f.Position*.3f)*speed*dt;
        else if(f.Action==KarateAction.Dodge && f.ActionTime<.25f)f.Position+=new Vector2(-toward.Y,toward.X)*speed*dt*.24f;
        else if(f.Action==KarateAction.Attack && f.Swing<.25f && distance>.33f)f.Position+=toward*Math.Min(distance-.33f,dt*.22f);
    }
    private static void Miss(KarateFighter f){f.StrikeMissed=true;f.Misses++;f.Zeal=Math.Max(0,f.Zeal-MissCost(f.Pet));}
    private static void Strike(KarateFighter attacker,KarateFighter defender)
    {
        if(attacker.Action!=KarateAction.Attack || attacker.Struck || attacker.Swing<KarateContact.ImpactSwing)return;
        attacker.Struck=true;attacker.StrikeMissed=false;attacker.StrikeOrigin=attacker.Position;attacker.StrikeFacing=attacker.Facing;attacker.StrikeKind=attacker.AttackKind;attacker.StrikeAge=0;var delta=defender.Position-attacker.Position;float distance=delta.Length();
        if(!KarateContact.Touches(attacker,defender) || defender.Action is KarateAction.Knockdown or KarateAction.Rise){Miss(attacker);return;}
        if(defender.Action==KarateAction.Dodge && defender.ActionTime<.55f){defender.Dodges++;Miss(attacker);return;}
        float move=attacker.AttackKind>=3 ? 1.14f : 1;
        float damage=(4+attacker.Pet.Power*.022f)*move*(.92f+.08f*attacker.Pet.Energy/100)/Defence(defender.Pet);
        damage*=.98f+(float)attacker.Random.NextDouble()*.04f;bool guard=defender.Action==KarateAction.Guard;if(guard){damage*=.4f;defender.Blocks++;}
        if(defender.Action==KarateAction.Meditate)damage*=1.12f;
        defender.Health=Math.Max(0,defender.Health-damage);attacker.Hits++;attacker.Impact++;attacker.ImpactPoint=KarateContact.Tip(attacker.Position,attacker.Facing,attacker.AttackKind);
        attacker.Zeal=Math.Min(KarateFighter.MaxZeal,attacker.Zeal+4+attacker.Pet.Luck/990f*2);defender.Zeal=Math.Max(0,defender.Zeal-(guard ? 2 : 7));
        defender.Push=delta/distance*(guard ? .35f : (1.15f+attacker.Pet.Power*.0022f)*move);
        bool fall=!guard && (damage>12 && attacker.AttackKind>=2 || defender.Zeal<=0 && damage>8);
        if(fall){defender.Knockdowns++;Begin(defender,KarateAction.Knockdown,1.3f);}
        else if(!guard)Begin(defender,KarateAction.Hit,.28f);
    }
}
