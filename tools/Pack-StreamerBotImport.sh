#!/usr/bin/env bash
# Pack VKVideoLiveService.cs into a Streamer.bot import string (.txt).
#
# Format: Base64( "SBAE" + gzip(utf8 JSON) ).
# Execute Code (type 99999) stores source in byteCode as Base64(UTF-8 text).
#
# Version is read from the file header comment:
#   ///   Version:      x.y.z[_dev]
#
# Output name: VkLiveService_{version}.txt
# Written to OutputDir and copied to docs/static/files/VkLiveService/.
# Also ensures Execute C# Method actions for GetRewardDemands / RejectRewardDemand /
# AcceptRewardDemand when missing (Set channelName + method, same pattern as Get Rewards).
#
# Examples:
#   ./tools/Pack-StreamerBotImport.sh --source-dir .
#   ./tools/Pack-StreamerBotImport.sh --git-ref dev

set -euo pipefail

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required" >&2
  exit 1
fi

PACK_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PACK_SCRIPT_DIR

exec python3 - "$@" <<'PY'
from __future__ import annotations

import argparse
import base64
import copy
import gzip
import json
import os
import re
import subprocess
import sys
import uuid
from pathlib import Path

MAGIC = b"SBAE"
DEFAULT_OUTPUT_DIR = Path(
    "/mnt/d/Projects/NuboHeimer/Output/Code/Streamer.bot/VkLiveService"
)
DEFAULT_TEMPLATE = "VkLiveService_5.0.0_dev.3.txt"
DEFAULT_IMPORT_NAME = "VkVideoLive Service"
DEFAULT_CODE_BLOCK = "VKVideoLive Method Collection"
DEFAULT_SOURCE_FILE = "VKVideoLiveService.cs"
SET_CHANNEL_ACTION_NAME = "[VKVideoLive] Set channelName"
CODE_ACTION_NAME = "[VKVideoLive] Code"

# Method name → Streamer.bot action name (ensure on pack if missing).
REQUIRED_METHOD_ACTIONS = (
    ("GetRewardDemands", "[VKVideoLive] Get Reward Demands"),
    ("RejectRewardDemand", "[VKVideoLive] Reject Reward Demand"),
    ("AcceptRewardDemand", "[VKVideoLive] Accept Reward Demand"),
)


def repo_root_from_script() -> Path:
    script_dir = Path(os.environ["PACK_SCRIPT_DIR"]).resolve()
    return script_dir.parent


def decode_sbae_import(path: Path) -> str:
    b64 = path.read_text(encoding="utf-8").strip()
    raw = base64.b64decode(b64)
    if raw[:4] != MAGIC:
        sig = raw[:4].decode("ascii", errors="replace")
        raise SystemExit(f"Unexpected magic {sig!r} in {path} (expected SBAE)")
    return gzip.decompress(raw[4:]).decode("utf-8")


def encode_sbae_import(json_text: str) -> str:
    payload = json_text.encode("utf-8")
    compressed = gzip.compress(payload, compresslevel=9, mtime=0)
    return base64.b64encode(MAGIC + compressed).decode("ascii")


def get_source_text(file_name: str, source_dir: Path | None, git_ref: str | None, repo_root: Path) -> str:
    if source_dir is not None:
        path = source_dir / file_name
        if not path.is_file():
            raise SystemExit(f"Source file not found: {path}")
        return path.read_text(encoding="utf-8")
    if git_ref:
        proc = subprocess.run(
            ["git", "-C", str(repo_root), "show", f"{git_ref}:{file_name}"],
            capture_output=True,
        )
        if proc.returncode != 0:
            err = proc.stderr.decode("utf-8", errors="replace").strip() or "unknown"
            raise SystemExit(f"git show {git_ref}:{file_name} failed: {err}")
        return proc.stdout.decode("utf-8")
    raise SystemExit("Provide --source-dir or --git-ref")


def convert_to_crlf(text: str) -> str:
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    return normalized.replace("\n", "\r\n")


def version_from_source_comment(text: str) -> str:
    match = re.search(r"(?m)^///\s+Version:\s+(\S+)\s*$", text)
    if not match:
        raise SystemExit("Could not parse /// Version: from VKVideoLiveService.cs")
    return match.group(1)


def set_json_string_property(json_text: str, property_name: str, value: str) -> str:
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    pattern = re.compile(
        r'("' + re.escape(property_name) + r'"\s*:\s*")(?:\\.|[^"\\])*(")'
    )
    match = pattern.search(json_text)
    if not match:
        raise SystemExit(f"Property {property_name!r} not found for string replace")
    return pattern.sub(lambda m: m.group(1) + escaped + m.group(2), json_text, count=1)


