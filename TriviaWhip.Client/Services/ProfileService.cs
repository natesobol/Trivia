using Supabase;
using TriviaWhip.Shared.Models;

namespace TriviaWhip.Client.Services;

public class ProfileService
{
    private readonly Client _supabase;
    private bool _initialized;

    public Profile Current { get; private set; } = new();
    public Settings CurrentSettings { get; private set; } = new();
    public event Action? Changed;

    public ProfileService(Client supabase)
    {
        _supabase = supabase;
        Current.Milestones = BuildMilestones();
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            Changed?.Invoke();
            return;
        }

        var user = _supabase.Auth.CurrentUser ?? throw new InvalidOperationException("Not authenticated.");

        var profileResponse = await _supabase.From<TriviaProfileRow>()
            .Where(row => row.ProfileId == user.Id)
            .Get();

        var row = profileResponse.Models.FirstOrDefault();
        if (row == null)
        {
            var defaultSettings = BuildDefaultSettings();
            row = new TriviaProfileRow
            {
                ProfileId = user.Id,
                TotalCoins = Current.Coins,
                Lives = (short)Current.Lives,
                Settings = defaultSettings,
                CategoriesSelected = defaultSettings.CategoriesSelected,
                SubCategoriesSelected = defaultSettings.SubCategoriesSelected,
                Achievements = Current.Achievements
            };
            await _supabase.From<TriviaProfileRow>().Insert(row);
        }

        MapFromRow(row);
        _initialized = true;
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        var user = _supabase.Auth.CurrentUser;
        if (user == null)
        {
            return;
        }

        var settingsPayload = BuildSettingsEnvelope();

        var row = new TriviaProfileRow
        {
            ProfileId = user.Id,
            TotalCoins = Current.Coins,
            CoinsSpentStore = 0,
            CoinsSpentGame = 0,
            BuffCoinMultiplier = ConvertToStoredMultiplier(Current.Buffs.CoinMultiplier),
            BuffCorrectMultiplier = ConvertToStoredMultiplier(Current.Buffs.CorrectMultiplier),
            BuffSkipDiscount = ConvertToStoredMultiplier(Current.Buffs.SkipCostMultiplier),
            ExtraLife = Current.Buffs.ExtraLife,
            IdolLevel = (short)Current.IdolLevel,
            Streak = Current.Streak,
            Lives = (short)Current.Lives,
            TotalCorrect = Current.CorrectCount,
            Level = Current.Level,
            MilestoneProgress = Current.CorrectCount,
            BestScore = Current.BestStreak,
            DevMode = false,
            LastEmail = CurrentSettings.LastEmailAddress,
            Achievements = Current.Achievements,
            RatioScores = new(),
            CategoriesSelected = settingsPayload.CategoriesSelected,
            SubCategoriesSelected = settingsPayload.SubCategoriesSelected,
            Settings = settingsPayload
        };

        await _supabase.From<TriviaProfileRow>().Upsert(row);
        Changed?.Invoke();
    }

    private void MapFromRow(TriviaProfileRow row)
    {
        Current.Coins = row.TotalCoins;
        Current.Streak = row.Streak;
        Current.Lives = row.Lives;
        Current.Buffs = new Buff
        {
            CoinMultiplier = ConvertFromStoredMultiplier(row.BuffCoinMultiplier),
            CorrectMultiplier = ConvertFromStoredMultiplier(row.BuffCorrectMultiplier),
            SkipCostMultiplier = ConvertFromStoredMultiplier(row.BuffSkipDiscount),
            ExtraLife = row.ExtraLife
        };
        Current.IdolLevel = row.IdolLevel;
        Current.CorrectCount = row.TotalCorrect;
        Current.Level = row.Level;
        Current.Milestones = BuildMilestones();
        Current.Achievements = row.Achievements ?? new();
        Current.BestStreak = row.BestScore;

        var settings = row.Settings ?? BuildDefaultSettings();
        EnsureSettingsDefaults(settings);
        CurrentSettings = settings;

        Current.OwnedAvatars = settings.OwnedAvatars;
        Current.OwnedSchemes = settings.OwnedSchemes;
        Current.OwnedBuffs = settings.OwnedBuffs;
        Current.HasSeenLivesTutorial = settings.HasSeenLivesTutorial;
        Current.HasSeenQuestionMarkTutorial = settings.HasSeenQuestionMarkTutorial;
        Current.HasSeenCategoryTutorial = settings.HasSeenCategoryTutorial;
        Current.IncorrectCount = settings.IncorrectCount;

        CurrentSettings.CategoriesSelected = settings.CategoriesSelected;
        CurrentSettings.SubCategoriesSelected = settings.SubCategoriesSelected;
    }

    private Settings BuildSettingsEnvelope()
    {
        EnsureSettingsDefaults(CurrentSettings);
        CurrentSettings.OwnedAvatars = Current.OwnedAvatars;
        CurrentSettings.OwnedSchemes = Current.OwnedSchemes;
        CurrentSettings.OwnedBuffs = Current.OwnedBuffs;
        CurrentSettings.HasSeenLivesTutorial = Current.HasSeenLivesTutorial;
        CurrentSettings.HasSeenQuestionMarkTutorial = Current.HasSeenQuestionMarkTutorial;
        CurrentSettings.HasSeenCategoryTutorial = Current.HasSeenCategoryTutorial;
        CurrentSettings.IncorrectCount = Current.IncorrectCount;
        return CurrentSettings;
    }

    private static void EnsureSettingsDefaults(Settings settings)
    {
        settings.CategoriesSelected ??= new();
        settings.SubCategoriesSelected ??= new();
        settings.OwnedAvatars ??= new() { 0 };
        settings.OwnedSchemes ??= new() { 0 };
        settings.OwnedBuffs ??= new();
    }

    private static Settings BuildDefaultSettings()
    {
        return new Settings
        {
            DarkTheme = false,
            TimerMode = false,
            AchievementMode = true,
            QuestionCount = 10,
            EnableQuestionButtons = true,
            AlphabeticalCategories = false,
            ViewCategoriesOnStart = false,
            OwnedSchemes = new() { 0 },
            OwnedAvatars = new() { 0 },
            OwnedBuffs = new(),
            CategoriesSelected = new(),
            SubCategoriesSelected = new(),
            LastEmailAddress = string.Empty
        };
    }

    private static List<int> BuildMilestones()
    {
        return new() { 5, 15, 30, 50, 80, 120, 170, 230, 300 };
    }

    private static double ConvertFromStoredMultiplier(int stored)
    {
        if (stored <= 1)
        {
            return 1d;
        }

        return stored / 100d;
    }

    private static int ConvertToStoredMultiplier(double multiplier)
    {
        return (int)Math.Round(multiplier * 100);
    }
}
