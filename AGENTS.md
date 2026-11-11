# Instructions for the coding assistant

These instructions apply to the assistant working in this repository. They do not impose a Git workflow on human contributors.

- Integrate the assistant's changes into `phuc` before promoting those changes to `main`.
- Do not enforce that route on other contributors through branch protection, rulesets, required status checks, GitHub Actions, or a mandatory PR template.
- Preserve teammates' branches, commits and uncommitted work. Do not force push.
- If `phuc` contains unrelated work that the user has not approved for release, do not promote it incidentally. Release only the assistant's already-integrated correction through an isolated PR and identify the earlier integration PR.
- Keep Backend in `BE/` and Frontend in `FE/`, in the existing checkout. Do not clone another project folder for routine work.
- Keep credentials, `.env`, local configuration, JWT/TOTP material, database files and build output out of Git.
