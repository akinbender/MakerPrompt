# MakerPrompt
[![Build](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml) [![Publish MAUI Apps](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-maui.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-maui.yml) [![Deploy Blazor WASM to GitHub Pages](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-github-pages.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-github-pages.yml)

Open source "cross-platform" 3D printer management software powered by Blazor Hybrid. WASM version can be found [here](https://akinbender.github.io/MakerPrompt).
*Is still under initial development, use at your own risk.*

## Motivation
In our 3D-lab @[x-hain](https://x-hain.de) hackspace we have several printers from different manufacturers, and I wanted to create a unified interface for maintenance while demonstrating the capabilities of Blazor Hybrid. I've been using [Pronterface](https://github.com/kliment/Printrun) for almost a decade and have been meaning to write sth new, just found the time after recently losing my job.

## Current Status = Fast Fertig
- **WEB**: A WASM project that uses Web Serial (Chromium browsers only)
- **MAUI Windows**: Uses good-old System.IO.Ports
- **MAUI MacOS**: Uses my nuget [UsbSerialForMacOS](https://github.com/akinbender/UsbSerialForMacOS)
- **MAUI Android**: apk builds but not yet, uses [UsbSerialForAndroid.Net](https://github.com/LUJIAN2020/UsbSerialForAndroid.Net)..
- **Prusalink**: Implemented but untested, so atm disabled
- **Moonraker**: Partially tested (no auth) on K1 Max

### Feature / Client Matrix

| Functionality                              | Web (WASM) | MAUI Windows | MAUI macOS | MAUI Android |
|--------------------------------------------|------------|-------------|-----------|-------------|
| Local serial connection & telemetry        | ✅         | ✅          | ⚠️        | ⚠️ |
| G-code command prompt / console            | ✅         | ✅          | ⚠️        | ⚠️ |
| Dashboard (status + control tabs)          | ✅         | ✅          | ⚠️        | ⚠️ |
| Motion & printer control panel             | ✅         | ✅          | ⚠️        | ⚠️ |
| PID tuning                                 | ⚠️         | ⚠️          | ⚠️        | ⚠️ |
| Thermal model calibration                  | ⚠️         | ⚠️          | ⚠️        | ⚠️ |
| Printer file explorer (printer storage)    | ✅         | ✅          | ⚠️        | ⚠️ |
| Start print from printer storage           | ✅         | ✅          | ⚠️        | ⚠️ |
| Copy printer files to app storage          | ✅         | ✅          | ⚠️        | ⚠️ |
| Local app storage explorer                 | ✅         | ✅          | ⚠️        | ⚠️ |
| G-code viewer                              | ✅         | ✅          | ⚠️        | ⚠️ |
| Moonraker connection                       | ✅         | ✅          | ⚠️        | ⚠️ |
| PrusaLink connection                       | ⚠️         | ⚠️          | ⚠️        | ⚠️ |
| Webcam viewer (Moonraker, future PrusaLink)| ✅         | ❌          | ⚠️        | ⚠️ |
| BrailleRAP text→Braille G-code tools       | ⚠️         | ⚠️          | ⚠️        | ⚠️ |
| Theme selection (light/dark etc.)          | ✅         | ✅          | ⚠️        | ⚠️ |
| Localization / culture switching           | ✅         | ✅          | ⚠️        | ⚠️ |
| Calculators (price, steps/mm, lead screw)  | ✅         | ✅          | ⚠️        | ⚠️ |

Legend: ✅= implemented and PoC, ❌= known issues, ⚠️= probably implemented but not yet fully validated on this client / platform.

## TODOs
- [ ] TEST & BUGFIX
- [x] Add logo
- [x] Versioning
- [x] Enable PrusaLink
- [ ] PrusaLink webcam?
- [x] Fix Web serial telemety update
- [x] Fix CommandPrompt usage
- [x] Fix ControlPanel coordinate axis movement
- [x] Implement File list component
- [x] Add Android&Macos support
- [x] Moonraker GCode list
- [x] Moonraker Webcam
- [x] Expand language support? (es, pl, fr)
- [ ] Mention used open source projects

The BrailleRAP integration is based on logic from [AccessBrailleRAP](https://github.com/braillerap/AccessBrailleRAP) and adapted for the MakerPrompt architecture.

