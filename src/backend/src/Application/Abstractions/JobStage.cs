namespace Application.Abstractions;

/// <summary>Where a running intake job is; the text of each is what the database and the API's <c>stage</c> hold.</summary>
public enum JobStage
{
    /// <summary>Reading what the person gave, and the page behind a link.</summary>
    Reading,

    /// <summary>The model has the material and has not written yet.</summary>
    Thinking,

    /// <summary>The draft is growing.</summary>
    Writing,

    /// <summary>The draft is whole and being saved as a recipe.</summary>
    Saving
}

/// <summary>The stored text of a <see cref="JobStage"/>.</summary>
public static class JobStages
{
    /// <summary>The lower-case word the database and the API use.</summary>
    public static string Text(this JobStage stage) => stage switch
    {
        JobStage.Reading => "reading",
        JobStage.Thinking => "thinking",
        JobStage.Writing => "writing",
        JobStage.Saving => "saving",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Not a stage a running job can be in.")
    };
}
