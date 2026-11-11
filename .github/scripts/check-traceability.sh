#!/usr/bin/env bash
# Traceability guardrail (story 003, FND-007).
#
# Rejects a PR body that is missing a ticket ID or an FR/TC reference, and
# names what is missing. Exits non-zero on rejection, zero on acceptance.
#
# Usage:
#   check-traceability.sh <pr-body-file>
#   check-traceability.sh -            # read the body from stdin
#
# Framework-only PRs opt out with "not applicable — framework only" in the
# Ticket field.
#
# POSIX sh + grep -E only — no jq, no python (see the repo's hook rules).

set -u

# A placeholder is not a value.
PLACEHOLDER_RE='^[[:space:]]*(tbd|n/a|na|none|-|\.\.\.|todo|\?|\[.*\]|x)[[:space:]]*$'
# A ticket ID looks like FND-001, DOC-002, FND-012a — letters, digits, dashes.
TICKET_RE='[A-Z]{2,4}-[0-9]{3,4}[a-z]?'
# A requirement-side reference: FR-COL-007, NFR-PERF-001, TR-fnd-003.
# A TC id (TC-COL-001) is a *test case*; on its own it does not satisfy the
# field — a traceability link must reach the requirement it verifies. A TC id
# may accompany an FR id, but never replaces it.
REQ_RE='(FR|NFR|TR)-[A-Za-z]+-[0-9]{3}'

OPT_OUT_RE='not[[:space:]]+applicable.*framework only'

body=""
if [ "${1:-}" = "-" ]; then
    body=$(cat)
elif [ -n "${1:-}" ] && [ -f "$1" ]; then
    body=$(cat "$1")
else
    echo "usage: check-traceability.sh <pr-body-file>|-" >&2
    exit 2
fi

if [ -z "$(printf '%s' "$body" | tr -d '[:space:]')" ]; then
    echo "FAIL: PR body is empty." >&2
    echo "Missing: ticket ID, FR/TC reference, change list, evidence link, reviewer." >&2
    exit 1
fi

# Extract the value of a labelled field: the text after "**Label:**" or
# "Label:" up to the end of that line. The delimiter is "|" so that a label
# containing "/" (e.g. "FR/TC") does not need escaping. Markdown bold may
# wrap the colon too ("**Label:**"), so any stray "**" is stripped from the
# ends of the result.
field_value() {
    printf '%s\n' "$body" \
        | grep -E "^[[:space:]]*(\*\*)?${1}(\*\*)?[[:space:]]*:" \
        | head -n 1 \
        | sed -E "s|^[[:space:]]*(\*\*)?${1}(\*\*)?[[:space:]]*:[[:space:]]*||" \
        | sed -E "s|^[[:space:]]*\*\*||; s|\*\*[[:space:]]*$||"
}

is_placeholder() {
    printf '%s' "$1" | grep -qE "$PLACEHOLDER_RE"
}

is_opt_out() {
    printf '%s' "$1" | grep -qE "$OPT_OUT_RE"
}

# check_field <label> <value> <ok-regex-or-empty> <requirement text>
# Appends a status line to $report and sets `failed` when the value is bad.
# A field that opts out ("not applicable — framework only") is accepted
# whatever its content.
report=""
failed=0

check_field() {
    label="$1"; value="$2"; ok_re="$3"; want="$4"
    if is_opt_out "$value"; then
        report="${report}  OK       ${label} (framework-only opt-out)\n"
        return
    fi
    if [ -z "$(printf '%s' "$value" | tr -d '[:space:]')" ] || is_placeholder "$value"; then
        report="${report}  MISSING  ${label}: the field is empty or a placeholder (${want})\n"
        failed=1
        return
    fi
    if [ -n "$ok_re" ] && ! printf '%s' "$value" | grep -qE "$ok_re"; then
        report="${report}  MISSING  ${label}: ${want}\n"
        failed=1
        return
    fi
    report="${report}  OK       ${label}\n"
}

# --- Ticket ---------------------------------------------------------------
ticket=$(field_value "Ticket")
check_field "Ticket" "$ticket" "$TICKET_RE" \
    "no id like FND-001 (a bare word is not a ticket id)"

# --- FR/TC ----------------------------------------------------------------
check_field "FR/TC" "$(field_value "FR/TC")" "$REQ_RE" \
    "cites no FR-, NFR- or TR- id (a TC id alone does not trace to a requirement)"

# --- Changes --------------------------------------------------------------
check_field "Changes" "$(field_value "Changes")" "" "the change list is empty"

# --- Evidence -------------------------------------------------------------
check_field "Evidence" "$(field_value "Evidence")" "" "the evidence link is empty"

# --- Reviewer -------------------------------------------------------------
check_field "Reviewer" "$(field_value "Reviewer")" "" "no reviewer is named"

if [ "$failed" -ne 0 ]; then
    echo "FAIL: PR body is missing required traceability." >&2
    printf "%b" "$report" >&2
    echo >&2
    echo "Fill in every labelled field of .github/pull_request_template.md." >&2
    exit 1
fi

printf "%b" "$report"
echo "PASS: ticket, FR/TC reference, changes, evidence and reviewer are all present."
exit 0
