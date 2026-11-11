# Merge Discipline Checklist

> Source: doc 24 §6 (merge discipline). Doc 24 is a `.docx` and is not
> machine-readable in-repo, so the itemised rules below are the authoritative
> in-repo form of §6. Each rule gets its own checkbox line — a rule that is
> collapsed into another line is not enforced.
>
> Fill this in on the PR (or link the PR that does) before merging.

## Independent review

- [ ] **Independent review** — the PR was reviewed by someone other than its
      author, and the review is recorded on the PR.
- [ ] The reviewer is named in the PR's `Reviewer:` field.
- [ ] The reviewer confirmed they did not author any part of the change.

## No hidden scope

- [ ] **No hidden scope** — every change in the PR is described in the
      `Changes:` field; nothing was left unstated.
- [ ] The change list matches the actual diff — no unreviewed files slipped in.
- [ ] Any scope the PR did *not* cover is stated explicitly, not silently
      dropped.

## Traceability

- [ ] The PR cites a ticket ID and an FR/NFR/TR reference (see
      `.github/pull_request_template.md` and
      `.github/scripts/check-traceability.sh`).
- [ ] The PR links its evidence — a test run, a smoke check, a log, or a
      screenshot — that shows the change works.

## Release gates

- [ ] Any release gate the change touches (benchmark, playtest, performance
      budget) was run and its result is in the `Evidence:` field.
- [ ] No fixed number was invented to satisfy a gate that is not yet
      configured (ADR-0020: no invented fixed number is approved).
