using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private float discoveryReviewTime,nextDiscoveryCapture=2;
    private int discoveryReviewShot;
    private bool discoveryReviewReady;
    private readonly List<object> discoveryFrames=new();
    private void ReviewDiscoveries(float dt)
    {
        discoveryReviewTime+=Mathf.Min(dt,.1f);
        if(!discoveryReviewReady)
        {
            discoveryReviewReady=true;
            pet.GardenEggs.Clear();pet.Run=200;pet.Bond=100;pet.Energy=95;pet.Curiosity=100;pet.Initiative=990;pet.Sociability=100;
            pet.GardenEggs.Add(new() { Id="review-cautious",Hatched=true,Color="Yellow",Chao=new() { Name="Fern",Run=170,Bond=40,Energy=90,Curiosity=-70,Initiative=25,Sociability=30 } });
            pet.GardenEggs.Add(new() { Id="review-outgoing",Hatched=true,Color="Black",Chao=new() { Name="Pip",Run=190,Bond=95,Energy=95,Curiosity=90,Initiative=900,Sociability=100,Kindness=70 } });
            pet.Expedition.LastTeamIds=ExpeditionRoster.Available(pet).Select(p=>p.Id).ToList();
            foreach(var a in pet.Expedition.LastTeamIds)foreach(var b in pet.Expedition.LastTeamIds.Where(b=>a!=b))
            { var link=pet.SocialLink(a,b,true)!;link.Met=true;link.Score=60; }
            SwitchZone(GardenZone.Expedition);player.Position=new(-1,0,16);
            var nav=new ExpeditionNavigation(world);var plant=world.GetChildren().OfType<DiscoveryInterest>().First(i=>i.Definition.Kind==DiscoveryKind.Plant);
            var starts=new[]{new Vector3(-4.6f,0,19),new Vector3(-2.1f,0,19.4f),new Vector3(-6.3f,0,18)};
            for(int i=0;i<3;i++)
            {
                var m=expeditionTeam!.Members[i];m.Body.Position=nav.Nearest(starts[i]);m.Body.ResetPhysicsInterpolation();
                m.Body.Rotation=new(0,Mathf.Atan2(plant.Position.X-m.Body.Position.X,plant.Position.Z-m.Body.Position.Z),0);
                m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,40);
            }
            cameraRig.Distance=7.5f;cameraRig.Pitch=.32f;cameraRig.Yaw=0;
        }
        cameraRig.Follow(player.Position,dt,true);teamUi.Update(expeditionTeam,true);UpdateUi();
        if(discoveryReviewTime>=nextDiscoveryCapture)
        {
            string file=$"discovery-{discoveryReviewShot++:00}.png";
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/"+file));
            discoveryFrames.Add(new { seconds=discoveryReviewTime,image=file,
                pets=expeditionTeam!.Members.Select(m=>new { id=m.Pet.Id,name=m.Pet.State.Name,intent=m.Intent.ToString(),
                    x=m.Body.Position.X,y=m.Body.Position.Y,z=m.Body.Position.Z,
                    phase=expeditionTeam.Discoveries.Sessions.FirstOrDefault(s=>s.Pet==m)?.Phase.ToString(),
                    known=pet.Expedition.Memory(m.Pet.Id).Objects.Count,studied=pet.Expedition.Memory(m.Pet.Id).Objects.Count(r=>r.Visits>0) }).ToArray() });
            nextDiscoveryCapture+=15;
            GD.Print($"DISCOVERY_REVIEW {discoveryReviewTime:F0}s: "+string.Join("; ",expeditionTeam!.Members.Select(m=>$"{m.Pet.State.Name} {m.Intent} / known {pet.Expedition.Memory(m.Pet.Id).Objects.Count} / studied {pet.Expedition.Memory(m.Pet.Id).Objects.Count(r=>r.Visits>0)}")));
        }
        if(discoveryReviewTime<190)return;
        var system=expeditionTeam!.Discoveries;
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/discovery-review.json"),JsonSerializer.Serialize(new {
            seconds=discoveryReviewTime,frames=discoveryFrames,
            events=system.Events.Select(e=>new {time=e.Time,pet=e.Pet,item=e.Object,action=e.Action}),
            rays=system.Perception.VisibilityQueries, recoveries=expeditionTeam.Members.Sum(m=>m.Recoveries),
            memory=pet.Expedition.Discoveries,history=expeditionTeam.Members.ToDictionary(m=>m.Pet.Id,m=>m.Pet.State.History) },new JsonSerializerOptions { WriteIndented=true }));
        Save();
        GD.Print("DISCOVERY_REVIEW_OK: 190 seconds of ordinary perception and team AI; captures and individual discovery traces saved");
        GetTree().Quit();
    }
}
