"""Read-only retained-memory browser. No requests, save writes or inferred events."""
import json
import time
import tkinter as tk
from tkinter import ttk

SORTS = {'Last recorded': 'lastAt', 'First recorded': 'firstAt', 'Importance': 'importance',
         'Influence now': 'influence', 'Occurrences': 'occurrences'}
FILTERS = ('kind', 'area', 'source', 'participant', 'tone', 'description')


def active_time(value):
    value = float(value)
    readable = f'{value/3600:.2f} h' if value >= 3600 else f'{value/60:.2f} min' if value >= 60 else f'{value:.2f} s'
    return f'{readable} ({value:.6f} active seconds)'


class MemoryBrowser:
    def __init__(self):
        self.data = None
        self.filters = dict.fromkeys(FILTERS, '')
        self.sort = 'Last recorded'
        self.selected = None
        self.subtab = 0
        self.scroll = 0
        self.stamp = None
        self.received_at = None
        self.error = ''

    def update(self, data, session=None, publication=None):
        self.data = data
        stamp = (session, publication)
        if publication is not None and stamp != self.stamp:
            self.received_at = time.monotonic()
        self.stamp = stamp
        self.error = ''

    def freshness(self):
        if self.error:
            return 'Last received snapshot — ' + self.error
        if self.received_at is None:
            return 'Last received snapshot — freshness unconfirmed'
        if time.monotonic()-self.received_at > 5:
            return 'Snapshot not advancing; game may be closed or paused. Values are from the last received snapshot.'
        return 'Latest received game snapshot · influence evaluated at its active memory time'

    def rows(self):
        result = []
        for e in (self.data or {}).get('events', []):
            if any(self.filters[k] and self.filters[k].casefold() != str(e[k]).casefold() for k in ('kind', 'area', 'source', 'tone')):
                continue
            peer = self.filters['participant'].casefold()
            if peer and not any(p['id'].casefold() == peer for p in e['participants']):
                continue
            if self.filters['description'].casefold() not in e['description'].casefold():
                continue
            result.append(e)
        return sorted(result, key=lambda e: (-e[SORTS[self.sort]], e['id']))

    def clear(self):
        self.filters = dict.fromkeys(FILTERS, '')


