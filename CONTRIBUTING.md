# Contributing to Bytery

## Scope

This repository contains both:

- the Bytery protocol specification
- the reference implementation

Contributions can target either area, but changes to the protocol require extra care because they affect cross-language compatibility.

## Before You Open a Pull Request

Please do the following first:

1. Read `docs/spec.md`.
2. Check whether the change is protocol-level or implementation-level.
3. Open an issue first for breaking protocol changes, format ambiguities, or compatibility changes.

## Contribution Types

Good contributions include:

- bug fixes in encoder or decoder logic
- additional validation and test coverage
- spec clarifications
- benchmark improvements
- examples and documentation improvements
- additional implementation ports in other languages

## Protocol Change Rules

When proposing a protocol change:

1. Describe the problem in protocol terms, not only implementation terms.
2. Explain wire-format impact.
3. State whether the change is backward compatible.
4. Update the spec and examples together.
5. Avoid silent format drift between code and documentation.

Protocol changes should not be merged as incidental side effects of implementation work.

## Implementation Change Rules

- Preserve deterministic encoding behavior where possible.
- Prefer explicit validation over permissive decoding for malformed payloads.
- Keep protocol terminology aligned with the spec.
- Do not mix unrelated refactors into the same pull request.

## Repository Areas

- `docs/`: protocol and publication documentation
- `Bytery/`: canonical VB.NET implementation
- `Test/`: canonical tests and benchmarks
- `implementations/typescript/`: TypeScript implementation artifacts
- `examples/`: browser demos and usage samples

## Testing

Before submitting a pull request, run the relevant build and test steps for your change.

Suggested commands:

```powershell
dotnet build .\Bytery.slnx -c Release
dotnet run --project .\Test\Test.vbproj -c Release -f net8.0
```

If your change affects the specification only, say so clearly in the pull request.

## Pull Request Expectations

Each pull request should include:

- a clear summary
- the reason for the change
- compatibility notes if the wire format is affected
- updated docs when behavior changes

## Style Guidance

- Keep documentation direct and technical.
- Keep protocol language precise.
- Prefer small, reviewable pull requests.

## Code of Conduct

By participating in this project, you agree to follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
