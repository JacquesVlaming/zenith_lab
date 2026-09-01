


  

Claude finished the response

![](https://claude.ai/api/5b1bd22b-b3c1-4f10-ac3b-a3b268f4b9c2/files/9e09c116-8dc5-4a7a-ba00-734f70e46ec7/preview)1788171941244_image.png

This shows the IIS application pool configuration for **ibankapi** with the Elastic APM .NET profiler attached. Here's a breakdown of what's configured:

**App Pool Basics**

- Name: `ibankapi`
- Runtime: .NET v4.0, Integrated pipeline mode
- Identity: ApplicationPoolIdentity

**Elastic APM Agent Settings**

|Variable|Value|
|---|---|
|`ELASTIC_APM_ENVIRONMENT`|`uat`|
|`ELASTIC_APM_SERVICE_NAME`|`IbankAPI`|
|`ELASTIC_APM_SERVER_URL`|`http://10.55.15.15:8200`|
|`COR_ENABLE_PROFILING`|`1`|
|`COR_PROFILER`|`{FA65FE15-F085-4681-9B20-95E04F6C03CC}`|
|`COR_PROFILER_PATH`|`C:\inetpub\wwwroot\elastic_apm_profiler_framework\elastic_apm_profiler.dll`|

**Other Notable Settings**

- `ELASTIC_APM_PROFILER_EXCLUDE_INTEGRATIONS`: `OracleCommand;SqlCommand` — database calls are intentionally excluded from tracing
- `OTEL_LOG_LEVEL`: `debug` — verbose logging is enabled
- `OTEL_DOTNET_AUTO_LOG_DIRECTORY`: `F:\elastic_apm_framework_logs` — logs written to F: drive
- `OTEL_DOTNET_AUTO_LOG_TARGETS`: `file`

**Things to be aware of:**

1. The APM server URL uses **HTTP, not HTTPS** — data is sent unencrypted on the internal network.
2. `OTEL_LOG_LEVEL=debug` is fine for UAT but should be turned down (e.g. `warn`) in production to reduce noise/disk usage.
3. `OracleCommand` and `SqlCommand` are excluded from auto-instrumentation — if you're trying to trace DB performance, you'd need to remove those exclusions.

Is there something specific you're troubleshooting or trying to verify with this config?

it is a NET framework application.

Yes, that's confirmed right in the config — `managedRuntimeVersion="v4.0"` tells us it's **.NET Framework** (not .NET Core/.NET 5+).

This is also why the profiler uses the **COR_*** environment variables instead of the newer **CORECLR_*** ones:

|Variable|Used For|
|---|---|
|`COR_ENABLE_PROFILING=1`|.NET Framework|
|`COR_PROFILER={GUID}`|.NET Framework|
|`COR_PROFILER_PATH=...`|.NET Framework|

vs. what you'd see for .NET Core/.NET 5+:

|Variable|Used For|
|---|---|
|`CORECLR_ENABLE_PROFILING=1`|.NET Core / .NET 5+|
|`CORECLR_PROFILER={GUID}`|.NET Core / .NET 5+|
|`CORECLR_PROFILER_PATH=...`|.NET Core / .NET 5+|

The profiler DLL path also confirms it — `elastic_apm_profiler_framework` — the `framework` in the folder name explicitly indicates this is the **.NET Framework variant** of the Elastic APM profiler, not the cross-platform one.

Is there an issue you're running into with the APM agent on this app, or are you setting it up / validating the config?

PowerShell is misinterpreting the parentheses. You have two options:

**Option 1 — Run it from CMD (not PowerShell):**

cmd

```cmd
icacls F:\elastic_apm_framework_logs /grant "IIS AppPool\ibankapi":(OI)(CI)F
```

**Option 2 — Escape the parentheses in PowerShell:**

powershell

```powershell
icacls F:\elastic_apm_framework_logs /grant `"IIS AppPool\ibankapi`":`(OI`)`(CI`)F
```

Option 1 is easier — just type `cmd` in your PowerShell window to switch to a CMD prompt, run the command, then type `exit` to return to PowerShell.

what about acces to the profiler directory

Good thinking. The app pool identity also needs **read access to the profiler DLL directory**. Check current permissions:

cmd

```cmd
icacls "C:\inetpub\wwwroot\elastic_apm_profiler_framework"
```

Then grant read/execute access:

cmd

```cmd
icacls "C:\inetpub\wwwroot\elastic_apm_profiler_framework" /grant "IIS AppPool\ibankapi":(OI)(CI)RX
```

`RX` is sufficient here — the app pool only needs to **read and execute** the profiler DLL, not write to it. Full `F` access is not needed unlike the logs directory.

After granting both permissions, recycle the app pool and check for logs:

cmd

```cmd
%windir%\System32\inetsrv\appcmd recycle apppool /apppool.name:ibankapi
```

  

[Claude is AI and can make mistakes. Please double-check responses.](https://support.anthropic.com/en/articles/8525154-claude-is-providing-incorrect-or-misleading-responses-what-s-going-on)