def set_code_block_bytecode(json_text: str, block_name: str, bytecode_b64: str) -> str:
    pattern = re.compile(
        r'("name"\s*:\s*"'
        + re.escape(block_name)
        + r'".{0,50000}?"byteCode"\s*:\s*")[^"]*(")',
        re.DOTALL,
    )
    match = pattern.search(json_text)
    if not match:
        raise SystemExit(f"Code block {block_name!r} / byteCode not found in template JSON")
    return pattern.sub(lambda m: m.group(1) + bytecode_b64 + m.group(2), json_text, count=1)


def set_ui_version_comment(json_text: str, version_label: str) -> str:
    escaped = version_label.replace("\\", "\\\\").replace('"', '\\"')
    pattern = re.compile(r'("value"\s*:\s*")v[\d][^"]*(")')
    if not pattern.search(json_text):
        raise SystemExit("UI version comment (value v...) not found in template")
    return pattern.sub(lambda m: m.group(1) + escaped + m.group(2), json_text, count=1)


def find_action(actions: list, name: str) -> dict | None:
    return next((action for action in actions if action.get("name") == name), None)


def find_code_block_id(code_action: dict, block_name: str) -> str:
    for sa in code_action.get("subActions") or []:
        if sa.get("name") == block_name and sa.get("type") == 99999:
            block_id = sa.get("id")
            if not block_id:
                raise SystemExit(f"Code block {block_name!r} has no id")
            return block_id
    raise SystemExit(f"Code block {block_name!r} not found in {CODE_ACTION_NAME}")


def find_method_template_action(actions: list, code_block_id: str, set_channel_id: str) -> dict:
    for action in actions:
        sub = action.get("subActions") or []
        if len(sub) != 2:
            continue
        run_set, execute = sub[0], sub[1]
        if (
            run_set.get("type") == 4
            and run_set.get("actionId") == set_channel_id
            and execute.get("type") == 99998
            and execute.get("executeCodeId") == code_block_id
            and execute.get("method")
        ):
            return action
    raise SystemExit(
        "No template action found (Set channelName + Execute C# Method on VKVideoLive code block)"
    )


def existing_execute_methods(actions: list, code_block_id: str) -> set[str]:
    methods: set[str] = set()
    for action in actions:
        for sa in action.get("subActions") or []:
            if sa.get("type") == 99998 and sa.get("executeCodeId") == code_block_id:
                method = sa.get("method")
                if method:
                    methods.add(method)
    return methods


