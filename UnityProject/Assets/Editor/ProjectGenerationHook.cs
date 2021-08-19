using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using SyntaxTree.VisualStudio.Unity.Bridge;
using UnityEngine;
using System.Linq;
using System;

public class ProjectFilePostprocessor : AssetPostprocessor
{
    private const string csharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

    private static Regex assemblyNameRegex = new Regex(@"<AssemblyName>(.*?)</AssemblyName>", RegexOptions.Multiline);
    private static Regex guidRegex = new Regex(@"<ProjectGuid>(.*?)</ProjectGuid>", RegexOptions.Multiline);

    private static string[] projectFilePaths = new[]
    {
        @"..\ExternalLibrary\ExternalLibrary.csproj"
    };

    private class ExternalProjectInfo
    {
        public string FilePath;
        public string AssemblyName;
        public string Guid;
    }

    private static ExternalProjectInfo[] ExternalProjectInfos;

    public static string OnGeneratedSlnSolution(string path, string content)
    {
        Debug.Log($"SolutionGenerationHook {Path.GetFileName(path)}");

        UpdateExternalProjectInfos();

        foreach (var epi in ExternalProjectInfos)
        {
            AddProjectToSolution(ref content, epi);
        }

        return content;
    }

    private static void UpdateExternalProjectInfos()
    {
        ExternalProjectInfos = projectFilePaths.Select(fp =>
            {
                try
                {
                    var contents = File.ReadAllText(fp);

                    var assemblyName = assemblyNameRegex.Match(contents).Groups[1].Value;
                    var guid = guidRegex.Match(contents).Groups[1].Value;

                    return new ExternalProjectInfo
                    {
                        FilePath = fp,
                        AssemblyName = assemblyName,
                        Guid = guid
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error reading project file {fp}\n{ex}");
                    return null;
                }
            })
            .Where(epi => epi != null)
            .ToArray();
    }

    private static void AddProjectToSolution(ref string content, ExternalProjectInfo epi)
    {
        if (content.Contains("\"" + epi.AssemblyName + "\""))
            return;

        var signature = new StringBuilder();
        const string csharpProjectTypeGuid = ProjectFilePostprocessor.csharpProjectTypeGuid;
        signature.AppendLine($"Project(\"{csharpProjectTypeGuid}\") = \"{epi.AssemblyName}\", \"{epi.FilePath}\", \"{epi.Guid}\"");
        signature.AppendLine("EndProject");
        signature.AppendLine("Global");

        var regex = new Regex("^Global", RegexOptions.Multiline);
        content = regex.Replace(content, signature.ToString());
    }

    public static string OnGeneratedCSProject(string path, string content)
    {
        Debug.Log($"ProjectGenerationHook {Path.GetFileName(path)}");

        foreach (var epi in ExternalProjectInfos)
        {
            if (TryRemoveAssemblyReferenceFromProject(ref content, epi))
            {
                Debug.Log($"Add project reference to {Path.GetFileName(path)}");
                content = AddProjectReferenceToProject(content, epi);
            }
            //content = AddCopyAssemblyToAssetsPostBuildEvent(content, assemblyName);
        }

        return content;
    }

    private static string AddCopyAssemblyToAssetsPostBuildEvent(string content, string assemblyName)
    {
        if (content.Contains("PostBuildEvent"))
            return content; // already added

        var signature = new StringBuilder();
        var dataPath = Application.dataPath.Replace('/', Path.DirectorySeparatorChar);

        signature.AppendLine("  <PropertyGroup>");
        signature.AppendLine("    <RunPostBuildEvent>Always</RunPostBuildEvent>");
		signature.AppendLine(string.Format(@"    <PostBuildEvent>copy /Y $(TargetDir){0}.dll {1}</PostBuildEvent>", assemblyName, dataPath));
        signature.AppendLine("  </PropertyGroup>");
        signature.AppendLine("</Project>");

        var regex = new Regex("^</Project>", RegexOptions.Multiline);
        return regex.Replace(content, signature.ToString());
    }

    private static bool TryRemoveAssemblyReferenceFromProject(ref string content, ExternalProjectInfo epi)
    {
        var regex = new Regex(@$"^\s*<Reference Include=""{epi.AssemblyName}"">\r\n\s*<HintPath>.*{epi.AssemblyName}.dll</HintPath>\r\n\s*</Reference>\r\n", RegexOptions.Multiline);
        if (regex.Matches(content).Count == 0)
            return false;

        content = regex.Replace(content, string.Empty);
        return true;
    }

    private static string AddProjectReferenceToProject(string content, ExternalProjectInfo epi)
    {
        if (content.Contains(">" + epi.AssemblyName + "<"))
            return content; // already added

        var signature = new StringBuilder();
        signature.AppendLine("  <ItemGroup>");
        signature.AppendLine(string.Format("    <ProjectReference Include=\"{0}\">", epi.FilePath));
        signature.AppendLine(string.Format("      <Project>{0}</Project>", epi.Guid));
        signature.AppendLine(string.Format("      <Name>{0}</Name>", epi.AssemblyName));
        signature.AppendLine("    </ProjectReference>");
        signature.AppendLine("  </ItemGroup>");
        signature.AppendLine("</Project>");

        var regex = new Regex("^</Project>", RegexOptions.Multiline);
        return regex.Replace(content, signature.ToString());
    }

}