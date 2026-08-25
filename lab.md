
Here's a minimal lab spec to test the Elastic APM .NET profiler on IIS without touching production.

## Deployment Status
| Step | Status |
|---|---|
| GCP Windows Server 2022 (africa-south1-a, 34.35.125.118) | ✅ Done |
| IIS Install | ✅ Done |
| .NET 8 Hosting Bundle (v8.0.30) | ✅ Done |
| Test App (FixedIncomeTest) | 🔄 In progress |
| App Pool + Site | ⏳ Pending |
| Profiler Install | ⏳ Pending |
| Env Vars + Permissions | ⏳ Pending |
| Verify APM data in Kibana | ⏳ Pending |

---

### Lab Spec: Elastic APM Profiler on IIS (.NET / No Managed Code)

---

#### Commands Run

**IIS Install**
```powershell
Install-WindowsFeature -Name Web-Server, Web-Mgmt-Console, Web-Scripting-Tools -IncludeManagementTools
```

**ASP.NET Core Hosting Bundle (v8.0.30)**
```powershell
Invoke-WebRequest "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/8.0.30/dotnet-hosting-8.0.30-win.exe" `
  -OutFile "C:\dotnet-hosting-bundle.exe"

Start-Process "C:\dotnet-hosting-bundle.exe" -ArgumentList "/quiet /norestart" -Wait

Stop-Service WAS -Force; Start-Service W3SVC
```

---

#### Infrastructure

|Component|Spec|
|---|---|
|OS|Windows Server 2019 or 2022|
|IIS Version|10+|
|.NET Version|.NET 8 (to match No Managed Code pool)|
|APM Server|Existing Elastic cluster or local Docker instance|
|Architecture|x64 only|

---

#### Lab Application

A minimal ASP.NET Core Web API — no need to use real code:

powershell

```powershell
# On the lab server
dotnet new webapi -n FixedIncomeTest
cd FixedIncomeTest
dotnet publish -c Release -o C:\inetpub\FixedIncomeTest
```

---

#### IIS Setup

**Create the app pool:**

powershell

```powershell
& "$($env:systemroot)\system32\inetsrv\AppCmd.exe" add apppool /name:"FixedIncomeTest" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated"
```

`managedRuntimeVersion:""` sets it to No Managed Code.

**Create the site/application:**

powershell

```powershell
& "$($env:systemroot)\system32\inetsrv\AppCmd.exe" add site `
    /name:"FixedIncomeTest" `
    /physicalPath:"C:\inetpub\FixedIncomeTest" `
    /bindings:"http/*:8080:"

& "$($env:systemroot)\system32\inetsrv\AppCmd.exe" set app `
    "FixedIncomeTest/" /applicationPool:"FixedIncomeTest"
```

---

#### Profiler Setup

**Download and unzip:**

powershell

```powershell
$version = "1.34.1"  # match your production version
$dest = "C:\elastic_apm_profiler"
Invoke-WebRequest `
    "https://github.com/elastic/apm-agent-dotnet/releases/download/v$version/elastic_apm_profiler_$version.zip" `
    -OutFile "C:\elastic_apm_profiler.zip"
Expand-Archive "C:\elastic_apm_profiler.zip" -DestinationPath $dest
```

**Unblock all DLLs** (critical on Windows Server):

powershell

```powershell
Get-ChildItem $dest -Recurse | Unblock-File
```

**Apply env vars to the test pool:**

powershell

```powershell
$appcmd = "$($env:systemroot)\system32\inetsrv\AppCmd.exe"
$appPool = "FixedIncomeTest"
$profilerHomeDir = "C:\elastic_apm_profiler"

$environment = @{
  CORECLR_ENABLE_PROFILING            = "1"
  CORECLR_PROFILER                    = "{FA65FE15-F085-4681-9B20-95E04F6C03CC}"
  CORECLR_PROFILER_PATH               = "$profilerHomeDir\elastic_apm_profiler.dll"
  ELASTIC_APM_PROFILER_HOME           = "$profilerHomeDir"
  ELASTIC_APM_PROFILER_INTEGRATIONS   = "$profilerHomeDir\integrations.yml"
  ELASTIC_APM_SERVER_URL              = "https://<your-apm-server>:8200"
  ELASTIC_APM_API_KEY                 = "<your-api-key>"
  ELASTIC_APM_SERVICE_NAME            = "FixedIncomeTest"
  OTEL_LOG_LEVEL                      = "debug"
  OTEL_DOTNET_AUTO_LOG_DIRECTORY      = "C:\elastic_apm_logs"
}

$environment.Keys | ForEach-Object {
  & $appcmd set config -section:system.applicationHost/applicationPools `
    /+"[name='$appPool'].environmentVariables.[name='$_',value='$($environment[$_])']"
}
```

---

#### Permissions

powershell

```powershell
$profilerHomeDir = "C:\elastic_apm_profiler"
$logDir = "C:\elastic_apm_logs"
$appPool = "FixedIncomeTest"

New-Item -ItemType Directory -Force -Path $logDir

# Profiler dir - read/execute
$acl = Get-Acl $profilerHomeDir
$acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\$appPool","ReadAndExecute",
    "ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl $profilerHomeDir $acl

# Log dir - modify
$acl = Get-Acl $logDir
$acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\$appPool","Modify",
    "ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl $logDir $acl

# App dir - read/execute
$acl = Get-Acl "C:\inetpub\FixedIncomeTest"
$acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\$appPool","ReadAndExecute",
    "ContainerInherit,ObjectInherit","None","Allow")))
Set-Acl "C:\inetpub\FixedIncomeTest" $acl
```

---

#### Start & Test

powershell

```powershell
# Restart IIS properly
Stop-Service WAS -Force
Start-Service W3SVC

# Hit an endpoint to wake the pool
Invoke-WebRequest http://localhost:8080/weatherforecast

# Check for log file
Start-Sleep -Seconds 5
Get-ChildItem "C:\elastic_apm_logs"
Get-Content "C:\elastic_apm_logs\*.log" -Tail 50
```

---

#### Success Criteria

| Check                                     | Expected                              |
| ----------------------------------------- | ------------------------------------- |
| Log file created                          | Yes, in `C:\elastic_apm_logs`         |
| Log contains `PayloadSenderV2 Sent items` | Confirms connectivity to APM server   |
| Log contains `Transaction`                | Confirms HTTP instrumentation working |
| Kibana APM → Services                     | `FixedIncomeTest` appears             |
| Kibana APM → Transactions                 | `GET /weatherforecast` appears        |