class MemoryPanel(ttk.Frame):
    def __init__(self, parent, browser):
        super().__init__(parent, padding=5)
        self.browser = browser
        self.updating = False
        self.summary = tk.StringVar()
        self.fresh = tk.StringVar()
        self.summary_label = ttk.Label(self, textvariable=self.summary, wraplength=650)
        self.summary_label.pack(fill='x')
        self.fresh_label = ttk.Label(self, textvariable=self.fresh, wraplength=650, foreground='#555555')
        self.fresh_label.pack(fill='x', pady=(2, 4))
        self.bind('<Configure>', lambda e: (self.summary_label.configure(wraplength=max(250,e.width-12)), self.fresh_label.configure(wraplength=max(250,e.width-12))))
        self.tabs = ttk.Notebook(self)
        self.tabs.pack(fill='both', expand=True)
        self.tabs.enable_traversal()
        self.pages = {}
        for name in ('Timeline', 'Influence', 'Places', 'Partners'):
            page = ttk.Frame(self.tabs); self.pages[name] = page; self.tabs.add(page, text=name)
        self.build_timeline(self.pages['Timeline'])
        self.object_tree, self.object_details = self.analysis_page(self.pages['Influence'], ('source','area','success','failure','objectBias'),
            'ChaoMemory.ObjectBias → DiscoveryDecisions. Separate from toy/fruit preferences. A possible contribution, not proof of a current choice.')
        self.place_tree, self.place_details = self.analysis_page(self.pages['Places'], ('type','area','point_or_destination','bias','weight'),
            'Raw PlaceBias: horizontal distance² < 9. Leisure uses its own scorer and excludes garden/leisure/ from SpatialBias. Reference weights assume availability; routes still require safe access.')
        self.partner_tree, self.partner_details = self.analysis_page(self.pages['Partners'], ('name','id','count','positive','negative','partnerBias'),
            'Possible contribution to partner selection. Memory PartnerBias is separate from directional SocialLinks. Sleeping/unavailable peers are not made eligible by memory.')
        self.object_tree.bind('<<TreeviewSelect>>', self.select_object)
        self.place_tree.bind('<<TreeviewSelect>>', self.select_place)
        self.partner_tree.bind('<<TreeviewSelect>>', self.select_partner)
        self.tabs.select(min(browser.subtab, 3))
        self.tabs.bind('<<NotebookTabChanged>>', lambda _e: setattr(browser,'subtab',self.tabs.index(self.tabs.select())))
        self.refresh()

    @staticmethod
    def table(parent, columns, height=6):
        frame=ttk.Frame(parent)
        tree=ttk.Treeview(frame, columns=columns, show='headings', height=height, selectmode='browse')
        for c in columns:
            title={'influence':'Influence now','lastAt':'LastAt (active s)','partnerBias':'Memory PartnerBias','point_or_destination':'Point / destination','objectBias':'ObjectBias'}.get(c,c.replace('_',' ').title())
            tree.heading(c, text=title)
            tree.column(c, width=190 if c=='description' else 115, minwidth=60, stretch=False)
        vs=ttk.Scrollbar(frame, command=tree.yview); hs=ttk.Scrollbar(frame, orient='horizontal', command=tree.xview)
        tree.configure(yscrollcommand=vs.set, xscrollcommand=hs.set)
        tree.grid(row=0,column=0,sticky='nsew');vs.grid(row=0,column=1,sticky='ns');hs.grid(row=1,column=0,sticky='ew')
        frame.rowconfigure(0,weight=1);frame.columnconfigure(0,weight=1)
        return frame,tree

    @staticmethod
    def text(parent):
        frame=ttk.Frame(parent); text=tk.Text(frame,height=6,wrap='word',state='disabled',takefocus=True)
        scroll=ttk.Scrollbar(frame,command=text.yview);text.configure(yscrollcommand=scroll.set)
        scroll.pack(side='right',fill='y');text.pack(side='left',fill='both',expand=True)
        return frame,text

    @staticmethod
    def write(widget, text, keep_scroll=False):
        old=widget.yview()[0];widget.configure(state='normal');widget.delete('1.0','end');widget.insert('end',text);widget.configure(state='disabled')
        widget.yview_moveto(old if keep_scroll else 0)

    def analysis_page(self, page, columns, hint):
        label=ttk.Label(page,text=hint,wraplength=440);label.pack(fill='x',pady=3)
        page.bind('<Configure>',lambda e:label.configure(wraplength=max(250,e.width-10)))
        panes=ttk.Panedwindow(page,orient='vertical');panes.pack(fill='both',expand=True)
        frame,tree=self.table(panes,columns,4);panes.add(frame,weight=1)
        frame,details=self.text(panes);panes.add(frame,weight=1)
        return tree,details

    def build_timeline(self, page):
        form=ttk.Frame(page);form.pack(fill='x')
        self.filter_vars={};self.filter_boxes={}
        for index,key in enumerate(FILTERS):
            row,col=divmod(index,3); col*=2
            ttk.Label(form,text={'participant':'Participant ID','tone':'Valence','description':'Search'}.get(key,key.title())).grid(row=row,column=col,sticky='w')
            var=tk.StringVar(value=self.browser.filters[key]);self.filter_vars[key]=var
            box=ttk.Combobox(form,textvariable=var,width=10,state='normal' if key in ('area','source','description','participant') else 'readonly')
            box.grid(row=row,column=col+1,sticky='ew',padx=2,pady=2);form.columnconfigure(col+1,weight=1)
            self.filter_boxes[key]=box;var.trace_add('write',lambda *_a:self.filter_changed())
        bottom=ttk.Frame(page);bottom.pack(fill='x',pady=2)
        self.sort_var=tk.StringVar(value=self.browser.sort)
        sort=ttk.Combobox(bottom,textvariable=self.sort_var,values=list(SORTS),width=17,state='readonly');sort.pack(side='left')
        sort.bind('<<ComboboxSelected>>',lambda _e:self.filter_changed())
        ttk.Button(bottom,text='Clear filters',command=self.clear_filters).pack(side='left',padx=3)
        self.result=tk.StringVar();ttk.Label(bottom,textvariable=self.result).pack(side='left')
        panes=ttk.Panedwindow(page,orient='vertical');panes.pack(fill='both',expand=True)
        frame,self.tree=self.table(panes,('kind','description','area','valence','importance','influence','occurrences','lastAt'),5);panes.add(frame,weight=2)
        frame,self.details=self.text(panes);panes.add(frame,weight=1)
        self.tree.bind('<<TreeviewSelect>>',self.select_event)

    def filter_changed(self):
        if self.updating:return
        self.browser.filters={k:v.get() for k,v in self.filter_vars.items()}
        peer=self.browser.filters['participant']
        self.browser.filters['participant']=getattr(self,'participant_choices',{}).get(peer,peer)
        self.browser.sort=self.sort_var.get();self.render_timeline()

    def clear_filters(self):
        self.updating=True
        for var in self.filter_vars.values():var.set('')
        self.updating=False;self.browser.clear();self.render_timeline()

    def refresh(self):
        if not self.winfo_exists():return
        data=self.browser.data or {};self.fresh.set(self.browser.freshness())
        if not data.get('available'):
            self.summary.set('Memory diagnostics unavailable: '+str(data.get('error') or 'No compatible game snapshot.'))
            self.render_timeline()
            for tree in (self.object_tree,self.place_tree,self.partner_tree):self.fill(tree,[])
            for details in (self.object_details,self.place_details,self.partner_details):self.write(details,'Memory diagnostics unavailable; no source was repaired.')
            return
        history='Earlier history may be unknown (PastUnknown=true).' if data['pastUnknown'] else 'PastUnknown=false; retained records are still not a complete life history.'
        self.summary.set(f"Read-only · {data['name']} · {data['chaoId']}\nMemory time {data['seconds']:.2f}s · Garden clock (root GardenSeconds) {data['gardenSeconds']:.2f}s\n"
            f"Retained memories: {data['count']}/{data['capacity']} · Visited areas: {len(data['visited'])} · Positive {data['positive']} / Neutral {data['neutral']} / Negative {data['negative']}\n"+history)
        self.updating=True
        for key in ('kind','area','source','tone'):
            self.filter_boxes[key].configure(values=['']+sorted({e[key] for e in data['events']}))
        peers={p['id']:p for e in data['events'] for p in e['participants']}
        self.participant_choices={(f"{p['name']} [{p['id']}]" if p['inRoster'] else f"{p['id']} — Not in current roster"):p['id'] for p in peers.values()}
        self.filter_boxes['participant'].configure(values=['']+sorted(self.participant_choices))
        self.updating=False
        self.render_timeline()
        self.fill(self.object_tree,[(json.dumps([r['source'],r['area']]),(r['source'],r['area'],f"{r['success']:.6f}",f"{r['failure']:.6f}",f"{r['objectBias']:+.6f}")) for r in data['objects']])
        place_rows=[('p:'+r['id'],('Raw point',r['area'],str(r['point']),f"{r['placeBias']:+.6f}",'Not a selection weight')) for r in data['places']]
        place_rows += [('l:'+r['referenceScore']['Id'],('Leisure reference','garden',r['referenceScore']['Id'],f"{r['referenceScore']['TotalBias']:+.6f}",f"{r['referenceScore']['SelectionWeight']:.6f}")) for r in data['leisure']]
        self.fill(self.place_tree,place_rows)
        self.fill(self.partner_tree,[(r['id'],(r['name'],r['id'],r['count'],r['positive'],r['negative'],f"{r['partnerBias']:+.6f}")) for r in data['partners']])
        self.select_object(None);self.select_place(None);self.select_partner(None)
        if not self.object_tree.selection():self.write(self.object_details,self.summary_text(),True)

    def summary_text(self):
        data=self.browser.data
        def describe(key):
            e=self.event(data.get(key));return f"{e['kind']} · {e['area']} · {e['description']}" if e else 'None recorded'
        peers={}
        for e in data['events']:
            if e['valence']>0:
                for p in e['participants']:peers[(p['name'],p['id'])]=peers.get((p['name'],p['id']),0)+1
        return ('Retained-memory summary — counts do not measure current happiness.\n'
            f"Most significant: {describe('mostImportantId')}\nMost recent: {describe('latestId')}\nFavoritePlace in current recorded area: {describe('favoritePlaceId')}\n"
            'Participants in positive records:\n'+'\n'.join(f'{name} [{ident}]: {count}' for (name,ident),count in sorted(peers.items(),key=lambda p:-p[1]))+
            '\nVisited areas: '+', '.join(data['visited'])+'\nSelect a source/area above for the actual ObjectBias breakdown. This is separate from LearnedPreferences, ActivityHabits, SocialLinks and current ChaoMood.')

    @staticmethod
    def fill(tree, rows):
        selection=tree.selection();y=tree.yview()[0];x=tree.xview()[0]
        tree.delete(*tree.get_children())
        for ident,values in rows:tree.insert('', 'end',iid=ident,values=values)
        if selection and tree.exists(selection[0]):tree.selection_set(selection[0])
        tree.yview_moveto(y);tree.xview_moveto(x)

    def render_timeline(self):
        if self.tree.get_children():self.browser.scroll=self.tree.yview()[0]
        rows=self.browser.rows();self.fill(self.tree,[(e['id'],(e['kind'],e['description'][:90],e['area'],f"{e['valence']:+.3f}",f"{e['importance']:.3f}",f"{e['influence']:.6f}",e['occurrences'],f"{e['lastAt']:.2f}s")) for e in rows]);self.tree.yview_moveto(self.browser.scroll)
        self.result.set(f'{len(rows)} matching records')
        if self.browser.selected and self.tree.exists(self.browser.selected):self.tree.selection_set(self.browser.selected)
        if not rows:self.write(self.details,'No retained memories match these filters.')
        else:self.select_event(None)

    def event(self, ident):
        return next((e for e in (self.browser.data or {}).get('events',[]) if e['id']==ident),None)

    def event_lines(self, contributions):
        return '\n'.join(f"{self.event(c['eventId'])['kind']} · {self.event(c['eventId'])['description']} · {c['contribution']:+.8f} before clamp" for c in contributions)

    def select_event(self, _event):
        selection=self.tree.selection()
        if not selection:
            if self.tree.get_children():self.write(self.details,'Select a retained event for details. First/last times describe one merged record, not exact times of every repetition.')
            return
        e=self.event(selection[0]);keep=self.browser.selected==e['id'];self.browser.selected=e['id']
        peers='\n'.join(f"- {p['name']} [{p['id']}]"+('' if p['inRoster'] else ' — Not in current roster') for p in e['participants']) or 'No recorded participants'
        point=e['place'];position='Position not recorded' if point is None else f"X={point['X']}, Y={point['Y']}, Z={point['Z']}"
        self.write(self.details,f"Memory details\nKind: {e['kind']}\nSource: {e['source']}\nArea: {e['area']}\nDescription:\n{e['description']}\n"
            f"Valence: {e['valence']:+.6f} ({e['tone']})\nImportance: {e['importance']:.6f}\nCurrent influence: {e['influence']:.9f}\nHalf-life: {active_time(e['halfLife'])}\n"
            f"First recorded: {active_time(e['firstAt'])}\nLast recorded: {active_time(e['lastAt'])}\nMemory age: {active_time(e['ageSeconds'])}\nFirst-to-last span: {active_time(e['spanSeconds'])}\n"
            f"Occurrences: {e['occurrences']} — merged count, not a list of exact timestamps\nParticipants:\n{peers}\nPosition: {position}\n"
            'Influence = Importance × 0.5^(max(0, memory time − LastAt)/(1800 + Importance × 18000)).\n'
            'Positive memory is not current mood. Eligibility, needs and protected actions can take priority.',keep)

    def select_object(self, _event):
        selected=self.object_tree.selection()
        if not selected:return
        data=self.browser.data;r=next(r for r in data['objects'] if json.dumps([r['source'],r['area']])==selected[0])
        def names(ids):return '\n'.join(f"{self.event(i)['kind']} · {self.event(i)['description']}" for i in ids) or 'None'
        self.write(self.object_details,f"Source: {r['source']}\nArea: {r['area']}\nSuccess: {r['success']:.9f}\nFailure: {r['failure']:.9f}\nObjectBias: {r['objectBias']:+.9f}\n"
            'Success sums Influence × (1 + min(Occurrences,4) × .15) for positive records; failure sums Influence for negative records.\n'
            'Result = clamp(success × .10 − failure × .24/(1+success), −.25, +.25). Valence sign selects the branch; its magnitude is not multiplied here.\n'
            f"Positive events:\n{names(r['positiveEvents'])}\nNegative events:\n{names(r['negativeEvents'])}\n"
            'Used in DiscoveryDecisions appeal/response with perception, familiarity, energy, current task and other traits. This view does not establish a current decision or discovery eligibility.\n'
            'Summary: '+json.dumps({k:data.get(k) for k in ('mostImportantId','latestId','favoritePlaceId')},ensure_ascii=False),True)

    def select_place(self, _event):
        selected=self.place_tree.selection()
        if not selected:return
        key=selected[0];data=self.browser.data
        if key.startswith('p:'):
            r=next(r for r in data['places'] if r['id']==key[2:]);f=self.event(r['favoriteEventId'])
            text=f"Area: {r['area']}\nPoint: {r['point']}\nBefore clamp: {r['beforeClamp']:+.9f}\nPlaceBias: {r['placeBias']:+.9f}\nFavoritePlace in area: {f['description'] if f else 'None recorded'}\n"
            text+='Valence × Influence × .18 per event within horizontal distance² < 9; clamp(−.30,+.60).\n'+self.event_lines(r['contributions'])
            text+='\nRaw PlaceBias/FavoritePlace are used by expedition landmark exploration; this is not a garden route score or a reachability check.'
        else:
            r=next(r for r in data['leisure'] if r['referenceScore']['Id']==key[2:]);score=r['referenceScore']
            text='Leisure reference — assumes available, no recent-repeat penalty. Does not grant access to a destination.\n'+ '\n'.join(f'{k}: {v}' for k,v in score.items())
            text+='\nLearnedBias: matching positive Pleasant completion; SpatialBias: other positioned events near any route point, excluding all garden/leisure/ sources. Each event contributes at most once.\n'
            text+='Last actual candidate snapshot (may be older; not current eligibility): '+json.dumps(r['lastSelection'],ensure_ascii=False)
        self.write(self.place_details,text,True)

    def select_partner(self, _event):
        selected=self.partner_tree.selection()
        if not selected:return
        r=next(r for r in self.browser.data['partners'] if r['id']==selected[0])
        text=f"Partner: {r['name']} [{r['id']}]"+('' if r['inRoster'] else ' — Not in current roster')
        text+=f"\nRelevant memories: {r['count']} · Positive {r['positive']} · Negative {r['negative']}\nRaw valence × influence sum: {r['rawSum']:+.9f}\nBefore clamp (× .07): {r['beforeClamp']:+.9f}\nPartnerBias: {r['partnerBias']:+.9f}\nClamp: −.30…+.40\nContributing events:\n"+self.event_lines(r['contributions'])
        text+='\nChaoSocialPartnerScorer reference, distance 0, unavailable until checked by gameplay:\n'+json.dumps(r['referenceScore'],indent=2,ensure_ascii=False)
        text+='\nLast actual candidate snapshot (not current eligibility):\n'+json.dumps(r['lastSelection'],indent=2,ensure_ascii=False)
        text+='\nRelationshipContribution uses directional SocialLinks; MemoryContribution uses PartnerBias. Garden introductions, music joining and ball partner selection use this scorer. Expedition social preference also reads PartnerBias. This is only a possible contribution.'
        self.write(self.partner_details,text,True)


class MemoryEditorMixin:
    def sync_memory_snapshot(self, snapshot):
        if not hasattr(self,'memory_browsers'):self.memory_browsers={}
        for row in snapshot.get('chao',[]):
            browser=self.memory_browsers.setdefault(row['id'],MemoryBrowser())
            browser.update(row.get('memoryDiagnostics'),snapshot.get('session'),snapshot.get('publication'))
        self.refresh_memory()

    def build_memory(self):
        browser=self.memory_browsers.setdefault(self.selected,MemoryBrowser())
        self.memory_panel=MemoryPanel(self.tabs,browser)
        self.memory_target=self.selected
        self.tabs.insert(3,self.memory_panel,text='Memory')

    def refresh_memory(self):
        panel=getattr(self,'memory_panel',None)
        if panel is not None and panel.winfo_exists() and getattr(self,'memory_target',None)==self.selected:panel.refresh()

    def memory_unavailable(self):
        browser=getattr(self,'memory_browsers',{}).get(self.selected)
        if browser:browser.error='Game snapshot temporarily unavailable; retained data was not replaced.'
        self.refresh_memory()
