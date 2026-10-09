using AutoSpectre;
using JRK.JevRunner;

namespace Playground;

[AutoSpectreForm]
public partial class Connector
{
    public Connector()
    {
        DefaultApiKey = Environment.GetEnvironmentVariable("JEV_TOKEN");
    }
    
    public string? DefaultApiKey { get; set; }
    
    [TextPrompt(DefaultValueSource = nameof(DefaultApiKey))] public partial string ApiKey { get; set; } = string.Empty;
    
    public JevRunner GetRunner() => JevRunner.Init(ApiKey);
}