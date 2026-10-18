using Domain.Shared;

namespace Api.Agents;

/// <summary>Why a tool call never became a request: the arguments do not describe one.</summary>
internal static class AgentErrors
{
    internal static Error UnknownTool(string name) => new(
        "agent.unknown_tool",
        $"There is no tool '{name}'. List the tools to see what there is.",
        ErrorType.NotFound);

    internal static Error UnknownArgument(string name) => new(
        "agent.unknown_argument",
        $"The tool takes no argument '{name}'. Check the spelling against its input schema.",
        ErrorType.Validation);

    internal static Error MissingArgument(string name) => new(
        "agent.missing_argument",
        $"The argument '{name}' is required.",
        ErrorType.Validation);

    internal static Error InvalidArgument(string name, string expected) => new(
        "agent.invalid_argument",
        $"The argument '{name}' must be {expected}.",
        ErrorType.Validation);
}
