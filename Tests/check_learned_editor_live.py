"""Actual Tk Apply buttons against the isolated running Godot bridge."""
import importlib.util
import json
import os
from pathlib import Path
import tkinter as tk

directory=Path(os.environ['MORI_EDITOR_DIRECTORY']).resolve()
if 'artifacts' not in directory.parts or not (directory/'learned-editor-live-ready.json').exists():
    raise RuntimeError('Requires isolated native live test')
spec=importlib.util.spec_from_file_location('learned_live_app',Path(__file__).resolve().parents[1]/'Tools/chao_editor.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
window=tk.Tk();editor=module.ChaoEditor(window)
def experience_tab():return next(t for t in editor.tabs.tabs() if editor.tabs.tab(t,'text')=='Experience')
baseline={r['id']:r for r in editor.read()['chao']}
tasks=[(ident,'toys','rattle',.123) for ident in baseline]+[('main','fruits','Garden Fruit',-.123),('main','habits','Leisure',.075)]
results=[];waiting=None;remaining=1500


def finish(passed,error=''):
    report=dict(passed=passed,error=error,results=results)
    (directory/'learned-editor-live-ui.json').write_text(json.dumps(report),encoding='utf-8')
    (directory/'learned-editor-live-done.json').write_text(json.dumps(report),encoding='utf-8')
    for task in window.tk.call('after','info'):window.after_cancel(task)
    window.destroy()


def step():
    global waiting,remaining
    try:
        remaining-=1
        if remaining<=0:raise RuntimeError('Live test timeout')
        if waiting:
            if editor.pending is not None:
                window.after(80,step);return
            ident,g,i,n=waiting
            row=next(r for r in editor.read()['chao'] if r['id']==ident)
            entry=next(r for r in row['learnedEditor']['rows'] if r['group']==g and r['id']==i)
            old=next(r for r in baseline[ident]['learnedEditor']['rows'] if r['group']==g and r['id']==i)
            if abs(entry['storedBias']-n)>.000001 or entry['experienceCount']!=old['experienceCount'] or entry['lastCreditSeconds']!=(old['lastCreditSeconds'] if old['entryPresent'] else 0):
                raise RuntimeError('Bias/history acknowledgement mismatch: '+editor.status.get())
            if row['personality']!=baseline[ident]['personality'] or editor.learned_patch():raise RuntimeError('Personality or confirmed baseline changed')
            results.append(dict(id=ident,group=g,item=i,bias=n));waiting=None
        if not tasks:
            editor.list.selection_clear(0,'end');editor.list.selection_set(list(editor.roster).index('main'));editor.select_chao(None)
            window.geometry('710x540');editor.tabs.select(experience_tab());editor.experience_sections.select(0);window.update()
            button=editor.learned_apply_buttons[0]
            if not button.winfo_ismapped() or button.winfo_rooty()+button.winfo_height()>window.winfo_rooty()+window.winfo_height() or button.winfo_rootx()+button.winfo_width()>window.winfo_rootx()+window.winfo_width():raise RuntimeError('Apply clipped at minimum size')
            if len(editor.learned_vars)!=17:raise RuntimeError('Missing rows')
            window.update_idletasks();finish(True);return
        ident,g,i,n=tasks.pop(0)
        editor.list.selection_clear(0,'end');editor.list.selection_set(list(editor.roster).index(ident));editor.select_chao(None)
        editor.tabs.select(experience_tab());editor.experience_sections.select(4 if g=='habits' else 0)
        key=(g,i);editor.learned_vars[key].set(str(n));window.update_idletasks()
        if str(n) not in str(editor.learned_patch()):raise RuntimeError('Missing sparse preview')
        editor.reset_learned(key)
        if editor.learned_patch():raise RuntimeError('Local reset did not restore baseline')
        editor.learned_vars[key].set(str(n));editor.learned_apply_buttons[0].invoke()
        if editor.pending is None:raise RuntimeError('Apply rejected: '+editor.status.get())
        waiting=(ident,g,i,n);window.after(80,step)
    except Exception as error:finish(False,str(error))


window.after(400,step);window.mainloop()
if not json.loads((directory/'learned-editor-live-ui.json').read_text(encoding='utf-8'))['passed']:raise SystemExit(1)
