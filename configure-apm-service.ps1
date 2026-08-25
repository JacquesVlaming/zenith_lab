$profilerPath = "D:\Elastic\Profiler"
$serviceName = "AutoClearingWorkerService"

$environment = [string[]]@(
    "CORECLR_ENABLE_PROFILING=1",
    "CORECLR_PROFILER={FA65FE15-F085-4681-9B20-95E04F6C03CC}",
    "CORECLR_PROFILER_PATH=$profilerPath\elastic_apm_profiler.dll",
    "ELASTIC_APM_PROFILER_HOME=$profilerPath",
    "ELASTIC_APM_PROFILER_INTEGRATIONS=$profilerPath\integrations.yml",
    "ELASTIC_APM_SERVER_URL=https://your-apm-server:8200",
    "ELASTIC_APM_SERVICE_NAME=auto-clearing-worker",
    "ELASTIC_APM_ENVIRONMENT=production"
)

Set-ItemProperty `
    -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" `
    -Name Environment `
    -Value $environment

# Verify
(Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName").Environment

# Restart service
Restart-Service -Name $serviceName
