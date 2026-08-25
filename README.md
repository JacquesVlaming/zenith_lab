# Elastic APM .NET Profiler Lab — IIS / No Managed Code

Minimal lab to test the Elastic APM .NET profiler on IIS without touching production.

---

## Deployment Status

| Step | Status |
|---|---|
| GCP Windows Server 2022 (africa-south1-a) | ✅ Done |
| IIS Install | ✅ Done |
| .NET 8 SDK (v8.0.424) | ✅ Done |
| .NET 8 Hosting Bundle (v8.0.30) | ✅ Done |
| Test App (FixedIncomeTest) | ✅ Done |
| App Pool + Site (port 8080) | ✅ Done |
| Profiler Install (v1.34.6-win-x64) | ✅ Done |
| Env Vars + Permissions | ✅ Done |
| Verify APM data in Kibana | ✅ Done |

---

## Infrastructure

| Component | Value |
|---|---|
| OS | Windows Server 2022 |
| IIS Version | 10 |
| .NET Version | .NET 8 |
| Architecture | x64 |
| VM | GCP e2-standard-4, africa-south1-a |
| External IP | <your-vm-ip> |
| App Port | 8080 |
| APM Server | <your-apm-server-url> |

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

### 1. IIS
```powershell
Install-WindowsFeature -Name Web-Server, Web-Mgmt-Console, Web-Scripting-Tools -IncludeManagementTools
```

### 2. .NET 8 Hosting Bundle
```powershell
Invoke-WebRequest "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/8.0.30/dotnet-hosting-8.0.30-win.exe" -OutFile "C:\dotnet-hosting-bundle.exe"
Start-Process "C:\dotnet-hosting-bundle.exe" -ArgumentList "/quiet /norestart" -Wait
Stop-Service WAS -Force; Start-Service W3SVC
```

### 3. .NET 8 SDK
```powershell
Invoke-WebRequest "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.424/dotnet-sdk-8.0.424-win-x64.exe" -OutFile "C:\dotnet-sdk.exe"
Start-Process "C:\dotnet-sdk.exe" -ArgumentList "/quiet /norestart" -Wait
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
dotnet --version
```

### 4. Create and publish the app
```powershell
dotnet new webapi -n FixedIncomeTest -o C:\FixedIncomeTest --no-openapi
```

Clone the repo and copy all files into the project:
```powershell
git clone https://github.com/JacquesVlaming/zenith_lab.git C:\zenith_lab
New-Item -ItemType Directory -Path C:\FixedIncomeTest\Controllers
Copy-Item C:\zenith_lab\Program.cs C:\FixedIncomeTest\Program.cs
Copy-Item C:\zenith_lab\Models.cs C:\FixedIncomeTest\Models.cs
Copy-Item C:\zenith_lab\BondsController.cs C:\FixedIncomeTest\Controllers\BondsController.cs
Copy-Item C:\zenith_lab\TreasuryBillsController.cs C:\FixedIncomeTest\Controllers\TreasuryBillsController.cs
Copy-Item C:\zenith_lab\YieldCurveController.cs C:\FixedIncomeTest\Controllers\YieldCurveController.cs
Copy-Item C:\zenith_lab\PortfolioController.cs C:\FixedIncomeTest\Controllers\PortfolioController.cs
Copy-Item C:\zenith_lab\SettlementsController.cs C:\FixedIncomeTest\Controllers\SettlementsController.cs
```

Publish (stop IIS first to avoid file lock):
```powershell
Stop-Service WAS -Force; dotnet publish C:\FixedIncomeTest -c Release -o C:\inetpub\FixedIncomeTest; Start-Service W3SVC
```

### 5. IIS App Pool and Site
```powershell
& "$env:systemroot\system32\inetsrv\AppCmd.exe" add apppool /name:"FixedIncomeTest" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated"
& "$env:systemroot\system32\inetsrv\AppCmd.exe" add site /name:"FixedIncomeTest" /physicalPath:"C:\inetpub\FixedIncomeTest" /bindings:"http/*:8080:"
& "$env:systemroot\system32\inetsrv\AppCmd.exe" set app "FixedIncomeTest/" /applicationPool:"FixedIncomeTest"
```

### 6. Permissions
```powershell
New-Item -ItemType Directory -Force -Path C:\elastic_apm_logs
$acl = Get-Acl "C:\inetpub\FixedIncomeTest"; $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\FixedIncomeTest","ReadAndExecute","ContainerInherit,ObjectInherit","None","Allow"))); Set-Acl "C:\inetpub\FixedIncomeTest" $acl
$acl = Get-Acl "C:\elastic_apm_logs"; $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\FixedIncomeTest","Modify","ContainerInherit,ObjectInherit","None","Allow"))); Set-Acl "C:\elastic_apm_logs" $acl
```

### 7. Install Profiler
```powershell
Invoke-WebRequest "https://github.com/elastic/apm-agent-dotnet/releases/download/v1.34.6/elastic_apm_profiler_1.34.6-win-x64.zip" -OutFile "C:\elastic_apm_profiler.zip"
Expand-Archive "C:\elastic_apm_profiler.zip" -DestinationPath "C:\elastic_apm_profiler"
Get-ChildItem "C:\elastic_apm_profiler" -Recurse | Unblock-File
```

### 8. Set Env Vars on App Pool
```powershell
$appcmd = "$env:systemroot\system32\inetsrv\AppCmd.exe"
$appPool = "FixedIncomeTest"
$profilerDir = "C:\elastic_apm_profiler"

$vars = @{
  "CORECLR_ENABLE_PROFILING"          = "1"
  "CORECLR_PROFILER"                  = "{FA65FE15-F085-4681-9B20-95E04F6C03CC}"
  "CORECLR_PROFILER_PATH"             = "$profilerDir\elastic_apm_profiler.dll"
  "ELASTIC_APM_PROFILER_HOME"         = "$profilerDir"
  "ELASTIC_APM_PROFILER_INTEGRATIONS" = "$profilerDir\integrations.yml"
  "ELASTIC_APM_SERVER_URL"            = "<your-apm-server-url>"
  "ELASTIC_APM_API_KEY"               = "<api-key>"
  "ELASTIC_APM_SERVICE_NAME"          = "FixedIncomeTest"
  "ELASTIC_APM_LOG_LEVEL"             = "Debug"
  "ELASTIC_APM_PROFILER_LOG_DIR"      = "C:\elastic_apm_logs"
}

$vars.Keys | ForEach-Object { & $appcmd set config -section:system.applicationHost/applicationPools /+"[name='$appPool'].environmentVariables.[name='$_',value='$($vars[$_])']" }
```

### 9. Restart and Test
```powershell
iisreset /stop
iisreset /start
Invoke-WebRequest http://localhost:8080/api/bonds
Start-Sleep -Seconds 5; Get-Content "C:\elastic_apm_logs\*.log" -Tail 30
```

---

## Success Criteria

| Check | Expected |
|---|---|
| Log file created | Yes, in `C:\elastic_apm_logs` |
| Log contains `PayloadSenderV2 Sent items` | Confirms connectivity to APM server |
| Log contains `Transaction` | Confirms HTTP instrumentation working |
| Kibana APM → Services | `FixedIncomeTest` appears |
| Kibana APM → Transactions | `GET /api/bonds` etc. appear |
