"""Story005 local workflow/preflight controls; these do not claim remote PR execution."""

import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[4]
SCRIPTS = ROOT / '.github/scripts'
spec = importlib.util.spec_from_file_location('unity_ci', SCRIPTS / 'unity-ci.py')
unity_ci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unity_ci)


class UnityEligibilityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='curio-ci005-')
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        self.story = self.root / unity_ci.STORY
        self.story.parent.mkdir(parents=True)
        self.output = self.root / 'outputs.txt'
        self.summary = self.root / 'summary.txt'
        self.env = dict(os.environ, GITHUB_OUTPUT=str(self.output), GITHUB_STEP_SUMMARY=str(self.summary))

    def fixture(self, status='Complete', smoke="printf 'fixture-smoke\\n'", editor=None):
        self.story.write_text(f'# Story010\n> **Status**: {status}\n\n## Body\nComplete\n', encoding='utf-8')
        project = {'engine': {'name': 'Unity', 'version': '6.3 LTS', 'path': editor or sys.executable},
                   'commands': {'smoke': smoke}}
        (self.root / 'project.yaml').write_text(yaml.safe_dump(project), encoding='utf-8')

    def run_cli(self, mode):
        return subprocess.run([sys.executable, str(SCRIPTS / 'unity-ci.py'), mode, '--root', str(self.root)],
                              env=self.env, text=True, capture_output=True, timeout=30)

    def test_blocked_header_does_not_enable_from_complete_in_body_or_placeholder(self):
        self.fixture('Blocked — ADR-0022 is Proposed', '"<Unity editor>" -batchmode')
        result = self.run_cli('preflight')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('unity_enabled=false', self.output.read_text())
        self.assertIn('ADR-0022 is Proposed', self.summary.read_text())
        self.assertEqual(1, self.run_cli('run').returncode)

    def test_complete_enables_automatically_and_runs_the_exact_fixture_command(self):
        self.fixture()
        result = self.run_cli('preflight')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('unity_enabled=true', self.output.read_text())
        result = self.run_cli('run')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('fixture-smoke', result.stdout)

    def test_configured_command_and_pipeline_nonzero_results_propagate(self):
        for command, expected in [('exit 7', 7), ('false | true', 1)]:
            with self.subTest(command=command):
                self.fixture(smoke=command)
                self.assertEqual(expected, self.run_cli('run').returncode)

    def test_complete_with_unconfigured_smoke_fails_instead_of_skipping(self):
        for smoke in ['', ' ', '"<Unity editor>" -batchmode', 'TODO']:
            with self.subTest(smoke=smoke):
                self.fixture(smoke=smoke)
                result = self.run_cli('preflight')
                self.assertEqual(1, result.returncode)
                self.assertIn('commands.smoke', result.stderr)

    def test_invalid_engine_pin_and_editor_settings_fail_before_enabling(self):
        self.fixture(editor='<Unity editor>')
        self.assertEqual(1, self.run_cli('preflight').returncode)
        self.fixture()
        path = self.root / 'project.yaml'
        path.write_text(path.read_text().replace('6.3 LTS', '6.4'), encoding='utf-8')
        self.assertEqual(1, self.run_cli('preflight').returncode)

    def test_missing_malformed_unknown_or_duplicate_status_fails_closed(self):
        for text in ['# No status', '> **Status**: Unexpected', '> Status: Complete',
                     '> **Status**: Complete\n> **Status**: Blocked', '> **Status**: Blockedness']:
            with self.subTest(text=text):
                self.fixture()
                self.story.write_text(text, encoding='utf-8')
                self.assertEqual(1, self.run_cli('preflight').returncode)
        self.story.unlink()
        self.assertEqual(1, self.run_cli('preflight').returncode)

    def test_blank_status_cannot_consume_complete_from_the_next_line(self):
        self.fixture()
        self.story.write_text('> **Status**:\nComplete\n\n## Body\n', encoding='utf-8')
        result = self.run_cli('preflight')
        self.assertEqual(1, result.returncode)
        self.assertFalse(self.output.exists(), 'A malformed status must not publish an enable decision.')

    def test_missing_runner_editor_is_a_failure_not_a_compile_pass(self):
        self.fixture(editor=str(self.root / 'absent-editor'))
        result = self.run_cli('run')
        self.assertEqual(1, result.returncode)
        self.assertIn('absent on this runner', result.stderr)

    def test_ambiguous_or_malformed_yaml_never_enables_unity(self):
        for text in ['commands: [', 'commands: {}\ncommands: {}', '[]']:
            with self.subTest(text=text):
                self.fixture()
                (self.root / 'project.yaml').write_text(text, encoding='utf-8')
                self.assertEqual(1, self.run_cli('preflight').returncode)


