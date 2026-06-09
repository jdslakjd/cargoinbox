using CargoInbox.Core.Entities;

namespace CargoInbox.Application.DTOs;

public record AssignMailRequest(string AssignedToUserId);

public record CreateCommentRequest(string Content);

public record MailCommentDto(
    string Id,
    string? MailId,
    string UserId,
    string UserName,
    string Content,
    DateTime CreatedAt
);

public record MailDetailDto(
    string Id,
    string FromAddress,
    string ToAddress,
    string Subject,
    string TextBody,
    string HtmlBody,
    DateTime DateTime,
    bool IsRead,
    MailStatus Status,
    string? AssignedToUserId,
    List<MailCommentDto> Comments
);
