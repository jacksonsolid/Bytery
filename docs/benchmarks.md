# Benchmarks

## Summary

The current benchmark narrative published in `spec.md` shows Bytery outperforming plain JSON and JSON plus GZIP on realistic nested payloads.

The published examples currently include:

- `NormalRegular`
- `NormalHeavy`

## Interpretation Guidance

Benchmark numbers depend on:

- payload shape
- repetition patterns
- runtime
- hardware
- implementation details
- comparison method

They should be treated as repository-specific benchmark results, not universal guarantees.

## Current Benchmark Sources

Benchmark code currently lives under:

- `tests/Bytery.Tests/Benchmark`

## Publication Guidance

When publishing new benchmark results:

1. Record the scenario name.
2. Describe the payload shape.
3. Record hardware/runtime context.
4. Compare against a clear JSON baseline.
5. Distinguish raw Bytery from Bytery plus outer GZIP.
