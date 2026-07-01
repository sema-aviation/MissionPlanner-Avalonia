#!/usr/bin/env python3
"""Emit manifest.json for the auto-updater: every file under a publish dir with its SHA-256 + size.

Usage: gen-manifest.py <publish_dir> <version> <notes_url> <out_manifest.json>

Paths are stored relative to <publish_dir> with forward slashes (the updater joins them onto the
per-platform Pages base URL and onto the local install dir). The client verifies the Ed25519
signature over the exact bytes of the emitted file, so sign this file as-is (no re-formatting).
"""
import hashlib
import json
import os
import sys


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    if len(sys.argv) != 5:
        sys.exit("usage: gen-manifest.py <publish_dir> <version> <notes_url> <out.json>")
    publish_dir, version, notes, out = sys.argv[1:]

    files = []
    for root, _, names in os.walk(publish_dir):
        for name in names:
            full = os.path.join(root, name)
            rel = os.path.relpath(full, publish_dir).replace(os.sep, "/")
            files.append({"path": rel, "sha256": sha256(full), "size": os.path.getsize(full)})
    files.sort(key=lambda f: f["path"])

    with open(out, "w") as f:
        json.dump({"version": version, "notes": notes, "files": files}, f, indent=2)
    print(f"manifest: {len(files)} files -> {out}")


if __name__ == "__main__":
    main()
