# MakerPrompt
Open source "cross-platform" 3D printer management software powered by Blazor Hybrid. Current demo can be found [here](https://yellow-sea-04668c503.6.azurestaticapps.net/). *Is still under development, use at your own risk.*

## Motivation
In our 3D-lab @[x-hain](https://x-hain.de) hackspace we have several printers from different manufacturers, and I wanted to create a unified interface for maintenance while demonstrating the capabilities of Blazor Hybrid. I've been using Pronterface for almost a decade and have been meaning to write sth new, just found the time after recently losing my job.

## Current Status = Fast Fertig
- **Serial**: Uses Web Serial (Chromium browsers only), can connect and recieve data but there is a buffer issue. **MAUI Windows App**: Uses good-old System.IO.Ports. Partial implementation (missing fan, print speed & flow telemetry update)
- **Prusalink**: Implemented but untested, so atm disabled
- **Moonraker**: Partially tested (no auth) on K1 Max, cause its has problems as always 
- **Android Support**: Not yet..

## TODOs
- [ ] TEST & BUGFIX
- [x] Add logo
- [x] Versioning
- [ ] Enable PrusaLink
- [ ] Fix Web serial telemety update
- [ ] Fix CommandPrompt usage
- [ ] Fix ControlPanel coordinate axis movement
- [x] Implement File list component
- [ ] Add Android&Macos support
- [ ] Moonraker GCode list
- [ ] Expand language support? (es, pl, fr)
