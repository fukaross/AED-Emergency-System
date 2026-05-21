namespace AedBackend.Core.Constants;

public static class ErrorCodes
{
    public const int Success = 0;

    public const int ParameterInvalid = 1001;
    public const int Unauthorized = 1002;
    public const int Forbidden = 1003;
    public const int ResourceNotFound = 1004;
    public const int ResourceOffline = 1005;
    public const int TooManyRequests = 1006;
    public const int LocationMissing = 1007;
    public const int UploadFailed = 1008;
    public const int DuplicateHelpRequest = 1009;
    public const int AedReportProcessing = 1010;
    public const int ReportContactMissing = 1011;
    public const int SubscribeMessageNotGranted = 1012;
    public const int VitalAlertCoolingDown = 1013;
    public const int HelpAlreadyClosed = 1014;
    public const int HelpReportMissingFields = 1015;

    public const int AedNotFound = 2001;
    public const int CourseNotFound = 2002;
    public const int ArchiveNotFound = 2003;

    public const int FileTooLarge = 3001;
    public const int FileTypeNotAllowed = 3002;
    public const int OperationRateLimited = 3003;

    public const int InternalServerError = 5000;
}
