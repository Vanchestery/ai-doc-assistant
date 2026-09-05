namespace AiDocAssistant.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Storage root folder. A relative path is resolved from the app working directory.</summary>
    public string Root { get; set; } = "uploads";
}
