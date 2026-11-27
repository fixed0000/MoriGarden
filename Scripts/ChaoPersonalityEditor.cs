using System;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace Mori;

// Editor protocol only. No new saved schema, personality formula or migration.
internal static class ChaoPersonalityEditor
{
    internal static readonly string[] Names={"Curious","Kindness","Energetic","Naive","Solitude","Vitality","BigEater","Regain","Skillful","Charm","Chatty","Carefree","Fickle"};
    internal static readonly string[] Legacy={"Curiosity","Kindness","Glutton","Crybaby"};
    internal static string Revision(PetState state)
    {
        if(state.PersonalitySchemaVersion!=1 || state.Personality==null)throw new ArgumentException("Personality unavailable or unsupported. Read from game.");
        state.Personality.Validate();
        var text=new StringBuilder("MoriGarden/personality-editor/v1:1");
        foreach(string name in Names)text.Append(':').Append(Get(state.Personality,name).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    internal static int Get(ChaoPersonality p,string name)=>name switch
    {"Curious"=>p.Curious,"Kindness"=>p.Kindness,"Energetic"=>p.Energetic,"Naive"=>p.Naive,"Solitude"=>p.Solitude,"Vitality"=>p.Vitality,"BigEater"=>p.BigEater,"Regain"=>p.Regain,"Skillful"=>p.Skillful,"Charm"=>p.Charm,"Chatty"=>p.Chatty,"Carefree"=>p.Carefree,"Fickle"=>p.Fickle,_=>throw new ArgumentException("Unknown personality trait: "+name)};
    internal static void Apply(PetState next,PetState original,JsonObject patch,string? revision,JsonObject ordinary)
    {
        if(patch.Count==0)return;
        if(revision!=Revision(original))throw new ArgumentException("Personality changed since this snapshot. Read from game before applying; no changes applied.");
        if(ordinary.Any(p=>Legacy.Contains(p.Key)))throw new ArgumentException("Do not combine legacy personality adapters with canonical personality changes.");
        var p=original.Personality!.Copy();
        foreach(var (name,node) in patch)
        {
            if(!Names.Contains(name))throw new ArgumentException("Unknown personality trait: "+name);
            if(node is not JsonValue value || !value.TryGetValue<int>(out int n))throw new ArgumentException(name+" must be an integer between -100 and 100.");
            switch(name){case "Curious":p.Curious=n;break;case "Kindness":p.Kindness=n;break;case "Energetic":p.Energetic=n;break;case "Naive":p.Naive=n;break;case "Solitude":p.Solitude=n;break;case "Vitality":p.Vitality=n;break;case "BigEater":p.BigEater=n;break;case "Regain":p.Regain=n;break;case "Skillful":p.Skillful=n;break;case "Charm":p.Charm=n;break;case "Chatty":p.Chatty=n;break;case "Carefree":p.Carefree=n;break;case "Fickle":p.Fickle=n;break;}
        }
        p.Validate();next.Personality=p;
    }
}
