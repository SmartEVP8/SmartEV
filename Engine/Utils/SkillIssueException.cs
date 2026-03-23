namespace Engine.Utils;

public class SkillissueException : Exception
{
    private static readonly string _skillIssue = ". Skill issue?";

    public SkillissueException()
    {
    }

    public SkillissueException(string message)
        : base(message.Contains("skill issue?", StringComparison.OrdinalIgnoreCase)
               ? message
               : message + _skillIssue)
    {
    }

    public SkillissueException(string message, Exception inner)
        : base(
            message.Contains("skill issue?", StringComparison.OrdinalIgnoreCase)
               ? message
               : message + _skillIssue, inner)
    {
    }
}
