"""Real Tk controls and isolated IPC; no personal saves."""
import copy
import unittest
from unittest.mock import patch
from Tests import test_chao_editor_personality as fixtures
from chao_learned_editor import KEYS, LearnedDraft, bias_number


class LearnedEditorTests(unittest.TestCase):
    write = fixtures.PersonalityEditorTests.write
    request = fixtures.PersonalityEditorTests.request
    tearDown = fixtures.PersonalityEditorTests.tearDown

    def setUp(self):
        fixtures.PersonalityEditorTests.setUp(self)
        for row in self.rows:
            row['learnedRevision'] = 'learned-'+row['id']
            row['learnedEditor'] = dict(itemSchemaVersion=1, habitSchemaVersion=1, activeSeconds=1000,
                rows=[dict(group=g, id=i, storedBias=0, effectiveBias=0, experienceCount=0,
                           lastUpdateSeconds=None, lastCreditSeconds=None, entryPresent=False) for g, i in KEYS])
        self.write()
        self.editor.update_roster(self.snapshot)
        self.editor.reload(force=True)

    def value(self, group, ident, n):
        self.editor.learned_vars[group, ident].set(n)

    def ack(self, success=True):
        if success:
            changes = self.request().get('learnedAdjustments', {})
            for r in self.rows[0]['learnedEditor']['rows']:
                if r['id'] in changes.get(r['group'], {}):
                    r['effectiveBias'] = r['storedBias'] = changes[r['group']][r['id']]
            self.rows[0]['learnedRevision'] = 'confirmed'
        self.snapshot.update(lastRequest=self.editor.pending, status='Applied' if success else 'Invalid learned update')
        self.write()
        self.editor.check(0)

    def test_01_all_seventeen_controls(self):
        self.assertEqual(set(self.editor.learned_vars), set(KEYS))
        self.assertEqual(self.editor.learned_patch(), {})

    def test_02_slider_and_number_sync(self):
        self.editor.learned_slider_changed(('toys', 'rattle'), '.2534')
        self.assertEqual(self.editor.learned_vars['toys', 'rattle'].get(), '0.253')
        self.value('toys', 'rattle', '-.123')
        self.assertAlmostEqual(self.editor.learned_sliders['toys', 'rattle'].get(), -.123)

    def test_03_strict_ranges_and_precision(self):
        for text in ('NaN', 'inf', 'True', '', '.3001', '.301', '-.301', 'abc', '1e-2'):
            with self.assertRaises(ValueError):
                bias_number(text, 'toys')
        for text in ('-.001', '.101'):
            with self.assertRaises(ValueError):
                bias_number(text, 'habits')
        self.assertEqual(float(bias_number('-.300', 'toys')), -.3)
        self.assertEqual(float(bias_number('.100', 'habits')), .1)

    def test_04_sparse_request_and_revision(self):
        self.value('toys', 'rattle', '.25')
        self.editor.apply(learned_only=True)
        r = self.request()
        self.assertEqual(r['learnedAdjustments'], {'toys': {'rattle': .25}})
        self.assertEqual(r['learnedRevision'], 'learned-main')
        self.assertEqual(r['target'], 'main')
        self.assertNotIn('personality', r)
        self.assertEqual(r['patch'], {})

    def test_05_all_three_groups(self):
        self.value('toys', 'flute', '.1'); self.value('fruits', 'Garden Fruit', '-.1'); self.value('habits', 'Leisure', '.08')
        self.assertEqual(len(self.editor.learned_patch()), 3)

    def test_06_reset_selected(self):
        self.value('toys', 'rattle', '.2'); self.value('habits', 'Play', '.08')
        self.editor.reset_learned(('toys', 'rattle'))
        self.assertEqual(self.editor.learned_patch(), {'habits': {'Play': .08}})

    def test_07_reset_all_is_local(self):
        self.value('toys', 'rattle', '.2'); self.editor.reset_learned()
        self.assertEqual(self.editor.learned_patch(), {})
        self.assertFalse((self.directory/'chao-editor-request.json').exists())

    def test_08_neutral_nonzero_and_missing(self):
        d = self.editor.active_learned_draft()
        d.current['toys','rattle']['storedBias'] = .00001
        self.editor.neutral_learned(('toys','rattle'))
        self.assertEqual(self.editor.learned_patch(), {'toys': {'rattle': 0}})
        self.editor.neutral_learned(('toys','flute'))
        self.assertNotIn('flute', self.editor.learned_patch()['toys'])

    def test_09_preview(self):
        self.value('toys', 'rattle', '.2')
        self.assertIn('rattle: +0.000', self.editor.learned_previews[0].get('1.0','end'))
        self.assertIn('+0.200', self.editor.learned_previews[0].get('1.0','end'))

    def test_10_tabs_preserve_draft(self):
        self.value('toys', 'rattle', '.2')
        self.editor.tabs.select(2); self.editor.tabs.select(3)
        self.assertEqual(self.editor.learned_patch(), {'toys': {'rattle': .2}})

    def test_11_decay_refresh_does_not_create_patch(self):
        d = self.editor.active_learned_draft()
        row = copy.deepcopy(self.rows[0]); row['learnedEditor']['rows'][0]['effectiveBias'] = .123456
        fresh = LearnedDraft(row, self.snapshot['session']); d.refresh(fresh)
        self.assertEqual(d.patch(), {})
        self.assertEqual(d.values['toys','toy-car'], '0.123')

    def test_12_stale_real_learning_keeps_draft(self):
        self.value('toys', 'rattle', '.2')
        self.rows[0]['learnedRevision'] = 'real-learning'
        self.editor.update_roster(self.snapshot)
        self.assertTrue(self.editor.active_learned_draft().stale)
        self.assertEqual(self.editor.active_learned_draft().revision, 'learned-main')
        self.assertEqual(self.editor.learned_vars['toys','rattle'].get(), '.2')

    def test_13_new_session_blocks(self):
        self.value('toys', 'rattle', '.2'); self.snapshot['session'] = 'new'
        self.editor.update_roster(self.snapshot)
        with self.assertRaises(ValueError): self.editor.learned_patch()

    def test_14_switch_cancel(self):
        self.value('toys', 'rattle', '.2')
        with patch('chao_learned_editor.messagebox.askyesnocancel', return_value=None):
            self.editor.list.selection_clear(0,'end'); self.editor.list.selection_set(1); self.editor.select_chao(None)
        self.assertEqual(self.editor.selected, 'main')

    def test_15_switch_discard_independent(self):
        self.value('toys', 'rattle', '.2')
        with patch('chao_learned_editor.messagebox.askyesnocancel', return_value=False):
            self.editor.list.selection_clear(0,'end'); self.editor.list.selection_set(1); self.editor.select_chao(None)
        self.assertEqual(self.editor.selected, 'resident-1')
        self.assertEqual(self.editor.learned_patch(), {})
        self.assertFalse(self.editor.learned_drafts['main'].dirty)

    def test_16_read_cancel(self):
        self.value('toys', 'rattle', '.2')
        with patch('chao_learned_editor.messagebox.askyesnocancel', return_value=None): self.editor.reload()
        self.assertTrue(self.editor.active_learned_draft().dirty)

    def test_17_success_ack(self):
        self.value('toys', 'rattle', '.2'); self.editor.apply(learned_only=True); self.ack()
        self.assertEqual(self.editor.learned_patch(), {})
        self.assertEqual(self.editor.learned_vars['toys','rattle'].get(), '0.200')

    def test_18_error_keeps_values(self):
        self.value('toys', 'rattle', '.2'); self.editor.apply(learned_only=True); self.ack(False)
        self.assertEqual(self.editor.learned_vars['toys','rattle'].get(), '.2')
        self.assertTrue(self.editor.active_learned_draft().dirty)

    def test_19_personality_only_preserves_learned(self):
        self.value('toys', 'rattle', '.2'); self.editor.personality_vars['Chatty'].set('80')
        self.editor.apply(personality_only=True)
        self.assertNotIn('learnedAdjustments', self.request())
        self.rows[0]['personality']['Chatty'] = 80; self.ack()
        self.assertEqual(self.editor.learned_vars['toys','rattle'].get(), '.2')

    def test_20_learned_only_preserves_other_drafts(self):
        self.value('toys', 'rattle', '.2'); self.editor.personality_vars['Chatty'].set('80'); self.editor.variables['Joy'].set('75')
        self.editor.apply(learned_only=True); self.ack()
        self.assertEqual(self.editor.personality_patch(), {'Chatty': 80})
        self.assertEqual(self.editor.variables['Joy'].get(), '75')

    def test_21_mixed_apply(self):
        self.value('toys', 'rattle', '.2'); self.editor.personality_vars['Chatty'].set('80'); self.editor.apply()
        self.assertIn('personality', self.request()); self.assertIn('learnedAdjustments', self.request())

    def test_22_target_owner_guard(self):
        self.value('toys', 'rattle', '.2'); self.editor.selected = 'resident-1'; self.editor.apply(learned_only=True)
        self.assertFalse((self.directory/'chao-editor-request.json').exists())

    def test_23_metadata_readonly_and_pending(self):
        self.value('toys', 'rattle', '.2'); self.editor.apply(learned_only=True)
        self.assertTrue(all(b.instate(('disabled',)) for b in self.editor.learned_apply_buttons))
        self.assertNotIn('ExperienceCount', str(self.request()))
        self.assertNotIn('LastUpdateSeconds', str(self.request()))
        self.editor.apply(learned_only=True)
        self.assertEqual(self.request()['id'], self.editor.pending)

    def test_24_future_schema_and_missing_target(self):
        bad = copy.deepcopy(self.rows[0]); bad['learnedEditor']['habitSchemaVersion'] = 2
        with self.assertRaises(ValueError): LearnedDraft(bad, 'session')
        self.value('toys', 'rattle', '.2'); self.snapshot['chao'] = self.rows[1:]; self.editor.update_roster(self.snapshot)
        self.assertEqual(self.editor.selected, 'main')
        with self.assertRaises(ValueError): self.editor.learned_patch()

    def test_25_minimum_size_apply_and_keyboard_scroll(self):
        e = self.editor; self.window.deiconify(); self.window.geometry('710x540')
        e.tabs.select(next(t for t in e.tabs.tabs() if e.tabs.tab(t,'text') == 'Experience'))
        e.experience_sections.select(0); self.window.update()
        button = e.learned_apply_buttons[0]
        self.assertTrue(button.winfo_ismapped())
        self.assertLessEqual(button.winfo_rootx()+button.winfo_width(),self.window.winfo_rootx()+self.window.winfo_width())
        self.assertLessEqual(button.winfo_rooty()+button.winfo_height(),self.window.winfo_rooty()+self.window.winfo_height())
        self.assertEqual(len(set(v._name for v in e.learned_vars.values())),17)


if __name__ == '__main__': unittest.main()
