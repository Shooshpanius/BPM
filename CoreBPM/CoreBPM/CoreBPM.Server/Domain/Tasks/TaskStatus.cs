namespace CoreBPM.Server.Domain.Tasks;
public enum TaskStatus
{
    New = 0, Read = 1, InProgress = 2, PreApproval = 3, OnApproval = 4,
    Approved = 5, PreApprovalRejected = 6, ApprovalRejected = 7, Done = 8,
    DoneNeedsControl = 9, DoneControlled = 10, CannotDo = 11,
    CannotDoNeedsControl = 12, CannotDoControlled = 13, Closed = 14, Postponed = 15
}
