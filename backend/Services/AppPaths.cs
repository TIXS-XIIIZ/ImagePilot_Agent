namespace ImagePilot.Api.Services;

public sealed class AppPaths
{
    public AppPaths(IWebHostEnvironment environment)
    {
        Root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, ".."));
        DataFolder = Path.Combine(Root, "data");
        BrowserProfilesFolder = Path.Combine(Root, "browser-profiles");
        ProjectsFolder = Path.Combine(Root, "projects");
        ProviderConfigsFolder = Path.Combine(Root, "configs", "providers");
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(BrowserProfilesFolder);
        Directory.CreateDirectory(ProjectsFolder);
        Directory.CreateDirectory(ProviderConfigsFolder);
    }

    public string Root { get; }
    public string DataFolder { get; }
    public string BrowserProfilesFolder { get; }
    public string ProjectsFolder { get; }
    public string ProviderConfigsFolder { get; }
    public string DataFile => Path.Combine(DataFolder, "imagepilot-data.json");
    public string SchemaFile => Path.Combine(Root, "configs", "schema.sql");
}
