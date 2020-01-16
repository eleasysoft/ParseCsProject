namespace ParseCsProject
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    class Program
    {
        #region [ Fields ]
        private static List<string> _configurations;
        private static string _platform;
        private static string _defaultLanguage;
        #endregion

        #region [ Private Methods - Main ]
        private static void Main(string[] args)
        {
            try
            {
                var filePath = args[0];
                var projectList = GetProjectListAsync(filePath).Result;
                var commands = GetCommands(args);


                _configurations = GetConfigurations(commands);
                _platform = GetPlatform(commands);

                var propertyGroupConditions =
                    _configurations.Select(
                        configuration => $"'$(Configuration)|$(Platform)' == '{configuration}|{_platform}'").ToList();
                _defaultLanguage = "en-US";


                foreach (var project in projectList)
                {
                    var projectCollection = new ProjectCollection();
                    var proj = projectCollection.LoadProject(project);
                    var projectPropertyGroupElements = proj.Xml.PropertyGroups.Where(e => propertyGroupConditions.Any(c => e.Condition.Contains(c))).ToList();

                    Console.WriteLine($" {projectPropertyGroupElements.Count} entries for {String.Join(" ",_configurations)}|{_platform} in {project}");

                    foreach (var command in commands)
                    {
                        switch (command.Key)
                        {
                            case "/TREATWARNINGSASERRORS":
                            case "/TWE":
                                SetProperty(projectPropertyGroupElements, "TreatWarningsAsErrors", command.Value);
                                break;

                            case "/RUNCODEANALYSIS":
                            case "/RCA":
                                SetProperty(projectPropertyGroupElements, "RunCodeAnalysis", command.Value);
                                break;

                            case "/FXCOPRULESET":
                            case "/FR":
                                SetProperty(projectPropertyGroupElements, "CodeAnalysisRuleSet", command.Value);
                                break;

                            case "/DEFAULTLANGUAGE":
                            case "/DL":
                                _defaultLanguage = command.Value;
                                SetProperty(projectPropertyGroupElements, "CodeAnalysisCulture", _defaultLanguage);
                                break;

                            case "/CustomDictionary":
                            case "/CD":
                                var paths = command.Value.Split('\\');
                                proj.AddItem(
                                    "CodeAnalysisDictionary",
                                    command.Value,
                                    new List<KeyValuePair<string, string>>()
                                        {
                                            new KeyValuePair<string, string>("Link",paths.Last())
                                });
                                break;
                            case "/DEBUGTYPE":
                            case "/DT":
                                SetProperty(projectPropertyGroupElements, "DebugType", command.Value);
                                break;
                        }
                    }

                    proj.Save();
                }

            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
        #endregion

        #region [ Private Methods - Helper ]
        private static string GetPlatform(IReadOnlyDictionary<string, string> commands)
        {
            string platform;
            if (!(commands.TryGetValue("/P", out platform) ||
                  commands.TryGetValue("/PLATFORM", out platform)))
            {
                platform = "AnyCPU";
            }

            return platform;
        }

        private static List<string> GetConfigurations(Dictionary<string, string> commands)
        {
            string configurations;
            if (!(commands.TryGetValue("/C", out configurations) ||
                  commands.TryGetValue("/CONFIGURATION", out configurations)))
            {
                configurations = "Debug";
            }

            return configurations.Split(',').Select(e => e.Trim()).ToList();
        }

        private static Dictionary<string, string> GetCommands(string[] args)
        {
            var commandDictionary = new Dictionary<string, string>();
            for (var i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("/")) throw new InvalidOperationException();
                var keyValuePair = args[i].Split('=');
                commandDictionary.Add(keyValuePair[0].ToUpper().Trim(), keyValuePair[1].Trim());
            }

            return commandDictionary;
        }

        private static async Task<List<string>> GetProjectListFromTxtAsync(string filePath)
        {
            var projectList = new List<string>();
            using (var file = new StreamReader(filePath))
            {
                string project;
                while ((project = await file.ReadLineAsync()) != null)
                {
                    projectList.Add(project);
                }
            }

            return projectList;
        }
        private static async Task<List<string>> GetProjectListFromSlnAsync(string filePath)
        {
            string solutionDir = Directory.GetParent(filePath).FullName;
            var projectList = new List<string>();
            Regex regex = new Regex("Project\\(\"(?<slnid>.*)\"\\).*=.*\"(?<prjname>.*)\".*,.*\"(?<projfilepath>.*)\".*,.*\"\\{(?<projid>.*)\\}.*\"");
            using (var file = new StreamReader(filePath))
            {
                string line;
                while ((line = await file.ReadLineAsync()) != null)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        string projectPath = Path.GetFullPath($"{solutionDir}/{match.Groups["projfilepath"]}");
                        if (File.Exists(projectPath))
                            projectList.Add(projectPath);
                        else
                            Console.WriteLine($"referenced project file in solution is not availlable at {projectPath}");
                    }
                }
            }
            Console.WriteLine($" {projectList.Count} project found in solution");

            return projectList;
        }

        private static async Task<List<string>> GetProjectListAsync(string filePath)
        {
            if (filePath.EndsWith(".sln"))
                return await GetProjectListFromSlnAsync(filePath);
            else
                return await GetProjectListFromTxtAsync(filePath);
        }

        private static void SetProperty(IEnumerable<ProjectPropertyGroupElement> projectPropertyGroupElements, string key, string value)
        {
            foreach (var projectPropertyGroupElement in projectPropertyGroupElements)
            {
                projectPropertyGroupElement.SetProperty(key, value);
            }
        }
        #endregion
    }
}
