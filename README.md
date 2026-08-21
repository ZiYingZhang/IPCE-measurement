# IPCE Measurement

An offline toolkit for measuring incident power density, calculating sample IPCE, and integrating IPCE with a solar spectrum. The repository contains the MATLAB reference implementation and a self-contained C# WPF application for Windows.

## Documentation

- [中文教程](docs/USER_GUIDE_CN.md)
- [English guide](docs/USER_GUIDE_EN.md)
- [中文项目说明与公式](README_CN.md)

## Quick start

### C# WPF application (recommended for Windows users)

Download `csharp/dist/IPCEApp_Windows_x64.zip`, extract it, and run `IPCEApp.exe`. The package is self-contained and does not require MATLAB or a separately installed .NET Runtime.

The complete workflow is available from the application: import calibration data, silicon and sample i-t traces, time anchors, and a solar spectrum; calculate power density and IPCE; integrate against the spectrum; and export the results.

### MATLAB application

The MATLAB source is under `matlab/`. The portable MATLAB package requires MATLAB Runtime R2023b (64-bit). See [README_CN.md](README_CN.md) for the reference workflow and formulas.

## Data layout

```text
data/
├── defaults/   Shared startup files used by MATLAB and C#
└── examples/   Reproducible example inputs for a complete calculation
```


The default files are loaded by the application when available. Example files are optional; users can replace them with their own files while keeping the documented column and unit rules.

## Repository structure

- `csharp/` — C# WPF application, numerical core, I/O, tests, and portable build script.
- `matlab/` — MATLAB reference application, numerical functions, self-test, and packaging scripts.
- `data/defaults/` — exact shared startup inputs.
- `data/examples/` — public example measurement and alignment inputs.
- `docs/` — bilingual user tutorials.

## Verification

The release was verified with the MATLAB self-test/UI smoke test and the C# solution tests (Core 58, IO 44, Desktop 130; 232 total). Clean-machine execution and Windows display-scaling checks remain deployment gates for a future release.

## License

Released under the [MIT License](LICENSE).
