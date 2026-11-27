"""Real frontend/backend IPC exercise. Requires the native isolated live-check marker."""
import importlib.util
import json
import os
from pathlib import Path
import tkinter as tk

directory = Path(os.environ["MORI_EDITOR_DIRECTORY"]).resolve()
if not (directory/"personality-editor-live-ready.json").exists() or "artifacts" not in directory.parts:
    raise RuntimeError("Requires an isolated native personality-editor live test")
spec = importlib.util.spec_from_file_location("chao_editor_live", Path(__file__).resolve().parents[1]/"Tools/chao_editor.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
window = tk.Tk()
editor = module.ChaoEditor(window)
window.title("Chao Editor — generated personality live test")
baseline = {row["id"]: row["personality"].copy() for row in editor.read()["chao"]}
tasks = [(ident, trait, value) for ident in baseline for trait, value in (("Chatty", 67 if baseline[ident]["Chatty"] != 67 else -67), ("Chatty", baseline[ident]["Chatty"]))]
grades = ["E", "C", "S"]
waiting = None
results = []
remaining = 1000


def finish(passed, error=""):
    report = dict(passed=passed, error=error, results=results, pets=list(baseline))
    (directory/"personality-editor-live-ui.json").write_text(json.dumps(report), encoding="utf-8")
    (directory/"personality-editor-live-done.json").write_text(json.dumps(report), encoding="utf-8")
    for task in window.tk.call("after", "info"):
        window.after_cancel(task)
    window.destroy()


def step():
    global waiting, remaining
    try:
        remaining -= 1
        if remaining <= 0:
            raise RuntimeError("Timed out waiting for live IPC")
        if waiting:
            if editor.pending is not None:
                window.after(60, step)
                return
            if len(waiting) == 2:
                _, grade = waiting
                row = next(r for r in editor.read()["chao"] if r["id"] == "main")
                stat = row["statistics"]["Run"]
                if stat["Grade"] != "EDCBAS".index(grade) or stat["Level"] != 99 or stat["Points"] != 99*(13+3*"EDCBAS".index(grade)):
                    raise RuntimeError("Existing sports frontend/backend round-trip failed")
                results.append(dict(id="main", grade=grade, points=stat["Points"]))
            else:
                ident, trait, value = waiting
                row = next(r for r in editor.read()["chao"] if r["id"] == ident)
                if row["personality"][trait] != value or editor.personality_patch() != {}:
                    raise RuntimeError("Apply acknowledgement or confirmed baseline is wrong: "+editor.status.get())
                expected = baseline[ident].copy()
                expected[trait] = value
                if row["personality"] != expected:
                    raise RuntimeError("Unedited traits changed")
                results.append(dict(id=ident, trait=trait, value=value))
            waiting = None
        if not tasks:
            if grades:
                grade = grades.pop(0)
                editor.list.selection_clear(0, "end")
                editor.list.selection_set(list(editor.roster).index("main"))
                editor.select_chao(None)
                editor.tabs.select(1)
                editor.stat_preset(grade)
                editor.apply()
                if editor.pending is None:
                    raise RuntimeError("Existing stats frontend rejected preset: "+editor.status.get())
                waiting = ("stats", grade)
                window.after(60, step)
                return
            snapshot = editor.read()
            if any(row["personality"] != baseline[row["id"]] for row in snapshot["chao"]):
                raise RuntimeError("Independent original personality values were not restored")
            window.geometry("710x540")
            editor.tabs.select(2)
            window.update_idletasks()
            button = editor.personality_apply
            if not button.winfo_ismapped() or button.winfo_rootx()+button.winfo_width() > window.winfo_rootx()+window.winfo_width():
                raise RuntimeError("Apply is clipped at minimum window size")
            finish(True)
            return
        ident, trait, value = tasks.pop(0)
        editor.list.selection_clear(0, "end")
        editor.list.selection_set(list(editor.roster).index(ident))
        editor.select_chao(None)
        editor.tabs.select(2)
        if len(editor.personality_vars) != 13 or len(editor.stat_variables) != 7:
            raise RuntimeError("Actual frontend omitted personality or sports controls")
        editor.personality_vars[trait].set(str(value))
        window.update_idletasks()
        editor.personality_apply.invoke()
        if editor.pending is None:
            raise RuntimeError("Actual Apply button rejected changes: "+editor.status.get())
        waiting = (ident, trait, value)
        window.after(60, step)
    except Exception as error:
        finish(False, str(error))


window.after(400, step)
window.mainloop()
if not json.loads((directory/"personality-editor-live-ui.json").read_text(encoding="utf-8"))["passed"]:
    raise SystemExit(1)
