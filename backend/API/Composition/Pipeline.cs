using CurioDistrict.API.Health;
using CurioDistrict.API.Middleware;
using Microsoft.AspNetCore.Builder;

namespace CurioDistrict.API.Composition;

/// <summary>
/// The request pipeline (ADR-0004: the API/host owns the pipeline).
///
/// Story 008 adds the global exception/error middleware at the OUTERMOST
/// position — it must wrap the health endpoint and every future endpoint
/// so that no fault can escape without producing the documented error
/// envelope (AC1/AC3) and carrying the correlation id (AC4).
/// </summary>
public static class Pipeline
{
    /// <summary>Configures the middleware pipeline for the API host.</summary>
    public static WebApplication UseCurioDistrictPipeline(this WebApplication app)
    {
        // Outermost: catch-all error envelope + correlation handling.
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Endpoints.
        app.MapCurioDistrictHealth();

        return app;
    }
}
