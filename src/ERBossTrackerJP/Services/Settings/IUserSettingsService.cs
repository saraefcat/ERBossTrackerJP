namespace ERBossTrackerJP.Services.Settings;

public interface IUserSettingsService
{
    UserSettings Load();

    bool TrySave(UserSettings settings);
}
