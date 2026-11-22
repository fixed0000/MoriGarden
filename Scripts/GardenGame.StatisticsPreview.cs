using System;
using System.IO;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool statisticsPreviewReady;
    // Explicit isolated preview; never loads or writes the user's garden.
    private void InspectStatistics()
    {
        if(statisticsPreviewReady)return;statisticsPreviewReady=true;
        GetWindow().Title="MoriGarden — GENERATED STAT TEST · Not your personal garden";
        PetState Profile(string name,int grade)
        {
            var p=PetState.NewEgg();p.Hatch();p.Name=name;p.GardenEggs.Clear();p.Bond=50;p.Form="bloom";p.Energy=p.Fullness=p.Fun=100;
            foreach(string ability in ChaoStatistics.Names)
            {var s=p.Stat(ability);s.Grade=ChaoStatistics.Hidden(ability) ? 0 : grade;s.Level=99;s.Points=ChaoStatistics.Hidden(ability) ? 1287 : 99*(13+grade*3);}
            return p;
        }
        pet=Profile("Grade C",2);
        pet.GardenEggs.Add(new(){Id="stats-e",Hatched=true,Color="Blue",X=3,Z=4,Chao=Profile("Grade E",0)});
        pet.GardenEggs.Add(new(){Id="stats-s",Hatched=true,Color="Yellow",X=5,Z=4,Chao=Profile("Grade S",5)});
        // Pre-acquainted fixtures avoid locking the editor during introductions.
        foreach(var pair in new[]{("main","stats-e"),("main","stats-s"),("stats-e","stats-s")})
        {var link=pet.SocialLink(pair.Item1,pair.Item2,true)!;link.Met=link.Introduced=true;}
        testMode=false;SwitchZone(GardenZone.Garden,restoring:true);
        editor=new ChaoEditorBridge(Path.GetDirectoryName(savePath)!);
        Notify("Stat preview · Test pets only · Your personal saves are not open",12);
        GD.Print("STATS_PREVIEW_READY: isolated garden and editor bridge");
    }
}
