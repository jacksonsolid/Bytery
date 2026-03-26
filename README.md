![Bytery banner](bytery.jpg)

# Bytery

Bytery is a schema-aware binary serialization format and reference library designed to replace JSON in transport and storage scenarios.

It aims to make structured payloads smaller and faster to encode and decode by combining:

- compact binary primitive encodings
- string table reuse
- date table reuse
- schema table reuse
- optional outer GZIP compression

## Bytery Viewer

Below is a real Bytery payload being inspected by the built-in viewer.

The screenshot shows the full wire structure in a human-readable way: original JSON preview, optional headers, colorized hex dump, string table, date table, schema table, and the decoded data tree. This makes it much easier to debug the binary format and verify exactly how each value is represented on the wire.

In this example, the payload includes optional headers such as `author` and `when`, but headers are not required by the format.

![Bytery Viewer - colorized binary payload inspector](./bytery-viewer.png)

## Repository Purpose

This repository contains two related deliverables:

- the Bytery protocol specification
- the reference implementation and supporting experiments

## Project Status

- Protocol status: draft v1.1
- Reference implementation: VB.NET
- Secondary implementation artifacts: TypeScript
- Compatibility note: the specification is stabilizing and may still receive clarifications before a first public stable release

## Repository Structure

```text
docs/
  spec.md                  Protocol specification
  full-examples.md         Worked examples and decoding notes
  benchmarks.md            Benchmark summary and methodology notes
  protocol-status.md       Stability and compatibility notes
  roadmap.md               Planned work
Bytery/
  ...                      Canonical VB.NET library project
Test/
  ...                      Canonical tests and benchmarks project
implementations/
  typescript/              TypeScript implementation artifacts
examples/
  README.md                Browser demo notes
  json-roundtrip.html      JSON -> Bytery -> JSON demo
src/
  README.md                Source layout note
tests/
  README.md                Test layout note
assets/
  README.md                Reserved for diagrams, logos, and media
```

## Documentation

- Specification: [docs/spec.md](docs/spec.md)
- Full examples: [docs/full-examples.md](docs/full-examples.md)
- Benchmarks: [docs/benchmarks.md](docs/benchmarks.md)
- Protocol status: [docs/protocol-status.md](docs/protocol-status.md)
- Roadmap: [docs/roadmap.md](docs/roadmap.md)
- Browser example: [examples/json-roundtrip.html](examples/json-roundtrip.html)

## Quick Start

Primary implementation paths in the current repository:

- `Bytery`
- `Test`
- `implementations/typescript`

Published documentation paths:

- `docs/spec.md`
- `examples/json-roundtrip.html`

## Design Goals

- Smaller payloads than plain JSON
- Faster encode and decode for repeated structured data
- Deterministic binary wire format
- Clear separation between protocol and implementation
- Friendly to future multi-language implementations

## Publishing Guidance

If you are arriving from GitHub:

1. Read the protocol first in `docs/spec.md`.
2. Review the VB.NET implementation in `Bytery/`.
3. Review the TypeScript implementation artifacts in `implementations/typescript/`.
4. Use `CONTRIBUTING.md` before opening pull requests.

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or pull request.

## Security

See [SECURITY.md](SECURITY.md) for responsible disclosure guidance.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
