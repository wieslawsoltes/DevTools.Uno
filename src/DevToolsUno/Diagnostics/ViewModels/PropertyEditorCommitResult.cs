namespace DevToolsUno.Diagnostics.ViewModels;

internal readonly record struct PropertyEditorCommitResult(bool Success, string? ErrorMessage)
{
    public static PropertyEditorCommitResult Applied()
        => new(true, null);

    public static PropertyEditorCommitResult Failed(string? errorMessage)
        => new(false, string.IsNullOrWhiteSpace(errorMessage) ? "Unable to apply the edited value." : errorMessage);
}
