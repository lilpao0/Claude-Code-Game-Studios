# DEV_SEED source import

`dev-seed.v0.1.json` imports every original field and value from source doc29 (machine-readable catalog), implementing doc20's final CARD POOL SEED CORRECTION. The only added root field is explicit `configVersion: "0.1"`; original `metadata.version: "0.1.0"` is preserved as source metadata. The loader never infers one version from the other.

All prices, weights, capacities and future milestone labels remain test data carrying DEV_ONLY_NOT_PRODUCTION_BALANCED. Importing them enables no gameplay or production economy. The separate unit-test v0.2 fixture is a synthetic second version with identical data, not an approved release profile.

Doc29 source JSON (UTF8/LF extraction) SHA256: cc8cb3708ff1850b0b4fa9c6f89035f2105e2c7b93defa7a3f39f214280f2fc8. Independent source comparison reads the original DOCX, not this hash alone.
