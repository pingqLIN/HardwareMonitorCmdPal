# Dock configuration checks

On Windows with the .NET 10 SDK and the extension's Windows SDK build requirements:

```powershell
dotnet run --project tests/DockSettings/DockSettings.csproj -c Debug -p:Platform=x64 -p:AppxPackageSigningEnabled=false
```

The console harness loads the built extension and real Command Palette SDK objects. It publishes deterministic snapshots through the hub's private snapshot/event fields; it does not start sampling or invoke the provider constructor. Pins are written only to a new temporary directory whose path is printed on completion. No driver, scheduled task, installed package, or user configuration is changed.

Checks cover selection and immediate persistence, live updates with stable row objects, removal of unavailable sensors, an empty selection across restart, a reachable configuration button with no readings, and disposal. Exceptions terminate with a nonzero exit code. Run the pin-persistence harness separately for legacy/default initialization cases.

Visible host acceptance still requires installing a development package and using Command Palette: add **Configure Dock readings** from the Dock's **Add a band** picker, click its button, choose sensors, and verify the **Hardware readings** band changes immediately. Hide every reading, verify configuration is still accessible, then restart the extension and verify the empty selection remains. Also test Compact and Default Dock layouts. These console checks do not assert host rendering.
