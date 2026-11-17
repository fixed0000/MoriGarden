"""Explicit live UI integration test against the isolated stats-preview only."""
import importlib.util
import json
import os
from pathlib import Path
import tkinter as tk

directory = Path(os.environ["MORI_EDITOR_DIRECTORY"])
if directory.name != "stats-preview":
    raise RuntimeError("Live tests require the separate stats-preview garden")
spec = importlib.util.spec_from_file_location("chao_editor", Path(__file__).resolve().parents[1]/"Tools"/"chao_editor.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
window = tk.Tk()
editor = module.ChaoEditor(window)
window.title("Chao Editor — automated test")
results = []
steps = iter(("S", "E", "C"))
deadline = 150
waiting = None

def step():
    global deadline, waiting
    try:
        deadline -= 1
        if deadline <= 0:
            raise RuntimeError("Timed out waiting for the running game")
        if waiting is not None:
            if editor.pending is not None:
                window.after(100,step)
                return
            state=editor.read()
            row=next(row for row in state["chao"] if row["id"]==editor.selected)
            actual=row["statistics"]["Run"]
            expected=99*(13+3*"EDCBAS".index(waiting))
            if actual["Grade"]!="EDCBAS".index(waiting) or actual["Level"]!=99 or actual["Points"]!=expected:
                raise RuntimeError(f"Frontend did not apply {waiting}: {actual}, {editor.status.get()}")
            results.append(dict(grade=waiting,points=actual["Points"]))
            waiting=None
        editor.tabs.select(1)
        window.update_idletasks()
        if len(editor.stat_variables)!=7 or len(editor.roster)!=3:
            raise RuntimeError("The UI omitted stats or a test pet")
        panel=editor.tabs.nametowidget(editor.tabs.select())
        for widget in panel.winfo_children():
            if widget.winfo_ismapped() and (widget.winfo_rootx()+widget.winfo_width()>window.winfo_rootx()+window.winfo_width() or widget.winfo_rooty()+widget.winfo_height()>window.winfo_rooty()+window.winfo_height()):
                raise RuntimeError("Stat controls extend beyond the window")
        grade=next(steps,None)
        if grade is None:
            (directory/"editor-live-check.json").write_text(json.dumps(dict(passed=True,results=results,stats=7,pets=3,geometry="controls fit window")),encoding="utf-8")
            window.destroy()
            return
        editor.stat_preset(grade)
        editor.apply()
        if editor.pending is None:
            raise RuntimeError("Frontend rejected the preset: "+editor.status.get())
        waiting=grade
        window.after(100,step)
    except Exception as error:
        (directory/"editor-live-check.json").write_text(json.dumps(dict(passed=False,error=str(error))),encoding="utf-8")
        window.destroy()

window.after(500,step)
window.mainloop()
