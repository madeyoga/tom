#!/usr/bin/env python3
"""Print a Markdown trace table from the local Aspire dashboard."""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

SQL_KEYS = ("db.statement", "db.query.text")
METHOD_KEYS = ("http.request.method", "http.method")
ROUTE_KEYS = ("http.route", "url.path", "http.target")
STATUS_KEYS = ("http.response.status_code", "http.status_code")
SQL_LIMIT = 120


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--service", default=os.environ.get("OTEL_SERVICE_NAME") or "Tom.WebApi")
    parser.add_argument("--base", default=os.environ.get("ASPIRE_DASHBOARD_URL") or "http://localhost:18888")
    parser.add_argument("--route", default="")
    parser.add_argument("--trace-id", default="")
    parser.add_argument("--limit", type=int, default=200)
    parser.add_argument("--wait", type=float, default=0, help="Retry until a row appears, or this many seconds pass")
    args = parser.parse_args()
    base = args.base.rstrip("/")
    deadline = time.monotonic() + max(0.0, args.wait)
    while True:
        rows = collect(base, args.service, args.route, args.trace_id, args.limit)
        if rows or time.monotonic() >= deadline:
            break
        time.sleep(1)
    if not rows:
        print("No matching traces.", file=sys.stderr)
        sys.exit(1)
    print_table(rows)


def collect(base: str, service: str, route: str, trace_id: str, limit: int) -> list[dict]:
    if trace_id:
        payloads = [fetch_ok(f"{base}/api/telemetry/traces/{urllib.parse.quote(trace_id)}")]
    else:
        payloads = list_payloads(base, service, limit)
    rows = []
    seen = set()
    for payload in payloads:
        for tid in trace_ids(payload):
            if tid in seen:
                continue
            seen.add(tid)
            detail = fetch_ok(f"{base}/api/telemetry/traces/{urllib.parse.quote(tid)}")
            row = summarize(detail)
            if row is None:
                continue
            if route and route.casefold() not in row["request"].casefold():
                continue
            if trace_id and tid != trace_id:
                continue
            rows.append(row)
    rows.sort(key=lambda row: row["start"], reverse=True)
    return rows


def list_payloads(base: str, service: str, limit: int) -> list[dict]:
    status, payload = fetch(f"{base}/api/telemetry/traces?{query(service, limit)}")
    if status == 200 and payload is not None:
        return [payload]
    if status != 404:
        fail(status, service)
    resources = fetch_ok(f"{base}/api/telemetry/resources")
    names = []
    for resource in resources if isinstance(resources, list) else []:
        if resource.get("name") != service:
            continue
        instance = resource.get("instanceId")
        names.append(f"{service}-{instance}" if instance else service)
    payloads = []
    for name in names:
        status, payload = fetch(f"{base}/api/telemetry/traces?{query(name, limit)}")
        if status == 200 and payload is not None:
            payloads.append(payload)
    if payloads:
        return payloads
    status, payload = fetch(f"{base}/api/telemetry/traces?{urllib.parse.urlencode({'limit': str(limit)})}")
    if status != 200 or payload is None:
        return []
    return [filter_service(payload, service)]


def filter_service(payload: dict, service: str) -> dict:
    kept = []
    for resource_spans in (payload.get("data") or {}).get("resourceSpans") or []:
        if attr((resource_spans.get("resource") or {}), "service.name") == service:
            kept.append(resource_spans)
    data = dict(payload.get("data") or {})
    data["resourceSpans"] = kept
    return {"data": data}


def summarize(payload: dict) -> dict | None:
    spans = list(iter_spans(payload))
    if not spans:
        return None
    server = pick_server(spans)
    attrs = attributes(server)
    method = first(attrs, METHOD_KEYS)
    route = first(attrs, ROUTE_KEYS)
    request = f"{method} {route}" if method and route else (server.get("name") or "")
    statements = []
    for span in spans:
        sql = first(attributes(span), SQL_KEYS)
        if sql:
            statements.append(" ".join(sql.split()))
    return {
        "trace": spans[0].get("traceId") or "",
        "request": request,
        "status": first(attrs, STATUS_KEYS),
        "duration": duration_ms(server),
        "start": int(server.get("startTimeUnixNano") or 0),
        "statements": statements,
    }


