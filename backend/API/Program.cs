// Composition root — ADR-0004: "composition root at the API/host".
// This is the only place that knows which concrete implementation satisfies
// which Domain port. Nothing below it may reference a concrete type directly.
using CurioDistrict.API.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCurioDistrictComposition();

var app = builder.Build();

app.UseCurioDistrictPipeline();

app.Run();

/// <summary>Exposed so the integration test host can reference the entry point.</summary>
public partial class Program;
