public static class GameSessionState
{
    public static int ZombieKills { get; private set; }
    public static int ReachedWave { get; private set; }
    public static bool WasVictory { get; private set; }
    public static string FailureReason { get; private set; } = string.Empty;
    public static float TotalPlayTime { get; private set; }
    private static float _runStartedAt;

    public static void BeginRun()
    {
        ZombieKills = 0;
        ReachedWave = 0;
        WasVictory = false;
        FailureReason = string.Empty;
        TotalPlayTime = 0f;
        _runStartedAt = UnityEngine.Time.realtimeSinceStartup;
    }

    public static void RegisterWave(int wave) => ReachedWave = wave > ReachedWave ? wave : ReachedWave;
    public static void RegisterKill() => ZombieKills++;

    public static void Finish(bool victory, string reason)
    {
        WasVictory = victory;
        FailureReason = reason ?? string.Empty;
        TotalPlayTime = UnityEngine.Time.realtimeSinceStartup - _runStartedAt;
    }
}