class TraceabilityEventTests(unittest.TestCase):
    BODY = ('Ticket: FND-005\nFR/TC: TR-fnd-005, TR-fnd-009\nChanges: PR checks\n'
            'Evidence: production/qa/story009-local-verification-2026-11-11.md\nReviewer: independent review\n')

    def run_body(self, body):
        with tempfile.TemporaryDirectory(prefix='curio-pr005-') as folder:
            event = pathlib.Path(folder) / 'event.json'
            event.write_text(json.dumps({'pull_request': {'body': body}}), encoding='utf-8')
            return subprocess.run([sys.executable, str(SCRIPTS / 'check-pr-event.py')], text=True,
                                  env=dict(os.environ, GITHUB_EVENT_PATH=str(event)),
                                  capture_output=True, timeout=30)

    def test_complete_body_is_accepted_by_the_existing_guard(self):
        result = self.run_body(self.BODY)
        self.assertEqual(0, result.returncode, result.stderr)

    def test_missing_ticket_requirement_placeholders_and_tc_only_are_rejected(self):
        for body in [self.BODY.replace('FND-005', ''), self.BODY.replace('TR-fnd-005, TR-fnd-009', ''),
                     self.BODY.replace('TR-fnd-005, TR-fnd-009', 'TC-COL-001'),
                     'Ticket: TBD\nFR/TC: n/a\nChanges: -\nEvidence: TBD\nReviewer: -', None]:
            with self.subTest(body=body):
                self.assertEqual(1, self.run_body(body).returncode)

    def test_pr_shell_syntax_remains_data(self):
        with tempfile.TemporaryDirectory(prefix='curio-pr-data005-') as folder:
            marker = pathlib.Path(folder) / 'must-not-exist'
            payload = f'$(touch "{marker.as_posix()}"); `touch "{marker.as_posix()}"`; "quoted"'
            result = self.run_body(self.BODY.replace('PR checks', payload))
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertFalse(marker.exists(), 'PR text must never be evaluated as shell code.')


class WorkflowShapeTests(unittest.TestCase):
    def test_pr_jobs_are_unfiltered_read_only_and_fail_closed(self):
        workflow = yaml.safe_load((ROOT / '.github/workflows/pr.yml').read_text())
        self.assertEqual({'pull_request'}, set(workflow['on']))
        self.assertEqual({'contents': 'read'}, workflow['permissions'])
        self.assertNotIn('paths', workflow['on']['pull_request'])
        self.assertNotIn('branches', workflow['on']['pull_request'])
        jobs = workflow['jobs']
        self.assertEqual({'build', 'content', 'governance', 'unity'}, set(jobs))
        self.assertIn('unity_enabled', jobs['unity']['if'])
        for job in jobs.values():
            self.assertNotIn('continue-on-error', job)
            self.assertNotIn('uses', job)  # No transitively invoked release workflow.
            for step in job['steps']:
                self.assertNotIn('continue-on-error', step)
                self.assertNotIn('secrets', step)
                if step.get('uses', '').startswith('actions/checkout@'):
                    self.assertIs(False, step['with']['persist-credentials'])
                    self.assertEqual('${{ github.event.pull_request.head.sha }}', step['with']['ref'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
