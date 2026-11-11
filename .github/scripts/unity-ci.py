"""Gate Unity on story010's authoritative status, then invoke its configured smoke command."""

import argparse
import os
import pathlib
import re
import subprocess
import sys

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[2]
STORY = 'production/epics/ep-core/story-010-unity-project.md'
DEFERRED = 'Unity disabled: FND-003 story010 is blocked; ADR-0022 is Proposed.'


class UniqueLoader(yaml.SafeLoader):
    """Reject duplicate configuration keys rather than selecting an ambiguous command."""


def unique_mapping(loader, node, deep=False):
    """Build a mapping only when all keys are unique."""
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        if key in result:
            raise ValueError(f'Duplicate YAML key: {key}')
        result[key] = loader.construct_object(value_node, deep=deep)
    return result


UniqueLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, unique_mapping)


def story_status(text):
    """Read exactly one metadata status before the first story section."""
    header = text.split('## ', 1)[0]
    matches = re.findall(r'^> \*\*Status\*\*:[ \t]*(.+?)[ \t]*$', header, re.MULTILINE)
    if len(matches) != 1:
        raise ValueError('Story010 must have exactly one authoritative header Status field.')
    value = matches[0]
    if value.startswith('Blocked') and (value == 'Blocked' or value[len('Blocked')] in ' —-:'):
        return 'Blocked'
    if value in ('Complete', 'Done'):
        return 'Complete'
    raise ValueError(f'Unsupported Story010 status: {value}')


def configured_smoke(project):
    """Require the pinned engine and explicit editor/command settings before enabling Unity."""
    engine, commands = project.get('engine'), project.get('commands')
    if not isinstance(engine, dict) or not isinstance(commands, dict):
        raise ValueError('project.yaml requires engine and commands mappings.')
    if engine.get('name') != 'Unity' or engine.get('version') != '6.3 LTS':
        raise ValueError('The Unity pin must remain 6.3 LTS.')
    editor, smoke = engine.get('path'), commands.get('smoke')
    for name, value in (('engine.path', editor), ('commands.smoke', smoke)):
        if not isinstance(value, str) or not value.strip() or re.search(r'<[^>]*>|\bTODO\b', value, re.I):
            raise ValueError(f'{name} must be configured without placeholders before closing FND-003.')
    return editor, smoke


def eligibility(root):
    """Return a decision without changing story/configuration state."""
    status = story_status((root / STORY).read_text(encoding='utf-8-sig'))
    project = yaml.load((root / 'project.yaml').read_text(encoding='utf-8-sig'), Loader=UniqueLoader)
    if not isinstance(project, dict):
        raise ValueError('project.yaml must be a mapping.')
    if status == 'Blocked':
        return False, DEFERRED, None
    editor, smoke = configured_smoke(project)
    return True, 'Unity enabled: FND-003 is Complete and its smoke command is configured.', (editor, smoke)


def emit_decision(enabled, reason):
    """Publish a Boolean job output and a visible skip/enable explanation."""
    print(reason)
    output = os.environ.get('GITHUB_OUTPUT')
    if output:
        with open(output, 'a', encoding='utf-8') as stream:
            stream.write(f'unity_enabled={str(enabled).lower()}\n')
    summary = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary:
        with open(summary, 'a', encoding='utf-8') as stream:
            stream.write(f'### Unity compilation\n\n{reason}\n')


def main(argv=None):
    """Preflight or run the exact configured command, propagating a failed native exit."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('preflight', 'run'))
    parser.add_argument('--root', type=pathlib.Path, default=ROOT)
    args = parser.parse_args(argv)
    root = args.root.resolve()
    enabled, reason, command = eligibility(root)
    emit_decision(enabled, reason)
    if args.mode == 'preflight':
        return 0
    if not enabled:
        raise ValueError('Refusing a Unity run while FND-003 is blocked.')
    editor, smoke = command
    if not pathlib.Path(editor).is_file():
        raise ValueError('Configured editor is absent on this runner; provision the pinned editor before running Unity.')
    return subprocess.run(['bash', '--noprofile', '--norc', '-e', '-o', 'pipefail', '-c', smoke],
                          cwd=root, check=False, timeout=600).returncode


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, TypeError, yaml.YAMLError, subprocess.TimeoutExpired) as error:
        print(f'FAIL: Unity CI: {error}', file=sys.stderr)
        sys.exit(1)
