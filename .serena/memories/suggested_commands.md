# Suggested commands (PowerShell)
- Build MAUI host: dotnet build NodeEditorMax\NodeEditorMax.csproj
- Build Blazor library: dotnet build NodeEditor.Blazor\NodeEditor.Blazor.csproj
- Run web host: dotnet run --project NodeEditor.Blazor.WebHost\NodeEditor.Blazor.WebHost.csproj --urls "http://localhost:5173"
- Run tests: dotnet test NodeEditor.Blazor.Tests\NodeEditor.Blazor.Tests.csproj

Utility (PowerShell):
- List files: Get-ChildItem
- Find text: Select-String -Path <path> -Pattern <pattern>
