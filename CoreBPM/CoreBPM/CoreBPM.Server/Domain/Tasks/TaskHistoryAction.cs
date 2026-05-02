namespace CoreBPM.Server.Domain.Tasks;
public enum TaskHistoryAction
{
    Created = 0, Updated = 1, StatusChanged = 2, Reassigned = 3,
    CommentAdded = 4, AttachmentAdded = 5, RelationAdded = 6, RelationRemoved = 7,
    TagAdded = 8, TagRemoved = 9, ParticipantAdded = 10, ParticipantRemoved = 11, Copied = 12
}
