using Domain.Shared;

namespace Application.Recipes.Intake;

/// <summary>Why an intake request is refused, and the codes a job ends with when the worker could not finish it.</summary>
public static class IntakeErrors
{
    /// <summary>The job code when the model's stream ended before the draft was whole.</summary>
    public const string Interrupted = "RecipeIntake.Interrupted";

    /// <summary>No import of this person's, or not one that can be retried.</summary>
    public static readonly Error NotFound = new(
        "intake.not_found",
        "That import does not exist, or is not one that can be tried again.",
        ErrorType.NotFound);
}
