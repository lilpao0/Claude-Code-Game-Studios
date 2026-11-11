# Pull request checks

The workflow in `.github/workflows/pr.yml` runs on pull requests targeting any
branch. It checks the PR head revision, uses hosted runners with read-only
repository permissions and does not persist checkout credentials.

Required jobs build and test the backend, run the standalone content validator,
and invoke the existing traceability guard. PR-body edits trigger governance
again. The body is read from `GITHUB_EVENT_PATH` and passed as stdin data to
`check-traceability.sh -`; it is never inserted into shell source.

The selected content set is the imported `dev-seed.v0.1.json` profile. Do not
pass both full profile versions together: they intentionally redeclare the same
IDs and pools. Future production content must be added to the workflow's input
set when its own approved content story lands. Invalid content fails the job.

## Unity deferral and automatic eligibility

`unity-ci.py preflight` reads the authoritative Story010 header and `project.yaml`.
The current Blocked header emits `unity_enabled=false` and a visible explanation
naming FND-003 and ADR-0022 Proposed. Missing, malformed, duplicate or unrecognized
status/configuration fails the preflight. Body text saying Complete cannot
override a Blocked or blank header.

Once Story010 is Complete/Done, the preflight automatically enables the Unity job
when `engine.name`, the `6.3 LTS` pin, `engine.path` and `commands.smoke` are valid.
Closing the story with blank or placeholder settings fails the check. The run
wrapper invokes the exact configured smoke command through Bash with `-e` and
`pipefail`, preserving its native failure exit and enforcing a timeout.

The Windows hosted runner is not promised to contain a Unity editor. Story010
and the associated runner work must supply the pinned editor and a command that
works there before compilation can pass. An absent configured executable fails;
fixture commands prove wrapper behavior only. This change installs no editor,
uses no Unity licence secret and makes no actual Unity compilation claim.

## Local controls

Install the pinned YAML reader into the chosen Python environment:

```sh
python -m pip install -r .github/scripts/requirements-ci.txt
python -m unittest discover -s backend/tests/integration/ci -p 'test_*.py' -v
python .github/scripts/unity-ci.py preflight
```

The controls need Python3 and Bash. On Windows use Git for Windows Bash on PATH.
They exercise the real guard with complete/incomplete PR bodies, verify shell
text remains data, and drive preflight/run subprocesses against temporary story
and configuration fixtures. No production story/configuration is mutated.

Backend commands use .NET SDK10.0.403 and Release configuration. Integration
tests include actual CLI child processes. The standalone live-Postgres case
explicitly skips without its test connection string; a successful job does not
certify that live database path. No workflow publishes or deploys anything.

Local passing controls do not establish GitHub status publication or merge
enforcement. Stories005/009 remain open until authorized real PR runs retain
deliberately failing unit/content revisions and corrected green checks, with
URLs and head revisions. Required-check repository rules must be verified
before claiming that failed checks prevent merging.
