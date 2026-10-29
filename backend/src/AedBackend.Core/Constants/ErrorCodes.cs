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
    public const int RescueSessionClosed = 1016;
    public const int RescueNotParticipant = 1017;
    public const int AccountDisabled = 1018;

    public const int AedNotFound = 2001;
    public const int CourseNotFound = 2002;
    public const int ArchiveNotFound = 2003;
    public const int AedOfflineRequiresNote = 2004;
    public const int AedNameDuplicate = 2005;
    public const int RescuerNotFound = 2006;
    public const int VolunteerApplicationNotFound = 2007;
    public const int DuplicateSchoolBinding = 2008;
    public const int ResponderClaimFailed = 2009;
    public const int VolunteerApplicationAlreadyProcessed = 2010;
    public const int ResponderAlreadyBound = 2011;
    public const int FeatureRequiresSqlite = 2012;
    public const int SchoolBindingNotFound = 2013;
    public const int SchoolBindingAlreadyProcessed = 2014;
    public const int SchoolProfileNotFound = 2015;
    public const int ResponderClaimNotFound = 2016;
    public const int ResponderClaimAlreadyProcessed = 2017;
    public const int PendingApplicationExists = 2018;
    public const int AdminAlreadyInitialized = 2019;

    public const int FileTooLarge = 3001;
    public const int FileTypeNotAllowed = 3002;
    public const int OperationRateLimited = 3003;

    public const int InternalServerError = 5000;
}
