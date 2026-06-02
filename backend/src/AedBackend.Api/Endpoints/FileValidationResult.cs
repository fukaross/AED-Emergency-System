namespace AedBackend.Api.Endpoints;

internal sealed record FileValidationResult(string? Extension, IResult? Error)
{
    public static FileValidationResult Ok(string extension)
    {
        return new FileValidationResult(extension, null);
    }

    public static FileValidationResult Fail(IResult error)
    {
        return new FileValidationResult(null, error);
    }
}
