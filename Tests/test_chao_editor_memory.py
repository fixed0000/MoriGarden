"""Real Tk memory browser, generated data and isolated IPC snapshots."""
import copy
import json
import time
import unittest
from unittest.mock import patch
from Tests import test_chao_editor_learned as fixtures
from chao_memory_view import MemoryBrowser

KINDS=('FirstVisit','ReturnVisit','AnimalSeen','IndependentStudy','PlayerDiscovery','SharedStudy','HelpReceived','HelpGiven','FailedAttempt','Pleasant','Unpleasant','SharedRest','Introduction')


def memory(ident='main', count=13):
    events=[dict(id='event-'+str(i),kind=KINDS[i%13],source='source-'+str(i),area='garden' if i%2==0 else 'meadow',description='Plain discovery '+str(i),
        firstAt=float(i*2),lastAt=float(i*3+10),ageSeconds=1000-i*3-10,spanSeconds=i+10,valence=(i%3-1)*.5,tone=('Negative','Neutral','Positive')[i%3],importance=.1+i*.01,
        influence=.05+i*.001,halfLife=3600+i*180,occurrences=i%10+1,place=dict(X=1,Y=2,Z=3) if i%2==0 else None,
        participants=[dict(id='resident-1',name='White Chao',inRoster=True),dict(id='absent',name='absent',inRoster=False)] if i%2==0 else []) for i in range(count)]
    return dict(available=True,error=None,chaoId=ident,name='Mori' if ident=='main' else 'Other Chao',seconds=1000,gardenSeconds=2000,capacity=64,count=count,pastUnknown=True,lastArea='garden',visited=['garden','meadow'],events=events,
        partners=[dict(id='resident-1',name='White Chao',inRoster=True,count=1,positive=1,negative=0,rawSum=.5,beforeClamp=.035,partnerBias=.035,contributions=[dict(eventId='event-0',contribution=.035)],referenceScore=None,lastSelection=None)],
        places=[dict(id='event-0',area='garden',point=dict(X=1,Y=2,Z=3),beforeClamp=.03,placeBias=.03,favoriteEventId='event-0',contributions=[dict(eventId='event-0',contribution=.03)])] if count else [],
        objects=[dict(source='source-0',area='garden',success=.5,failure=.1,objectBias=.034,positiveEvents=['event-0'],negativeEvents=[])] if count else [],
        leisure=[],positive=sum(e['valence']>0 for e in events),neutral=sum(e['valence']==0 for e in events),negative=sum(e['valence']<0 for e in events),mostImportantId=events[-1]['id'] if count else None,latestId=events[-1]['id'] if count else None,favoritePlaceId='event-0' if count else None)


