# Elastic APM .NET Framework Profiler Lab

Tests the Elastic APM profiler on a .NET Framework 4.8 Web API 2 app hosted in IIS.
Uses the same GCP VM as the .NET 6 lab — new app pool on port **8081**.

**Key difference from .NET 6 lab:** uses `COR_*` env vars (not `CORECLR_*`).

---

## Deployment Status

| Step | Status |
|---|---|
| VS Build Tools 2022 | |
| NuGet CLI | |
| FixedIncomeFramework app (net48) | |
| App Pool + Site (port 8081) | |
| Profiler env vars (COR_*) | |
| Permissions | |
| Verify APM data in Kibana | |

---

## Infrastructure

| Component | Value |
|---|---|
| OS | Windows Server 2022 |
| IIS Version | 10 |
| .NET Version | .NET Framework 4.8 |
| Architecture | x64 |
| VM | GCP e2-standard-4, africa-south1-a (same VM as .NET 6 lab) |
| App Port | 8081 |
| App Pool Managed Runtime | v4.0 |
| Profiler | C:\elastic_apm_profiler (already installed from .NET 6 lab) |

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| GET | /api/bonds | List all bonds |
| GET | /api/bonds/{isin} | Get bond by ISIN |
| GET | /api/treasurybills | List treasury bills |
| GET | /api/yield-curve | Current yield curve |
| GET | /api/portfolio/summary | Portfolio summary |
| GET | /api/settlements | List settlements |
| GET | /api/settlements/{id} | Get settlement by ID |
| POST | /api/settlements | Submit a settlement |

---

## Step-by-Step Commands

### 1. Install VS Build Tools 2022 (for MSBuild + Roslyn C# compiler)

```powershell
Invoke-WebRequest "https://aka.ms/vs/17/release/vs_buildtools.exe" -OutFile "C:\vs_buildtools.exe"
Start-Process "C:\vs_buildtools.exe" -ArgumentList "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools" -Wait
```

> This installs MSBuild and the .NET Framework targeting packs. Takes ~5 minutes.

### 2. Install NuGet CLI

```powershell
Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "C:\nuget.exe"
```

### 3. Clone repo and set up project

```powershell
git clone https://github.com/JacquesVlaming/zenith_lab.git C:\zenith_lab

$proj = "C:\FixedIncomeFramework"
New-Item -ItemType Directory -Force -Path $proj\App_Start, $proj\Controllers

Copy-Item "C:\zenith_lab\Dot NET Framework\FixedIncomeFramework\*" $proj -Recurse -Force
```

### 4. Restore NuGet packages

```powershell
C:\nuget.exe restore C:\FixedIncomeFramework\FixedIncomeFramework.csproj -PackagesDirectory C:\FixedIncomeFramework\packages
```

### 5. Build

```powershell
$msbuild = (Get-ChildItem "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild" -Recurse -Filter MSBuild.exe | Select-Object -First 1).FullName
& $msbuild C:\FixedIncomeFramework\FixedIncomeFramework.csproj /p:Configuration=Release
```

### 6. Deploy to IIS

```powershell
$src  = "C:\FixedIncomeFramework"
$dest = "C:\inetpub\FixedIncomeFramework"
$pkgs = "$src\packages"

New-Item -ItemType Directory -Force -Path "$dest\bin"

Copy-Item "$src\Web.config"  $dest
Copy-Item "$src\Global.asax" $dest
Copy-Item "$src\bin\FixedIncomeFramework.dll" "$dest\bin"

Copy-Item "$pkgs\Microsoft.AspNet.WebApi.Core.5.2.9\lib\net45\System.Web.Http.dll"              "$dest\bin"
Copy-Item "$pkgs\Microsoft.AspNet.WebApi.WebHost.5.2.9\lib\net45\System.Web.Http.WebHost.dll"  "$dest\bin"
Copy-Item "$pkgs\Microsoft.AspNet.WebApi.Client.5.2.9\lib\net45\System.Net.Http.Formatting.dll" "$dest\bin"
Copy-Item "$pkgs\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll"                          "$dest\bin"
```

### 7. IIS App Pool and Site

