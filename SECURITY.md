# Security Policy

## Reporting a Vulnerability

If you discover a security issue in the Bytery parser, decoder, encoder, or supporting tooling, please report it privately before public disclosure.

Please include:

- affected component
- impact summary
- reproduction steps or crafted payload
- version or commit if known

Avoid opening a public issue for exploitable parser or decoder vulnerabilities until the maintainer has had a reasonable opportunity to investigate and patch the problem.

## Security Areas of Interest

Reports are especially valuable for:

- malformed binary payload handling
- out-of-bounds reads or writes
- denial-of-service inputs
- unsafe recursion or uncontrolled memory growth
- parser differentials between specification and implementation
