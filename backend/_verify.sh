#!/bin/sh
# Story 004 (FND-004) verification script.
#
# Usage: sh _verify.sh [HOST_PORT]
#   HOST_PORT is optional (default 8080) and is the published API port.
#
# Design — safe to rerun, isolated from any pre-existing stack:
#   * Runs a UNIQUE disposable compose project (COMPOSE_PROJECT_NAME=
#     cd-fnd004-<timestamp>-<pid>). Because the project name is unique, the
#     managed Postgres volume is created fresh for this run and removed with
#     it. It never touches a pre-existing `curiodistrict` project or volume.
#   * The fail-fast probes (unset AND empty) run in a clean environment from a
#     scratch directory that holds only a copy of docker-compose.yml, so a
#     developer's local backend/.env can neither leak a value in nor mask a
#     missing one.
#   * Every failed assertion leaves the script with a non-zero exit status.
#   * No credential value is ever printed.
#   * Tears down only what this run created: `down -v` on its own disposable
#     project. It performs no teardown of any other project.

set -u

cd "$(dirname "$0")"

COMPOSE_FILE="docker-compose.yml"
RUN_ID="$(date +%s)-$$"
PROJECT="cd-fnd004-${RUN_ID}"
export COMPOSE_PROJECT_NAME="${PROJECT}"

API_PORT="${1:-8080}"
FAIL=0
note() { printf '%s\n' "$*"; }
ok()   { note "  ok: $*"; }
fail() { note "  FAIL: $*"; FAIL=1; }

# A generated password for this run. Printed only as a length/prefix marker,
# never as its value.
PASS="$(head -c16 /dev/urandom | od -An -tx1 | tr -d ' \n')"

# ---------------------------------------------------------------------------
note "== 1. dotnet build (Release) =="
if dotnet build CurioDistrict.sln -c Release --nologo; then
  ok "solution built"
else
  fail "dotnet build failed — cannot verify the runtime image"; exit 1
fi

# ---------------------------------------------------------------------------
note "== 2. compose availability =="
# The only hard requirement is that the `docker compose` plugin is present.
# The `${VAR:?...}` fail-fast behaviour this story relies on is validated
# directly in step 3, not gated on a version number.
if docker compose version >/dev/null 2>&1; then
  note "  docker compose $(docker compose version 2>/dev/null | head -n1)"
  ok "compose plugin present"
else
  fail "docker compose plugin not available"; exit 1
fi

# ---------------------------------------------------------------------------
note "== 3. fail-fast probes (isolated from any local .env) =="
SCRATCH="$(mktemp -d)"
cp "$COMPOSE_FILE" "$SCRATCH/docker-compose.yml"
cd "$SCRATCH"

# Each probe runs in a subshell so it cannot leak state into the next step
# and so the six target variables are controlled explicitly, while the rest
# of the environment is preserved. The Docker CLI needs that preserved
# environment: on Windows it discovers the compose plugin through
# USERPROFILE, so an `env -i`-style clean environment silently loses the
# plugin and every probe fails with "unknown command docker compose" — for
# a reason that has nothing to do with the variables under test. The
# positive control below is what keeps any such failure detectable: a
# control that passes proves the harness works, so a probe failure is the
# variables' doing.

# (a) all required vars unset: compose must refuse to render the file.
if (
  unset POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD DATABASE_CONNECTION_STRING API_PORT ASPNETCORE_ENVIRONMENT
  docker compose -f docker-compose.yml -p probe-unset-$$ config >/dev/null 2>&1
); then
  fail "compose config SUCCEEDED with required vars unset — no fail-fast (AC2)"
else
  ok "compose fails fast when required vars are unset"
fi

# (b) credential vars present but EMPTY: the :? form must reject these too.
#     The two non-credential vars are left valid so the rejection is
#     unambiguously the credential references, not a missing port.
if (
  POSTGRES_DB= POSTGRES_USER= POSTGRES_PASSWORD= DATABASE_CONNECTION_STRING= \
  API_PORT=8080 ASPNETCORE_ENVIRONMENT=Development \
  docker compose -f docker-compose.yml -p probe-empty-$$ config >/dev/null 2>&1
); then
  fail "compose config SUCCEEDED with empty credential vars — :? must reject empty (AC2)"
else
  ok "compose fails fast when credential vars are empty"
fi

# (c) positive control: with all required vars set to non-empty values,
#     compose config MUST succeed. Without this control, a failure in (a)/(b)
#     could be blamed on a broken compose file or a broken compose install
#     instead of on the missing/empty variables — the control proves the
#     fail-fast behaviour is caused by the variables, not by anything else.
if (
  POSTGRES_DB=curiodistrict POSTGRES_USER=curiodistrict POSTGRES_PASSWORD=probe-password \
  DATABASE_CONNECTION_STRING='Host=postgres;Port=5432;Database=curiodistrict;Username=curiodistrict;Password=probe-password' \
  API_PORT=8080 ASPNETCORE_ENVIRONMENT=Development \
  docker compose -f docker-compose.yml -p probe-positive-$$ config >/dev/null 2>&1
); then
  ok "control: compose config succeeds with all required vars set (fail-fast is var-driven)"
else
  fail "control FAILED: compose config rejects a fully-set environment — the compose file or compose install is broken, not just the variables"
fi

