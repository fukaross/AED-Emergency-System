using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.User;

namespace AedBackend.Core.Services.User;

public sealed record SchoolBindingApproveCommand(
    AdminPrincipal Reviewer,
    int? SchoolProfileId,
    SchoolPersonProfileRecord? NewProfile,
    string? ReviewNote,
    string? ClientIp,
    string? UserAgent);

public sealed record ResponderClaimApproveCommand(
    AdminPrincipal Reviewer,
    string ResponderProfileId,
    string? ReviewNote,
    string? ClientIp,
    string? UserAgent);

public sealed record UserIdentityRejectCommand(
    AdminPrincipal Reviewer,
    string ReviewNote,
    string? ClientIp,
    string? UserAgent);

public sealed class UserIdentityReviewException : Exception
{
    public UserIdentityReviewException(int errorCode, string message, int statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public int ErrorCode { get; }
    public int StatusCode { get; }
}
