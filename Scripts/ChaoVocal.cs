using Godot;

namespace Mori;

// Existing per-actor game caller remains the only periodic executor. Model owns the channel across editor rebinds.
internal sealed class ChaoVocal
{
    public void Step(Node3D art,PetState state,bool sleeping,float dt,bool actionBlocked=false)
    {if(art is ImportedChao model)model.StepVocal(state,sleeping,dt,actionBlocked);}
}

public partial class ImportedChao
{
    private ChaoVoicePolicy? vocal;
    private AudioStreamPlayer3D? voiceAudio;
    private Tween? voiceFade;
    private bool vocalWasSleeping;
    internal ChaoVocalDiagnostic? VocalDiagnostics=>vocal==null ? null : vocal.Diagnostic(personality?.VocalSignals.Count ?? 0) with { WhetherVocalIsPlaying=voiceAudio?.Playing==true };
    internal void BindVocal(string id)
    {
        vocal ??=new(id);
        if(voiceAudio==null){voiceAudio=new(){Name="ChaoVoice",MaxDistance=15,VolumeDb=-11};AddChild(voiceAudio);}
    }
    private bool VocalAllowed(bool sleeping,out string reason)
    {
        reason="";
        if(heldExternally || sportMotionDriven){reason="Held or sport animation";return false;}
        if(sleeping)return true;
        if(PoseKind is not (ChaoPoseKind.Idle or ChaoPoseKind.Walk)) {reason="Protected "+PoseKind;return false;}
        if(personality?.GardenActivity?.SelectedActivity is ChaoActivity.Play or ChaoActivity.Socialize or ChaoActivity.Eat or ChaoActivity.Sleep)
        {reason="Active gameplay interaction";return false;}
        return true;
    }
    internal void StepVocal(PetState state,bool sleeping,float dt,bool actionBlocked=false)
    {
        if(vocal==null || !state.CanAct)return;
        if(vocalWasSleeping!=sleeping){voiceFade?.Kill();voiceFade=null;voiceAudio?.Stop();vocalWasSleeping=sleeping;}
        bool allowed=VocalAllowed(sleeping,out string why);
        if(actionBlocked && !sleeping){allowed=false;why="Active social/music interaction";}
        bool important=!actionBlocked && !heldExternally && !sportMotionDriven && PoseKind is ChaoPoseKind.Idle or ChaoPoseKind.Walk or ChaoPoseKind.Distress;
        var decision=vocal.Step(dt,state.Personality,state.Mood.Snapshot,sleeping,state.Glutton || state.Aggressiveness>=25,allowed,why,state.VocalSignals,state.Mood.Now,important);
        if(decision!=null)PlayVocal(decision);
    }
    internal bool PlayAnimationVoice(string cue,float start)
    {
        if(vocal==null)return false;
        bool sleeping=personality?.Mood.Sleeping==true || PoseKind==ChaoPoseKind.Sleep;
        var kind=cue switch {"angry"=>ChaoVocalKind.Frustrated,"sniff"=>ChaoVocalKind.Sad,"breath"=>ChaoVocalKind.SleepBreath,"snore"=>ChaoVocalKind.Snore,_=>ChaoVocalKind.Neutral};
        if(sleeping && kind is not (ChaoVocalKind.SleepBreath or ChaoVocalKind.Snore))return true;
        // Explicit animation calls retain their choreography, including deliberately close replies.
        var d=vocal.Request(kind,ChaoVocalPriority.Animation,"animation:"+cue,personality?.Personality);
        if(d!=null)PlayVocal(d,start,cue=="sniff");return true;
    }
    private void PlayVocal(ChaoVoiceDecision d,float start=0,bool sniff=false)
    {
        if(voiceAudio==null){voiceAudio=new(){Name="ChaoVoice",MaxDistance=15,VolumeDb=-11};AddChild(voiceAudio);}
        voiceFade?.Kill();voiceFade=null;
        void Start()
        {
            voiceAudio.Stream=ChaoSounds.VocalStream(d.Kind,sniff);voiceAudio.PitchScale=d.Pitch;
            voiceAudio.VolumeDb=d.Kind is ChaoVocalKind.SleepBreath or ChaoVocalKind.Snore ? -19 : d.Kind==ChaoVocalKind.Sad ? -14 : -11;
            voiceAudio.Play(start);
        }
        // Explicit choreography may replace a lower voice. A bounded 8ms release avoids a hard PCM cut;
        // never add a second player or synthesize a replacement buffer.
        if(voiceAudio.Playing){voiceFade=CreateTween();voiceFade.TweenProperty(voiceAudio,"volume_db",-60f,.008);voiceFade.TweenCallback(Callable.From(Start));}
        else Start();
    }
}
