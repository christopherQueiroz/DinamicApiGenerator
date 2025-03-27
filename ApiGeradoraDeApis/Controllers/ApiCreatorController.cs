using ApiGeradoraDeApis.Entities;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;


namespace ApiGeradoraDeApis.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiCreatorController : ControllerBase
    {
        [HttpPost("create")]
        public IActionResult CreateSolution([FromBody] CreateSolutionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SolutionName))
                return BadRequest("O nome da solução é obrigatório.");

            string solutionPath = Path.Combine(request.DirectoryPath, request.SolutionName);

            try
            {
                Directory.CreateDirectory(solutionPath);
                RunCommand("dotnet", $"new sln -n {request.SolutionName}", solutionPath);

                // Criar projetos
                CreateProject("Presentation", "webapi", solutionPath);
                CreateProject("Domain", "classlib", solutionPath);
                CreateProject("Application", "classlib", solutionPath);
                CreateProject("Infra.Data", "classlib", solutionPath);
                CreateProject("Infra.IoC", "classlib", solutionPath);

                // Adicionar referências entre os projetos
                AddProjectReference("Application", "Domain", solutionPath);
                AddProjectReference("Infra.Data", "Domain", solutionPath);
                AddProjectReference("Infra.Data", "Application", solutionPath);
                AddProjectReference("Infra.IoC", "Application", solutionPath);
                AddProjectReference("Infra.IoC", "Infra.Data", solutionPath);
                AddProjectReference("Presentation", "Application", solutionPath);
                AddProjectReference("Presentation", "Infra.IoC", solutionPath);

                // Criar arquivos básicos
                CreateBasicFiles(solutionPath);

                return Ok($"Solução '{request.SolutionName}' criada com sucesso!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar a solução: {ex.Message}");
            }
        }

        private void CreateProject(string projectName, string template, string solutionPath)
        {
            RunCommand("dotnet", $"new {template} -n {projectName}", solutionPath);
            string projectPath = Path.Combine(solutionPath, projectName, $"{projectName}.csproj");
            RunCommand("dotnet", $"sln add \"{projectPath}\"", solutionPath);
        }

        private void AddProjectReference(string project, string reference, string solutionPath)
        {
            string projectPath = Path.Combine(solutionPath, project, $"{project}.csproj");
            string referencePath = Path.Combine(solutionPath, reference, $"{reference}.csproj");
            RunCommand("dotnet", $"add \"{projectPath}\" reference \"{referencePath}\"", solutionPath);
        }

        private void CreateBasicFiles(string solutionPath)
        {
            // Domain
            string domainPath = Path.Combine(solutionPath, "Domain");
            System.IO.File.WriteAllText(Path.Combine(domainPath, "IRepository.cs"),
                "namespace Domain;\n\npublic interface IRepository<T>\n{\n    void Add(T entity);\n}");

            // Application
            string applicationPath = Path.Combine(solutionPath, "Application");
            System.IO.File.WriteAllText(Path.Combine(applicationPath, "IService.cs"),
                "namespace Application;\n\npublic interface IService<T>\n{\n    void Execute(T entity);\n}");

            // Infra.Data
            string infraDataPath = Path.Combine(solutionPath, "Infra.Data");
            System.IO.File.WriteAllText(Path.Combine(infraDataPath, "Repository.cs"),
                "using Domain;\n\nnamespace Infra.Data;\n\npublic class Repository<T> : IRepository<T>\n{\n    public void Add(T entity) {}\n}");

            // Infra.IoC
            string iocPath = Path.Combine(solutionPath, "Infra.IoC");
            System.IO.File.WriteAllText(Path.Combine(iocPath, "DependencyInjection.cs"),
                "using Application;\nusing Domain;\nusing Infra.Data;\nusing Microsoft.Extensions.DependencyInjection;\n\nnamespace Infra.IoC;\n\npublic static class DependencyInjection\n{\n    public static void AddInfrastructure(this IServiceCollection services)\n    {\n        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));\n    }\n}");

            // Presentation (Configurar IoC no Program.cs)
            string presentationPath = Path.Combine(solutionPath, "Presentation");
            string programPath = Path.Combine(presentationPath, "Program.cs");
            if (System.IO.File.Exists(programPath))
            {
                string programContent = System.IO.File.ReadAllText(programPath);
                if (!programContent.Contains("AddInfrastructure"))
                {
                    programContent = programContent.Replace("var app = builder.Build();",
                        "Infra.IoC.DependencyInjection.AddInfrastructure(builder.Services);\n\nvar app = builder.Build();");
                    System.IO.File.WriteAllText(programPath, programContent);
                }
            }
        }

        private void RunCommand(string fileName, string arguments, string workingDirectory)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"Erro ao executar comando '{fileName} {arguments}': {error}");
        }
    }
}
