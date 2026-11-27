using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Mori;

public sealed record MemoryParticipantDiagnostic(string id,string name,bool inRoster);
public sealed record MemoryEventDiagnostic(string id,string kind,string source,string area,string description,
    double firstAt,double lastAt,double ageSeconds,double spanSeconds,float valence,string tone,float importance,
    float influence,double halfLife,int occurrences,WorldPoint? place,MemoryParticipantDiagnostic[] participants);
public sealed record MemoryContributionDiagnostic(string eventId,float contribution);
public sealed record MemoryPartnerDiagnostic(string id,string name,bool inRoster,int count,int positive,int negative,
    float rawSum,float beforeClamp,float partnerBias,MemoryContributionDiagnostic[] contributions,
    SocialPartnerScore? referenceScore,SocialPartnerScore? lastSelection);
public sealed record MemoryPlaceDiagnostic(string id,string area,WorldPoint point,float beforeClamp,float placeBias,
    string? favoriteEventId,MemoryContributionDiagnostic[] contributions);
public sealed record MemoryObjectDiagnostic(string source,string area,float success,float failure,float objectBias,string[] positiveEvents,string[] negativeEvents);
public sealed record MemoryLeisureDiagnostic(LeisurePlaceScore referenceScore,LeisurePlaceScore? lastSelection);
public sealed record MemoryDiagnosticSnapshot(bool available,string? error,string chaoId,string name,double seconds,double gardenSeconds,
    int capacity,int count,bool pastUnknown,string lastArea,string[] visited,MemoryEventDiagnostic[] events,
    MemoryPartnerDiagnostic[] partners,MemoryPlaceDiagnostic[] places,MemoryObjectDiagnostic[] objects,MemoryLeisureDiagnostic[] leisure,
    int positive,int neutral,int negative,string? mostImportantId,string? latestId,string? favoritePlaceId);

