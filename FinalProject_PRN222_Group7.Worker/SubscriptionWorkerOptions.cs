namespace FinalProject_PRN222_Group7.Worker;

public sealed class SubscriptionWorkerOptions
{
    public const string SectionName = "SubscriptionWorker";

    public int CheckIntervalMinutes { get; set; } = 5;

    public int ExpireWarningDays { get; set; } = 3;

    public int PendingPaymentTimeoutMinutes { get; set; } = 30;
}
