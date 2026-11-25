#!/usr/bin/env python3
"""
Paste a part's published solutions into its Starter project.

This exists so CI can prove something that matters: that the code printed in
SOLUTIONS.md is the code that actually passes the tests. A solutions *branch*
would rot quietly the first time someone fixed a bug in one place and not the
other. Extracting straight from the markdown makes drift impossible — if the
docs are wrong, the build goes red.

For each <details> block in SOLUTIONS.md it takes the FIRST ```csharp fence,
which is the canonical answer. Later fences in a block are alternatives and
deliberate anti-examples ("don't do this"), and are ignored.

Usage:
    python3 .github/scripts/apply-solutions.py part1-inventory
"""

import pathlib
import re
import sys

FENCE = re.compile(r"```csharp\n(.*?)```", re.S)
DETAILS = re.compile(r"<details>(.*?)</details>", re.S)
# "public async Task<List<Toy>> FindByTagAsync(string tag)" -> FindByTagAsync
SIGNATURE = re.compile(r"^\s*public\s+(?:async\s+)?[\w<>,\[\]?\. ]+?\s+(\w+)\s*\(", re.M)


def solutions_from_markdown(path: pathlib.Path) -> dict[str, str]:
    """Method name -> the canonical solution body, as written in the docs."""
    found: dict[str, str] = {}

    for block in DETAILS.findall(path.read_text(encoding="utf-8")):
        fences = FENCE.findall(block)
        if not fences:
            continue

        code = fences[0].strip("\n")
        match = SIGNATURE.search(code)
        if not match:
            raise SystemExit(
                f"{path}: a solution block's first C# fence isn't a method:\n"
                f"{code.splitlines()[0]}"
            )

        # A block usually answers one method. A step that is really several small methods (Part 4B's Step 7) may
        # answer them all in one block: each top-level `public` method, starting at column 0, is a separate solution.
        for name, method in split_methods(code):
            if name in found:
                raise SystemExit(f"{path}: two solution blocks both define {name}().")
            found[name] = method

    if not found:
        raise SystemExit(f"{path}: no solutions found. Has the file's shape changed?")

    return found


TOP_LEVEL = re.compile(r"^public\s+(?:async\s+)?[\w<>,\[\]?\. ]+?\s+(\w+)\s*\(", re.M)


def split_methods(code: str) -> list[tuple[str, str]]:
    """
    (name, code) for each top-level public method in a solution block.

    Almost always that is one pair: the whole block. Only a block with SEVERAL declarations that start at column 0
    is cut up, so every existing single-method block is unchanged.
    """
    starts = list(TOP_LEVEL.finditer(code))
    if len(starts) <= 1:
        first = SIGNATURE.search(code)
        return [(first.group(1), code)]

    pieces = []
    for index, start in enumerate(starts):
        end = starts[index + 1].start() if index + 1 < len(starts) else len(code)
        pieces.append((start.group(1), code[start.start():end].rstrip("\n")))
    return pieces


def method_span(source: str, name: str) -> tuple[int, int]:
    """
    The character range of `name`'s whole declaration in `source`, from the start
    of its signature line to the closing brace of its body.

    Brace counting rather than a regex: method bodies contain parentheses, braces
    and comments that no sensible pattern survives.
    """
    declaration = re.search(rf"^[ \t]*public\s+[^\n(]*?\b{re.escape(name)}\s*\(", source, re.M)
    if not declaration:
        raise SystemExit(f"No method named {name}() in the starter file.")

    start = declaration.start()

    opening = source.index("{", declaration.end())
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return start, index + 1

    raise SystemExit(f"{name}() has unbalanced braces — the starter file is broken.")


def indent(code: str, spaces: int = 4) -> str:
    pad = " " * spaces
    return "\n".join(pad + line if line.strip() else line for line in code.splitlines())


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2

    part = pathlib.Path(sys.argv[1])
    markdown = part / "SOLUTIONS.md"

    starters = sorted((part / "Starter").rglob("*.cs"))
    if not starters:
        raise SystemExit(f"No .cs files under {part / 'Starter'}.")

    solutions = solutions_from_markdown(markdown)
    remaining = dict(solutions)

    for starter in starters:
        source = starter.read_text(encoding="utf-8")

        # Right to left, so that replacing one method doesn't move the next one.
        edits = []
        for name, code in list(remaining.items()):
            # Match the DECLARATION, not any mention: a name can be called from another starter file
            # (Part 3B's GiftFinder calls VoyageEmbedder.EmbedAsync).
            if re.search(rf"^[ \t]*public\s+[^\n(]*?\b{re.escape(name)}\s*\(", source, re.M):
                edits.append((method_span(source, name), code))
                del remaining[name]

        for (start, end), code in sorted(edits, reverse=True):
            source = source[:start] + indent(code).lstrip() + source[end:]

        if edits:
            starter.write_text(source, encoding="utf-8")
            print(f"  {starter}: applied {len(edits)} solution(s)")

    if remaining:
        raise SystemExit(
            "These solutions had no matching method in the starter project:\n  "
            + "\n  ".join(sorted(remaining))
        )

    print(f"Applied all {len(solutions)} solutions from {markdown}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
