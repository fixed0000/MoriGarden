"""Read-only casual encounter diagnostics against isolated real Tk editor widgets."""
import unittest
from Tests import test_chao_editor_personality as fixtures


class CasualEditorTests(unittest.TestCase):
    setUp = fixtures.PersonalityEditorTests.setUp
    tearDown = fixtures.PersonalityEditorTests.tearDown
    write = fixtures.PersonalityEditorTests.write
    request = fixtures.PersonalityEditorTests.request

    def publish(self):
        self.write()
        self.editor.update_roster(self.snapshot)
        self.editor.refresh_casual_social()

    def test_real_snapshot_only(self):
        self.assertIn('Unavailable', self.editor.casual_text.get())
        self.rows[0]['casualSocial'] = dict(Active=True, InitiatorId='main', PartnerId='resident-1',
                                          Phase='Exchange', SuccessfulEncounters=7, ActorCooldownRemaining=12)
        self.publish()
        text = self.editor.casual_text.get()
        self.assertIn('Phase: Exchange', text)
        self.assertIn('Partner ID: resident-1', text)
        self.assertIn('Successful encounters (executor): 7', text)

    def test_selected_pet_isolation(self):
        self.rows[0]['casualSocial'] = dict(Phase='Exchange')
        self.rows[1]['casualSocial'] = dict(Phase='Inactive')
        self.publish()
        self.editor.selected = 'resident-1'
        self.editor.refresh_casual_social()
        self.assertIn('Phase: Inactive', self.editor.casual_text.get())
        self.assertNotIn('Exchange', self.editor.casual_text.get())

    def test_missing_snapshot_unavailable(self):
        self.rows[0]['casualSocial'] = dict(Phase='Notice')
        self.publish()
        self.rows[0]['casualSocial'] = None
        self.publish()
        self.assertIn('Unavailable', self.editor.casual_text.get())
        self.assertNotIn('Phase: Notice', self.editor.casual_text.get())

    def test_diagnostics_never_in_outgoing_edit(self):
        self.rows[0]['casualSocial'] = dict(Phase='Exchange', SuccessfulEncounters=5)
        self.publish()
        self.editor.personality_vars['Curious'].set('20')
        self.editor.apply(personality_only=True)
        request = self.request()
        self.assertNotIn('casualSocial', request)
        self.assertNotIn('casualSocial', request['patch'])


if __name__ == '__main__':
    unittest.main()
