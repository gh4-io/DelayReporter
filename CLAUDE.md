# Claude Code Instructions

Read and follow `.agents/rules.md` before performing repository work. That file contains the
shared, agent-neutral repository instructions: the promises this project makes, its layout, the
things that will bite you, how to build and verify a change, and the Git, release and safety rules.
Claude Code loads it automatically through the import below.

The instructions in this file are Claude-specific and supplement those rules.

@.agents/rules.md

## Dynamic Model Routing Instructions

Analyze incoming user tasks and categorize their complexity before proceeding. Optimize for speed, capability, and token cost by adhering strictly to this routing logic:

1. USE HAIKU FOR:
- Single-file quick edits, typos, and formatting fixes.
- Adding simple comments or docstrings.
- Generating file structure summaries or fast file lookups.
- Low-complexity, localized utility functions.

2. USE SONNET FOR:
- General feature development and standard day-to-day coding.
- Writing unit tests and standard bug fixes.
- Refactoring well-defined, single or dual-file components.
- General explanations and technical writing.

3. USE OPUS FOR:
- Complex system architecture and high-level design decisions.
- Tracing subtle, multi-file bugs or deep performance bottlenecks.
- Database migrations, security audits, and framework upgrades.
- Tasks requiring heavy sustained reasoning across extensive contexts.

If working in an environment supporting subagent routing (such as [Claude Code Subagent Routing](https://medium.com/@roanmonteiro/claude-code-subagent-model-routing-stop-paying-for-opus-on-haiku-work-ee76dc32cb88)), automatically assign subagents the minimum viable model tier matching the criteria above.
