using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mori;

// Manual adjustments only: never Credit, completion IDs, memory events or gameplay writes.
internal static class ChaoLearnedEditor
{
    internal static IEnumerable<(string Group,string Id)> Keys=>LearnedPreferenceIds.Toys.Select(id=>("toys",id))
        .Concat(LearnedPreferenceIds.Fruits.Select(id=>("fruits",id))).Concat(ChaoActivityHabits.Supported.Select(a=>("habits",a.ToString())));
    private static void Validate(PetState p){p.LearnedPreferences.Validate();p.ActivityHabits.Validate();}
    private static string Number(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    private static bool SupportedPrecision(string raw)
    {
        // Inspect the JSON spelling before decimal conversion, which can round
        // extremely small or long numbers to a different value.
        var parts=raw.Split(new[]{'e','E'});int exponent=0;
        if(parts.Length>1 && (!int.TryParse(parts[1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out exponent) || exponent is < -1000 or >1000))return false;
        string mantissa=parts[0].TrimStart('-');int dot=mantissa.IndexOf('.');
        string digits=mantissa.Replace(".","");if(digits.All(c=>c=='0'))return true;
        int trailing=digits.Length-digits.TrimEnd('0').Length;
        int scale=(dot<0 ? 0 : mantissa.Length-dot-1)-exponent-trailing;
        return scale<=3;
    }
    private static (float Bias,int Count,double Update,double Credit)? Entry(PetState p,string group,string id)
    {
        if(group=="habits")return p.ActivityHabits.Entries.TryGetValue(id,out var h) ? (h.Bias,h.ExperienceCount,h.LastUpdateSeconds,h.LastCreditSeconds) : null;
        var entries=group=="toys" ? p.LearnedPreferences.ToyPreferences : p.LearnedPreferences.FruitPreferences;
        return entries.TryGetValue(id,out var e) ? (e.Bias,e.ExperienceCount,e.LastUpdateSeconds,e.LastCreditSeconds) : null;
    }
    internal static string Revision(PetState p)
    {
        Validate(p);var text=new StringBuilder("MoriGarden/learned-editor/v1:").Append(p.LearnedPreferences.SchemaVersion).Append(':').Append(p.ActivityHabits.SchemaVersion);
        foreach(var (group,id) in Keys)
        {
            text.Append('|').Append(group).Append(':').Append(id).Append(':');
            if(Entry(p,group,id) is {} e)text.Append('1').Append(':').Append(e.Bias.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(e.Count.ToString(CultureInfo.InvariantCulture)).Append(':').Append(Number(e.Update)).Append(':').Append(Number(e.Credit));
            else text.Append('0');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    internal static object Capture(PetState p)
    {
        Validate(p);
        return new {itemSchemaVersion=p.LearnedPreferences.SchemaVersion,habitSchemaVersion=p.ActivityHabits.SchemaVersion,activeSeconds=p.History.Seconds,
            rows=Keys.Select(key=>
            {
                var e=Entry(p,key.Group,key.Id);
                float effective=key.Group=="habits" ? p.ActivityHabits.EffectiveBias(Enum.Parse<ChaoActivity>(key.Id),p.History.Seconds) : p.LearnedPreferences.EffectiveBias(key.Group=="toys" ? LearnedItemGroup.Toy : LearnedItemGroup.Fruit,key.Id,p.History.Seconds);
                return new {group=key.Group,id=key.Id,entryPresent=e!=null,storedBias=e?.Bias ?? 0,effectiveBias=effective,experienceCount=e?.Count ?? 0,lastUpdateSeconds=e?.Update,lastCreditSeconds=e?.Credit};
            }).ToArray()};
    }
    internal static void SetEffectiveBiasForEditor(PetState next,string group,string id,decimal bias,double now)
    {
        if(!Keys.Contains((group,id)))throw new ArgumentException("Unknown learned preference group or ID: "+group+"/"+id);
        decimal low=group=="habits" ? 0 : -.30m, high=group=="habits" ? .10m : .30m;
        if(bias<low || bias>high || decimal.Round(bias,3)!=bias)throw new ArgumentException("Bias is outside its range or exceeds three decimal places.");
        if(!double.IsFinite(now) || now<0)throw new ArgumentException("Invalid active Chao time.");
        var old=Entry(next,group,id);
        if(old is {} e && (e.Update>now || e.Credit>now))throw new ArgumentException("Learned entry is ahead of the active Chao clock; no adjustment applied.");
        if(old==null && bias==0)return;
        if(group=="habits")next.ActivityHabits.Entries[id]=new(){Bias=(float)bias,ExperienceCount=old?.Count ?? 0,LastCreditSeconds=old?.Credit ?? 0,LastUpdateSeconds=now};
        else
        {
            var entries=group=="toys" ? next.LearnedPreferences.ToyPreferences : next.LearnedPreferences.FruitPreferences;
            entries[id]=new(){Bias=(float)bias,ExperienceCount=old?.Count ?? 0,LastCreditSeconds=old?.Credit ?? 0,LastUpdateSeconds=now};
        }
        Validate(next);
    }
    internal static void Apply(PetState next,PetState current,JsonObject patch,string? revision)
    {
        if(patch.Count==0)return;
        var edits=new List<(string Group,string Id,decimal Bias)>();
        foreach(var (group,node) in patch)
        {
            if(group is not ("toys" or "fruits" or "habits") || node is not JsonObject values)throw new ArgumentException("Invalid learned adjustment group.");
            foreach(var (id,value) in values)
            {
                if(value==null || value.GetValueKind()!=JsonValueKind.Number || !SupportedPrecision(value.ToJsonString()) || !decimal.TryParse(value.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out decimal n))throw new ArgumentException("Bias must be finite and numeric, with at most three decimal places.");
                edits.Add((group,id,n));
            }
        }
        if(edits.Count==0)return;
        if(revision!=Revision(current))throw new ArgumentException("Learned data changed since this snapshot. Read from game; no changes applied.");
        foreach(var e in edits)SetEffectiveBiasForEditor(next,e.Group,e.Id,e.Bias,current.History.Seconds);
    }
}
