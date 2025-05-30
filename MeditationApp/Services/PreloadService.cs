using MeditationApp.ViewModels;
using MeditationApp.Services;

namespace MeditationApp.Services;

public class PreloadService
{
    private readonly MeditationSessionDatabase _database;

    public PreloadService(MeditationSessionDatabase database)
    {
        _database = database;
    }

}
