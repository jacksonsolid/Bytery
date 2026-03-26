# Examples

This folder contains browser-facing examples for GitHub readers.

Available examples:

- `json-roundtrip.html`: encode JSON to Bytery, decode back, and compare the normalized result

Recommended way to run browser examples:

```powershell
python -m http.server 8080
```

Then open:

- `http://localhost:8080/examples/json-roundtrip.html`

Note: these examples use ES modules, so they should be served over HTTP instead of opened with `file://`.
