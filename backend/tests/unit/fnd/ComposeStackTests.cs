using System.Text.RegularExpressions;
using CurioDistrict.API.Health;
using CurioDistrict.Tests.Unit;

namespace CurioDistrict.Tests.Unit.Fnd;

/// <summary>
/// Story 004 AC1 + AC2 — the docker compose stack (backend/docker-compose.yml).
///
/// These tests own the parts of the story that can be checked without Docker:
///  1. The stack declares the three services, and each has a healthcheck, so
///     `docker compose ps` can report all three healthy (AC1).
///  2. The API's healthcheck URL and the compose `depends_on` conditions agree
///     with the endpoint the API actually serves (AC1 — the endpoint must not
///     drift between the compose file and the code).
///  3. Every credential is a `${VAR}` reference with NO default, and no
///     literal credential appears in the compose file, a command, an argument,
///     a healthcheck or a label (AC2).
///
/// The stack actually coming up — real Postgres, a real 200 from the endpoint —
/// is the smoke check, not these tests.
/// </summary>
public class ComposeStackTests
{
    private static string BackendRoot => TestPaths.BackendRoot;

    private static string ComposeFile => Path.Combine(BackendRoot, "docker-compose.yml");

    private static string Dockerfile => Path.Combine(BackendRoot, "Dockerfile");

    /// <summary>The services the story requires.</summary>
    private static readonly string[] RequiredServices = { "postgres", "api", "worker" };

    /// <summary>
    /// The variables that carry a credential. Each must appear in the compose
    /// file as a bare <c>${VAR}</c> reference.
    /// </summary>
    private static readonly string[] CredentialVariables =
    {
        "POSTGRES_PASSWORD",
        "DATABASE_CONNECTION_STRING",
    };

    [Fact]
    public void The_stack_declares_its_three_services()
    {
        var compose = File.ReadAllText(ComposeFile);

        foreach (var service in RequiredServices)
        {
            Assert.True(
                Regex.IsMatch(compose, $@"^\s{{2}}{service}:\s*$", RegexOptions.Multiline),
                $"The compose file declares no '{service}' service — AC1 requires postgres, api and worker.");
        }
    }

    [Fact]
    public void Every_service_declares_a_healthcheck()
    {
        // `docker compose ps` reports a service as "healthy" only when it has
        // a healthcheck. A service without one shows no health state at all,
        // so AC1 ("all three report healthy") cannot be met.
        var compose = File.ReadAllText(ComposeFile);

        foreach (var service in RequiredServices)
        {
            var block = ServiceBlock(compose, service);
            Assert.NotNull(block);

            Assert.True(
                block!.Contains("healthcheck:", StringComparison.Ordinal),
                $"The '{service}' service declares no healthcheck — it can never report healthy (AC1).");
        }
    }

    [Fact]
    public void The_api_healthcheck_calls_the_endpoint_the_api_serves()
    {
        // The compose healthcheck and the code must name the same path. If
        // one moves and the other does not, the API never becomes healthy and
        // the stack never comes up.
        var compose = File.ReadAllText(ComposeFile);
        var api = ServiceBlock(compose, "api")!;

        Assert.True(
            api.Contains(HealthEndpoint.Path, StringComparison.Ordinal),
            $"The api healthcheck does not call {HealthEndpoint.Path} — the endpoint the API serves is " +
            $"{HealthEndpoint.Path} (API/Health/HealthEndpoint.cs).");
    }

