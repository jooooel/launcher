using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class YamlSerializer
{
    public static ISerializer CreateSerializer()
    {
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            // .WithEmissionPhaseObjectGraphVisitor(args => new OmitDefaultAndEmptyArrayObjectGraphVisitor(args.InnerVisitor))
            .Build();
    }
}