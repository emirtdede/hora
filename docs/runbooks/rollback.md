# Runbook: Rollback

## Principle

Hora never auto-installs an update. A release is rolled forward, not pulled back:
the previous installer stays downloadable in the release history, and a bad build
is replaced by a higher version.

## Roll back a release

1. Confirm the defect. Reproduce it on the published artifact, not only locally.
2. Unpublish or mark the GitHub release as a pre-release so `/releases/latest`
   stops pointing at it.
3. Fix the defect on `development`, run the full gate, and publish the next tag.
4. Post a short note in the release description that names the broken version and
   the fixed version.

Do not delete the broken tag. Users who installed it need to be able to read what
happened.

## Roll back a single machine

1. Uninstall Hora from Windows Settings, Apps. User data is kept.
2. Install the previous setup from the release history.
3. Open Hora and confirm the version on About.

## What rollback must not do

- It must not touch `%LOCALAPPDATA%\Hora`; usage data survives an uninstall.
- It must not rewrite `hora-sync.json` in the backup folder. An older build reads
  the same versioned format.
- It must not downgrade `AppSettings` by deleting unknown fields. Unknown JSON
  members are ignored on load and preserved only for compatible keys.

## Communication

Every rollback needs one sentence for the release notes and one line in the
README if the defect affected installation. Users read the README before the
release history.
