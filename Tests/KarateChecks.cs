using System;
using System.Linq;
using System.Numerics;
using Mori;

internal static class KarateChecks
{
    private static ChaoKarate Contact(PetState? attacker=null,PetState? defender=null,KarateAction defence=KarateAction.Wait,float distance=.35f,int seed=2)
    {
        var a=new KarateFighter("a","Green",attacker ?? new(){Power=700},Vector2.Zero,1);var b=new KarateFighter("b","Blue",defender ?? new(),new(distance,0),seed);
        a.Facing=Vector2.UnitX;a.Action=KarateAction.Attack;a.Duration=1;a.ActionTime=.455f;a.AttackKind=0;b.Action=defence;b.Duration=1000;b.ObservedAttack=1;return new(a,b);
    }
    internal static void Run(Action<bool,string> check)
    {
        check(SportsCatalog.Events.Where(e=>e.Mode==SportMode.Karate).All(e=>e.Bouts==5),"Every Karate tournament contains five bouts");
        var owner=new PetState();check(SportsCatalog.Events.Count(e=>e.Mode==SportMode.Karate && SportsCatalog.Open(e,owner))==3,"Beginner, Standard and Expert start open independently of Race");
        foreach(string rank in SportsCatalog.KarateRanks.Take(3))owner.Athletics.Results["karate-"+rank.ToLowerInvariant()]=new(){Entries=1,Wins=1,BestBouts=5};
        check(SportsCatalog.Open(SportsCatalog.Find("karate-super")!,owner),"Super opens after clearing all three initial tournaments");
        check(ChaoKarate.AttackDuration(new(){Run=990})<ChaoKarate.AttackDuration(new(){Run=0})*.5f,"Run primarily controls attack frequency");
        check(ChaoKarate.Defence(new(){Swim=990})>ChaoKarate.Defence(new(){Swim=0})*2,"Swim controls defence instead of Power");
        var bare=Contact();var swimmer=Contact(defender:new(){Swim=800});bare.Step(3.02f);swimmer.Step(3.02f);
        check(bare.A.Hits==1 && bare.B.Health<bare.B.MaxHealth && swimmer.B.MaxHealth-swimmer.B.Health<(bare.B.MaxHealth-bare.B.Health)*.5f,"Actual contact damage is reduced by the defender's Swim");
        var guarded=Contact(defence:KarateAction.Guard);guarded.Step(3.02f);check(guarded.B.Blocks==1 && guarded.B.MaxHealth-guarded.B.Health<bare.B.MaxHealth-bare.B.Health,"Visible guard reduces received damage");
        var dodged=Contact(defence:KarateAction.Dodge);dodged.Step(3.02f);check(dodged.B.Dodges==1 && dodged.B.Health==dodged.B.MaxHealth,"A visible dodge avoids the hit");
        var missed=Contact(distance:.8f);float zeal=missed.A.Zeal;missed.Step(3.02f);check(missed.A.Misses==1 && missed.B.Health==missed.B.MaxHealth && missed.A.Zeal<zeal-5,"An out-of-range strike misses, causes no damage and loses Zeal");
        check(bare.A.Zeal>=99 && bare.B.Zeal<94,"A landed strike raises attacker's Zeal and lowers the victim's Zeal");
        check(ChaoKarate.MissCost(new(){Stamina=990})<ChaoKarate.MissCost(new(){Stamina=0})*.4f,"Stamina protects Zeal from misses");
        check(new KarateFighter("h","White",new(){Stamina=990},Vector2.Zero,1).MaxHealth>new KarateFighter("l","White",new(){Stamina=0},Vector2.Zero,1).MaxHealth*2,"Stamina determines maximum health");
        check(ChaoKarate.ZealDrain(new(){Intelligence=990})<ChaoKarate.ZealDrain(new(){Intelligence=0})*.4f,"Intelligence slows natural Zeal depletion");
        check(ChaoKarate.CheerRecovery(new(){Luck=990})>ChaoKarate.CheerRecovery(new(){Luck=0})*1.8f,"Luck improves Zeal recovery");
        var meditation=Contact();meditation.A.Action=KarateAction.Wait;meditation.A.Duration=1000;meditation.Step(3.1f);meditation.A.Zeal=0;meditation.Step(.1f);
        check(meditation.A.Action==KarateAction.Meditate && meditation.A.Meditations==1,"Empty Zeal interrupts fighting and enters meditation");
        zeal=meditation.A.Zeal;check(meditation.Cheer("a") && meditation.A.Zeal>zeal && !meditation.Cheer("a"),"Karate cheering restores Zeal with only input debouncing");meditation.Step(.15f);check(meditation.Cheer("a"),"Karate cheering supports rapid deliberate presses");meditation.Step(8);
        check(meditation.A.Action!=KarateAction.Meditate && meditation.A.Zeal>0,"A sufficiently recovered Chao resumes fighting");
        var passive=Contact();passive.A.Action=KarateAction.Guard;passive.A.Duration=1000;passive.A.Zeal=50;passive.Step(4);
        check(passive.A.Zeal<50,"Guard and waiting do not regenerate an ordinary stamina substitute");
        var square=Contact();square.A.Action=KarateAction.Wait;square.A.Duration=1000;square.B.Position=new(1.1f,1.1f);square.Step(3.1f);check(!square.Finished,"Square arena corners are legal positions");square.B.Position=new(1.21f,1.1f);square.Step(.02f);check(square.Finished && square.Winner=="a" && square.Method=="Ring Out","Crossing a square boundary causes Ring Out");
        var knockout=Contact();knockout.B.Health=1;knockout.Step(3.02f);check(knockout.Finished && knockout.Method=="Knockout" && knockout.Winner=="a","Zero health causes a knockout");
        ChaoKarate Timed(float healthA,float healthB)
        {
            var f=Contact(attacker:new(){Intelligence=990,Stamina=990},defender:new(){Intelligence=990,Stamina=100});f.A.Action=KarateAction.Wait;f.A.Duration=1000;f.A.Health=healthA;f.B.Health=healthB;f.Step(93.1f);return f;
        }
        var timed=Timed(50,45);check(timed.Finished && timed.Method=="Time Up" && timed.Winner=="a" && timed.Remaining==0,"The ninety-second decision compares remaining health rather than percentage");
        var tie=Timed(50,50);check(tie.Finished && tie.Winner==null && tie.Method=="Draw","Exactly equal health produces a draw");
        int highEvades=0,lowEvades=0;for(int seed=0;seed<60;seed++)foreach(int fly in new[]{0,900})
        {
            var f=Contact(defender:new(){Fly=fly},seed:seed);f.A.AttackSerial=1;f.B.ObservedAttack=0;f.A.ActionTime=.2f;f.Step(3.35f);if(fly==0)lowEvades+=f.B.Dodges;else highEvades+=f.B.Dodges;
        }
        check(highEvades>lowEvades+10,"High Fly causes more visible successful evasions across seeded bouts");
        bool finite=true;int calmWins=0;for(int seed=0;seed<20;seed++)
        {
            var trained=new PetState{Power=800,Run=650,Stamina=650,Swim=650,Fly=650,Intelligence=700,Aggressiveness=-90};var weak=new PetState{Power=100,Run=100,Stamina=100,Fly=100,Swim=100,Aggressiveness=100};
            var fight=new ChaoKarate(new("calm","Green",trained,new(-.65f,0),seed),new("aggressive","Red",weak,new(.65f,0),seed+100));while(!fight.Finished)fight.Step(.2f);finite&=fight.Time<94;if(fight.Winner=="calm")calmWins++;
        }
        check(finite && calmWins>=16,"Trained calm Chao usually beat aggressive weak Chao and every fight ends");
        var winner=new PetState();winner.GardenEggs.Clear();var athlete=new PetState{Name="Fern"};winner.GardenEggs.Add(new(){Id="fern",Hatched=true,Chao=athlete});string ticket=winner.Stadium.Begin("fern","karate-beginner");SportsRewards.Apply(winner,new(ticket,"fern","karate-beginner",1,50,5));
        check(athlete.Athletics.KarateRank=="6 Kyu" && athlete.Athletics.Cups.Contains("karate-beginner") && winner.Stadium.Toys.Contains("karate-ball"),"A five-win tournament saves the athlete's rank, cup and unique prize");
        ticket=winner.Stadium.Begin("fern","karate-beginner");var repeated=SportsRewards.Apply(winner,new(ticket,"fern","karate-beginner",1,50,5))!;check(repeated.Cup=="" && repeated.Toy=="" && athlete.Athletics.KarateRank=="6 Kyu","Repeated beginner wins cannot duplicate prizes or farm higher ranks");
        check(bare.A.Pet.PowerAbility==700 && bare.B.Pet.Energy==new PetState().Energy,"Combat health and Zeal never overwrite permanent pet attributes");
    }
}
