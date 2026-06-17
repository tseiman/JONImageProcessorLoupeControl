<img src="docs/logo.svg" align="right" width="72" height="72" alt="JIP logo">

# JONImageProcessorLoupeControl
A Loupedeck control plugin for the JONImageProcessor using the JONImageProcessor Gateway API
* https://github.com/tseiman/JONImageProcessor
* https://github.com/tseiman/JONImageProcessor-Gateway

## Current plugin scope

The plugin skeleton follows the structure of `LoupedeckAtemControlerPlugin`:

* .NET 8 Loupedeck plugin project under `src/JONImageProcessorLoupeControlPlugin`
* `LoupedeckWebConfigLib` integration for gateway configuration
* Gateway HTTP client for `POST /api/ipc`
* Gateway WebSocket client for `/api/ws?token=...`
* first action: `Camera ON/OFF`, backed by `camera.enabled`

The web configuration currently stores:

* Gateway URL or host, default `http://127.0.0.1:8080`
* Gateway API token

On Linux the plugin project cannot fully build unless the Loupedeck SDK `PluginApi.dll` is available at the configured SDK path. This matches the existing reference plugins.

## Build

Initialize the submodule first:

```sh
git submodule update --init --recursive
```

Build the plugin solution with .NET 8:

```sh
dotnet build src/JONImageProcessorLoupeControlPlugin.sln -c Debug
```

The project expects the Loupedeck SDK files in the same locations as the reference plugins:

* Windows: `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`
* macOS: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`

Successful builds write the plugin output to `bin/Debug/win` or `bin/Debug/mac`, depending on the build host, and create the Loupedeck `.link` file in the local Logi Plugin Service plugin folder. The `.link` file points to `bin/Debug` or `bin/Release`; that directory must contain `metadata/LoupedeckPackage.yaml` plus the platform folder containing the plugin DLL.