```powershell
$appcmd = "$env:systemroot\system32\inetsrv\AppCmd.exe"

# App pool with .NET Framework v4.0 managed runtime (not empty string like .NET 6 lab)
& $appcmd add apppool /name:"FixedIncomeFramework" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
& $appcmd add site /name:"FixedIncomeFramework" /physicalPath:"C:\inetpub\FixedIncomeFramework" /bindings:"http/*:8081:"
& $appcmd set app "FixedIncomeFramework/" /applicationPool:"FixedIncomeFramework"
```

### 8. Permissions

```powershell
New-Item -ItemType Directory -Force -Path C:\elastic_apm_logs_framework

$acl = Get-Acl "C:\inetpub\FixedIncomeFramework"
$acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\FixedIncomeFramework","ReadAndExecute","ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl "C:\inetpub\FixedIncomeFramework" $acl

$acl = Get-Acl "C:\elastic_apm_logs_framework"
$acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\FixedIncomeFramework","Modify","ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl "C:\elastic_apm_logs_framework" $acl

# Read/execute access to the profiler DLL (already granted to FixedIncomeTest — grant to this pool too)
icacls "C:\elastic_apm_profiler" /grant "IIS AppPool\FixedIncomeFramework":(OI)(CI)RX
```

> Run the icacls line from CMD (not PowerShell) to avoid parenthesis escaping issues.

### 9. Set Profiler Env Vars on App Pool

> **Critical:** Use `COR_*` (not `CORECLR_*`) for .NET Framework. This is the key difference.

```powershell
$appcmd    = "$env:systemroot\system32\inetsrv\AppCmd.exe"
$appPool   = "FixedIncomeFramework"
$profilerDir = "C:\elastic_apm_profiler"

$vars = @{
  "COR_ENABLE_PROFILING"              = "1"
  "COR_PROFILER"                      = "{FA65FE15-F085-4681-9B20-95E04F6C03CC}"
  "COR_PROFILER_PATH"                 = "$profilerDir\elastic_apm_profiler.dll"
  "ELASTIC_APM_PROFILER_HOME"         = "$profilerDir"
  "ELASTIC_APM_PROFILER_INTEGRATIONS" = "$profilerDir\integrations.yml"
  "ELASTIC_APM_SERVER_URL"            = "<your-apm-server-url>"
  "ELASTIC_APM_API_KEY"               = "<api-key>"
  "ELASTIC_APM_SERVICE_NAME"          = "FixedIncomeFramework"
  "ELASTIC_APM_LOG_LEVEL"             = "Debug"
  "ELASTIC_APM_PROFILER_LOG_DIR"      = "C:\elastic_apm_logs_framework"
}

$vars.Keys | ForEach-Object { & $appcmd set config -section:system.applicationHost/applicationPools /+"[name='$appPool'].environmentVariables.[name='$_',value='$($vars[$_])']" }
```

### 10. Restart and Test

```powershell
iisreset /stop
iisreset /start

Invoke-WebRequest http://localhost:8081/api/bonds
Start-Sleep -Seconds 5; Get-Content "C:\elastic_apm_logs_framework\*.log" -Tail 30
```

---

## COR_* vs CORECLR_* Reference

| Variable | .NET Framework | .NET Core / .NET 5+ |
|---|---|---|
| Enable profiling | `COR_ENABLE_PROFILING=1` | `CORECLR_ENABLE_PROFILING=1` |
| Profiler GUID | `COR_PROFILER={FA65FE15-...}` | `CORECLR_PROFILER={FA65FE15-...}` |
| Profiler DLL path | `COR_PROFILER_PATH=...` | `CORECLR_PROFILER_PATH=...` |
| App pool managed runtime | `v4.0` | `""` (no managed code) |

The profiler DLL (`elastic_apm_profiler.dll`) is the **same file** for both runtimes.

---

## Success Criteria

| Check | Expected |
|---|---|
| Log file created | Yes, in `C:\elastic_apm_logs_framework` |
| Log contains `PayloadSenderV2 Sent items` | Confirms connectivity to APM server |
| Log contains `Transaction` | Confirms HTTP instrumentation working |
| Kibana APM → Services | `FixedIncomeFramework` appears |
| Kibana APM → Transactions | `GET /api/bonds` etc. appear |