cd - >/dev/null
# Remove only the file this probe created, then the now-empty directory.
# An unexpected entry must prevent cleanup rather than be recursively deleted.
if ! rm -f -- "$SCRATCH/docker-compose.yml" || ! rmdir -- "$SCRATCH"; then
  fail "could not remove this run's probe directory"; exit 1
fi

# ---------------------------------------------------------------------------
note "== 4. bring up the disposable stack =="
export POSTGRES_DB=curiodistrict
export POSTGRES_USER=curiodistrict
export POSTGRES_PASSWORD="$PASS"
export DATABASE_CONNECTION_STRING="Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
export API_PORT="${API_PORT}"
export ASPNETCORE_ENVIRONMENT=Development

if ! docker compose up -d --build; then
  fail "docker compose up failed"; docker compose ps; exit 1
fi
ok "stack requested: project ${PROJECT} (host port ${API_PORT})"

# ---------------------------------------------------------------------------
note "== 5. wait for all three healthy =="
state_of() { docker compose ps "$1" --format '{{.Health}}' 2>/dev/null | tail -n1; }
settled=0
for _ in $(seq 1 60); do
  pg="$(state_of postgres)"; api="$(state_of api)"; wk="$(state_of worker)"
  note "  postgres=${pg:-?} api=${api:-?} worker=${wk:-?}"
  if [ "${pg:-}" = "healthy" ] && [ "${api:-}" = "healthy" ] && [ "${wk:-}" = "healthy" ]; then
    settled=1; break
  fi
  sleep 10
done
docker compose ps

if [ "$settled" != "1" ]; then
  fail "not all three services reached healthy within the wait window"
  docker compose logs --tail 40 || true
  # Leave the disposable stack up for inspection; the caller cleans it via the
  # project name printed above. Do not tear down a pre-existing stack.
  exit 1
fi
ok "postgres, api and worker all report healthy"

# ---------------------------------------------------------------------------
note "== 6. AC1: /health returns 200 =="
CODE="$(curl -sS -o /dev/null -w '%{http_code}' "http://localhost:${API_PORT}/health")"
note "  /health -> ${CODE}"
if [ "${CODE}" = "200" ]; then
  ok "health endpoint returned 200"
else
  fail "health endpoint returned ${CODE}, not 200"
  docker compose logs --tail 25 api || true
fi

# ---------------------------------------------------------------------------
note "== 7. AC2: credential is environment-supplied, not a literal =="
# (a) no literal credential in the live compose/Dockerfile content. Comments
#     are stripped first — the compose header documents the export form in a
#     comment, and a comment is not a live value.
if grep -hvE '^[[:space:]]*#' docker-compose.yml Dockerfile \
   | grep -nE '(:-|PASSWORD=.*[A-Za-z0-9])' >/dev/null 2>&1; then
  fail "a literal credential value is present in the live compose/Dockerfile content"
else
  ok "no literal credential value in the live compose/Dockerfile content"
fi

# (b) the value inside the running container equals the host-supplied value.
IN_PG="$(docker compose exec -T postgres printenv POSTGRES_PASSWORD)"
if [ "${IN_PG}" = "$PASS" ]; then
  ok "postgres container carries the host-supplied password"
else
  fail "postgres container password does not match the host environment"
fi

IN_API="$(docker compose exec -T api printenv ConnectionStrings__CurioDistrict)"
case "${IN_API}" in
  *"$PASS"*) ok "api connection string carries the host-supplied password" ;;
  *)         fail "api connection string does not carry the host-supplied password" ;;
esac

# ---------------------------------------------------------------------------
note "== 8. tear down this run's disposable project only =="
if [ "$FAIL" -eq 0 ]; then
  docker compose down -v
  rc=$?
  if [ $rc -eq 0 ]; then
    ok "removed ${PROJECT} and its disposable volume"
  else
    fail "docker compose down exited ${rc} — cleanup did not complete; run: docker compose -p ${PROJECT} down -v"
  fi
else
  note "  leaving ${PROJECT} up for inspection (verification failed)"
fi

# ---------------------------------------------------------------------------
note "== 9. cleanup validation: this run left nothing behind =="
# The listing exit codes are checked directly: a piped `wc` would report 0
# for a failed docker call and pass this step on an empty-but-false result.
LEFT_CON="$(docker ps -a --filter "label=com.docker.compose.project=${PROJECT}" --format '{{.Names}}')"
rc=$?
if [ $rc -ne 0 ]; then
  fail "cleanup validation: container listing failed (docker exit ${rc})"
elif [ -n "${LEFT_CON}" ]; then
  fail "cleanup left container(s) from ${PROJECT} behind: ${LEFT_CON}"
else
  ok "no containers from ${PROJECT} remain"
fi

LEFT_VOL="$(docker volume ls --filter "name=${PROJECT}_postgres-data" -q)"
rc=$?
if [ $rc -ne 0 ]; then
  fail "cleanup validation: volume listing failed (docker exit ${rc})"
elif [ -n "${LEFT_VOL}" ]; then
  fail "cleanup left volume(s) behind: ${LEFT_VOL}"
else
  ok "no volumes from ${PROJECT} remain"
fi
# The disposable project is unique per run, so no pre-existing project could
# have been touched — this is recorded here rather than re-checked.

# ---------------------------------------------------------------------------
if [ "$FAIL" -eq 0 ]; then
  note "== done: all FND-004 checks passed =="
else
  note "== done: one or more checks FAILED =="
fi
exit "$FAIL"
