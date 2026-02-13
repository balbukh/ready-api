namespace Ready.Domain.Documents;

public enum DocumentStatus
{
    Received = 0,
    Stored = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}