# Runbook: Incident Response

## Scope

This runbook covers a security report, a leaked credential, a supply-chain
concern, or a data-handling defect. A crash without data exposure is a normal
bug: use a GitHub issue instead.

## First hour

1. Acknowledge the report privately. Do not debate the finding in public.
2. Reproduce it on the latest release. Record the version, the OS build, and the
   exact steps.
3. Decide severity:
   - **Critical**: remote code execution, signing key exposure, or user data
     leaving the device.
   - **High**: local privilege misuse, updater tampering, or a bypass of the
     privacy filter that stores sensitive titles.
   - **Medium**: a defect that needs unusual user action.
   - **Low**: documentation or hardening gap with no exploit path.
4. If a secret is involved, revoke it before anything else. A rotated secret
   closes the window even while the fix is being written.

## Containment

| Situation | Action |
|---|---|
| Update feed compromised | Unpublish the release, rotate the token, publish a clean tag |
| Signing key leaked | Revoke the certificate with the CA, publish unsigned notice, re-sign after reissue |
| Data written outside the expected folder | Ship a fix that removes the file, document the exact path, tell affected users |
| Privacy filter bypass | Treat as High, fix the classifier, add a regression test |

## Fix and disclose

1. Write the fix on `development` with a regression test that fails before it.
2. Run the full gate: locale check, build with warnings as errors, full test suite.
3. Publish a patch release. Describe the impact in plain language: what was
   wrong, who was affected, what the user must do.
4. Credit the reporter if they agree.

## After

Add a short entry to `docs/FIXES.md` with the date, severity, root cause, and the
test that now guards it. Then re-read the threat model section of
[`../.github/SECURITY.md`](../../.github/SECURITY.md) and update it if the
assumptions changed.
