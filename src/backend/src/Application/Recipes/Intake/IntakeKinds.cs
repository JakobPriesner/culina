namespace Application.Recipes.Intake;

/// <summary>The composer's drafts that run as jobs, as they are stored in the job's material.</summary>
public static class IntakeKinds
{
    /// <summary>A sentence about dinner, written up as a recipe.</summary>
    public const string Idea = "idea";

    /// <summary>A rewrite of a recipe the person already has.</summary>
    public const string Revision = "revision";

    /// <summary>What an import's draft is composed as: a recipe read from material somebody shared.</summary>
    public const string Social = "social";
}
