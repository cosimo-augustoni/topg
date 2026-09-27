namespace topg.Web.Client.Creator.Validation;

public enum ValidationSeverity
{
    /// <summary>Blocks the export.</summary>
    Error,

    /// <summary>Must be acknowledged before exporting.</summary>
    Warning,
}

public enum ValidationTargetKind
{
    Project,
    Board,
    Category,
    Question,
}

/// <summary>
/// One problem found by <see cref="ProjectValidator"/>. Codes and texts follow the UX-7 catalog in ui-concept.md.
/// </summary>
/// <param name="TargetId">Id of the project, board, category or question the issue belongs to.</param>
/// <param name="BoardId">Board containing the target (null for project issues), used for navigation.</param>
public record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    ValidationTargetKind TargetKind,
    Guid TargetId,
    Guid? BoardId,
    string Message);