    [Fact]
    public void The_api_waits_for_postgres_to_be_healthy()
    {
        // `depends_on` with a plain service name is only an ordering hint.
        // The story requires that the API is not counted healthy until
        // Postgres accepts connections, so the condition must be
        // `service_healthy`.
        var compose = File.ReadAllText(ComposeFile);
        var api = ServiceBlock(compose, "api")!;

        Assert.Contains("condition: service_healthy", api, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_credential_is_an_environment_reference_without_a_default()
    {
        var compose = File.ReadAllText(ComposeFile);

        foreach (var variable in CredentialVariables)
        {
            // The reference must exist, in compose's "required" form.
            //
            // A plain `${VAR}` is NOT enough: compose substitutes an empty
            // string for an unset variable and starts the stack anyway, which
            // is exactly the blank-password failure AC2 forbids. The `:?`
            // form makes compose refuse to render the file at all.
            Assert.True(
                Regex.IsMatch(compose, @"\$\{" + Regex.Escape(variable) + @":\?[^}]*\}"),
                $"The compose file does not reference ${{{variable}}} in the required form " +
                $"`${{{variable}}}:?<message>` — the credential is not environment-supplied, or an unset " +
                "variable would silently become an empty string instead of failing fast (AC2).");

            // A `:-` default is a literal fallback secret in the file, which
            // AC2 forbids regardless of the `:?` form.
            Assert.False(
                Regex.IsMatch(compose, @"\$\{" + Regex.Escape(variable) + @":-"),
                $"${{{variable}}} carries a default value in the compose file — a literal fallback " +
                "credential. AC2 requires the reference to have no default so an unset variable fails fast.");
        }
    }

    [Fact]
    public void Every_required_variable_fails_fast_when_unset()
    {
        // The story requires that an unset credential makes compose fail fast
        // rather than start with a fallback. That behaviour comes from the
        // `:?` form on EVERY variable the file consumes — a credential and a
        // port alike — because an unset one is a misconfigured run either way.
        //
        // The check is structural: every `${...}` reference in the file must
        // carry `:?`. A plain `${VAR}` would be silently emptied.
        var compose = File.ReadAllText(ComposeFile);

        var lenient = new List<string>();

        // Only the file's live content counts: a comment may show a `${VAR}`
        // example, and compose never reads a comment.
        foreach (var raw in compose.Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            foreach (Match match in Regex.Matches(trimmed, @"\$\{([A-Za-z_][A-Za-z0-9_]*)([^}]*)\}"))
            {
                var name = match.Groups[1].Value;
                var suffix = match.Groups[2].Value;

                // `:-` is a default (rejected by the test above); `:?` is the
                // required form.
                if (suffix.StartsWith(":?", StringComparison.Ordinal)) continue;

                lenient.Add($"${{{name}{suffix}}}");
            }
        }

        Assert.True(
            lenient.Count == 0,
            "These compose references are not in the required `${VAR:?message}` form — an unset value " +
            "becomes an empty string instead of failing fast (AC2):\n  " +
            string.Join("\n  ", lenient));
    }

    [Fact]
    public void No_credential_value_is_written_literally()
    {
        // A `command:`, an argument, a healthcheck or a label carrying a
        // literal secret is a failure (AC2). The compose file may only name
        // variables and image names — never a value.
        //
        // The check is structural, not a secret scanner: it finds every
        // quoted scalar in the file and rejects the ones that could only be
        // a credential written by hand. A scalar that contains a `${...}`
        // reference, a path, a URL, an image tag, a command name or a number
        // is not a credential.
        var compose = File.ReadAllText(ComposeFile);

        var literals = new List<string>();

        foreach (var raw in compose.Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            foreach (Match match in Regex.Matches(trimmed, "\"([^\"]*)\"|'([^']*)'"))
            {
                var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (value.Length == 0) continue;

                if (value.Contains("${", StringComparison.Ordinal)) continue; // a variable reference
                if (value.StartsWith('$')) continue;                         // a `$$` escaped reference
                if (value.Contains('=') || value.Contains(';')) continue;   // an env assignment / connection string
                if (value.Contains('/') || value.Contains('.') || value.Contains(':')) continue; // a path, image or URL
                if (value.Contains('-')) continue;                           // a command name or a flag
                if (value.Contains('_')) continue;                           // an env-var name, e.g. POSTGRES_PASSWORD
                if (value.All(char.IsDigit)) continue;                      // a number

                literals.Add($"{trimmed}   -> '{value}'");
            }
        }

        Assert.True(
            literals.Count == 0,
            "The compose file carries a literal value where a credential reference belongs (AC2):\n  " +
            string.Join("\n  ", literals));
    }

    [Fact]
    public void The_image_bakes_no_credential_into_a_layer()
    {
        // The Dockerfile may not set a credential via ENV, ARG or a literal
        // RUN command — the value must arrive from the compose environment at
        // run time, never at build time.
        var dockerfile = File.ReadAllText(Dockerfile);

        var offenders = new List<string>();
        foreach (var raw in dockerfile.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var upper = line.ToUpperInvariant();
            if (!upper.Contains("PASSWORD") && !upper.Contains("SECRET")
                && !upper.Contains("CONNECTION_STRING") && !upper.Contains("API_KEY"))
            {
                continue;
            }

            // A credential may be *named* in a build instruction only if it
            // is a reference to something build-time-safe; a literal value is
            // forbidden. Nothing in this Dockerfile should mention one at all.
            offenders.Add(line);
        }

        Assert.True(
            offenders.Count == 0,
            "The Dockerfile mentions a credential — it must arrive at run time from the environment, " +
            "never be baked into an image layer (AC2):\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_dockerignore_keeps_credential_files_out_of_the_build_context()
    {
        // A `.env` in the build context would be COPYed into the image by the
        // `COPY . ./` step. The ignore file is what prevents that.
        var dockerignore = File.ReadAllText(Path.Combine(BackendRoot, ".dockerignore"));

        Assert.Contains(".env", dockerignore, StringComparison.Ordinal);
    }

    /// <summary>Returns the YAML block of one service (its lines up to the next service).</summary>
    private static string? ServiceBlock(string compose, string service)
    {
        var lines = compose.Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], $@"^\s{{2}}{service}:\s*$")) { start = i + 1; break; }
        }
        if (start < 0) return null;

        var block = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            // A new service or a top-level key ends the block.
            if (Regex.IsMatch(line, @"^\s{2}\S") || Regex.IsMatch(line, @"^\S")) break;
            block.Add(line);
        }
        return string.Join('\n', block);
    }
}
