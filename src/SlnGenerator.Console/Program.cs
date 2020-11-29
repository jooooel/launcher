using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Construction;

var command = new RootCommand()
{
    Description = "Tjenare",
};

command.AddCommand(CreateInitCommand());
command.AddCommand(CreateGoCommand());

// Show commandline help unless a subcommand was used.
command.Handler = CommandHandler.Create<IHelpBuilder>(help =>
{
    help.Write(command);
    return 1;
});

var builder = new CommandLineBuilder(command);
builder.UseHelp();
builder.UseVersionOption();
builder.UseDebugDirective();
builder.UseParseErrorReporting();
builder.ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated);

builder.CancelOnProcessTermination();
// builder.UseExceptionHandler(HandleException);

var parser = builder.Build();
await parser.InvokeAsync(args);


static Command CreateInitCommand()
{
    var command = new Command("init", "create a yaml manifest")
    {
        CommonArguments.Path_Optional,
    };

    command.AddArgument(new Argument<string>("hej", "hej"));
    command.AddOption(new Option(new[] { "-f", "--force" })
    {
        Description = "Overrides the tye.yaml file if already present for project.",
        Required = false
    });

    command.Handler = CommandHandler.Create<IConsole, FileInfo?, bool>((console, path, force) =>
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        
        var outputFilePath = GenerateYamlFile(path);
        console.Out.WriteLine($"Created '{outputFilePath}'.");

        watch.Stop();

        TimeSpan elapsedTime = watch.Elapsed;

        console.Out.WriteLine($"Time Elapsed: {elapsedTime.Hours:00}:{elapsedTime.Minutes:00}:{elapsedTime.Seconds:00}:{elapsedTime.Milliseconds / 10:00}");
    });

    return command;
}

static Command CreateGoCommand()
{
    var command = new Command("go", "go go go go")
    {
        new Argument<FileInfo>((r) => TryParsePath(r, required: false), isDefault: true)
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "file or directory, can be a yaml, sln, or project file",
            Name = "path",
        },
    };
}

// var solutionPath = args[0];
// var solutionPath = "../../../../../../notified/Notified.sln";
// GenerateYamlFile(solutionPath);

// var yamlPath = args[0];
var yamlPath = "../../../../../../notified/test.yaml";
await GenerateSlnFileAsync(yamlPath);

Console.WriteLine("Done");

static async Task GenerateSlnFileAsync(string yamlPath)
{
    var projectFile = new FileInfo(yamlPath);
    var yamlParser = new YamlParser(projectFile);
    var configApplication = yamlParser.ParseConfigApplication();

    var solutionFileName = $"test-{Guid.NewGuid():N}";
    var createSlnResult = await ProcessUtil.RunAsync("dotnet", $"new sln --name {solutionFileName} --output {projectFile.Directory.FullName}");
    if (createSlnResult.ExitCode != 0)
    {
        Console.WriteLine(createSlnResult.StandardError);
    }

    var projectList = string.Join(" ", configApplication
        .Services
        .Select(s => Path.Combine(projectFile.Directory.FullName, s.Project)));
    
    var solutionFilePath = Path.Combine(projectFile.Directory.FullName, $"{solutionFileName}.sln");
    var arguments = $"sln {solutionFilePath} add {projectList}";
    var addProjectsResult = await ProcessUtil.RunAsync("dotnet", arguments);
    if (addProjectsResult.ExitCode != 0)
    {
        Console.WriteLine(addProjectsResult.StandardError);
    }
    
    // TODO: Conditionally load depenedent projects
}

