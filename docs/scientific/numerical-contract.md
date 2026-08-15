# IPCE Numerical and Interoperability Contract

Last updated: 2026-08-15

## Authority

The MATLAB implementation is the numerical oracle. Existing C# golden-parity,
Core, IO, Desktop, end-to-end, and reproducible-export tests protect the C#
implementation. Localization is not authorization to change numerical or file
semantics.

## Canonical units

| Quantity | Canonical representation |
|---|---|
| i-t time | `s` (`Time_s`) |
| i-t current | `A` (`Current_A`) |
| time-alignment wavelength | `nm` |
| time-alignment confirmed time | `s` |
| external IPCE wavelength | `nm` |
| external IPCE value | `%` |
| spectrum irradiance | `W m^-2 nm^-1` |
| integrated/cumulative current density | `mA cm^-2` |

Accepted source time units are `s`, `sec`, `second`, `ms`, `min`, and `h`.
Accepted source current units are `A`, `mA`, `uA`, `µA`, `μA`, `nA`, and
`pA`.

## Non-negotiable behavior

- Missing i-t units are never guessed from magnitude; the user selects them.
- Time-alignment files are fixed as wavelength `nm` and confirmed time `s`.
- External IPCE uses the first numeric column as `nm` and the second as `%`.
- Finite external IPCE is not clipped to 0–100%.
- Calculated and external IPCE remain separate state objects.
- Integration does not extrapolate outside common IPCE/spectrum coverage.
- Numerical interpolation, scheduling, window extraction, dark subtraction,
  uncertainty propagation, and integration stay identical to the MATLAB/C#
  verified baseline.

## Localization boundary

Localization may change only presentation strings and culture-specific display
formatting. It must not change:

- stored or calculated `double` values;
- wavelength grids, coverage tests, interpolation, or integration;
- stable `IPCE:*` error codes;
- current/stale/missing state transitions;
- tabular export names, column order, column identifiers, or invariant numeric
  serialization;
- input files, user-chosen output paths, or file extension behavior;
- plot X/Y/error values, band limits, viewport calculations, or layer state.

Canonical unit symbols remain identical in English and Chinese resources:
`nm`, `s`, `A`, `W m⁻² nm⁻¹`, `µW cm⁻²`, `mA cm⁻²`, and `%`.

## Required regression gates

```powershell
dotnet build "csharp/IPCE.slnx" -c Release --no-restore
dotnet test "csharp/IPCE.slnx" -c Release --no-build --no-restore
matlab -batch "cd('matlab'); run_ipce_selftest; app = IPCEApp; drawnow; assert(isvalid(app)); close(app)"
```

Bilingual parity tests additionally construct identical workflow and plot
models under both languages and compare all scientific values exactly. Export
parity tests compare schemas and invariant serialized data across languages.
