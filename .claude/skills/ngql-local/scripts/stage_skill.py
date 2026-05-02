#!/usr/bin/env python3
"""Stage the NGql Skill for one of its published channels.

Reads the in-repo SKILL.md, rewrites its frontmatter `name:` and `description:` fields
to match the target channel, and writes the result into the staging directory along
with a per-channel plugin.json bumped to the requested version.

Usage:
    stage_skill.py --channel preview|stable --version <semver> --out <dir>

Output layout under <dir>:
    plugins/<plugin-name>/skills/ngql/SKILL.md           ← rewritten skill content
    plugins/<plugin-name>/.claude-plugin/plugin.json     ← name + version updated

The plugin name is derived from the channel:
    preview -> ngql-preview
    stable  -> ngql

This script is invoked by `make skill-stage`. It deliberately does not touch git or
the catalog repo — `make skill-publish` handles that step.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

CHANNEL_TO_PLUGIN_NAME = {
    "preview": "ngql-preview",
    "stable": "ngql",
}


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--channel", required=True, choices=sorted(CHANNEL_TO_PLUGIN_NAME))
    p.add_argument("--version", required=True, help="Semver to write into plugin.json")
    p.add_argument(
        "--source",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
        help="Source skill directory (defaults to .claude/skills/ngql-local)",
    )
    p.add_argument("--out", type=Path, required=True, help="Staging output directory")
    return p.parse_args()


def rewrite_frontmatter(src_md: str, plugin_name: str, description: str) -> str:
    """Replace the SKILL.md frontmatter `name:` and `description:` fields.

    The source frontmatter uses block-scalar style for description (`description: |` followed
    by an indented body). We emit the same style so the diff stays minimal even when description
    text contains characters that would need escaping in the inline form.
    """
    if not src_md.startswith("---\n"):
        raise SystemExit("source SKILL.md must begin with a `---` frontmatter delimiter")

    end = src_md.find("\n---\n", 4)
    if end == -1:
        raise SystemExit("source SKILL.md frontmatter delimiter (`---`) not found")

    body = src_md[end + len("\n---\n"):]

    # YAML block scalar: `description: |` then body lines indented by two spaces. Frontmatter
    # delimiters and top-level keys must start at column 0 — no leading whitespace.
    indented_description = "\n".join(
        "  " + line for line in description.rstrip("\n").splitlines()
    )

    new_frontmatter = (
        "---\n"
        f"name: {plugin_name}\n"
        "description: |\n"
        f"{indented_description}\n"
        "---\n"
    )
    return new_frontmatter + body


def write_plugin_json(out_dir: Path, plugin_name: str, version: str) -> None:
    plugin_dir = out_dir / "plugins" / plugin_name / ".claude-plugin"
    plugin_dir.mkdir(parents=True, exist_ok=True)
    payload = {
        "$schema": "https://json.schemastore.org/claude-code-plugin.json",
        "name": plugin_name,
        "version": version,
        "description": (
            "NGql Skill — generates NGql query-builder C# from natural language, paste, "
            "or curl. Pairs with the `dotnet-ngql` CLI to verify rendered GraphQL."
        ),
        "author": {"name": "Denis Olifer", "url": "https://github.com/dolifer"},
        "homepage": "https://github.com/dolifer/NGql",
        "repository": "https://github.com/dolifer/NGql",
        "license": "MIT",
        "keywords": ["graphql", "ngql", "query-builder", "dotnet"],
    }
    (plugin_dir / "plugin.json").write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def main() -> int:
    args = parse_args()

    plugin_name = CHANNEL_TO_PLUGIN_NAME[args.channel]
    source_dir: Path = args.source
    out_dir: Path = args.out

    skill_md_in = source_dir / "SKILL.md"
    description_in = source_dir / "descriptions" / f"{args.channel}.txt"

    if not skill_md_in.is_file():
        raise SystemExit(f"source SKILL.md not found: {skill_md_in}")
    if not description_in.is_file():
        raise SystemExit(f"description file not found: {description_in}")

    src_md = skill_md_in.read_text(encoding="utf-8")
    description = description_in.read_text(encoding="utf-8").strip()

    rewritten = rewrite_frontmatter(src_md, plugin_name, description)

    # Write the rewritten SKILL.md.
    skill_md_out = out_dir / "plugins" / plugin_name / "skills" / "ngql" / "SKILL.md"
    skill_md_out.parent.mkdir(parents=True, exist_ok=True)
    skill_md_out.write_text(rewritten, encoding="utf-8")

    # Write the channel-bumped plugin.json.
    write_plugin_json(out_dir, plugin_name, args.version)

    # Ship the Skill CHANGELOG alongside SKILL.md so users browsing the catalog can see
    # what changed in this release. Optional — skipped silently if the source repo
    # doesn't ship one yet.
    changelog_in = source_dir / "CHANGELOG.md"
    changelog_out = out_dir / "plugins" / plugin_name / "CHANGELOG.md"
    if changelog_in.is_file():
        changelog_out.parent.mkdir(parents=True, exist_ok=True)
        changelog_out.write_text(changelog_in.read_text(encoding="utf-8"), encoding="utf-8")

    print(f"Staged {plugin_name}@{args.version}")
    print(f"  SKILL.md    -> {skill_md_out}")
    print(f"  plugin.json -> {out_dir / 'plugins' / plugin_name / '.claude-plugin' / 'plugin.json'}")
    if changelog_in.is_file():
        print(f"  CHANGELOG   -> {changelog_out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
