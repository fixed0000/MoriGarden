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

    def test_unrelated_patch_does_not_replace_personality(self):
        editor = module.ChaoEditor.__new__(module.ChaoEditor)
        personality = dict(Curious=-31, Kindness=12, Energetic=-75, Naive=-60,
                           Solitude=7, Vitality=22, BigEater=75, Regain=-9,
                           Skillful=34, Charm=-17, Chatty=6, Carefree=23, Fickle=-10)
        editor.snapshot = dict(session="isolated-session", target="main", parameters=dict(Joy=60),
                               personality=personality.copy(), learnedPreferences={"entries": [{"id": "rattle", "effectiveBias": 0.2}]},
                               socialPreferences={"partners": [{"id": "peer", "score": {"PartnerWeight": 1.2}}]},
                               activityHabits={"entries": [{"activity": "Play", "effectiveBias": 0.08}]}, relationships=[])
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
            self.assertNotIn("activityHabits", request)
            self.assertNotIn("ActivityHabits", request["patch"])
            self.assertEqual(editor.snapshot["activityHabits"]["entries"][0]["effectiveBias"], 0.08)
            self.assertEqual(editor.snapshot["socialPreferences"]["partners"][0]["score"]["PartnerWeight"], 1.2)
            self.assertEqual(editor.snapshot["learnedPreferences"]["entries"][0]["effectiveBias"], 0.2)
            self.assertEqual(editor.snapshot["personality"], personality)


if __name__ == "__main__": unittest.main()
