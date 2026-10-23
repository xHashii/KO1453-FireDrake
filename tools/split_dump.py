#!/usr/bin/env python3
"""
split_dump.py - Split a large file (e.g. a 120 MB SQL dump or its .zip) into
parts small enough for GitHub, and rejoin them with checksum verification.

Why: GitHub rejects single files over 100 MB in a git push and over 25 MB
in the browser uploader. Split parts pushed with normal git are, so far, the
most reliable way to hand a big file to the agent (Git LFS / release assets
download through hosts this sandbox cannot reach).

Split (on your machine):
  python tools/split_dump.py split "C:\\path\\dump.sql" db-drop --part-size-mb 40

Rejoin (anywhere):
  python tools/split_dump.py join db-drop/dump.sql.manifest.txt dump.sql
"""
import argparse
import hashlib
import os
import sys

CHUNK = 1024 * 1024


def sha256_of(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        while chunk := fh.read(CHUNK):
            h.update(chunk)
    return h.hexdigest()


def cmd_split(args) -> int:
    part_size = args.part_size_mb * 1024 * 1024
    if part_size <= 0:
        print("part size must be positive", file=sys.stderr)
        return 2

    os.makedirs(args.outdir, exist_ok=True)
    base = os.path.basename(args.input)
    total_size = os.path.getsize(args.input)

    whole = hashlib.sha256()
    parts = []  # (name, size, sha256)

    with open(args.input, "rb") as src:
        index = 0
        while True:
            # Peek one byte to know whether another part is needed (avoids an
            # empty trailing part when the size is an exact multiple).
            first = src.read(1)
            if not first:
                break
            index += 1
            part_name = f"{base}.part{index:03d}"
            part_path = os.path.join(args.outdir, part_name)
            part_hash = hashlib.sha256()
            written = 0
            with open(part_path, "wb") as dst:
                data = first
                while data and written < part_size:
                    dst.write(data)
                    part_hash.update(data)
                    whole.update(data)
                    written += len(data)
                    remaining = part_size - written
                    data = src.read(min(CHUNK, remaining)) if remaining > 0 else b""
            parts.append((part_name, written, part_hash.hexdigest()))
            print(f"  {part_name}: {written:,} bytes  sha256={part_hash.hexdigest()[:16]}…")

    manifest_path = os.path.join(args.outdir, f"{base}.manifest.txt")
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as mf:
        mf.write(f"file: {base}\n")
        mf.write(f"size: {total_size}\n")
        mf.write(f"sha256: {whole.hexdigest()}\n")
        mf.write(f"parts: {len(parts)}\n")
        for name, size, digest in parts:
            mf.write(f"part: {name} {size} {digest}\n")

    print(f"[split] {base}: {total_size:,} bytes -> {len(parts)} part(s) in {args.outdir}")
    print(f"[split] manifest: {manifest_path}")
    print("[split] commit the whole folder (parts + manifest) - every part is < "
          f"{args.part_size_mb} MB.")
    return 0


def cmd_join(args) -> int:
    manifest_dir = os.path.dirname(os.path.abspath(args.manifest))
    meta = {}
    parts = []
    with open(args.manifest, "r", encoding="utf-8") as mf:
        for line in mf:
            line = line.strip()
            if line.startswith("part: "):
                _, name, size, digest = line.split(" ", 3)
                parts.append((name, int(size), digest))
            elif ": " in line:
                key, value = line.split(": ", 1)
                meta[key] = value

    whole = hashlib.sha256()
    written = 0
    with open(args.output, "wb") as dst:
        for name, size, digest in parts:
            part_path = os.path.join(manifest_dir, name)
            if not os.path.exists(part_path):
                print(f"[join] MISSING part: {part_path}", file=sys.stderr)
                return 1
            actual = sha256_of(part_path)
            if actual != digest:
                print(f"[join] CHECKSUM MISMATCH: {name}", file=sys.stderr)
                return 1
            with open(part_path, "rb") as src:
                while chunk := src.read(CHUNK):
                    dst.write(chunk)
                    whole.update(chunk)
                    written += len(chunk)

    ok_size = written == int(meta.get("size", -1))
    ok_hash = whole.hexdigest() == meta.get("sha256")
    if not (ok_size and ok_hash):
        print(f"[join] VERIFY FAILED: size_ok={ok_size} sha256_ok={ok_hash}", file=sys.stderr)
        return 1

    print(f"[join] OK: {args.output} ({written:,} bytes, sha256 verified)")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    sp = sub.add_parser("split", help="Split a file into parts + manifest")
    sp.add_argument("input")
    sp.add_argument("outdir")
    sp.add_argument("--part-size-mb", type=int, default=40,
                    help="Part size in MB (default 40; use 25 for browser uploads, max 95 for git CLI)")
    sp.set_defaults(fn=cmd_split)

    jp = sub.add_parser("join", help="Rejoin parts listed in a manifest and verify SHA-256")
    jp.add_argument("manifest")
    jp.add_argument("output")
    jp.set_defaults(fn=cmd_join)

    args = parser.parse_args()
    return args.fn(args)


if __name__ == "__main__":
    raise SystemExit(main())