// A detached bounded view. Never Validate/Record/Visit/Grow/Credit or repair source data.
public static class ChaoMemoryDiagnostics
{
    private static string Identity(ChaoMemoryEvent e)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{e.Kind,e.Source,e.Area,e.Participants})));
    private static WorldPoint? Point(WorldPoint? p)=>p is {} v ? new(v.X,v.Y,v.Z) : null;
    private static bool Finite(double x)=>double.IsFinite(x) && x>=0;
    private static void Check(ChaoMemory h)
    {
        if(h==null || !Finite(h.Seconds) || h.Events==null || h.Visited==null || h.Events.Count>ChaoMemory.Capacity || h.Visited.Count>32)
            throw new ArgumentException("Memory collections or active time cannot be represented safely; source was not changed.");
        if(h.LastArea==null || h.LastArea.Length>96 || h.Visited.Any(area=>!DiscoveryMemory.ValidId(area)))throw new ArgumentException("Invalid retained area metadata; source was not changed.");
        foreach(var e in h.Events)
            if(e==null || !Enum.IsDefined(e.Kind) || !DiscoveryMemory.ValidId(e.Source) || !DiscoveryMemory.ValidId(e.Area) ||
               e.Description==null || e.Description.Length>180 || e.Participants==null || e.Participants.Length>3 || e.Participants.Any(p=>!DiscoveryMemory.ValidId(p)) ||
               !Finite(e.FirstAt) || !Finite(e.LastAt) || e.FirstAt>e.LastAt || e.LastAt>h.Seconds ||
               !float.IsFinite(e.Valence) || e.Valence is < -1 or >1 || !float.IsFinite(e.Importance) || e.Importance is < .05f or >1 || e.Occurrences is <1 or >20 ||
               e.Place is {} p && (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) || Math.Abs(p.X)>10000 || Math.Abs(p.Z)>10000))
                throw new ArgumentException("A retained event cannot be represented safely; source was not changed.");
        if(h.Events.Select(Identity).Distinct(StringComparer.Ordinal).Count()!=h.Events.Count)throw new ArgumentException("Duplicate retained event identities; source was not changed.");
    }
    public static MemoryDiagnosticSnapshot Capture(PetState state,string chaoId,IReadOnlyDictionary<string,PetState> roster)
    {
        try
        {
            var h=state.History;Check(h);
            double gardenSeconds=roster.TryGetValue("main",out var gardenOwner) ? gardenOwner.GardenSeconds : state.GardenSeconds;
            if(!Finite(gardenSeconds))throw new ArgumentException("Invalid garden time; source was not changed.");
            MemoryParticipantDiagnostic Participant(string id)=>roster.TryGetValue(id,out var peer) ? new(id,peer.Name,true) : new(id,id,false);
            var events=h.Events.Select(e=>new MemoryEventDiagnostic(Identity(e),e.Kind.ToString(),e.Source,e.Area,e.Description,e.FirstAt,e.LastAt,
                Math.Max(0,h.Seconds-e.LastAt),e.LastAt-e.FirstAt,e.Valence,e.Valence>0 ? "Positive" : e.Valence<0 ? "Negative" : "Neutral",
                e.Importance,e.Influence(h.Seconds),1800+e.Importance*18000,e.Occurrences,Point(e.Place),e.Participants.Select(Participant).ToArray())).ToArray();
            string? Id(ChaoMemoryEvent? e)=>e==null ? null : Identity(e);
            var partners=roster.Keys.Concat(events.SelectMany(e=>e.participants.Select(p=>p.id))).Where(id=>id!=chaoId).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).Select(id=>
            {
                var rows=events.Where(e=>e.participants.Any(p=>p.id==id)).ToArray();var peer=Participant(id);
                float sum=rows.Sum(e=>e.valence*e.influence);
                return new MemoryPartnerDiagnostic(id,peer.name,peer.inRoster,rows.Length,rows.Count(e=>e.valence>0),rows.Count(e=>e.valence<0),sum,sum*.07f,h.PartnerBias(id),
                    rows.Select(e=>new MemoryContributionDiagnostic(e.id,e.valence*e.influence*.07f)).ToArray(),
                    roster.TryGetValue("main",out var root) ? ChaoSocialPartnerScorer.Evaluate(root,chaoId,state,id,available:false) : null,
                    state.SocialSelection.LastCandidates.FirstOrDefault(c=>c.Id==id));
            }).ToArray();
            var places=events.Where(e=>e.place!=null).DistinctBy(e=>(e.area,e.place)).Select(anchor=>
            {
                var p=anchor.place!.Value;var rows=events.Where(e=>e.area==anchor.area && e.place is {} v && (v.X-p.X)*(v.X-p.X)+(v.Z-p.Z)*(v.Z-p.Z)<9).ToArray();
                return new MemoryPlaceDiagnostic(anchor.id,anchor.area,p,rows.Sum(e=>e.valence*e.influence)*.18f,h.PlaceBias(anchor.area,p),Id(h.FavoritePlace(anchor.area)),
                    rows.Select(e=>new MemoryContributionDiagnostic(e.id,e.valence*e.influence*.18f)).ToArray());
            }).ToArray();
            var objects=events.DistinctBy(e=>(e.source,e.area)).Select(e=>
            {
                var result=ChaoObjectBiasScorer.Evaluate(h.Events,h.Seconds,e.source,e.area);
                return new MemoryObjectDiagnostic(e.source,e.area,result.Success,result.Failure,result.Bias,
                    events.Where(x=>x.source==e.source && x.area==e.area && x.valence>0).Select(x=>x.id).ToArray(),events.Where(x=>x.source==e.source && x.area==e.area && x.valence<0).Select(x=>x.id).ToArray());
            }).ToArray();
            return new(true,null,chaoId,state.Name,h.Seconds,gardenSeconds,ChaoMemory.Capacity,events.Length,h.PastUnknown,h.LastArea,
                h.Visited.OrderBy(s=>s,StringComparer.Ordinal).ToArray(),events,partners,places,objects,
                LeisureDestinationCatalog.All.Select(d=>new MemoryLeisureDiagnostic(ChaoLeisurePreferenceScorer.Evaluate(h,LeisureDestinationCatalog.Area,d.Id),state.LeisureSelection?.Destinations.FirstOrDefault(s=>s.Id==d.Id))).ToArray(),
                events.Count(e=>e.valence>0),events.Count(e=>e.valence==0),events.Count(e=>e.valence<0),
                events.OrderByDescending(e=>e.importance).ThenByDescending(e=>e.influence).FirstOrDefault()?.id,events.OrderByDescending(e=>e.lastAt).FirstOrDefault()?.id,Id(h.FavoritePlace(h.LastArea)));
        }
        catch(ArgumentException e)
        {
            return new(false,e.Message,chaoId,state.Name,0,0,ChaoMemory.Capacity,0,false,"",Array.Empty<string>(),Array.Empty<MemoryEventDiagnostic>(),
                Array.Empty<MemoryPartnerDiagnostic>(),Array.Empty<MemoryPlaceDiagnostic>(),Array.Empty<MemoryObjectDiagnostic>(),Array.Empty<MemoryLeisureDiagnostic>(),0,0,0,null,null,null);
        }
    }
}
