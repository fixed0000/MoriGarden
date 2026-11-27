"""Real Tk widgets against generated temporary IPC snapshots, never personal saves."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import tkinter as tk
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("personality_editor_app", Path(__file__).resolve().parents[1]/"Tools/chao_editor.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
from chao_personality_editor import TRAITS, PersonalityDraft


class PersonalityEditorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="MoriEditorPersonalityTest-")
        self.directory = Path(self.temp.name)
        self.rows = []
        for index, name in enumerate(("Mori", "White Chao", "Black Chao", "Yellow Chao")):
            ident = "main" if index == 0 else f"resident-{index}"
            self.rows.append(dict(id=ident, parameters=dict(Name=name, Fullness=72.0, Energy=90.0, Joy=60.0, Curiosity=-6, Glutton=False),
                statistics={n: dict(Grade=2, Level=0, Points=0, Progress=0) for n in ("Swim", "Fly", "Run", "Power", "Stamina", "Luck", "Intelligence")},
                personalitySchemaVersion=1, personality={k: i-6+index for i, k in enumerate(TRAITS)}, personalityRevision="revision-"+ident,
                relationships=[]))
        for row in self.rows:
            row["relationships"] = [dict(id=r["id"], name=r["parameters"]["Name"], met=False, score=0, status="Acquaintance") for r in self.rows if r != row]
        self.snapshot = dict(session="test-session", lastRequest="", status="Ready", profileDirectory=str(self.directory), chao=self.rows)
        self.write()
        self.window = tk.Tk()
        self.window.withdraw()
        with patch.dict(os.environ, MORI_SAVE_DIRECTORY=str(self.directory)):
            self.editor = module.ChaoEditor(self.window)
        self.window.update_idletasks()

    def tearDown(self):
        for task in self.window.tk.call("after", "info"):
            self.window.after_cancel(task)
        self.window.destroy()
        self.temp.cleanup()

    def write(self):
        (self.directory/"chao-editor-state.json").write_text(json.dumps(self.snapshot), encoding="utf-8")

    def request(self):
        return json.loads((self.directory/"chao-editor-request.json").read_text(encoding="utf-8"))

    def test_all_controls_values_and_sync(self):
        e = self.editor
        self.assertEqual(set(e.personality_vars), set(TRAITS))
        self.assertEqual(e.personality_patch(), {})
        for trait in TRAITS:
            self.assertEqual(int(e.personality_vars[trait].get()), self.rows[0]["personality"][trait])
        e.personality_slider_changed("Curious", "81")
        self.assertEqual(e.personality_vars["Curious"].get(), "81")
        e.personality_vars["Curious"].set("-45")
        self.assertEqual(e.personality_sliders["Curious"].get(), -45)
        self.assertEqual(e.personality_patch(), {"Curious": -45})

    def test_validation_exact_bounds_and_invalid_input(self):
        for value in ("-100", "+100"):
            self.editor.personality_vars["Curious"].set(value)
            self.assertEqual(self.editor.personality_patch()["Curious"], int(value))
        for value in ("-101", "101", "hello", "1.2", "", "True"):
            self.editor.personality_vars["Curious"].set(value)
            with self.assertRaises(ValueError):
                self.editor.personality_patch()
            self.assertTrue(self.editor.personality_apply.instate(("disabled",)))

    def test_changed_only_target_and_revision(self):
        e = self.editor
        e.personality_vars["Naive"].set("-100")
        e.apply(personality_only=True)
        request = self.request()
        self.assertEqual(request["personality"], {"Naive": -100})
        self.assertEqual(request["personalityRevision"], "revision-main")
        self.assertEqual(request["target"], "main")
        self.assertEqual(request["patch"], {})
        self.assertNotIn("ActivityHabits", request)
        before = request.copy()
        e.apply(personality_only=True)
        self.assertEqual(self.request(), before)

    def test_unchanged_general_apply_has_no_personality_section(self):
        e = self.editor
        e.variables["Joy"].set("75")
        e.apply()
        self.assertEqual(self.request()["patch"], {"Joy": 75.0})
        self.assertNotIn("personality", self.request())

    def test_reset_row_all_and_return_to_baseline(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        e.personality_vars["Chatty"].set("90")
        e.reset_personality("Curious")
        self.assertEqual(e.personality_patch(), {"Chatty": 90})
        e.reset_personality()
        self.assertEqual(e.personality_patch(), {})
        e.personality_vars["Curious"].set("80")
        e.personality_vars["Curious"].set("-6")
        self.assertEqual(e.personality_patch(), {})
        self.assertFalse((self.directory/"chao-editor-request.json").exists())

    def test_tabs_and_auto_refresh_preserve_dirty_values(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        e.tabs.select(1)
        e.tabs.select(2)
        self.rows[0]["personality"]["Curious"] = -80
        self.rows[0]["personalityRevision"] = "new-revision"
        e.update_roster(self.snapshot)
        self.assertEqual(e.personality_vars["Curious"].get(), "80")
        self.assertEqual(e.active_personality_draft().revision, "revision-main")
        self.assertEqual(e.active_personality_draft().baseline["Curious"], -6)

    def test_read_and_pet_switch_cancel_protection(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        with patch("chao_personality_editor.messagebox.askyesnocancel", return_value=None):
            e.reload()
            e.list.selection_clear(0, "end")
            e.list.selection_set(1)
            e.select_chao(None)
        self.assertEqual(e.selected, "main")
        self.assertEqual(e.personality_vars["Curious"].get(), "80")

    def test_pet_switch_discard_and_independence(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        with patch("chao_personality_editor.messagebox.askyesnocancel", return_value=False):
            e.list.selection_clear(0, "end")
            e.list.selection_set(1)
            e.select_chao(None)
        self.assertEqual(e.selected, "resident-1")
        self.assertEqual(int(e.personality_vars["Curious"].get()), -5)
        e.personality_vars["Chatty"].set("99")
        e.apply(personality_only=True)
        self.assertEqual(self.request()["target"], "resident-1")
        self.assertFalse(e.personality_drafts["main"].dirty)

    def test_navigation_can_apply_before_switching(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        with patch("chao_personality_editor.messagebox.askyesnocancel", return_value=True):
            self.assertFalse(e.guard_personality_navigation())
        self.assertIsNotNone(e.pending)
        self.assertEqual(e.selected, "main")

    def test_ack_rebases_and_error_keeps_draft(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        e.apply(personality_only=True)
        self.snapshot.update(lastRequest=e.pending, status="Invalid personality")
        self.write()
        e.check(0)
        self.assertEqual(e.personality_vars["Curious"].get(), "80")
        self.assertTrue(e.active_personality_draft().dirty)
        e.apply(personality_only=True)
        self.rows[0]["personality"]["Curious"] = 80
        self.rows[0]["personalityRevision"] = "confirmed-80"
        self.snapshot.update(lastRequest=e.pending, status="Applied")
        self.write()
        e.check(0)
        self.assertEqual(e.personality_patch(), {})
        self.assertEqual(e.active_personality_draft().baseline["Curious"], 80)

    def test_new_session_blocks_old_draft(self):
        e = self.editor
        e.personality_vars["Curious"].set("80")
        self.snapshot["session"] = "restarted"
        e.update_roster(self.snapshot)
        self.assertEqual(e.personality_vars["Curious"].get(), "80")
        with self.assertRaises(ValueError):
            e.personality_patch()

    def test_missing_and_future_models_are_unavailable(self):
        for values in (None, {}, dict(Curious=0)):
            bad = dict(self.rows[0], personality=values)
            with self.assertRaises(ValueError):
                PersonalityDraft(bad, "session")
        with self.assertRaises(ValueError):
            PersonalityDraft(dict(self.rows[0], personalitySchemaVersion=2), "session")

    def test_stats_and_relationships_still_work(self):
        e = self.editor
        self.assertEqual(len(e.stat_variables), 7)
        e.stat_preset("S")
        e.relation_met.set(True)
        e.relation_score.set("25")
        e.apply()
        request = self.request()
        self.assertEqual(request["statistics"]["Run"]["Points"], 2772)
        self.assertEqual(request["relationship"]["score"], 25)
        self.assertNotIn("personality", request)

    def test_readonly_and_legacy_adapters_not_sent(self):
        e = self.editor
        e.variables["Curiosity"].set("99")
        e.variables["Joy"].set("75")
        e.apply()
        self.assertEqual(self.request()["patch"], {"Joy": 75.0})
        self.assertNotIn("personality", self.request())

    def test_minimum_size_has_visible_apply_and_scrollable_traits(self):
        self.window.deiconify()
        self.window.geometry("710x540")
        self.editor.tabs.select(2)
        self.window.update_idletasks()
        self.window.update()
        button = self.editor.personality_apply
        self.assertTrue(button.winfo_ismapped())
        self.assertLessEqual(button.winfo_rootx()+button.winfo_width(), self.window.winfo_rootx()+self.window.winfo_width())
        self.assertLessEqual(button.winfo_rooty()+button.winfo_height(), self.window.winfo_rooty()+self.window.winfo_height())
        self.assertEqual(len(self.editor.personality_vars), 13)

    def test_keyboard_focus_reveals_last_trait_in_scroll_area(self):
        self.window.deiconify()
        self.window.geometry("710x540")
        self.editor.tabs.select(2)
        self.window.update()
        entry = self.editor.personality_controls[-1]
        canvas = entry.master.master
        entry.event_generate("<FocusIn>")
        self.window.update_idletasks()
        self.assertGreaterEqual(entry.winfo_rooty(), canvas.winfo_rooty())
        self.assertLessEqual(entry.winfo_rooty()+entry.winfo_height(), canvas.winfo_rooty()+canvas.winfo_height())

    def test_every_control_owns_its_variable(self):
        self.assertEqual(len({v._name for v in self.editor.personality_vars.values()}), 13)
        self.assertEqual(len({v._name for v in self.editor.personality_sliders.values()}), 13)

    def test_mismatched_form_owner_cannot_send_to_previous_pet(self):
        self.editor.selected = "resident-1"
        self.editor.personality_vars["Curious"].set("80")
        self.editor.apply(personality_only=True)
        self.assertFalse((self.directory/"chao-editor-request.json").exists())
        self.assertIn("Selected Chao changed", self.editor.status.get())

    def test_transient_snapshot_lock_retries_without_writing(self):
        text = json.dumps(self.snapshot)
        with patch.object(Path, "read_text", side_effect=[PermissionError("busy"), text]) as read:
            self.assertEqual(self.editor.read()["session"], "test-session")
            self.assertEqual(read.call_count, 2)
        self.assertFalse((self.directory/"chao-editor-request.json").exists())

    def test_general_apply_supports_personality_only(self):
        self.editor.personality_vars["Carefree"].set("80")
        self.editor.apply()
        self.assertEqual(self.request()["personality"], {"Carefree": 80})
        self.assertEqual(self.request()["patch"], {})

    def test_clean_form_tracks_latest_confirmed_snapshot(self):
        self.rows[0]["personality"]["Curious"] = 35
        self.rows[0]["personalityRevision"] = "latest"
        self.editor.update_roster(self.snapshot)
        self.assertEqual(self.editor.personality_vars["Curious"].get(), "35")
        self.assertEqual(self.editor.active_personality_draft().revision, "latest")
        self.assertEqual(self.editor.personality_patch(), {})

    def test_personality_only_ack_preserves_other_unsaved_controls(self):
        e = self.editor
        e.variables["Joy"].set("75")
        e.stat_variables["Run"]["Progress"].set("20")
        e.relation_score.set("12")
        e.personality_vars["Curious"].set("80")
        e.apply(personality_only=True)
        self.assertEqual(self.request()["patch"], {})
        self.rows[0]["personality"]["Curious"] = 80
        self.rows[0]["personalityRevision"] = "confirmed"
        self.snapshot.update(lastRequest=e.pending, status="Applied")
        self.write()
        e.check(0)
        self.assertEqual(e.variables["Joy"].get(), "75")
        self.assertEqual(e.stat_variables["Run"]["Progress"].get(), "20")
        self.assertEqual(e.relation_score.get(), "12")
        self.assertEqual(e.personality_patch(), {})

    def test_missing_selected_pet_retains_blocked_draft(self):
        self.editor.personality_vars["Curious"].set("80")
        self.snapshot["chao"] = self.rows[1:]
        self.editor.update_roster(self.snapshot)
        self.assertEqual(self.editor.selected, "main")
        self.assertEqual(self.editor.personality_vars["Curious"].get(), "80")
        with self.assertRaises(ValueError):
            self.editor.personality_patch()

    def test_session_change_during_pending_does_not_ack_old_request(self):
        self.editor.personality_vars["Curious"].set("80")
        self.editor.apply(personality_only=True)
        self.snapshot.update(session="restarted", lastRequest=self.editor.pending, status="Applied")
        self.write()
        self.editor.check(0)
        self.assertEqual(self.editor.personality_vars["Curious"].get(), "80")
        self.assertIsNone(self.editor.pending)
        self.assertIn("restarted", self.editor.status.get())

    def test_unavailable_form_never_creates_zero_controls(self):
        self.rows[0]["personality"] = None
        self.editor.personality_drafts.pop("main")
        self.write()
        self.editor.reload(force=True)
        self.assertEqual(self.editor.personality_vars, {})
        self.assertTrue(self.editor.personality_apply.instate(("disabled",)))


if __name__ == "__main__":
    unittest.main()
