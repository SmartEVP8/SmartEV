namespace Engine.Utils;

/// <summary>
/// Custom exception type for skill issue errors, appending a humorous message to the original error message if not already present.
/// </summary>
public class SkillissueException : Exception
{
    private static readonly string _skillIssue = ". Skill issue?";

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillissueException"/> class with a default message.
    /// </summary>
    public SkillissueException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillissueException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The skill issue message that describes the error.</param>
    public SkillissueException(string message)
        : base(message.Contains("skill issue?", StringComparison.OrdinalIgnoreCase)
               ? message
               : message + _skillIssue)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillissueException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The skill issue message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public SkillissueException(string message, Exception inner)
        : base(
            message.Contains("skill issue?", StringComparison.OrdinalIgnoreCase)
               ? message
               : message + _skillIssue, inner)
    {
    }
}
