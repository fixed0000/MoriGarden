import importlib.util
from pathlib import Path
import unittest
import json
import tempfile
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location("chao_editor", Path(__file__).resolve().parents[1] / "Tools" / "chao_editor.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class Value:
    def __init__(self, value): self.value = str(value)
    def get(self): return self.value
    def set(self, value): self.value = str(value)


class EditorStatTests(unittest.TestCase):
    def editor(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        editor.snapshot = {"statistics": {"Run": dict(Grade=2, Level=60, Points=1980, Progress=0)}}
        editor.stat_variables = {"Run": {key: Value(value) for key, value in dict(Grade="C", Level=60, Points=1980, Progress=0).items()}}
        return editor

    def test_unchanged_import_is_not_rejected(self):
        self.assertEqual(self.editor().statistics_patch(), {})

    def test_presets_are_consistent(self):
        for grade, points in (("E",1287),("C",1881),("S",2772)):
            editor=self.editor();editor.stat_preset(grade)
            patch=editor.statistics_patch()["Run"]
            self.assertEqual((patch["Level"],patch["Points"],patch["Grade"]),(99,points,"EDCBAS".index(grade)))

    def test_limits_and_impossible_combinations(self):
        for key, value in (("Level",100),("Points",3267),("Progress",-1),("Level",0)):
            editor=self.editor();editor.stat_variables["Run"][key].set(value)
            with self.assertRaises(ValueError):editor.statistics_patch()

    def test_legacy_bar_can_be_edited_without_erasing_strength(self):
        editor=self.editor();editor.stat_variables["Run"]["Progress"].set(45)
        self.assertEqual(editor.statistics_patch()["Run"]["Points"],1980)

    def test_idle_gesture_view_is_read_only_and_handles_unavailable_models(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        editor.selected = "main"
        editor.gesture_text = Value("")
        editor.gesture_rows = {"main": dict(CurrentIdleGesture="PawFidget", GestureElapsed=0.6, GestureDuration=1.8,
            TimeUntilNextGesture=7, CurrentGestureAmplitude=0.07, CuriousContribution=0, EnergeticContribution=0.22,
            CarefreeContribution=0, SolitudePoseAdjustment=0, CurrentPose="Idle", Suppressed=False,
            SuppressionReason="", Started=3, Completed=2)}
        original = json.dumps(editor.gesture_rows)
        editor.refresh_idle_gestures()
        self.assertIn("PawFidget", editor.gesture_text.get())
        self.assertIn("0.0700", editor.gesture_text.get())
        self.assertEqual(json.dumps(editor.gesture_rows), original)
        editor.gesture_rows = {}
        editor.refresh_idle_gestures()
        self.assertIn("Unavailable", editor.gesture_text.get())

    def test_expression_view_is_read_only_live_model_data(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        editor.selected = "main"
        editor.expression_text = Value("")
        editor.expression_rows = {"main": dict(DominantEmotion="Happy", EmotionIntensity=0.7,
            Valence=0.6, Arousal=0.8, CurrentExpression="Happy", CurrentPose="Walk", Source="Mood",
            AppliedPersonalityMultiplier=1.12, FinalVisualIntensity=0.784, SmoothedVisualIntensity=0.78,
            BlinkAmount=0, ProtectedAnimation=False, SuppressionReason="")}
        original = json.dumps(editor.expression_rows)
        editor.refresh_expression()
        self.assertIn("Walk", editor.expression_text.get())
        self.assertIn("0.784", editor.expression_text.get())
        self.assertEqual(json.dumps(editor.expression_rows), original)
        editor.expression_rows = {}
        editor.refresh_expression()
        self.assertIn("No active visual model", editor.expression_text.get())

    def test_daily_rhythm_is_read_only_and_formats_live_context(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        editor.selected = "main"
        editor.rhythm_text = Value("")
        editor.rhythm_rows = {"main": dict(active=True, gardenSeconds=360, personalPhaseOffset=3,
            energy=70, cycle=dict(CurrentPhase=3, CyclePosition=505, DaylightIntensity=0, NightIntensity=1),
            restEligibility=dict(Allowed=True, Reason="Mild night fatigue"), safePlace="safe available bed",
            adjustments=[dict(Activity="Sleep", Adjustment=0.08)],
            habits=dict(entries=[dict(activity="Play", effectiveBias=0.1)]))}
        original = json.dumps(editor.rhythm_rows)
        editor.refresh_daily_rhythm()
        self.assertIn("Night", editor.rhythm_text.get())
        self.assertIn("+0.0800", editor.rhythm_text.get())
        self.assertIn("Play: +0.1000", editor.rhythm_text.get())
        self.assertEqual(json.dumps(editor.rhythm_rows), original)
        editor.rhythm_rows["main"]["active"] = False
        editor.refresh_daily_rhythm()
        self.assertIn("Inactive", editor.rhythm_text.get())

    def test_unrelated_patch_does_not_replace_personality(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        personality = dict(Curious=-31, Kindness=12, Energetic=-75, Naive=-60,
                           Solitude=7, Vitality=22, BigEater=75, Regain=-9,
                           Skillful=34, Charm=-17, Chatty=6, Carefree=23, Fickle=-10)
        editor.snapshot = dict(session="isolated-session", target="main", parameters=dict(Joy=60),
                               personality=personality.copy(), learnedPreferences={"entries": [{"id": "rattle", "effectiveBias": 0.2}]},
                               socialPreferences={"partners": [{"id": "peer", "score": {"PartnerWeight": 1.2}}]},
                               activityHabits={"entries": [{"activity": "Play", "effectiveBias": 0.08}]}, dailyRhythm={"gardenSeconds": 360}, expression={"CurrentExpression": "Happy"}, relationships=[])
        editor.variables = dict(Joy=Value(75))
        editor.stat_variables = {}
        editor.relationship_peer = None
        editor.pending = None
        editor.status = Value("")
        editor.window = SimpleNamespace(after=lambda *args: None)
        editor.read = lambda: dict(session="isolated-session", chao=[dict(id="main")])
        with tempfile.TemporaryDirectory(prefix="MoriEditorPersonality-") as directory:
            editor.directory = Path(directory)
            editor.apply()
            request = json.loads((editor.directory/"chao-editor-request.json").read_text())
            self.assertEqual(request["patch"], dict(Joy=75))
            self.assertNotIn("personality", request)
            self.assertNotIn("learnedPreferences", request)
            self.assertNotIn("LearnedPreferences", request["patch"])
            self.assertNotIn("socialPreferences", request)
            self.assertNotIn("SocialSelection", request["patch"])
            self.assertNotIn("idleGestures", request)
            self.assertNotIn("IdleGestures", request["patch"])
            self.assertNotIn("expression", request)
            self.assertNotIn("expression", request["patch"])
            self.assertNotIn("dailyRhythm", request)
            self.assertNotIn("GardenSeconds", request["patch"])
            self.assertNotIn("activityHabits", request)
            self.assertNotIn("ActivityHabits", request["patch"])
            self.assertEqual(editor.snapshot["activityHabits"]["entries"][0]["effectiveBias"], 0.08)
            self.assertEqual(editor.snapshot["socialPreferences"]["partners"][0]["score"]["PartnerWeight"], 1.2)
            self.assertEqual(editor.snapshot["learnedPreferences"]["entries"][0]["effectiveBias"], 0.2)
            self.assertEqual(editor.snapshot["personality"], personality)


if __name__ == "__main__": unittest.main()
