using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public class YamlParser : IDisposable
{
    private YamlStream _yamlStream;
    private FileInfo? _fileInfo;
    private TextReader _reader;

    public YamlParser(string yamlContent)
        : this(new StringReader(yamlContent))
    {
    }

    public YamlParser(FileInfo fileInfo)
        : this(fileInfo.OpenText())
    {
        _fileInfo = fileInfo;
    }

    internal YamlParser(TextReader reader)
    {
        _reader = reader;
        _yamlStream = new YamlStream();
    }

    public ConfigApplication ParseConfigApplication()
    {
        try
        {
            _yamlStream.Load(_reader);
        }
        catch (YamlException ex)
        {
            // throw new TyeYamlException(ex.Start, "Unable to parse tye.yaml. See inner exception.", ex);
        }

        var app = new ConfigApplication();

        // TODO assuming first document.
        var document = _yamlStream.Documents[0];
        var node = document.RootNode;
        ConfigApplicationParser.HandleConfigApplication((YamlMappingNode)node, app);
        //
        // app.Source = _fileInfo!;
        // app.Name ??= NameInferer.InferApplicationName(_fileInfo!);
        //
        // // TODO confirm if these are ever null.
        // foreach (var service in app.Services)
        // {
        //     service.Bindings ??= new List<ConfigServiceBinding>();
        //     service.Configuration ??= new List<ConfigConfigurationSource>();
        //     service.Volumes ??= new List<ConfigVolume>();
        //     service.Tags ??= new List<string>();
        // }
        //
        // foreach (var ingress in app.Ingress)
        // {
        //     ingress.Bindings ??= new List<ConfigIngressBinding>();
        //     ingress.Rules ??= new List<ConfigIngressRule>();
        //     ingress.Tags ??= new List<string>();
        // }
        //
        return app;
    }

    public static string GetScalarValue(YamlNode node)
    {
        if (node.NodeType != YamlNodeType.Scalar)
        {
            // throw new TyeYamlException(node.Start, CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), node.NodeType.ToString()));
        }

        return ((YamlScalarNode)node).Value!;
    }

    public static string GetScalarValue(string key, YamlNode node)
    {
        if (node.NodeType != YamlNodeType.Scalar)
        {
            // throw new TyeYamlException(node.Start, CoreStrings.FormatExpectedYamlScalar(key));
        }

        return ((YamlScalarNode)node).Value!;
    }

    
    public void Dispose()
    {
        _reader.Dispose();
    }
}

public static class ConfigApplicationParser
{
    public static void HandleConfigApplication(YamlMappingNode yamlMappingNode, ConfigApplication app)
    {
        foreach (var child in yamlMappingNode.Children)
        {
            var key = YamlParser.GetScalarValue(child.Key);

            switch (key)
            {
                case "name":
                    app.Name = YamlParser.GetScalarValue(key, child.Value);
                    break;
                case "services":
                    // YamlParser.ThrowIfNotYamlSequence(key, child.Value);
                    ConfigServiceParser.HandleServiceMapping((child.Value as YamlSequenceNode)!, app.Services);
                    break;
                default:
                    break;
                    // throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
            }
        }
    }
}

public static class ConfigServiceParser
    {
        public static void HandleServiceMapping(YamlSequenceNode yamlSequenceNode, List<ConfigService> services)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                // YamlParser.ThrowIfNotYamlMapping(child);
                var service = new ConfigService();
                HandleServiceNameMapping((YamlMappingNode)child, service);
                services.Add(service);
            }
        }

        private static void HandleServiceNameMapping(YamlMappingNode yamlMappingNode, ConfigService service)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        service.Name = YamlParser.GetScalarValue(key, child.Value).ToLowerInvariant();
                        break;
                    case "project":
                        service.Project = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        break;
                        // throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }
    }