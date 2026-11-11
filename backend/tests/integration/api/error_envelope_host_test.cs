using CurioDistrict.API.Composition;
using CurioDistrict.API.Errors;
using CurioDistrict.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CurioDistrict.Tests.Integration.Api;

/// <summary>
/// Story 008 AC1/AC3 on actual loopback Kestrel. Test-only responders cover
/// raw status-only bodies, thrown errors, success and secret-safe faults using
/// the production pipeline. Production Program.cs maps only /health.
/// </summary>
public class ErrorEnvelopeHostTests : IClassFixture<ApiHostFixture>
{
    private readonly ApiHostFixture _host;

    public ErrorEnvelopeHostTests(ApiHostFixture host) => _host = host;

    private const string HealthPath = "/health";

    [Theory]
    [InlineData("GET", "/missing-route", 404, "not_found")]
    [InlineData("POST", "/test/success", 405, "method_not_allowed")]
    [InlineData("GET", "/test/status-only/418", 418, "http_error")]
    public async Task Routing_and_other_error_statuses_use_the_envelope(string method, string path, int status, string code)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await _host.Client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 405) Assert.Contains("GET", response.Content.Headers.Allow);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var envelope = ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, envelope.Code);
        Assert.Equal(ApiHostFixture.CorrelationOf(response), envelope.CorrelationId);
    }

    [Fact]
    public async Task Malformed_multiple_and_oversized_headers_generate_safe_ids_on_Kestrel()
    {
        foreach (var values in new[] { new[] { "bad id" }, new[] { "first-id", "second-id" }, new[] { new string('a', 129) } })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/missing-route");
            request.Headers.Add(Correlation.HeaderName, values);
            using var response = await _host.Client.SendAsync(request);
            Assert.Equal(404, (int)response.StatusCode);
            var id = ApiHostFixture.CorrelationOf(response);
            Assert.True(Guid.TryParseExact(id, "N", out _));
            Assert.Equal(id, ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync()).CorrelationId);
        }
    }

    [Theory]
    [InlineData(400, "bad_request")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(409, "conflict")]
    [InlineData(422, "unprocessable_request")]
    [InlineData(503, "service_unavailable")]
    public async Task A_status_only_body_is_normalized_before_Kestrel_commits_it(int status, string expectedCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/test/status-only/{status}");
        request.Headers.Add(Correlation.HeaderName, "corr-status-only-host");
        using var response = await _host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("raw-error-marker", body);
        var envelope = ApiHostFixture.ParseEnvelope(body);
        Assert.Equal(expectedCode, envelope.Code);
        Assert.Equal("corr-status-only-host", envelope.CorrelationId);
        Assert.Equal(envelope.CorrelationId, ApiHostFixture.CorrelationOf(response));
    }

    [Fact]
    public async Task A_successful_body_and_headers_pass_through_the_real_host()
    {
        using var response = await _host.Client.GetAsync("/test/success");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("successful-response", await response.Content.ReadAsStringAsync());
        Assert.Equal("preserved", response.Headers.GetValues("X-Test-Success").Single());
    }

    // ------------------------------------------------------------------
    // AC1 — the documented statuses, real host.
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_health_503_is_the_documented_envelope_with_a_correlation_id()
    {
        // No Postgres is configured in this host: the readiness check is
        // unhealthy, so /health is 503 and the health writer authors the
        // documented envelope — the concrete status-only path on a real
        // host today.
        var response = await _host.Client.GetAsync(HealthPath);
        var envelope = ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync());

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("service_unavailable", envelope.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
        Assert.Equal(ApiHostFixture.CorrelationOf(response), envelope.CorrelationId);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(400, "bad_request")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(409, "conflict")]
    [InlineData(422, "unprocessable_request")]
    public async Task The_exception_route_returns_the_documented_envelope_for_each_status(int status, string expectedCode)
    {
        // The production route real endpoints use: a test-only endpoint
        // throws the <c>ApiException</c> and the global middleware authors
        // the documented envelope. Pinned per status so a regression on any
        // of the documented codes fails here.
        var response = await _host.Client.GetAsync($"{TestRoutes.ApiException}/{status}");
        var envelope = ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync());

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(expectedCode, envelope.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
        Assert.Equal(ApiHostFixture.CorrelationOf(response), envelope.CorrelationId);
    }

    [Fact]
    public async Task An_inbound_correlation_id_is_echoed_in_header_and_envelope_on_the_real_host()
    {
        const string supplied = "req-real-host-7";

        using var request = new HttpRequestMessage(HttpMethod.Get, HealthPath);
        request.Headers.Add(Correlation.HeaderName, supplied);
        var response = await _host.Client.SendAsync(request);
        var envelope = ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync());

        Assert.Equal(supplied, envelope.CorrelationId);
        Assert.Equal(supplied, ApiHostFixture.CorrelationOf(response));
    }

    [Fact]
    public async Task A_missing_inbound_correlation_id_is_generated_and_echoed_on_the_real_host()
    {
        var response = await _host.Client.GetAsync(HealthPath);
        var id = ApiHostFixture.CorrelationOf(response);

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal(id, ApiHostFixture.ParseEnvelope(await response.Content.ReadAsStringAsync()).CorrelationId);
    }

    // ------------------------------------------------------------------
    // AC3 — an unhandled fault on the real host leaks nothing.
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_unhandled_fault_returns_500_envelope_and_the_marker_leaks_nowhere()
    {
        // The AC3 QA case on a real host: a test-only endpoint throws a
        // fault whose message carries a marker. The 500 body must be the
        // documented envelope — no marker, no exception type — and the
        // whole response, body AND headers, must be clean.
        var response = await _host.Client.GetAsync(TestRoutes.UnhandledFault);
        var body = await response.Content.ReadAsStringAsync();
        var envelope = ApiHostFixture.ParseEnvelope(body);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Equal("internal_error", envelope.Code);
        Assert.Equal(ExceptionHandlingMiddleware.UnhandledMessage, envelope.Message);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));

        var whole = body
            + "\n" + string.Join("\n", response.Headers.Concat(response.Content.Headers)
                .SelectMany(h => h.Value.Select(v => h.Key + ": " + v)));
        Assert.DoesNotContain(ApiHostFixture.FaultMarker, whole);
        Assert.DoesNotContain("InvalidOperationException", whole);
    }

    /// <summary>Test-only route names — test host surface, never production.</summary>
    private static class TestRoutes
    {
        public const string ApiException = "/test/api-exception";
        public const string UnhandledFault = "/test/unhandled-fault";
    }
}

