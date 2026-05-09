namespace MirageQueue.Publishers.Abstractions;

/// <summary>
/// Result of a publish/schedule. <see cref="MessageId"/> is non-null only when
/// the call carried an idempotency key (keyed overloads of <see cref="IPublisher"/>
/// or <see cref="MirageQueue.Outbox.IDbContextOutbox{TDbContext}"/>); for unkeyed
/// outbox publishes the message id isn't surfaced through this struct.
/// </summary>
public readonly record struct PublishResult(Guid? MessageId, bool IsDuplicate);
