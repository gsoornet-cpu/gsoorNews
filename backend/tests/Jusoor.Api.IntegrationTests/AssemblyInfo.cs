using Xunit;

// This session's test run failed 2 pre-existing, previously-passing tests
// (AuthEndpointsTests, ReviewEndpointsTests) with "connection actively
// refused" against their own Postgres container — not a data/assertion
// failure, a connectivity failure, and it started exactly when a 4th
// IClassFixture<JusoorApiFactory> test class (DiasporaProfileHierarchyTests)
// was added to the 3 that already existed (Auth/Review/Geography).
//
// xUnit v2's default behavior runs separate test classes as separate
// collections IN PARALLEL unless told otherwise — with 4 such classes, up
// to 4 Postgres containers and 4 in-process ASP.NET Core test hosts can be
// starting up simultaneously. That is a well-known source of exactly this
// symptom with Testcontainers-based suites on a resource-constrained
// Docker Desktop instance (the developer's own earlier log showed Docker
// Desktop configured with 5.79 GB total memory — modest for 4 concurrent
// Postgres containers plus 4 app hosts plus Hangfire's own connections in
// each).
//
// This is the standard, minimal mitigation: force all test collections in
// this assembly to run sequentially, so only one Testcontainers-backed
// fixture is ever starting up at a time. Trades some wall-clock time
// (extra container-startup rounds instead of one parallel batch) for not
// depending on the developer's machine having enough headroom for 4x the
// concurrent load this suite had when it last ran clean. This is a
// plausible, well-reasoned root cause given the timing and error shape —
// not something this session's sandbox can independently reproduce or
// confirm (no Docker/Testcontainers available here), so it needs the
// developer's next run to actually verify it resolves the flakiness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]