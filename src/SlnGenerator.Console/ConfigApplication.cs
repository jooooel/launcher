using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

public class ConfigApplication
{
    // This gets set by all of the code paths that read the application
    [YamlIgnore] public FileInfo Source { get; set; } = default!;

    public string? Name { get; set; }

    public string? Namespace { get; set; }

    public string? Registry { get; set; }

    public string? Network { get; set; }

    public List<Dictionary<string, object>> Extensions { get; set; } = new List<Dictionary<string, object>>();

    public List<ConfigService> Services { get; set; } = new List<ConfigService>();
}