import importlib.util
from pathlib import Path
import unittest

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


if __name__ == "__main__": unittest.main()
