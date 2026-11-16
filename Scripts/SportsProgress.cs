using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public sealed class SportsRecord
{
    public int Entries {get;set;}
    public int Wins {get;set;}
    public int BestPlace {get;set;}=99;
    public float BestSeconds {get;set;}
    public int BoutWins {get;set;}
}
public sealed class ChaoAthletics
{
    public Dictionary<string,SportsRecord> Results {get;set;}=new();
    public HashSet<string> Medals {get;set;}=new();
    public HashSet<string> Cups {get;set;}=new();
    public void Validate()
    {
        Results=(Results ?? new()).Where(p=>DiscoveryMemory.ValidId(p.Key) && p.Value!=null).Take(64).ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var r in Results.Values){r.Entries=Math.Clamp(r.Entries,0,1000000);r.Wins=Math.Clamp(r.Wins,0,r.Entries);r.BestPlace=Math.Clamp(r.BestPlace,1,99);r.BoutWins=Math.Clamp(r.BoutWins,0,1000000);r.BestSeconds=float.IsFinite(r.BestSeconds) ? Math.Clamp(r.BestSeconds,0,3600) : 0;}
        Medals=new((Medals ?? new()).Where(DiscoveryMemory.ValidId).Take(128));Cups=new((Cups ?? new()).Where(DiscoveryMemory.ValidId).Take(32));
    }
}
public sealed class StadiumProgress
{
    public string? ActiveTicket {get;set;}
    public string? ActivePet {get;set;}
    public string? ActiveEvent {get;set;}
    public string LastPet {get;set;}="main";
    public HashSet<string> Toys {get;set;}=new();
    public List<string> Receipts {get;set;}=new();
    public string Begin(string pet,string competition)
    {ActivePet=pet;ActiveEvent=competition;return ActiveTicket=Guid.NewGuid().ToString("N");}
    public void Cancel(){ActiveTicket=ActivePet=ActiveEvent=null;}
    public void Validate()
    {
        Toys=new((Toys ?? new()).Where(t=>t is "champion-rattle" or "karate-ball"));Receipts=(Receipts ?? new()).Where(DiscoveryMemory.ValidId).Distinct().TakeLast(64).ToList();
        LastPet=DiscoveryMemory.ValidId(LastPet) ? LastPet : "main";
        if(!DiscoveryMemory.ValidId(ActiveTicket) || !DiscoveryMemory.ValidId(ActivePet) || SportsCatalog.Find(ActiveEvent ?? "")==null)Cancel();
    }
}
public sealed record SportsOutcome(string Ticket,string PetId,string EventId,int Place,float Seconds,int BoutWins=0,bool Cleared=true,bool Withdrawn=false);
public sealed record SportsPrize(int Stars,string Medal,string Cup,string Toy,bool NewRecord,string Message);
public static class SportsRewards
{
    public static SportsPrize? Apply(PetState owner,SportsOutcome outcome)
    {
        var ledger=owner.Stadium;var e=SportsCatalog.Find(outcome.EventId);
        var participant=ExpeditionRoster.Available(owner).FirstOrDefault(p=>p.Id==outcome.PetId);
        if(e==null || participant==null || ledger.ActiveTicket!=outcome.Ticket || ledger.ActivePet!=outcome.PetId || ledger.ActiveEvent!=outcome.EventId || ledger.Receipts.Contains(outcome.Ticket))return null;
        var pet=participant.State;var history=pet.Athletics;
        if(!history.Results.TryGetValue(e.Id,out var record))history.Results[e.Id]=record=new();
        bool firstWin=record.Wins==0,win=outcome.Cleared && outcome.Place==1;
        bool best=e.Mode==SportMode.Race && outcome.Cleared && (record.BestSeconds<=0 || outcome.Seconds<record.BestSeconds);
        record.Entries++;record.BestPlace=Math.Min(record.BestPlace,outcome.Place);record.BoutWins+=outcome.BoutWins;
        if(best)record.BestSeconds=outcome.Seconds;if(win)record.Wins++;
        string medal="",cup="",toy="";
        if(outcome.Cleared && outcome.Place<=3)
        {
            string name=outcome.Place==1 ? "Gold" : outcome.Place==2 ? "Silver" : "Bronze";
            if(history.Medals.Add(e.Id+"-"+name.ToLowerInvariant()))medal=name+" Medal";
        }
        if(win && (e.Mode==SportMode.Karate || e.Level==3) && history.Cups.Add(e.Id))cup=e.Title+" Cup";
        if(win && (e.Mode==SportMode.Race && e.Category=="Beginner" && e.Level==3 || e.Mode==SportMode.Karate && e.Level==1))
        {string id=e.Mode==SportMode.Race ? "champion-rattle" : "karate-ball";if(ledger.Toys.Add(id))toy=id=="champion-rattle" ? "Champion Rattle" : "Karate Beach Ball";}
        int stars=outcome.Withdrawn ? outcome.BoutWins*2 : win ? (firstWin ? 8+e.Level*2 : 3+e.Level) : outcome.Cleared ? Math.Max(1,5-outcome.Place) : outcome.BoutWins*2;
        owner.Stars+=stars;
        if(e.Mode==SportMode.Race && outcome.Cleared)
        {int wallet=pet.Stars;pet.CompleteRace(Math.Max(0,5-outcome.Place));pet.Stars=wallet;}
        else {pet.Energy=Math.Max(0,pet.Energy-(outcome.Withdrawn ? 2 : 8));if(outcome.BoutWins>0)pet.Power=Math.Min(990,pet.Power+Math.Min(3,outcome.BoutWins));}
        string area="stadium";
        if(record.Entries==1)pet.History.Record(ChaoMemoryKind.Pleasant,"sports-entry-"+e.Mode.ToString().ToLowerInvariant(),area,e.Mode==SportMode.Race ? "Took part in Chao Race." : "Took part in Chao Karate.",.35f,.55f);
        if(win && !pet.History.Events.Any(m=>m.Source=="sports-win-"+e.Mode.ToString().ToLowerInvariant()))pet.History.Record(ChaoMemoryKind.Pleasant,"sports-win-"+e.Mode.ToString().ToLowerInvariant(),area,e.Mode==SportMode.Race ? "Won a first Chao Race." : "Won a first Chao Karate tournament.",.85f,.9f);
        if(cup.Length>0)pet.History.Record(ChaoMemoryKind.Pleasant,"cup-"+e.Id,area,"Won the "+cup+".",1,1);
        if(!win && !outcome.Withdrawn && e.Level>=3)pet.History.Record(ChaoMemoryKind.Unpleasant,"sports-setback-"+e.Mode.ToString().ToLowerInvariant(),area,"Faced a difficult competition and kept trying.",-.25f,.4f);
        ledger.Receipts.Add(outcome.Ticket);if(ledger.Receipts.Count>64)ledger.Receipts.RemoveAt(0);ledger.Cancel();
        return new(stars,medal,cup,toy,best,$"Stars +{stars}"+(medal.Length>0 ? " · "+medal : "")+(cup.Length>0 ? " · "+cup : "")+(toy.Length>0 ? " · "+toy : ""));
    }
}
