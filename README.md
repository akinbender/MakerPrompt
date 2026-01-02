# MakerPrompt
[![Build](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/build.yml) [![Publish MAUI Apps](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-maui.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-maui.yml) [![Deploy Blazor WASM to GitHub Pages](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-github-pages.yml/badge.svg)](https://github.com/akinbender/MakerPrompt/actions/workflows/publish-github-pages.yml)

Open source "cross-platform" 3D printer management software powered by Blazor Hybrid. WASM version can be found [here](https://akinbender.github.io/MakerPrompt).
*Is still under initial development, use at your own risk.*

## Motivation
In our 3D-lab @[x-hain](https://x-hain.de) hackspace we have several printers from different manufacturers, and I wanted to create a unified interface for maintenance while demonstrating the capabilities of Blazor Hybrid. I've been using [Pronterface](https://github.com/kliment/Printrun) for almost a decade and have been meaning to write sth new, just found the time after recently losing my job.

## Current Status = Fast Fertig
I dont have access to any machines atm, so cannot do much bugfixing..
- **Serial**: Uses Web Serial (Chromium browsers only), can connect and recieve data but there is a buffer issue. 
**MAUI Windows App**: Uses good-old System.IO.Ports. Partial implementation (missing fan, print speed & flow telemetry update)
- **Prusalink**: Implemented but untested, so atm disabled
- **Moonraker**: Partially tested (no auth) on K1 Max, cause its had problems before I left 
- **Android Support**: apk builds but not yet..

## TODOs
- [ ] TEST & BUGFIX
- [x] Add logo
- [x] Versioning
- [ ] Enable PrusaLink
- [ ] Fix Web serial telemety update
- [ ] Fix CommandPrompt usage
- [ ] Fix ControlPanel coordinate axis movement
- [x] Implement File list component
- [x] Add Android&Macos support
- [ ] Moonraker GCode list
- [x] Expand language support? (es, pl, fr)

## Features

### BrailleRAP Integration
MakerPrompt now includes integrated support for BrailleRAP Braille embossers. Convert text to Braille and generate G-code for embossing directly through the application.

**Key Features:**
- Text-to-Braille translation (English Grade 1 supported)
- Configurable page layout (columns, rows, line spacing)
- Adjustable machine settings (feed rate, offsets)
- Real-time Braille preview
- Multi-page support with navigation
- Direct G-code generation for BrailleRAP hardware
- Send G-code to printer via existing communication pipelines (serial, Moonraker, etc.)

**Usage:**
1. Navigate to the BrailleRAP page in the application
2. Enter your text in the input area
3. Adjust page and machine settings as needed
4. Preview the Braille output
5. Generate G-code
6. Send directly to your connected BrailleRAP printer

The BrailleRAP integration is based on logic from [AccessBrailleRAP](https://github.com/braillerap/AccessBrailleRAP) and adapted for the MakerPrompt architecture.

