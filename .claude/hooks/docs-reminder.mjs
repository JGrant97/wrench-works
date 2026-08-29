/**
 * PostToolUse hook: remind Claude to keep the project's memory files current.
 *
 * Fires after Write/Edit. If the edited file is real source in either project,
 * it injects a reminder pointing at the maintenance contract in CLAUDE.md.
 * Generated output and build artifacts are ignored — regenerating the Orval
 * client is not a reason to touch the docs.
 *
 * Reads the hook payload on stdin, writes hook JSON on stdout. Silent (exit 0,
 * no output) for anything that doesn't match, so it never interrupts a turn.
 */

let raw = "";
process.stdin.on("data", (d) => (raw += d));
process.stdin.on("end", () => {
  try {
    const payload = JSON.parse(raw);
    const path = (
      payload.tool_input?.file_path ??
      payload.tool_response?.filePath ??
      ""
    ).replace(/\\/g, "/");

    if (!path) return;

    const isSource =
      /wrench-works-api\/(src|tests)\//.test(path) ||
      /wrench-works-web\/src\//.test(path);

    const isIgnored =
      /wrench-works-web\/src\/api\/generated\//.test(path) ||
      /\/(bin|obj|node_modules|\.next)\//.test(path);

    if (!isSource || isIgnored) return;

    const area = /wrench-works-api/.test(path) ? "API" : "web app";

    process.stdout.write(
      JSON.stringify({
        hookSpecificOutput: {
          hookEventName: "PostToolUse",
          additionalContext:
            `[docs check] You changed ${area} source (${path}). Before ending this turn, ` +
            `apply the "Keeping this file and the companion docs current" contract in CLAUDE.md. ` +
            `Update CLAUDE.md, docs/app-flow.md or docs/bookings-crud.md if this change: altered an ` +
            `endpoint/DTO/route/permission; added or restructured a page; fixed a bug those docs list ` +
            `(move it to Fixed, keeping its root cause — never delete); revealed a new bug, trap or ` +
            `surprising behaviour; or answered an open question (record the answer AND the evidence). ` +
            `State how you verified anything you add. If none of that applies, no doc change is needed — ` +
            `do not mention this check.`,
        },
      })
    );
  } catch {
    // Malformed payload: stay silent rather than breaking the turn.
  }
});
