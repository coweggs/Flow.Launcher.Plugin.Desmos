# Flow Launcher Desmos Plugin

A small plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that opens graph expressions in [Desmos](https://www.desmos.com/calculator).

## Usage

Use the `des` keyword:

- `des y=x^2`
- `des y=sin(x)`
- `des y=x^2 ; y=2x+1`
- `des 3d z=sin(x)*cos(y)`

## Features

- 2D and 3D graph support
- inline preview in Flow Launcher
- recent expression history
- copies the expression to the clipboard
- opens the correct Desmos page for the graph type

## Install

1. Build the project.
2. Add the compiled plugin to your Flow Launcher plugins folder.
3. Restart Flow Launcher and type `des`.

## Project files

- `Flow.Launcher.Plugin.Desmos/` – plugin code
- `plugin.json` – plugin metadata and action keyword
- `SettingsControl.cs` – optional settings UI
