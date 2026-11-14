# Topic design docs

Each file here describes how one algorithm or mechanism in tilegen works, as it is built now. These are living documents: they are kept current with the code, the way OpenSpec keeps `openspec/specs/` current with behaviour.

They are not the same as the other kinds of document in this repo:

| Where | Says | Changes when |
| --- | --- | --- |
| `openspec/specs/` | What tilegen does, as observable behaviour | Behaviour changes |
| `docs/design/` (here) | How one algorithm or mechanism works | Its implementation changes |
| `docs/architecture/` | The rules all code must follow | The architecture changes |
| A change's `design.md` | Why a change was made the way it was | Never; it is archived with the change |

## What gets a topic doc

- Every technique: one doc per technique, named for it (`docs/design/<technique>.md`).
- Any mechanism whose workings aren't obvious from its code, such as how transitions become Wang sets, how a stage's seed is derived, or how the timeline decides which frames to keep.

## What a topic doc contains

- **What it does,** in a paragraph, and where it is used.
- **How it works:** the steps, in order, including what one iteration is and what frame it yields.
- **Parameters:** each one's meaning, range and effect.
- **Sources:** the paper, article or documentation the algorithm came from. If any code was adapted, name its source and licence here.
- **Decisions and limits:** choices that would surprise a reader, known weaknesses, and what was tried and dropped.
- **Where it lives:** the files that implement it and the tests and goldens that prove it.

Write it for someone who has not read the code. Describe the current state; history belongs in the archived change.

## Keeping them current

- A change's `design.md` has a "Topic docs" section listing each doc here that it creates or updates, or saying "none".
- A topic doc is updated in the same task group as the code it describes, so the two land in one PR.
- At archive, each named doc is checked against the implementation as built.
- A reviewer who sees a technique's behaviour change with no change to its topic doc should ask why.
