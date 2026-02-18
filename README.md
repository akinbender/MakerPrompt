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

Each client column lists support per connection type in the order `S / M / P / B`:

- `S` = Serial
- `M` = Moonraker
- `P` = PrusaLink
- `B` = BambuLab

| Functionality                              | Web (WASM)      | MAUI Windows      | MAUI macOS        |
|--------------------------------------------|------------------|-------------------|-------------------|
|                                       |  S / M / P / B  | S / M / P /  B    | S / M / P / B    |
| Connection                                 | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| G-code command prompt / console            | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Dashboard (status + control tabs)          | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Motion & printer control panel             | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| PID tuning                                 | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Thermal model calibration                  | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Printer file explorer (printer storage)    | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Start print from printer storage           | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Copy printer files to app storage          | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Local app storage explorer                 | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| G-code viewer                              | ✅ / ✅ / ✅ / ✅ | ✅ / ✅ / ✅ / ✅  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Webcam viewer                              | ✅ / ✅ / ✅ / ✅ | ❌ / ❌ / ❌ / ❌  | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| BrailleRAP text→Braille G-code tools       | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ | ⚠️ / ⚠️ / ⚠️ / ⚠️ |
| Theme selection                            | ✅ | ✅ | ✅ |
| Localization / culture switching           | ✅ | ✅ | ✅ |
| RepRap Calculators                         | ✅ | ✅ | ✅ |


Legend: ✅= implemented and PoC, ❌= known issues, ⚠️= probably implemented but not yet fully validated on this client / platform; sequence in each cell is `S / M / P / B` as described above.

## TODOs
- [ ] TEST & BUGFIX
- [ ] Mention used open source projects

The BrailleRAP integration is based on logic from [AccessBrailleRAP](https://github.com/braillerap/AccessBrailleRAP) and adapted for the MakerPrompt architecture.