def ensure_required_method_actions(payload: dict, code_block_name: str) -> list[str]:
    """Clone Set channelName + Execute Method actions for required methods if missing."""
    actions = payload["data"]["actions"]
    set_channel = find_action(actions, SET_CHANNEL_ACTION_NAME)
    if set_channel is None or not set_channel.get("id"):
        raise SystemExit(f"Missing action {SET_CHANNEL_ACTION_NAME!r}")
    code_action = find_action(actions, CODE_ACTION_NAME)
    if code_action is None:
        raise SystemExit(f"Missing action {CODE_ACTION_NAME!r}")
    code_block_id = find_code_block_id(code_action, code_block_name)
    template = find_method_template_action(actions, code_block_id, set_channel["id"])
    present = existing_execute_methods(actions, code_block_id)
    added: list[str] = []
    for method, action_name in REQUIRED_METHOD_ACTIONS:
        if method in present:
            continue
        if find_action(actions, action_name) is not None:
            raise SystemExit(
                f"Action {action_name!r} exists but does not call method {method!r}"
            )
        new_action = copy.deepcopy(template)
        new_action["id"] = str(uuid.uuid4())
        new_action["name"] = action_name
        new_action["triggers"] = []
        new_action["collapsedGroups"] = []
        for sa in new_action["subActions"]:
            sa["id"] = str(uuid.uuid4())
            if sa.get("type") == 99998:
                sa["method"] = method
                sa["executeCodeId"] = code_block_id
            elif sa.get("type") == 4:
                sa["actionId"] = set_channel["id"]
        actions.append(new_action)
        present.add(method)
        added.append(action_name)
    return added


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Pack VKVideoLiveService.cs into a Streamer.bot import string (.txt)."
    )
    parser.add_argument("--template-path", default="")
    parser.add_argument("--source-dir", default="")
    parser.add_argument("--git-ref", default="")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT_DIR))
    parser.add_argument("--docs-files-dir", default="")
    parser.add_argument("--repo-root", default="")
    parser.add_argument("--import-name", default=DEFAULT_IMPORT_NAME)
    parser.add_argument("--code-block-name", default=DEFAULT_CODE_BLOCK)
    parser.add_argument("--source-file-name", default=DEFAULT_SOURCE_FILE)
    parser.add_argument("--default-template", default=DEFAULT_TEMPLATE)
    parser.add_argument("--skip-docs-copy", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    repo_root = Path(args.repo_root).resolve() if args.repo_root else repo_root_from_script()
    docs_files_dir = (
        Path(args.docs_files_dir)
        if args.docs_files_dir
        else repo_root / "docs" / "static" / "files" / "VkLiveService"
    )
    output_dir = Path(args.output_dir)
    template_path = Path(args.template_path) if args.template_path else output_dir / args.default_template
    if not template_path.is_file():
        fallback = docs_files_dir / args.default_template
        if fallback.is_file():
            template_path = fallback
        else:
            raise SystemExit(f"Template not found: {template_path} (also tried {fallback})")

    source_dir: Path | None = Path(args.source_dir) if args.source_dir else None
    git_ref = args.git_ref or None
    if source_dir is None and git_ref is None:
        source_dir = repo_root

    sources_label = str(source_dir) if source_dir is not None else f"git:{git_ref}"
    print(f"Template:  {template_path}")
    print(f"Sources:   {sources_label}")

    source_text = get_source_text(args.source_file_name, source_dir, git_ref, repo_root)
    version = version_from_source_comment(source_text)
    ui_version = version if version.startswith("v") else f"v{version}"

    print(f"Version:   {version}")
    print(f"UI label:  {ui_version}")
    print(f"Name:      {args.import_name}")

    json_text = decode_sbae_import(template_path)
    json_text = set_json_string_property(json_text, "name", args.import_name)
    json_text = set_json_string_property(json_text, "version", version)
    json_text = set_ui_version_comment(json_text, ui_version)

    src = convert_to_crlf(source_text)
    bytecode_b64 = base64.b64encode(src.encode("utf-8")).decode("ascii")
    json_text = set_code_block_bytecode(json_text, args.code_block_name, bytecode_b64)
    print(f"  updated: {args.code_block_name} ({len(src)} chars)")

    check = json.loads(json_text)
    if check["meta"]["version"] != version:
        raise SystemExit(f"meta.version mismatch after patch: {check['meta']['version']}")
    code_action = find_action(check["data"]["actions"], CODE_ACTION_NAME)
    if code_action is None:
        raise SystemExit(f"Packed import missing action {CODE_ACTION_NAME!r}")
    method_sa = next(
        (sa for sa in code_action.get("subActions", []) if sa.get("name") == args.code_block_name),
        None,
    )
    if method_sa is None:
        raise SystemExit(f"Packed import missing code block {args.code_block_name!r}")
    method_src = base64.b64decode(method_sa["byteCode"]).decode("utf-8")
    if f"///   Version:      {version}" not in method_src:
        raise SystemExit(f"Packed source does not contain expected Version comment: {version}")

    added_actions = ensure_required_method_actions(check, args.code_block_name)
    for name in added_actions:
        print(f"  added action: {name}")
    for method, action_name in REQUIRED_METHOD_ACTIONS:
        if find_action(check["data"]["actions"], action_name) is None:
            raise SystemExit(f"Required action missing after ensure: {action_name}")
        code_block_id = find_code_block_id(code_action, args.code_block_name)
        if method not in existing_execute_methods(check["data"]["actions"], code_block_id):
            raise SystemExit(f"Required method not wired after ensure: {method}")

    json_text = json.dumps(check, ensure_ascii=False, separators=(",", ":"))
    encoded = encode_sbae_import(json_text)
    file_name = f"VkLiveService_{version}.txt"
    output_dir.mkdir(parents=True, exist_ok=True)
    out_path = output_dir / file_name
    out_path.write_bytes(encoded.encode("ascii"))
    print(f"Wrote:     {out_path}")
    print(f"Size:      {out_path.stat().st_size} bytes")

    if not args.skip_docs_copy:
        docs_files_dir.mkdir(parents=True, exist_ok=True)
        docs_path = docs_files_dir / file_name
        docs_path.write_bytes(out_path.read_bytes())
        print(f"Copied:    {docs_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
PY
