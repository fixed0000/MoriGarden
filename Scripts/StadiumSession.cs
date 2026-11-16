using System;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class StadiumSession
{
    private readonly Func<PetState> owner;
    private readonly Node3D world;
    private readonly Action save;
    private readonly Action<string> message;
    private readonly Action<bool> showResident;
    internal StadiumUi Ui {get;}
    internal ChaoRace? Race {get;private set;}
    internal ChaoKarate? Fight {get;private set;}
    internal StadiumView? View {get;private set;}
    internal ExpeditionPet? Entrant {get;private set;}
    internal SportsEvent? Event {get;private set;}
    internal SportsPrize? Prize {get;private set;}
    internal int BoutWins {get;private set;}
    internal int Bout {get;private set;}
    internal bool InCompetition=>Race!=null || Fight!=null;
    internal bool Blocking=>InCompetition || Ui.MenuVisible;
    internal bool BetweenBouts {get;private set;}
    internal bool ResultsReady {get;private set;}
    internal bool CanCheer=>InCompetition && !ResultsReady && !BetweenBouts && (Race==null || Race.Entrants[0].FinishedAt<0) && (Race?.Countdown ?? Fight?.Countdown ?? 1)<=0 && (Race?.CheerCooldown ?? Fight?.CheerCooldown ?? 1)<=0;
    private string ticket="";
    private int seed;
    private float elapsed;
    internal StadiumSession(Node3D parent,Node3D world,Func<PetState> owner,Action save,Action<string> message,Action<bool> showResident)
    {
        this.world=world;this.owner=owner;this.save=save;this.message=message;this.showResident=showResident;
        Ui=new();parent.AddChild(Ui);Ui.Initialize(owner);Ui.Start=Start;Ui.Leave=Leave;Ui.Next=Next;Ui.Cheer=Cheer;
    }
    internal void Open(){if(InCompetition)return;Ui.Open();}
    internal void Start(string petId,string eventId)
    {
        var entrant=ExpeditionRoster.Available(owner()).FirstOrDefault(p=>p.Id==petId);var competition=SportsCatalog.Find(eventId);
        if(entrant==null || competition==null || !SportsCatalog.Open(competition,entrant.State) || entrant.State.Energy<15){message("Choose an eligible Chao and an unlocked competition.");return;}
        ClearView();Entrant=entrant;Event=competition;owner().Stadium.LastPet=petId;ticket=owner().Stadium.Begin(petId,eventId);seed=Random.Shared.Next();
        Bout=BoutWins=0;ResultsReady=BetweenBouts=false;Prize=null;elapsed=0;showResident(false);
        entrant.State.History.Visit("stadium","Chao Stadium");
        if(competition.Mode==SportMode.Race)
        {
            var entrants=new System.Collections.Generic.List<RaceEntrant>{new(petId,entrant.Color,entrant.State,seed)};
            for(int i=0;i<3;i++){var rival=SportsRivals.All[(i+competition.Level)%SportsRivals.All.Length];entrants.Add(new("npc-"+rival.Id,rival.Color,rival.Create(competition.Rating),seed+i+1));}
            Race=new(competition,entrants);View=new(world);View.BuildRace(Race);
        }
        else BeginBout();
        save();
    }
    private void BeginBout()
    {
        ClearView();Race=null;ResultsReady=BetweenBouts=false;
        var rival=SportsRivals.All[(Bout+Event!.Level+2)%SportsRivals.All.Length];
        var opponent=rival.Create(Event.Rating+Bout*18);
        Fight=new(new(Entrant!.Id,Entrant.Color,Entrant.State,new System.Numerics.Vector2(-1.1f,0),seed+Bout*17),new("npc-"+rival.Id,rival.Color,opponent,new System.Numerics.Vector2(1.1f,0),seed+Bout*17+7));
        View=new(world);View.BuildKarate(Fight);
    }
    internal void Step(float dt,Camera3D camera)
    {
        if(!InCompetition)return;elapsed+=dt;
        if(!ResultsReady && !BetweenBouts)Entrant!.State.Tick(dt,false);
        if(Race is {} race)
        {
            if(!ResultsReady)race.Step(dt);View!.Race(race,Entrant!.Id,camera,dt);
            string countdown=race.Countdown>0 ? $"Starting in {Mathf.CeilToInt(race.Countdown)}" : "";
            string board=string.Join("\n",race.Standings.Select(e=>$"{e.Place}. {e.Pet.Name}  {(e.FinishedAt>=0 ? e.FinishedAt.ToString("0.00")+"s" : (e.Distance/race.Course.Length*100).ToString("0")+"%")}"));
            var selected=race.Entrants[0];string segment=race.Course.Surfaces[race.Course.Locate(selected.Distance).Section].ToString();
            if(!ResultsReady)Ui.Hud($"{Event!.Title} · {countdown}\n{board}\n{segment} · Stamina {selected.Fatigue/selected.Capacity*100:0}%",CanCheer,race.CheerCooldown);
            if(race.Finished && !ResultsReady)
            {
                var result=Race.Entrants[0];Prize=SportsRewards.Apply(owner(),new(ticket,Entrant.Id,Event!.Id,result.Place,result.FinishedAt<0 ? elapsed : result.FinishedAt,Cleared:result.FinishedAt>=0));ResultsReady=true;
                Ui.Results(board+$"\n\n{Entrant.State.Name}: place {result.Place}\n"+(Prize?.Message ?? "Entry closed.")+(Prize?.NewRecord==true ? "\nNew personal record!" : ""),false);save();
                ChaoSounds.Play(View.Bodies[0],"mood");
            }
        }
        else if(Fight is {} fight)
        {
            if(!ResultsReady && !BetweenBouts)fight.Step(dt);View!.Karate(fight,Entrant!.Id,camera,dt);
            if(!ResultsReady && !BetweenBouts)Ui.Hud($"{Event!.Title} · Bout {Bout+1}/{Event.Bouts}"+(fight.Countdown>0 ? $" · {Mathf.CeilToInt(fight.Countdown)}" : "")+
                $"\n{fight.A.Pet.Name}: HP {fight.A.Health:0}/{fight.A.MaxHealth:0} · Stamina {fight.A.Stamina:0}\n{fight.B.Pet.Name}: HP {fight.B.Health:0}/{fight.B.MaxHealth:0} · Stamina {fight.B.Stamina:0}\n{fight.A.Action}  /  {fight.B.Action}",CanCheer,fight.CheerCooldown);
            if(fight.Finished && !ResultsReady && !BetweenBouts)
            {
                bool won=fight.Winner==Entrant.Id,draw=fight.Winner==null;if(won)BoutWins++;
                BetweenBouts=(won && Bout+1<Event!.Bouts) || draw;
                string result=draw ? "Draw" : (won ? Entrant.State.Name : fight.B.Pet.Name)+" wins";
                if(!BetweenBouts)
                {
                    Prize=SportsRewards.Apply(owner(),new(ticket,Entrant.Id,Event!.Id,won ? 1 : 2,elapsed,BoutWins,won));ResultsReady=true;save();
                }
                Ui.Results($"{result} · {fight.Method}\nTournament victories: {BoutWins}/{Event!.Bouts}\n\n"+(BetweenBouts ? "Rest between bouts. You may continue or leave.\nTournament prizes are awarded when the entry closes." : Prize?.Message ?? "Entry closed."),BetweenBouts,draw ? "Retry bout" : "Next bout");
                ChaoSounds.Play(View.Bodies[0],"mood");
            }
        }
    }
    internal void Cheer()
    {
        bool heard=Race?.Cheer(Entrant!.Id) ?? Fight?.Cheer(Entrant!.Id) ?? false;
        if(heard && View!=null){View.Celebrate();ChaoSounds.Play(View.Bodies[0],"voice");}
    }
    internal void Next()
    {
        if(!BetweenBouts || Fight==null)return;if(Fight.Winner==Entrant!.Id)Bout++;
        BeginBout();
    }
    internal void Leave()
    {
        if(InCompetition && !ResultsReady)
        {
            if(BetweenBouts && BoutWins>0)SportsRewards.Apply(owner(),new(ticket,Entrant!.Id,Event!.Id,99,elapsed,BoutWins,false,true));
            else owner().Stadium.Cancel();
            save();
        }
        Race=null;Fight=null;ResultsReady=BetweenBouts=false;ClearView();Ui.HideAll();showResident(true);
    }
    internal void Key(Key key){if(key==Godot.Key.Q)Cheer();else if(key==Godot.Key.Escape)Leave();else if(key==Godot.Key.Enter && BetweenBouts)Next();}
    private void ClearView(){View?.Dispose();View=null;}
    internal void Dispose(){Leave();if(GodotObject.IsInstanceValid(Ui)){Ui.GetParent().RemoveChild(Ui);Ui.QueueFree();}}
}
