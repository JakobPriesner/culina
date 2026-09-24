namespace Application.Settings;

/// <summary>What saving server settings did to the running server.</summary>
public enum ServerChange
{
    /// <summary>Nothing differed from what it runs with, so nothing was saved.</summary>
    None,

    /// <summary>The settings were saved, and the server is restarting to use them.</summary>
    Restarting,
}
