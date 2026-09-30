using SharedKernel;

namespace Domain.Production.DailyRecordings;

public sealed record DailyRecordingSavedDomainEvent(Guid DailyRecordingId) : DomainEvent;
