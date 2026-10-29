using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.Volunteer;

namespace AedBackend.Core.Services.Volunteer;

public interface IVolunteerApplicationReviewService
{
    Task<VolunteerApplicationReviewResult> ApproveAsync(
        int id,
        VolunteerApplicationApproveCommand command,
        CancellationToken cancellationToken);

    Task<VolunteerApplicationReviewResult> RejectAsync(
        int id,
        VolunteerApplicationRejectCommand command,
        CancellationToken cancellationToken);
}

public sealed record VolunteerApplicationApproveCommand(
    AdminPrincipal Reviewer,
    string Campus,
    double Lat,
    double Lng,
    string? ReviewNote,
    string? ClientIp,
    string? UserAgent);

public sealed record VolunteerApplicationRejectCommand(
    AdminPrincipal Reviewer,
    string ReviewNote,
    string? ClientIp,
    string? UserAgent);

public sealed record VolunteerApplicationReviewResult(
    VolunteerApplicationRecord Application,
    ResponderProfileRecord? CreatedResponder);

public sealed class VolunteerApplicationReviewException : Exception
{
    public VolunteerApplicationReviewException(int errorCode, string message, int statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public int ErrorCode { get; }
    public int StatusCode { get; }
}