/// <summary>
/// The real host for the class: the production pipeline
/// (<c>ExceptionHandlingMiddleware</c> outermost, <c>/health</c>) plus the
/// test-only responder set the AC cases need. Kestrel on a loopback port;
/// no Postgres, no Docker — the readiness check answers no-configuration
/// unhealthy, which is exactly the /health 503 shape under test.
/// </summary>
public sealed class ApiHostFixture : IAsyncLifetime
{
    /// <summary>The marker a test-only fault carries; AC3's whole-response check.</summary>
    public const string FaultMarker = "CURIODISTRICT_HOST_FAULT_MARKER_7c2e";

    private WebApplication? _app;
    private HttpClient? _client;

    /// <summary>The test-only HTTP client bound to the host's Kestrel endpoint.</summary>
    public HttpClient Client => _client!;

    public async Task InitializeAsync()
    {
        // The production composition root and pipeline: this host runs the
        // real middleware and the health endpoint, not a test double.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // Kestrel, a free loopback port
        builder.Services.AddCurioDistrictComposition();

        _app = builder.Build();
        _app.UseCurioDistrictPipeline();
        _app.MapTestOnlyResponders();
        await _app.StartAsync();

        // The address Kestrel actually bound — with the port-0 case the
        // configured URL is still "0", so read the started server instead.
        var addresses = _app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses;
        _client = new HttpClient { BaseAddress = new Uri(addresses.First()) };

    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        _client = null;
    }

    /// <summary>Parses a response body as the documented error envelope.</summary>
    public static ApiErrorEnvelope ParseEnvelope(string body)
        => ApiErrorJson.Parse(body)!;

    /// <summary>Reads the correlation id off the response header.</summary>
    public static string? CorrelationOf(HttpResponseMessage response)
        => response.Headers.GetValues(Correlation.HeaderName).FirstOrDefault();
}

/// <summary>
/// Maps the test-only responder endpoints onto a host (story 008, FND-009,
/// AC1/AC3 host coverage). These routes do not exist in the production
/// host: <c>backend/API/Program.cs</c> maps only <c>/health</c>.
/// </summary>
public static class TestOnlyResponders
{
    public static WebApplication MapTestOnlyResponders(this WebApplication app)
    {
        app.MapGet("/test/status-only/{status:int}", async (HttpContext context, int status) =>
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("raw-error-marker");
            await context.Response.Body.FlushAsync();
        });
        app.MapGet("/test/success", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/plain";
            context.Response.Headers["X-Test-Success"] = "preserved";
            await context.Response.WriteAsync("successful-response");
        });
        // The production route real endpoints use: a documented error is
        // THROWN as an ApiException and the global middleware authors the
        // envelope for it.
        app.MapGet("/test/api-exception/{status:int}", (int status) =>
        {
            if (!ApiErrorCodes.DocumentedTable.ContainsKey(status))
            {
                throw new ArgumentException("test route: only documented statuses");
            }

            throw new ApiException(status, $"documented error for {status}");
        });

        // The AC3 QA case, test-only: an UNHANDLED fault carrying a marker.
        // The middleware must answer 500 + envelope and keep the marker and
        // the exception type out of the whole response.
        app.MapGet("/test/unhandled-fault", (HttpContext context) =>
        {
            context.Response.Headers["X-Test-Secret"] = ApiHostFixture.FaultMarker;
            context.Response.Headers["Content-Language"] = ApiHostFixture.FaultMarker;
            throw new InvalidOperationException(
                $"{ApiHostFixture.FaultMarker} host fault at some frame");
        });

        return app;
    }
}

