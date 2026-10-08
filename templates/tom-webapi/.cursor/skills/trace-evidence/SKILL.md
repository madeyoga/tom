---
name: trace-evidence
description: >-
  Produce Aspire dashboard trace evidence for a change: start the dashboard,
  run the scenario with OpenTelemetry export enabled, and paste a Markdown
  table of traces (including SQL) into the pull request. Use when proving a
  feature, an endpoint, or a data-access change.
---

# Trace evidence

The standalone Aspire dashboard 13.6.0 is installed by `.cursor/install.sh` under `/opt/aspire-dashboard/13.6.0` and started by `.cursor/start-aspire-dashboard.sh`. It listens on localhost only. Logs are `/tmp/aspire-dashboard.log`.

- UI: http://localhost:18888
- OTLP/gRPC: http://localhost:4317
- OTLP/HTTP: http://localhost:4318

`src/Tom.WebApi.Api/Program.cs` exports ASP.NET Core, HttpClient, and EF Core spans only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. An empty value leaves export off, which is how CI and production run. The test host is `WebApplicationFactory` of that same program, so `dotnet test` exports when the variables are set in the environment. The service name is `OTEL_SERVICE_NAME`, or `Tom.WebApi` when that variable is unset. The evidence script uses the same default.

SQL text is the `db.statement` attribute. Do not set `OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS` and do not copy parameter values onto spans. The host forces that variable to `false` before EF Core instrumentation starts.

## Produce evidence

1. Start the dashboard if it is not already up. A second run does not start another instance.

```bash
bash .cursor/start-aspire-dashboard.sh
```

2. Run the feature's scenario tests, or send the requests, with export turned on. Leave the variables unset for a normal CI run.

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_SERVICE_NAME=Tom.WebApi
dotnet test --filter FullyQualifiedName~Notes
```

The exporter batches spans (about every 5 seconds) and flushes when the host stops. Wait until `dotnet test` exits before reading traces. For a long-running `dotnet run`, wait a few seconds after the requests or stop the process so the batch flushes.

3. Print the table. `--route` keeps rows whose method and route contain that text. `--trace-id` reads one trace. `--wait` retries until a row appears or the seconds elapse.

```bash
python3 .cursor/skills/trace-evidence/scripts/trace-evidence.py
python3 .cursor/skills/trace-evidence/scripts/trace-evidence.py --route /api/notes
python3 .cursor/skills/trace-evidence/scripts/trace-evidence.py --trace-id <id>
```

The script calls `GET http://localhost:18888/api/telemetry/traces?resource=<service>` and then `GET http://localhost:18888/api/telemetry/traces/{traceId}`. It prints one Markdown row per trace: trace id, method and route, status code, duration, SQL span count, and the SQL statements (truncated). A statement that occurs more than once in the same trace is marked `possible N+1`.

4. Paste that table into the pull request body.
5. Attach one or two dashboard screenshots of the key traces (the trace list, and one trace that shows the SQL). Open http://localhost:18888/traces and the trace itself. Do not commit the screenshots.

The script prints to stdout and does not write a file. Do not commit evidence dumps, screenshots, or `/tmp/aspire-dashboard.log`. `.cursor/skills/trace-evidence/evidence/` is gitignored for anything placed there by hand.
