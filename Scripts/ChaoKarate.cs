using System;
using System.Numerics;

namespace Mori;

public enum KarateAction { Wait,Approach,Attack,Guard,Dodge,Retreat,Recover,Hit,Knockdown,Rise }
public sealed class KarateFighter
{
    public string Id {get;}
    public string Color {get;}
    public PetState Pet {get;}
    public Vector2 Position {get;internal set;}
    public Vector2 Facing {get;internal set;}
    public float MaxHealth=>65+Pet.Stamina*.065f;
    public float Health {get;internal set;}
    public float Capacity=>40+Pet.Stamina*.06f;
    public float Stamina {get;internal set;}
    public KarateAction Action {get;internal set;}
    public float ActionTime {get;internal set;}
    public float Duration {get;internal set;}=.5f;
    public int AttackKind {get;internal set;}
    public int Hits {get;internal set;}
    public int Blocks {get;internal set;}
    public int Dodges {get;internal set;}
    public int Knockdowns {get;internal set;}
    public int Impact {get;internal set;}
    public Vector2 ImpactPoint {get;internal set;}
    internal bool Struck;
    internal float CheerBoost;
    internal Vector2 Push;
    internal Random Random;
    public KarateFighter(string id,string color,PetState pet,Vector2 at,int seed)
    {Id=id;Color=color;Pet=pet;Position=at;Facing=at.LengthSquared()>.0001f ? Vector2.Normalize(-at) : Vector2.UnitX;Health=MaxHealth;Stamina=Capacity*Math.Clamp(pet.Energy/100,0,1);Random=new(seed);Duration=.35f+(float)Random.NextDouble()*.4f;}
}
public sealed class ChaoKarate
{
    public const float RingRadius=2.8f,ContactRange=.43f;
    public KarateFighter A {get;}
    public KarateFighter B {get;}
    public float Time {get;private set;}
    public float Countdown=>Math.Max(0,3-Time);
    public bool Finished {get;private set;}
    public string? Winner {get;private set;}
    public string Method {get;private set;}="";
    public float CheerCooldown {get;private set;}
    public ChaoKarate(KarateFighter a,KarateFighter b){A=a;B=b;}
    public bool Cheer(string id)
    {
        var fighter=A.Id==id ? A : B.Id==id ? B : null;
        if(fighter==null || Finished || Countdown>0 || CheerCooldown>0)return false;
        float bond=Math.Clamp(fighter.Pet.Bond/100f+fighter.Pet.RelationshipWith()/200f,0,1);
        fighter.Stamina=Math.Min(fighter.Capacity,fighter.Stamina+2+bond*4);fighter.CheerBoost=.02f+.06f*bond;CheerCooldown=10;return true;
    }
    public static double Weight(KarateAction action,KarateFighter self,KarateFighter other)
    {
        float energy=self.Stamina/self.Capacity,aggression=(self.Pet.Aggressiveness+100)/200f,intellect=self.Pet.Intelligence/990f,initiative=self.Pet.Initiative/990f;
        bool danger=other.Action==KarateAction.Attack && other.ActionTime<.4f;
        return action switch{
            KarateAction.Attack=>energy<.15f ? .05 : .8+aggression*1.1+initiative*.8+(danger ? -.4f : .3f),
            KarateAction.Guard=>.25+(1-aggression)*.65+(danger ? 1.8+intellect : 0),
            KarateAction.Dodge=>.15+self.Pet.Run/990f*.8+(danger ? intellect*.8 : 0),
            KarateAction.Recover=>energy<.45f ? 2+(1-energy)*4+intellect : .05,
            KarateAction.Retreat=>self.Position.Length()>2.1f ? .05 : energy<.35f ? 1 : .15,
            _=>.15+(1-initiative)*.2};
    }
    private static void Begin(KarateFighter f,KarateAction action,float duration)
    {f.Action=action;f.ActionTime=0;f.Duration=duration;f.Struck=false;if(action==KarateAction.Attack){f.AttackKind=f.Random.Next(3);f.Stamina=Math.Max(0,f.Stamina-7);}}
    public void Step(float dt)
    {
        if(Finished || !float.IsFinite(dt) || dt<=0)return;
        while(dt>0){float step=Math.Min(dt,1f/60);Advance(step);dt-=step;if(Finished)break;}
    }
    private void Advance(float dt)
    {
        Time+=dt;CheerCooldown=Math.Max(0,CheerCooldown-dt);if(Countdown>0)return;
        Act(A,B,dt);Act(B,A,dt);
        var delta=B.Position-A.Position;float distance=delta.Length();
        if(distance<.30f){var normal=distance<.001f ? Vector2.UnitX : delta/distance;A.Position-=normal*(.30f-distance)*.5f;B.Position+=normal*(.30f-distance)*.5f;}
        if((int)(Time*60)%2==0){Strike(A,B);Strike(B,A);}else{Strike(B,A);Strike(A,B);}
        if(A.Health<=0 || B.Health<=0){End(A.Health>B.Health ? A.Id : B.Health>A.Health ? B.Id : null,"Knockout");return;}
        if(A.Position.Length()>RingRadius || B.Position.Length()>RingRadius){End(A.Position.Length()<B.Position.Length() ? A.Id : B.Id,"Ring out");return;}
        if(Time>=93)
        {float scoreA=A.Health/A.MaxHealth,scoreB=B.Health/B.MaxHealth;End(Math.Abs(scoreA-scoreB)<.025f ? null : scoreA>scoreB ? A.Id : B.Id,"Decision");}
    }
    private void End(string? winner,string method){Finished=true;Winner=winner;Method=winner==null ? "Draw" : method;}
    private static void Act(KarateFighter f,KarateFighter other,float dt)
    {
        f.ActionTime+=dt;f.CheerBoost=Math.Max(0,f.CheerBoost-dt*.03f);
        f.Position+=f.Push*dt;f.Push*=MathF.Exp(-10*dt);
        Vector2 toward=other.Position-f.Position;float distance=toward.Length();if(distance>.001f)toward/=distance;else toward=f.Facing;
        if(f.Action is not (KarateAction.Knockdown or KarateAction.Rise or KarateAction.Hit))f.Facing=Vector2.Normalize(Vector2.Lerp(f.Facing,toward,Math.Clamp(dt*8,0,1)));
        if(f.ActionTime>=f.Duration)
        {
            if(f.Action==KarateAction.Knockdown){Begin(f,KarateAction.Rise,1.3f);return;}
            if(distance>.39f){Begin(f,KarateAction.Approach,.45f+(float)f.Random.NextDouble()*.3f);}
            else
            {
                var choices=new[]{KarateAction.Attack,KarateAction.Guard,KarateAction.Dodge,KarateAction.Retreat,KarateAction.Recover,KarateAction.Wait};
                double total=0;foreach(var c in choices)total+=Weight(c,f,other);double draw=f.Random.NextDouble()*total;
                var selected=KarateAction.Wait;foreach(var c in choices){draw-=Weight(c,f,other);if(draw<=0){selected=c;break;}}
                Begin(f,selected,selected==KarateAction.Attack ? .95f-f.Pet.Run/990f*.12f : selected==KarateAction.Recover ? 1.2f : .65f+(float)f.Random.NextDouble()*.3f);
            }
        }
        float speed=(.35f+f.Pet.Run*.00085f)*(.75f+.25f*f.Pet.Energy/100);
        if(f.Action==KarateAction.Approach && distance>.34f)f.Position+=toward*Math.Min(distance-.34f,speed*dt);
        else if(f.Action==KarateAction.Retreat)f.Position+=(-toward*.5f-f.Position*.22f)*speed*dt;
        else if(f.Action==KarateAction.Dodge && f.ActionTime<.4f)f.Position+=new Vector2(-toward.Y,toward.X)*speed*dt*.6f;
        else if(f.Action==KarateAction.Attack && f.ActionTime<.35f && distance>.32f)f.Position+=toward*Math.Min(distance-.32f,dt*.35f);
        if(f.Action is KarateAction.Wait or KarateAction.Guard or KarateAction.Recover)f.Stamina=Math.Min(f.Capacity,f.Stamina+dt*(f.Action==KarateAction.Recover ? 7 : 1.5f));
    }
    private static void Strike(KarateFighter attacker,KarateFighter defender)
    {
        if(attacker.Action!=KarateAction.Attack || attacker.Struck || attacker.ActionTime<.42f)return;
        attacker.Struck=true;
        var delta=defender.Position-attacker.Position;float distance=delta.Length();
        if(distance>ContactRange || distance<.001f || Vector2.Dot(attacker.Facing,delta/distance)<.7f || defender.Action is KarateAction.Knockdown or KarateAction.Rise)return;
        if(defender.Action==KarateAction.Dodge && defender.ActionTime<.55f){defender.Dodges++;return;}
        float damage=(5+attacker.Pet.Power*.021f)*(.55f+.45f*attacker.Stamina/attacker.Capacity)*(.7f+.3f*attacker.Pet.Energy/100)*(.97f+.03f*attacker.Pet.Joy/100)*(1+attacker.CheerBoost);
        damage*=.96f+(float)attacker.Random.NextDouble()*.08f;
        if(attacker.Random.NextDouble()<.025+attacker.Pet.Luck/990f*.045)damage*=1.12f;
        bool guard=defender.Action==KarateAction.Guard;
        if(guard){damage*=.24f;defender.Blocks++;defender.Stamina=Math.Max(0,defender.Stamina-3);}
        damage/=1+defender.Pet.Power*.00035f+defender.Pet.Stamina*.0002f;
        defender.Health=Math.Max(0,defender.Health-damage);attacker.Hits++;attacker.Impact++;attacker.ImpactPoint=(attacker.Position+defender.Position)*.5f;
        defender.Push=delta/distance*(guard ? .65f : 1.15f+attacker.Pet.Power*.0025f);
        bool fall=!guard && (damage>17 || defender.Stamina<8);
        if(fall){defender.Knockdowns++;Begin(defender,KarateAction.Knockdown,2.2f);}
        else if(!guard)Begin(defender,KarateAction.Hit,.33f);
    }
}
