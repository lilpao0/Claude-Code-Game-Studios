// Worker host — ADR-0018 (durable district tick) and ADR-0010 (offline
// automation limits). The background service that runs the district tick and
// the offline-automation resolution lives in Worker/Jobs.
//
// The tick and the offline rules themselves are story work in the
// EP-ECONOMY and EP-AUTOMATION epics; this story delivers the host only.
using CurioDistrict.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCurioDistrictWorker();

var host = builder.Build();

host.Run();