def pick_server(spans: list[dict]) -> dict:
    servers = [span for span in spans if span.get("kind") == 2]
    pool = [span for span in servers if not span.get("parentSpanId")] or servers
    if not pool:
        pool = [span for span in spans if not span.get("parentSpanId")] or spans
    return min(pool, key=lambda span: int(span.get("startTimeUnixNano") or 0))


def print_table(rows: list[dict]) -> None:
    print("| Trace | Request | Status | Duration | SQL spans | Statements |")
    print("| --- | --- | --- | --- | --- | --- |")
    for row in rows:
        counts: dict[str, int] = {}
        order: list[str] = []
        for statement in row["statements"]:
            if statement not in counts:
                order.append(statement)
            counts[statement] = counts.get(statement, 0) + 1
        shown = []
        for statement in order:
            label = cell(truncate(statement))
            if counts[statement] > 1:
                label = f"{label} ×{counts[statement]} (possible N+1)"
            shown.append(f"`{label}`")
        statements = "<br>".join(shown)
        print(
            f"| `{row['trace']}` | {cell(row['request'])} | {cell(row['status'])} | "
            f"{format_ms(row['duration'])} | {len(row['statements'])} | {statements} |"
        )


def trace_ids(payload: dict) -> list[str]:
    found = []
    seen = set()
    for span in iter_spans(payload):
        tid = span.get("traceId")
        if tid and tid not in seen:
            seen.add(tid)
            found.append(tid)
    return found


def iter_spans(payload: dict):
    for resource_spans in (payload.get("data") or {}).get("resourceSpans") or []:
        for scope in resource_spans.get("scopeSpans") or []:
            yield from scope.get("spans") or []


def attributes(span: dict) -> dict[str, str]:
    found = {}
    for item in span.get("attributes") or []:
        value = item.get("value") or {}
        if "stringValue" in value:
            found[item.get("key")] = str(value["stringValue"])
        elif "intValue" in value:
            found[item.get("key")] = str(value["intValue"])
    return found


def attr(resource: dict, key: str) -> str:
    return attributes(resource).get(key, "")


def first(found: dict[str, str], keys: tuple[str, ...]) -> str:
    for key in keys:
        if found.get(key):
            return found[key]
    return ""


def duration_ms(span: dict) -> float:
    start = int(span.get("startTimeUnixNano") or 0)
    end = int(span.get("endTimeUnixNano") or 0)
    return max(0, end - start) / 1_000_000


def format_ms(ms: float) -> str:
    if ms >= 1000:
        return f"{ms / 1000:.2f} s"
    if ms >= 10:
        return f"{ms:.0f} ms"
    return f"{ms:.1f} ms"


def truncate(text: str) -> str:
    if len(text) <= SQL_LIMIT:
        return text
    return text[: SQL_LIMIT - 3] + "..."


def cell(text: str) -> str:
    return text.replace("|", "\\|").replace("`", "'").replace("\n", " ")


def query(resource: str, limit: int) -> str:
    return urllib.parse.urlencode({"resource": resource, "limit": str(limit)})


def fetch_ok(url: str) -> dict:
    status, payload = fetch(url)
    if status != 200 or payload is None:
        raise SystemExit(f"GET {url} failed: HTTP {status}")
    return payload


def fetch(url: str) -> tuple[int, dict | list | None]:
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, None
    except urllib.error.URLError as error:
        raise SystemExit(f"GET {url} failed: {error.reason}") from error


def fail(status: int, service: str) -> None:
    raise SystemExit(f"Trace list for {service} failed: HTTP {status}")


if __name__ == "__main__":
    main()
