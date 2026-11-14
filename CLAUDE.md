## Commit messages

Commits follow Conventional Commits, checked by commitlint (`commitlint.config.mjs`):
- Subject is `type(scope)!: subject`. Types: `feat`, `fix`, `docs`, `test`, `ci`, `build`, `refactor`, `perf`, `style`, `chore`, `revert`.
- Scopes are lower-case and name the area touched. `deps` is the only one in use so far; add the others here as the project grows areas.
- The subject starts lowercase, except for an acronym (`docs: README, …`). Headers are at most 100 characters.
- `!` marks a breaking change (a changed key, config, API or default).
- Merges keep `Merge …`, which the linter skips.
- `git config core.hooksPath scripts/hooks` (once per clone) runs `scripts/lint-commits --edit` as a `commit-msg` hook. `scripts/lint-commits` alone checks the commits not on `origin/main`; `scripts/lint-commits BASE` checks `BASE..HEAD`; `scripts/lint-commits --title TITLE` checks a PR title.
- PRs are squash-merged, so the PR title becomes the commit on `main` with ` (#NN)` appended. Titles follow the same rules, and are at most 93 characters to leave room for the suffix. The squash commit's body is empty (the repository's squash default is the PR title alone), so mark a breaking change in the title with `!`.
- `.github/workflows/commits.yml` checks every pushed and PR commit; `.github/workflows/pr-title.yml` checks PR titles (a required check), with the script and rules taken from the base commit.
- It runs commitlint through `npx` (Node ≥ 22.12), with `COMMITLINT_VERSION` pinned in `scripts/lint-commits` and moved by hand. To vet a bump, run `scripts/lint-commits $(git rev-list --max-parents=0 HEAD)` (the root commit) over the whole history.
