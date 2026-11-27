"""Real Tk + native isolated bridge, strictly no outgoing edit requests."""
import importlib.util
import json
import os
from pathlib import Path
import tkinter as tk

directory=Path(os.environ['MORI_EDITOR_DIRECTORY']).resolve()
if 'artifacts' not in directory.parts or not (directory/'memory-diagnostics-live-ready.json').exists():raise RuntimeError('Requires isolated native memory live test')
spec=importlib.util.spec_from_file_location('memory_live_app',Path(__file__).resolve().parents[1]/'Tools/chao_editor.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
window=tk.Tk();editor=module.ChaoEditor(window)
snapshot=editor.read();baseline={r['id']:r['memoryDiagnostics'] for r in snapshot['chao']};tasks=list(baseline);results=[]
command=directory/'chao-editor-request.json';request_before=command.read_bytes() if command.exists() else None


def finish(passed,error=''):
    report=dict(passed=passed,error=error,results=results)
    (directory/'memory-diagnostics-live-ui.json').write_text(json.dumps(report),encoding='utf-8')
    (directory/'memory-diagnostics-live-done.json').write_text(json.dumps(report),encoding='utf-8')
    for task in window.tk.call('after','info'):window.after_cancel(task)
    window.destroy()


def step():
    try:
        if not tasks:
            if (command.read_bytes() if command.exists() else None)!=request_before:raise RuntimeError('Read-only memory sent an edit request')
            editor.list.selection_clear(0,'end');editor.list.selection_set(list(editor.roster).index('main'));editor.select_chao(None)
            editor.tabs.select(next(t for t in editor.tabs.tabs() if editor.tabs.tab(t,'text')=='Memory'))
            editor.memory_panel.tabs.select(0)
            window.geometry('710x540');window.update();panel=editor.memory_panel
            if not panel.tree.winfo_ismapped() or panel.tree.winfo_width()<100 or panel.details.winfo_height()<15:raise RuntimeError('Memory controls unavailable at minimum size')
            editor.personality_vars['Chatty'].set('80');editor.learned_vars['toys','rattle'].set('.2');editor.update_roster(editor.read())
            if editor.personality_patch().get('Chatty')!=80 or editor.learned_patch().get('toys',{}).get('rattle')!=.2:raise RuntimeError('Memory refresh damaged another editor draft')
            editor.reset_personality();editor.reset_learned();finish(True);return
        ident=tasks.pop(0);editor.list.selection_clear(0,'end');editor.list.selection_set(list(editor.roster).index(ident));editor.select_chao(None)
        editor.tabs.select(next(t for t in editor.tabs.tabs() if editor.tabs.tab(t,'text')=='Memory'));panel=editor.memory_panel;data=baseline[ident]
        if panel.browser.data['chaoId']!=ident or len(panel.tree.get_children())!=data['count']:raise RuntimeError('Chao history mixed or rows missing')
        if data['events']:
            event=data['events'][0];panel.tree.selection_set(event['id']);panel.select_event(None)
            if event['description'] not in panel.details.get('1.0','end'):raise RuntimeError('Full real description missing')
            panel.filter_vars['source'].set(event['source']);editor.update_roster(editor.read())
            if panel.browser.selected!=event['id'] or panel.filter_vars['source'].get()!=event['source']:raise RuntimeError('Refresh lost selection/filter')
            panel.clear_filters();panel.sort_var.set('Influence now');panel.filter_changed()
        for tab in range(4):panel.tabs.select(tab);window.update_idletasks()
        results.append(dict(id=ident,count=data['count'],allRows=True,details=True,filters=True))
        window.after(200,step)
    except Exception as error:finish(False,str(error))


window.after(300,step);window.mainloop()
if not json.loads((directory/'memory-diagnostics-live-ui.json').read_text(encoding='utf-8'))['passed']:raise SystemExit(1)
