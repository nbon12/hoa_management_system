#!/usr/bin/env python3
"""Find copy-paste clusters, and with --fuzzy renamed copies, across source files.

Standard library only, so it runs in any session without installing anything.

It drops blank lines, comments, imports and punctuation-only lines, then hashes every window of
--min-lines normalized lines. Windows that occur in two or more places are grown into runs,
and the runs are ranked by duplicated lines (size x extra copies). With --fuzzy, identifiers
and literals are replaced by placeholders, so a copy with renamed variables still matches.

The output is a list of candidates, not a verdict. Read each cluster before treating it as an
architectural duplicate.

Usage:
  find_clones.py [--min-lines N] [--fuzzy] [--top K] [--json]
                 [--ext .cs --ext .ts ...] [--exclude GLOB ...] PATH [PATH ...]
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import re
import sys
from collections import defaultdict

DEFAULT_EXT = [".cs", ".ts", ".html", ".scss", ".py", ".sh", ".tf"]
DEFAULT_EXCLUDE = [
    "*/node_modules/*", "*/bin/*", "*/obj/*", "*/dist/*", "*/.angular/*", "*/coverage/*",
    "*/.git/*", "*/Migrations/*", "*.Designer.cs", "*.min.js", "*.generated.*", "*.g.cs",
]

COMMENT_PREFIXES = ("//", "/*", "*", "#", "<!--", "-->")
IMPORT_RE = re.compile(r"^(using\s+[\w.=\s]+;|import\s|export\s+\*\s+from\s|namespace\s+[\w.]+;?$|#region|#endregion)")
PUNCT_ONLY_RE = re.compile(r"^[\s{}()\[\];,]*$")
STRING_RE = re.compile(r'@?\$?"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|`(?:[^`\\]|\\.)*`')
NUMBER_RE = re.compile(r"\b\d+(?:\.\d+)?[a-zA-Z]*\b")
IDENT_RE = re.compile(r"\b[A-Za-z_]\w*\b")
KEYWORDS = set("""
abstract as async await base bool break byte case catch char checked class const continue
decimal default delegate do double else enum event explicit extern false finally fixed float
for foreach goto if implicit in int interface internal is lock long namespace new null object
operator out override params private protected public readonly record ref return sbyte sealed
short sizeof stackalloc static string struct switch this throw true try typeof uint ulong
unchecked unsafe ushort using var virtual void volatile while yield get set init value where
select from let join orderby group into equals required file scoped
any boolean constructor declare export extends function implements import instanceof keyof
module never number readonly symbol type undefined unknown let of satisfies
""".split())


def normalize(line: str, fuzzy: bool) -> str | None:
    s = line.strip()
    if not s or s.startswith(COMMENT_PREFIXES) or IMPORT_RE.match(s) or PUNCT_ONLY_RE.match(s):
        return None
    s = re.sub(r"\s+", " ", s)
    if fuzzy:
        s = STRING_RE.sub("S", s)
        s = NUMBER_RE.sub("N", s)
        s = IDENT_RE.sub(lambda m: m.group(0) if m.group(0) in KEYWORDS else "I", s)
    return s


def excluded(path: str, patterns: list[str]) -> bool:
    p = "/" + path.replace(os.sep, "/")
    return any(fnmatch.fnmatch(p, pat) or fnmatch.fnmatch(p, "*/" + pat.lstrip("*/")) for pat in patterns)


def collect(paths: list[str], exts: list[str], patterns: list[str]) -> list[str]:
    files = []
    for root in paths:
        if os.path.isfile(root):
            files.append(root)
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = [d for d in dirnames if not excluded(os.path.join(dirpath, d) + "/", patterns)]
            for name in filenames:
                full = os.path.join(dirpath, name)
                if os.path.splitext(name)[1] in exts and not excluded(full, patterns):
                    files.append(full)
    return sorted(set(files))


def find_runs(files: list[str], window: int, fuzzy: bool):
    kept: dict[str, list[tuple[str, int]]] = {}
    for f in files:
        try:
            with open(f, encoding="utf-8", errors="replace") as fh:
                lines = [(n, i) for i, raw in enumerate(fh, 1) if (n := normalize(raw, fuzzy))]
        except OSError:
            continue
        if len(lines) >= window:
            kept[f] = lines

    groups: dict[bytes, list[tuple[str, int]]] = defaultdict(list)
    for f, lines in kept.items():
        for k in range(len(lines) - window + 1):
            text = "\n".join(n for n, _ in lines[k:k + window])
            groups[hashlib.blake2b(text.encode(), digest_size=12).digest()].append((f, k))

    # Grow runs: a window whose every copy sits one step after an active run's copies extends it.
    active: dict[tuple, list[list]] = {}
    for locs in sorted((sorted(v) for v in groups.values() if len(v) > 1), key=lambda v: v[0]):
        prev = tuple((f, k - 1) for f, k in locs)
        run = active.pop(prev, None)
        if run is None:
            run = [[f, k, k] for f, k in locs]
        else:
            for slot, (_, k) in zip(run, locs):
                slot[2] = k
        active[tuple(locs)] = run

    results = []
    for run in active.values():
        spans = []
        for f, start, last in run:
            lines = kept[f]
            spans.append((f, lines[start][1], lines[last + window - 1][1]))
        if any(a[0] == b[0] and a[1] <= b[2] and b[1] <= a[2]
               for i, a in enumerate(spans) for b in spans[i + 1:]):
            continue  # a block overlapping itself in one file (repeated lines), not a copy
        size = run[0][2] - run[0][1] + window
        results.append({"lines": size, "copies": len(spans), "score": size * (len(spans) - 1),
                        "locations": [{"file": f, "start": s, "end": e} for f, s, e in spans]})
    results.sort(key=lambda r: (-r["score"], r["locations"][0]["file"]))
    return results, len(kept)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--min-lines", type=int, default=8, help="window size in normalized lines (default 8)")
    ap.add_argument("--fuzzy", action="store_true", help="ignore identifier names and literal values")
    ap.add_argument("--top", type=int, default=40, help="clusters to print (default 40, 0 for all)")
    ap.add_argument("--ext", action="append", help="file extension to scan (repeatable)")
    ap.add_argument("--exclude", action="append", default=[], help="extra glob to skip (repeatable)")
    ap.add_argument("--json", action="store_true", help="print JSON instead of text")
    args = ap.parse_args()

    files = collect(args.paths, args.ext or DEFAULT_EXT, DEFAULT_EXCLUDE + args.exclude)
    results, scanned = find_runs(files, args.min_lines, args.fuzzy)
    shown = results if args.top == 0 else results[:args.top]
    if args.json:
        json.dump({"filesScanned": scanned, "clusters": len(results), "top": shown}, sys.stdout, indent=1)
        print()
        return 0
    print(f"{scanned} files scanned, {len(results)} clusters "
          f"({'fuzzy' if args.fuzzy else 'exact'}, window {args.min_lines})")
    for n, r in enumerate(shown, 1):
        print(f"\n#{n}  {r['lines']} lines x {r['copies']} copies")
        for loc in r["locations"]:
            print(f"    {loc['file']}:{loc['start']}-{loc['end']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
