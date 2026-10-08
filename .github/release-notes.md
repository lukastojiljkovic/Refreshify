Refreshify cleans up, repairs and updates your PC with the tools Windows already has, in one native Windows 11 app.

## What's new

{{CHANGES}}

## Download

**{{FILE}}** for Windows 11, or Windows 10 version 1809 or later, x64.

SHA-256: `{{SHA256}}`

- **SmartScreen.** The installer isn't code-signed yet, so Windows may warn you. Check the hash with `Get-FileHash .\{{FILE}}`, then select **More info** > **Run anyway**.
- **Administrator approval.** Setup installs to Program Files. Refreshify starts its own exe as administrator for the tools that need it, so it must live where only administrators can replace it.
- **Provenance.** GitHub attests that this installer was built by this repository's release workflow: `gh attestation verify {{FILE}} --repo {{REPOSITORY}}`.

## Verification

The [release build]({{RUN_URL}}) passed all {{TESTS}} unit tests before it built this installer. The README describes [how Refreshify is verified]({{SERVER}}/{{REPOSITORY}}#verification), including how to check the administrator tools.

## Limitations

See [Limitations]({{SERVER}}/{{REPOSITORY}}#limitations) in the README.

[Terms of Use]({{SERVER}}/{{REPOSITORY}}/blob/{{TAG}}/TERMS.md) · [Privacy Statement]({{SERVER}}/{{REPOSITORY}}/blob/{{TAG}}/PRIVACY.md) · [Third-Party Notices]({{SERVER}}/{{REPOSITORY}}/blob/{{TAG}}/THIRD-PARTY-NOTICES.md)
