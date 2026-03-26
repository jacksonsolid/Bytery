# Protocol Status

## Current State

Bytery is currently published as a protocol draft with an active reference implementation.

## Stability Notes

- The wire format is described in `docs/spec.md`.
- The implementation and the document are close, but the specification should still be treated as stabilizing.
- Clarifications may still be needed before declaring a first stable public release.

## Known Publication Considerations

- Protocol versioning should remain separate from library versioning.
- Ambiguities should be resolved in the specification before encouraging third-party implementations.
- Cross-language conformance tests would significantly strengthen the public release.

## Recommended Release Labels

Use one of the following labels publicly until the format is fully locked:

- experimental
- draft
- beta

Avoid calling the protocol permanently stable until encoder, decoder, and spec wording are fully aligned.
