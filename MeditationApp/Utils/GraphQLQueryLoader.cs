// MeditationApp/Utils/GraphQLQueryLoader.cs
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MeditationApp.Utils;

public static class GraphQLQueryLoader
{
    public static async Task<string> LoadQueryAsync(string filename)
    {
        try
        {
            using var stream = await Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync($"GraphQL/Queries/{filename}");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load GraphQL asset: {filename}, {ex.Message}");
            return string.Empty;
        }
    }
}