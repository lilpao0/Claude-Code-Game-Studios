"""Pass the PR body from event JSON to the existing traceability guard as stdin data."""

import json
import os
import pathlib
import subprocess
import sys


def main():
    """Preserve the guard's exit code; never interpolate PR text into executable shell code."""
    event_path = os.environ.get('GITHUB_EVENT_PATH')
    if not event_path:
        raise ValueError('GITHUB_EVENT_PATH is required.')
    event = json.loads(pathlib.Path(event_path).read_text(encoding='utf-8-sig'))
    pr = event.get('pull_request')
    if not isinstance(pr, dict):
        raise ValueError('Expected a pull_request event object.')
    body = pr.get('body')
    if body is None:
        body = ''
    if not isinstance(body, str):
        raise ValueError('The pull_request body must be text or null.')
    guard = pathlib.Path(__file__).resolve().with_name('check-traceability.sh')
    return subprocess.run(['bash', str(guard), '-'], input=body, text=True, check=False).returncode


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, TypeError) as error:
        print(f'FAIL: PR event: {error}', file=sys.stderr)
        sys.exit(1)
