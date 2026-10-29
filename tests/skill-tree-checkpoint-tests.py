"""Required finding provenance controls for repository-local Skill Tree helpers."""
import contextlib
import importlib.util
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
HELPERS = sorted(ROOT.glob('.agents/skills/skill-tree-*/scripts/workflow-state.py'))
spec = importlib.util.spec_from_file_location('workflow_state', HELPERS[0])
workflow = importlib.util.module_from_spec(spec)
spec.loader.exec_module(workflow)


def identity(prefix, value):
    return f'{prefix}-00000000-0000-4000-8000-{value:012d}'


class Store:
    def __init__(self, root_status='completed', open_root=False):
        self.root = identity('T', 1)
        self.parent = identity('T', 2)
        self.saved = False
        self.records = [{'task_id': self.root, 'disposition': root_status}] if root_status else []
        metadata = dict(id=self.parent, priority=99, status='Ready',
                        parent_pending_ids=[identity('P', 1)], processing_run_id=identity('PR', 1),
                        root_task_id=self.root, related_task_id=self.root, relation='followup', depends_on=[])
        body = '\n\n'.join(section + ':\n- Preserve exact identity.' for section in workflow.SECTIONS)
        self.document = '# Future\n\n## Pending Task Format\n\n## Pending Queue\n\n## Prioritized Next Changes\n\n'
        if open_root:
            self.document += workflow.render('Open root', dict(metadata, id=self.root,
                root_task_id=self.root, related_task_id=None, relation='main', priority=110), body)
        self.document += workflow.render('Open descendant', metadata, body)
        self.document += '\n## Backlog\n'

    def lock(self):
        return contextlib.nullcontext()

    def guard(self, value):
        pass

    def text(self):
        return self.document

    def load(self, text=None):
        return {'active_run': None}

    def outcomes(self):
        return self.records

    def save(self, text, state):
        self.document, self.saved = text, True


class RequiredFindingTests(unittest.TestCase):
    def payload(self, store, relation='blocker'):
        return dict(title='Qualify protected callback transport', description='Implement the required protected transport.',
                    expected_behavior='Preserve exact root and direct-parent identities.',
                    root_task_id=store.root, related_task_id=store.parent, relation=relation)

    def test_required_children_retain_completed_root_provenance(self):
        for relation in ['blocker', 'subtask']:
            with self.subTest(relation=relation):
                store = Store()
                result = workflow.intake(store, self.payload(store, relation))
                self.assertEqual(result['priority'], 100)
                self.assertEqual(result['root_task_id'], store.root)
                self.assertEqual(result['related_task_id'], store.parent)
                self.assertTrue(store.saved)

    def test_missing_cancelled_and_superseded_roots_never_supply_completion(self):
        for status in [None, 'cancelled', 'superseded']:
            with self.subTest(status=status):
                store = Store(status)
                with self.assertRaisesRegex(workflow.WorkflowError, 'Finding root requires'):
                    workflow.intake(store, self.payload(store))
                self.assertFalse(store.saved)

    def test_mismatched_root_never_reparents_finding(self):
        store = Store()
        with self.assertRaisesRegex(workflow.WorkflowError, 'does not match direct parent'):
            workflow.intake(store, dict(self.payload(store), root_task_id=identity('T', 3)))
        self.assertFalse(store.saved)

    def test_open_root_priority_and_followup_behavior_remain_available(self):
        store = Store(open_root=True)
        result = workflow.intake(store, self.payload(store, 'followup'))
        self.assertEqual(result['priority'], 109)
        self.assertEqual(result['root_task_id'], store.root)

    def test_installed_helper_copies_remain_identical(self):
        self.assertGreater(len(HELPERS), 1)
        self.assertEqual(len({path.read_bytes() for path in HELPERS}), 1)


if __name__ == '__main__':
    unittest.main()
