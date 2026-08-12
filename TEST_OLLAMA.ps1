param([string]$Model = "qwen3.5:2b")
$ErrorActionPreference = "Stop"
Write-Host "Checking Ollama..." -ForegroundColor Cyan
try {
    $version = Invoke-RestMethod -Uri "http://localhost:11434/api/version" -Method Get -TimeoutSec 5
} catch {
    Write-Host "Ollama is not reachable at localhost:11434." -ForegroundColor Red
    Write-Host "Install/start Ollama, then run: ollama pull $Model"
    exit 1
}
Write-Host "Ollama version: $($version.version)" -ForegroundColor DarkGray

$showBody = @{ model = $Model; verbose = $false } | ConvertTo-Json
try {
    Invoke-RestMethod -Uri "http://localhost:11434/api/show" -Method Post -ContentType "application/json" -Body $showBody -TimeoutSec 10 | Out-Null
} catch {
    Write-Host "Ollama is running, but $Model could not be opened." -ForegroundColor Yellow
    Write-Host "Run: ollama pull $Model"
    exit 2
}
Write-Host "Ollama is running and $Model is installed." -ForegroundColor Green

$body = @{
    model = $Model
    stream = $false
    think = $false
    options = @{ num_ctx = 4096; num_predict = 96; temperature = 0.6 }
    messages = @(
        @{ role = "system"; content = "You are testing an in-game chat integration. Reply in one short casual sentence, under 12 words." },
        @{ role = "user"; content = "Say hello and confirm you can respond." }
    )
} | ConvertTo-Json -Depth 8

$response = Invoke-WebRequest -Uri "http://localhost:11434/api/chat" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 90
Write-Host "Raw response:" -ForegroundColor Cyan
Write-Host $response.Content
try {
    $result = $response.Content | ConvertFrom-Json
    Write-Host "message.content: $($result.message.content)" -ForegroundColor Green
    if ($result.message.thinking) { Write-Host "thinking chars: $($result.message.thinking.Length)" -ForegroundColor Yellow }
    Write-Host "done_reason: $($result.done_reason); eval_count: $($result.eval_count)" -ForegroundColor DarkGray
} catch {
    Write-Host "Could not parse response JSON: $($_.Exception.Message)" -ForegroundColor Red
}