class MemoryEditorTests(unittest.TestCase):
    write=fixtures.LearnedEditorTests.write
    request=fixtures.LearnedEditorTests.request
    tearDown=fixtures.LearnedEditorTests.tearDown

    def setUp(self):
        fixtures.LearnedEditorTests.setUp(self)
        self.snapshot['publication']=1
        for r in self.rows:r['memoryDiagnostics']=memory(r['id'])
        self.write();self.editor.update_roster(self.snapshot);self.editor.reload(force=True)
        self.panel=self.editor.memory_panel;self.browser=self.panel.browser

    def ids(self):return list(self.panel.tree.get_children())
    def select(self,ident='event-0'):
        self.panel.tree.selection_set(ident);self.panel.select_event(None)
        return self.panel.details.get('1.0','end')

    def test_01_empty_memory(self):
        self.browser.update(memory(count=0));self.panel.refresh()
        self.assertEqual(self.ids(),[]);self.assertIn('No retained memories match',self.panel.details.get('1.0','end'))

    def test_02_all_kinds_and_capacity(self):
        self.assertEqual(set(e['kind'] for e in self.browser.rows()),set(KINDS))
        self.browser.update(memory(count=64));self.panel.refresh();self.assertEqual(len(self.ids()),64)

    def test_03_last_sort(self):self.assertEqual(self.ids()[0],'event-12')
    def test_04_first_sort(self):
        self.panel.sort_var.set('First recorded');self.panel.filter_changed();self.assertEqual(self.ids()[0],'event-12')
    def test_05_influence_sort(self):
        self.browser.data['events'][0]['influence']=1;self.panel.sort_var.set('Influence now');self.panel.filter_changed();self.assertEqual(self.ids()[0],'event-0')
    def test_06_importance_sort(self):
        self.browser.data['events'][0]['importance']=1;self.panel.sort_var.set('Importance');self.panel.filter_changed();self.assertEqual(self.ids()[0],'event-0')
    def test_07_occurrence_sort(self):
        self.panel.sort_var.set('Occurrences');self.panel.filter_changed();self.assertEqual(self.ids()[0],'event-9')

    def test_08_casefold_search_and_plain_text(self):
        e=self.browser.data['events'][0];e['description']='Discovery [set ::memory_injected 1] <script>'
        self.panel.filter_vars['description'].set('DISCOVERY [SET')
        self.assertEqual(self.ids(),['event-0']);self.assertIn(e['description'],self.select())
        self.assertEqual(self.window.tk.call('info','exists','::memory_injected'),0)

    def test_09_area_filter(self):
        self.panel.filter_vars['area'].set('GARDEN');self.assertEqual(len(self.ids()),7)
    def test_10_kind_filter(self):
        self.panel.filter_vars['kind'].set('FailedAttempt');self.assertEqual(self.ids(),['event-8'])
    def test_11_valence_filter(self):
        self.panel.filter_vars['tone'].set('Negative');self.assertTrue(all(e['valence']<0 for e in self.browser.rows()))
    def test_12_participant_filter(self):
        self.panel.filter_vars['participant'].set('RESIDENT-1');self.assertEqual(len(self.ids()),7)
    def test_13_clear_filters_does_not_clear_memory(self):
        before=copy.deepcopy(self.browser.data);self.panel.filter_vars['source'].set('SOURCE-0');self.assertEqual(self.ids(),['event-0']);self.panel.clear_filters()
        self.assertEqual(len(self.ids()),13);self.assertEqual(self.browser.data,before)
    def test_14_position(self):self.assertIn('X=1, Y=2, Z=3',self.select())
    def test_15_no_position_or_participants(self):
        text=self.select('event-1');self.assertIn('Position not recorded',text);self.assertIn('No recorded participants',text)
    def test_16_missing_participant(self):self.assertIn('absent [absent] — Not in current roster',self.select())

    def test_17_selection_and_scroll_refresh(self):
        self.select('event-4');self.snapshot['publication']=2;self.editor.update_roster(self.snapshot)
        self.assertEqual(self.panel.tree.selection(),('event-4',));self.assertEqual(self.browser.selected,'event-4')
    def test_18_filters_survive_refresh(self):
        self.panel.filter_vars['area'].set('garden');self.editor.update_roster(self.snapshot)
        self.assertEqual(self.panel.filter_vars['area'].get(),'garden');self.assertEqual(len(self.ids()),7)
    def test_19_influence_updates_without_lastAt_mutation(self):
        self.select();self.rows[0]['memoryDiagnostics']['events'][0]['influence']=.012345
        last=self.rows[0]['memoryDiagnostics']['events'][0]['lastAt'];self.snapshot['publication']=2;self.editor.update_roster(self.snapshot)
        self.assertIn('0.012345',self.panel.details.get('1.0','end'));self.assertEqual(self.browser.data['events'][0]['lastAt'],last)

    def test_20_no_memory_edit_controls(self):
        def descendants(w):
            for child in w.winfo_children():yield child;yield from descendants(child)
        buttons=[w.cget('text') for w in descendants(self.panel) if w.winfo_class()=='TButton']
        self.assertEqual(buttons,['Clear filters']);self.assertEqual(str(self.panel.details.cget('state')),'disabled')
    def test_21_no_memory_patch(self):
        self.select();self.panel.clear_filters();self.editor.variables['Joy'].set('75');self.editor.apply()
        r=self.request();self.assertNotIn('memoryDiagnostics',r);self.assertNotIn('History',r);self.assertEqual(r['patch'],{'Joy':75.0})
    def test_22_switch_identity_and_own_filters(self):
        self.panel.filter_vars['area'].set('garden');self.select('event-0')
        e=self.editor;e.list.selection_clear(0,'end');e.list.selection_set(1);e.select_chao(None)
        self.assertEqual(e.memory_panel.browser.data['chaoId'],'resident-1');self.assertEqual(e.memory_panel.browser.filters['area'],'')
        e.list.selection_clear(0,'end');e.list.selection_set(0);e.select_chao(None)
        self.assertEqual(e.memory_panel.browser.filters['area'],'garden');self.assertEqual(e.memory_panel.browser.selected,'event-0')
    def test_23_minimum_size(self):
        self.window.deiconify();self.window.geometry('710x540');self.editor.tabs.select(3);self.window.update()
        for widget in (self.panel.tree,self.panel.details,self.panel.filter_boxes['description']):
            self.assertTrue(widget.winfo_ismapped());self.assertGreater(widget.winfo_height(),12)
            self.assertLessEqual(widget.winfo_rootx()+widget.winfo_width(),self.window.winfo_rootx()+self.window.winfo_width())
    def test_24_personality_draft_survives(self):
        e=self.editor;e.personality_vars['Chatty'].set('80');e.tabs.select(3);self.select();e.update_roster(self.snapshot)
        self.assertEqual(e.personality_patch(),{'Chatty':80})
    def test_25_learned_draft_survives(self):
        e=self.editor;e.learned_vars['toys','rattle'].set('.2');e.tabs.select(3);self.select();e.update_roster(self.snapshot)
        self.assertEqual(e.learned_patch(),{'toys':{'rattle':.2}})
    def test_26_stopped_session_and_unavailable_snapshot(self):
        self.browser.received_at=time.monotonic()-10;self.panel.refresh();self.assertIn('closed or paused',self.panel.fresh.get())
        data=copy.deepcopy(self.browser.data)
        self.editor.memory_unavailable();self.assertEqual(self.browser.data,data);self.assertIn('Last received snapshot',self.panel.fresh.get())

    def test_27_invalid_memory_reports_error_without_old_tables(self):
        self.browser.update(dict(available=False,error='Invalid retained event',events=[]));self.panel.refresh()
        self.assertEqual(self.ids(),[]);self.assertEqual(self.panel.object_tree.get_children(),())
        self.assertEqual(self.panel.partner_tree.get_children(),());self.assertIn('Invalid retained event',self.panel.summary.get())

    def test_28_participant_display_label_uses_stable_id(self):
        self.panel.filter_vars['participant'].set('White Chao [resident-1]')
        self.assertEqual(self.browser.filters['participant'],'resident-1');self.assertEqual(len(self.ids()),7)

    def test_29_long_identity_at_minimum_size(self):
        self.browser.data['chaoId']='f'*32;self.browser.data['name']='White Chao';self.panel.refresh()
        self.window.deiconify();self.window.geometry('710x540');self.editor.tabs.select(3);self.window.update()
        self.assertGreater(self.panel.details.winfo_height(),15);self.assertGreater(self.panel.tree.winfo_height(),15)
        self.assertIn('root GardenSeconds',self.panel.summary.get())


if __name__=='__main__':unittest.main()