static string GenerateYamlFile(FileInfo? solutionFileInfo)
{
    Console.WriteLine($"Input: {solutionFileInfo.FullName}");
    
    var solutionFile = SolutionFile.Parse(solutionFileInfo.FullName);
    var projects = GetProjectsInSolution(solutionFile);

    var configServices = new List<ConfigService>();
    Console.WriteLine($"Projects: {Environment.NewLine}");
    foreach (var project in projects)
    {
        var launchSettings = Path.Combine(project.DirectoryName, "Properties", "launchSettings.json");
        if (File.Exists(launchSettings) || ContainsOutputTypeExe(project))
        {
            Console.WriteLine(NormalizeServiceName(Path.GetFileNameWithoutExtension(project.Name)));
            // Console.WriteLine(project.FullName.Replace('\\', '/'));
            Console.WriteLine($"Path: {Path.GetRelativePath(solutionFileInfo.DirectoryName, project.FullName)}");
            Console.WriteLine("------------------");

            configServices.Add(new ConfigService
            {
                Name = NormalizeServiceName(Path.GetFileNameWithoutExtension(project.Name)),
                Project = Path.GetRelativePath(solutionFileInfo.DirectoryName, project.FullName)
            });
        }

        // Console.WriteLine($"Name: {project.Name.Replace(project.Extension, string.Empty)}");
        // Console.WriteLine($"Path: {Path.GetRelativePath(solutionFileInfo.DirectoryName, project.FullName)}");
        // Console.WriteLine("------------------");
    }

    var configApplication = new ConfigApplication
    {
        Name = "Test Application",
        Services = configServices
    };
    var serializer = YamlSerializer.CreateSerializer();
    var serialized = serializer.Serialize(configApplication);
    
    var outputFilePath = Path.Combine(solutionFileInfo.Directory.FullName, "test.yaml");
    File.WriteAllText(outputFilePath, serialized);
    return outputFilePath;
}

static IEnumerable<FileInfo> GetProjectsInSolution(SolutionFile solutionFile)
{
    foreach (var project in solutionFile.ProjectsInOrder)
    {
        var extension = Path.GetExtension(project.AbsolutePath).ToLower();
        if (extension is not ".csproj")
        {
            continue;
        }

        yield return new FileInfo(project.AbsolutePath.Replace('\\', '/'));
    }
}

static bool ContainsOutputTypeExe(FileInfo projectFile)
{
    // Note, this will not work if OutputType is on separate lines.
    // TODO consider a more thorough check with xml reading, but at that point, it may be better just to read the project itself.
    var content = File.ReadAllText(projectFile.FullName);
    return content.Contains("<OutputType>exe</OutputType>", StringComparison.OrdinalIgnoreCase);
}

static string NormalizeServiceName(string name)
    => Regex.Replace(name.ToLowerInvariant(), "[^0-9A-Za-z-]+", "-");
    
    internal static class CommonArguments
    {
        public static Argument<FileInfo> Path_Optional
        {
            get
            {
                return new Argument<FileInfo>((r) => TryParsePath(r, required: false), isDefault: true)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = "file or directory, can be a yaml, sln, or project file",
                    Name = "path",
                };
            }
        }

        public static Argument<FileInfo> Path_Required
        {
            get
            {
                return new Argument<FileInfo>((r) => TryParsePath(r, required: true), isDefault: true)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = "file or directory, can be a yaml, sln, or project file",
                    Name = "path",
                };
            }
        }

        static FileInfo TryParsePath(ArgumentResult result, bool required)
        {
            var token = result.Tokens.Count switch
            {
                0 => ".",
                1 => result.Tokens[0].Value,
                _ => throw new InvalidOperationException("Unexpected token count."),
            };

            if (string.IsNullOrEmpty(token))
            {
                token = ".";
            }

            if (File.Exists(token))
            {
                return new FileInfo(token);
            }

            if (Directory.Exists(token))
            {
                // if (ConfigFileFinder.TryFindSupportedFile(token, out var filePath, out var errorMessage))
                // {
                //     return new FileInfo(filePath);
                // }
                // else if (required)
                // {
                //     result.ErrorMessage = errorMessage;
                //     return default!;
                // }
                // else
                // {
                //     return default!;
                // }
            }

            result.ErrorMessage = $"The file '{token}' could not be found.";
            return default!;
        }
    }