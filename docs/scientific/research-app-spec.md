# IPCE Research Application Specification

Last updated: 2026-08-15

## Purpose and users

IPCEApp is an offline Windows research application for photovoltaic and
photoelectrochemical IPCE measurement processing. It serves laboratory users
who may work in either Simplified Chinese or English and need the same
scientific workflow, files, plots, and results in both languages.

The repository contains two supported implementations:

- `matlab/`: numerical oracle, original programmatic UI, and MATLAB Runtime
  deployment route;
- `csharp/`: self-contained Windows x64 WPF application.

This specification governs the C# bilingual phase. MATLAB bilingualization is
a separate phase using the same terminology and numerical contract.

## Supported workflows

1. Silicon calibration and monochromatic power density
   - Inputs: calibrated silicon detector workbook, silicon i-t trace, optional
     wavelength/time anchors, scheduling parameters, dark-current range, and
     illuminated area.
   - Output: wavelength-resolved incident power density and uncertainty.
2. Sample IPCE calculation
   - Inputs: current silicon power density, sample i-t trace, optional
     wavelength/time anchors, scheduling parameters, dark-current range, and
     sample area.
   - Output: wavelength-resolved calculated IPCE and uncertainty.
3. External IPCE post-processing
   - Input: an external wavelength/IPCE file plus a solar spectrum.
   - Output: integrated and cumulative current density.
   - This workflow is standalone: it must work without calibration, silicon or
     sample i-t data, calculated IPCE, or anchors.
4. Export
   - Outputs: calculated measurement results, external IPCE, integrated current
     density, cumulative current-density curve, settings, anchors, and input
     metadata in the existing supported formats.

## Startup and deployment

- The exact shared startup files live in `data/defaults/`.
- Silicon and sample dark-current defaults remain `0.1–10 s` and `50–60 s`.
- Sample alignment initially uses anchors; fixed-delay mode remains explicit.
- The C# release is a self-contained Windows 10/11 x64 package and must run
  offline without MATLAB or a separately installed .NET Runtime.
- User data and language preference remain local to the computer.

## Bilingual interaction contract

- One build supports `English` (`en-US`) and `中文` (`zh-CN`).
- A valid saved preference wins. On first launch, any `zh-*` system culture
  selects Simplified Chinese; all other cultures select English.
- English is the neutral resource and fallback language.
- Language can change while the real window is open.
- A language change rerenders every user-facing surface: window and workflow
  text, status, prerequisites, dialogs, validation, errors, results, plot
  titles/axes/legends/hover text, toolbar text, summaries, and notifications.
- Scientific symbols and abbreviations remain canonical rather than being
  translated.

## State-preservation invariant

A language change is a presentation event only. It must not replace the main
view model or session, clear paths, reload or mutate imported traces, replace
anchors, change alignment/source selections, modify parameters, discard
calculated power density or IPCE, change integration results, or reset a plot
viewport. Existing state is rerendered through the selected resource catalog.

## Failure and recovery

- Expected domain/import/export failures remain recoverable and are presented
  in the current language by stable error code.
- Raw exception messages and stack traces remain diagnostic material.
- Preference corruption or preference I/O failure never prevents startup;
  system-language selection is used instead.
- Unexpected UI failures retain local diagnostic logging and a localized
  notification containing the log path.

## Acceptance boundary

Automated acceptance is defined in
`docs/scientific/bilingual-acceptance-checklist.md`. Clean-machine workflow
validation and 100%/125%/150% Windows scaling reviews remain external gates.
