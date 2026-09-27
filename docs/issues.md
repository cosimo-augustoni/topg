# Backlog issues

The backlog lives in GitHub Issues on `cosimo-augustoni/topg`. Every issue, whether a person or an agent writes it,
follows the structure below. This file is the only definition of that structure. Don't copy it anywhere else.

## Principles

- **Acceptance criteria come first.** They are the contract: the implementation is done when every criterion passes,
  and a reviewer tests against them. If you can't write a criterion that can be checked, the issue isn't ready.
- **Fleshed out, not padded.** Write down what a person implementing or testing the issue would otherwise have to
  ask. Leave out background everyone already knows, restated criteria and filler sections. Delete an optional
  section rather than writing "n/a".
- **Describe behaviour, not implementation.** Say what the host, player or spectator sees and does. Name code only
  under *Notes*, and only when it saves the implementer real searching.
- **One issue, one outcome.** If the criteria describe two things that could ship separately, split the issue.

## Title

An imperative phrase, max ~70 characters, with no type prefix (the label carries the type).
Examples: `Let the host reverse the turn order`, `Buzzer stays locked after a question is closed`.

## Labels

- Type (exactly one): `enhancement` (feature template) or `bug` (bug template)
- Area (one or more): `game`, `creator`

## Feature template

```markdown
## User story
As a <host | player | spectator | quiz author>, I want <capability> so that <benefit>.

## Acceptance criteria
- [ ] Given <context>, when <action>, then <observable result>.
- [ ] ...

## Out of scope            (optional)
- Things a reader might reasonably expect that this issue deliberately doesn't cover.

## Notes                   (optional)
Constraints, affected screens, pointers into the code, open questions.
```

## Bug template

```markdown
## User story
As a <role>, I expect <correct behaviour> so that <why it matters>.

## Steps to reproduce
1. ...

**Actual:** what happens.

## Acceptance criteria
- [ ] Given <the reproduction context>, when <action>, then <correct result>.
- [ ] ... (include any neighbouring behaviour that must not regress)

## Notes                   (optional)
Environment, suspected cause, pointers into the code.
```

## Writing acceptance criteria

- Use Given / When / Then, one behaviour per checkbox.
- Every criterion has to be observable in the running app (or in an automated test at the `QuizSession` /
  creator-model level). Write "the active player moves to Bob", not "direction is handled correctly".
- Cover the default, the main path, the edges (wrap-around, empty or single-item lists, mid-game changes) and which
  screens stay in sync (host, player, spectator).
- Use concrete example data (players `A, B, C`) whenever it makes the expected result unambiguous.
