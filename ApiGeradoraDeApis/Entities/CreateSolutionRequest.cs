namespace ApiGeradoraDeApis.Entities
{
    public class CreateSolutionRequest
    {
        public string SolutionName { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
    }
}
