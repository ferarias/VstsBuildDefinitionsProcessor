using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;

namespace Toolfactory.Vsts.BuidDefinitionProcessor
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            Task.Run(AsyncMain).Wait();
        }

        private static async Task AsyncMain()
        {
            // Create instance of VssConnection using AAD Credentials for AAD backed account
            var vssConnection = new VssConnection(new Uri("/"),
                new VssBasicCredential(string.Empty, ""));

            using (var projectClient = await vssConnection.GetClientAsync<ProjectHttpClient>())
            using (var buildClient = vssConnection.GetClient<BuildHttpClient>())
            {
                var myCustomBuildStep = await GetAddFeedStep(projectClient, buildClient);
                if (myCustomBuildStep == null) return;

                var projects = await projectClient.GetProjects();
                foreach (var teamProjectReference in projects)
                {
                    Console.WriteLine($"{teamProjectReference.Name}");
                    var definitionReferences =
                        await buildClient.GetDefinitionsAsync(teamProjectReference.Id, type: DefinitionType.Build);
                    foreach (var definitionReference in definitionReferences)
                    {
                        var definition =
                            await
                                buildClient.GetDefinitionAsync(definitionId: definitionReference.Id,
                                    project: teamProjectReference.Id) as BuildDefinition;
                        if (definition == null) continue;

                        Console.WriteLine(
                            $"\t{definitionReference.Name} ({definitionReference.Id}) - {definition.Steps.Count} steps");


                        if (!definition.Variables.ContainsKey("MyGetUsername"))
                        {
                            definition.Variables.Add("MyGetUsername",
                                new BuildDefinitionVariable() {Value = ""});
                        }
                        if (!definition.Variables.ContainsKey("MyGetPassword"))
                        {
                            definition.Variables.Add("MyGetPassword",
                                new BuildDefinitionVariable() {Value = "", IsSecret = true});
                        }


                        var currentAddFeedStep =
                            definition.Steps.FirstOrDefault(
                                s => s.TaskDefinition.Id == myCustomBuildStep.TaskDefinition.Id);
                        if (currentAddFeedStep == null)
                        {
                            currentAddFeedStep = new BuildDefinitionStep
                            {
                                TaskDefinition = myCustomBuildStep.TaskDefinition
                            };
                            foreach (var stepInput in myCustomBuildStep.Inputs)
                            {
                                currentAddFeedStep.Inputs.Add(stepInput);
                            }
                            definition.Steps.Add(currentAddFeedStep);
                        }

                        currentAddFeedStep.Enabled = true;

                        var orderedSteps = new List<BuildDefinitionStep>(definition.Steps.Count) {currentAddFeedStep};
                        orderedSteps.AddRange(
                            definition.Steps.Where(s => s.TaskDefinition.Id != myCustomBuildStep.TaskDefinition.Id));

                        definition.Steps.Clear();
                        definition.Steps.AddRange(orderedSteps);


                        await
                            buildClient.UpdateDefinitionAsync(definition, definitionId: definitionReference.Id,
                                project: teamProjectReference.Id);
                    }
                }
            }
        }

        private static async Task<BuildDefinitionStep> GetAddFeedStep(ProjectHttpClient projectClient,
            BuildHttpClient buildClient)
        {
            var project = await projectClient.GetProject("Logitravel");
            var definitionReferences = await buildClient.GetDefinitionsAsync(project.Id, type: DefinitionType.Build);
            foreach (var definitionReference in definitionReferences)
            {
                var definition =
                    await buildClient.GetDefinitionAsync(definitionId: definitionReference.Id, project: project.Id) as
                        BuildDefinition;
                var step = definition?.Steps.FirstOrDefault(s => s.DisplayName == "Add MyGetFeed NuGet feed");
                if (step != null) return step;
            }
            return null;
        }
    }
}