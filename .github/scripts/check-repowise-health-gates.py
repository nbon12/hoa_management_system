#!/usr/bin/env python3
"""Evaluate repowise health/risk JSON against repowise/health-gates.yaml."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    yaml = None  # type: ignore

ROOT = Path(__file__).resolve().parents[2]
GATES_PATH = ROOT / "repowise" / "health-gates.yaml"
REPORT_PATH = Path(
    sys.argv[1] if len(sys.argv) > 1 else "/tmp/repowise-gate-report.txt"
)
HEALTH_JSON = Path(
    sys.argv[2] if len(sys.argv) > 2 else "/tmp/repowise-health.json"
)
RISK_JSON = Path(sys.argv[3] if len(sys.argv) > 3 else "/tmp/repowise-risk.json")


def load_gates() -> dict:
    if yaml is None:
        print("PyYAML required: pip install pyyaml", file=sys.stderr)
        sys.exit(2)
    if not GATES_PATH.is_file():
        print(f"Missing {GATES_PATH}", file=sys.stderr)
        sys.exit(2)
    with GATES_PATH.open(encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def run_repowise(args: list[str]) -> tuple[int, str, str]:
    result = subprocess.run(
        ["repowise", *args],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )
    return result.returncode, result.stdout or "", result.stderr or ""


def parse_json_blob(text: str) -> dict | None:
    text = text.strip()
    if not text:
        return None
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        pass
    decoder = json.JSONDecoder()
    for i, ch in enumerate(text):
        if ch not in "{[":
            continue
        try:
            data, _ = decoder.raw_decode(text[i:])
            if isinstance(data, dict):
                return data
        except json.JSONDecodeError:
            continue
    return None


def ensure_health_json() -> dict:
    if HEALTH_JSON.is_file() and HEALTH_JSON.stat().st_size > 0:
        data = parse_json_blob(HEALTH_JSON.read_text(encoding="utf-8"))
        if data:
            return data
    code, stdout, stderr = run_repowise(["health", "--format", "json"])
    if code != 0:
        print(stderr or stdout, file=sys.stderr)
        raise RuntimeError("repowise health failed")
    data = parse_json_blob(stdout)
    if not data:
        raise RuntimeError("repowise health did not return JSON")
    HEALTH_JSON.write_text(json.dumps(data, indent=2), encoding="utf-8")
    return data


def ensure_risk_json(base_ref: str) -> dict | None:
    revspec = f"{base_ref}..HEAD"
    if RISK_JSON.is_file() and RISK_JSON.stat().st_size > 0:
        data = parse_json_blob(RISK_JSON.read_text(encoding="utf-8"))
        if data:
            return data
    code, stdout, stderr = run_repowise(["risk", revspec, "--format", "json"])
    combined = f"{stderr}\n{stdout}".strip()
    if "No counted file changes" in combined:
        return {"ref": revspec, "score": 0, "skipped": True}
    data = parse_json_blob(stdout) or parse_json_blob(combined)
    if not data:
        return None
    RISK_JSON.write_text(json.dumps(data, indent=2), encoding="utf-8")
    return data


def path_included(path: str, prefixes: list[str]) -> bool:
    normalized = path.replace("\\", "/")
    return any(normalized.startswith(p) for p in prefixes)


def filtered_metrics(health: dict, prefixes: list[str]) -> list[dict]:
    metrics = health.get("metrics") or []
    return [m for m in metrics if path_included(m.get("file_path", ""), prefixes)]


def avg_score(metrics: list[dict]) -> float | None:
    scores = [m["score"] for m in metrics if m.get("score") is not None]
    if not scores:
        return None
    return sum(scores) / len(scores)


def score_for_file(health: dict, rel_path: str) -> float | None:
    target = rel_path.replace("\\", "/")
    for m in health.get("metrics") or []:
        if m.get("file_path") == target:
            return m.get("score")
    return None


def main() -> int:
    gates = load_gates()
    enforce = gates.get("mode", "enforce") != "warn"
    violations: list[str] = []
    lines: list[str] = []

    try:
        health = ensure_health_json()
    except RuntimeError as exc:
        print(exc, file=sys.stderr)
        return 2

    prefixes = gates.get("include_path_prefixes") or []
    kpis = health.get("kpis") or {}
    metrics = filtered_metrics(health, prefixes) if prefixes else (health.get("metrics") or [])

    avg = avg_score(metrics)
    repo_cfg = gates.get("repo") or {}
    if avg is not None and repo_cfg.get("min_avg_health") is not None:
        floor = float(repo_cfg["min_avg_health"])
        if avg < floor:
            violations.append(
                f"repo avg health {avg:.2f} < min_avg_health {floor} "
                f"(filtered prefixes: {prefixes})"
            )

    hotspot = kpis.get("hotspot_health")
    if hotspot is not None and repo_cfg.get("min_hotspot_health") is not None:
        floor = float(repo_cfg["min_hotspot_health"])
        if float(hotspot) < floor:
            violations.append(
                f"hotspot_health {float(hotspot):.2f} < min_hotspot_health {floor}"
            )

    for entry in gates.get("files") or []:
        path = entry.get("path")
        min_h = entry.get("min_health")
        if not path or min_h is None:
            continue
        score = score_for_file(health, path)
        if score is None:
            violations.append(f"no health score for governed file {path}")
        elif score < float(min_h):
            violations.append(f"{path} health {score:.2f} < min_health {min_h}")

    base_ref = __import__("os").environ.get("REPOWISE_BASE_REF", "origin/main")
    risk = ensure_risk_json(base_ref)
    pr_cfg = gates.get("pr") or {}
    if risk and not risk.get("skipped") and pr_cfg.get("max_change_risk") is not None:
        risk_score = risk.get("score")
        if risk_score is not None and float(risk_score) > float(pr_cfg["max_change_risk"]):
            violations.append(
                f"PR change risk {float(risk_score):.2f} > max_change_risk "
                f"{pr_cfg['max_change_risk']} ({base_ref}..HEAD)"
            )

    lines.append("=== repowise status ===")
    _, status_out, status_err = run_repowise(["status"])
    lines.append((status_out or status_err).strip() or "(no output)")
    lines.append("")
    lines.append("=== repowise health (kpis) ===")
    lines.append(json.dumps(kpis, indent=2))
    if prefixes:
        lines.append(f"Filtered avg ({len(metrics)} files): {avg}")
    lines.append("")
    lines.append("=== repowise risk ===")
    lines.append(json.dumps(risk, indent=2) if risk else "(skipped or failed)")
    lines.append("")
    lines.append("=== gate violations ===")
    if violations:
        lines.extend(f"  - {v}" for v in violations)
    else:
        lines.append("  (none)")

    REPORT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines))

    if violations:
        if enforce:
            print(f"Repowise health gate failed ({len(violations)} violation(s)).", file=sys.stderr)
            return 1
        print(f"Repowise health gate warnings ({len(violations)}); mode=warn.", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
