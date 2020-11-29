using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Construction;

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

static void GenerateYamlFile(string solutionPath)
{
    Console.WriteLine($"Input: {solutionPath}");

    var solutionFileInfo = new FileInfo(solutionPath);
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