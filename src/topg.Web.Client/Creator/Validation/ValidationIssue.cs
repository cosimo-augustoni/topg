namespace topg.Web.Client.Creator.Validation;

public enum ValidationSeverity
{
    Error,

    Warning,
}

public enum ValidationTargetKind
{
    Project,
    Board,
    Category,
    Question,
}

public record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    ValidationTargetKind TargetKind,
    Guid TargetId,
    Guid? BoardId,
    string Message);
