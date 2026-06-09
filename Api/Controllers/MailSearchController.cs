using CargoInbox.Application.Services;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mails")]
public class MailSearchController(CargoInboxContext context, IEmbeddingService embeddingService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        int skip = (page - 1) * pageSize;

        if (string.IsNullOrWhiteSpace(q))
        {
            var total = await context.Conversations.CountAsync();
            var latest = await context.Conversations.AsNoTracking()
                .OrderByDescending(c => c.LastMessageAt)
                .Skip(skip).Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    subject = c.Title,
                    fromAddress = c.Title,
                    dateTime = c.LastMessageAt,
                    isRead = c.Status != Core.Entities.MailStatus.Open,
                    score = 1.0
                })
                .ToListAsync();
            return Ok(new { data = latest, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling((double)total / pageSize) });
        }

        if (type == "vector")
            return await VectorSearch(q, page, pageSize, skip);

        return await TextSearch(q, page, pageSize, skip);
    }

    private async Task<IActionResult> TextSearch(string q, int page, int pageSize, int skip)
    {
        var matchingIds = await context.ConversationMessages.AsNoTracking()
            .Where(m => EF.Functions.Like(m.Subject, $"%{q}%")
                     || EF.Functions.Like(m.FromAddress, $"%{q}%")
                     || EF.Functions.Like(m.TextBody, $"%{q}%")
                     || EF.Functions.Like(m.ToAddress, $"%{q}%"))
            .Select(m => m.ConversationId)
            .Distinct()
            .ToListAsync();

        var titleMatches = await context.Conversations.AsNoTracking()
            .Where(c => EF.Functions.Like(c.Title, $"%{q}%"))
            .Select(c => c.Id)
            .ToListAsync();

        var allIds = matchingIds.Concat(titleMatches).Distinct().ToList();
        var totalCount = allIds.Count;

        var results = await context.Conversations.AsNoTracking()
            .Where(c => allIds.Contains(c.Id))
            .OrderByDescending(c => c.LastMessageAt)
            .Skip(skip).Take(pageSize)
            .Select(c => new
            {
                id = c.Id,
                subject = c.Title,
                fromAddress = c.Title,
                dateTime = c.LastMessageAt,
                isRead = c.Status != Core.Entities.MailStatus.Open,
                score = 0.8
            })
            .ToListAsync();

        return Ok(new { data = results, page, pageSize, totalCount, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    private async Task<IActionResult> VectorSearch(string q, int page, int pageSize, int skip)
    {
        float[] queryVector;
        try { queryVector = await embeddingService.GetEmbeddingAsync(q); }
        catch { queryVector = Array.Empty<float>(); }

        if (queryVector.Length == 0)
            return await TextSearch(q, page, pageSize, skip);

        var pgVector = new Vector(queryVector);
        var query = context.ConversationMessages.AsNoTracking().Where(m => m.Embedding != null);
        var totalCount = await query.CountAsync();

        var results = await query.Select(m => new
        {
            m.ConversationId,
            m.Subject,
            m.FromAddress,
            m.DateTime,
            TextBonus = (EF.Functions.Like(m.Subject, $"%{q}%") || EF.Functions.Like(m.TextBody, $"%{q}%") || EF.Functions.Like(m.FromAddress, $"%{q}%")) ? 0.3 : 0.0,
            VectorScore = m.Embedding != null ? (1.0 - (double)m.Embedding.CosineDistance(pgVector)) : 0.0
        })
        .OrderByDescending(x => x.VectorScore + x.TextBonus)
        .Skip(skip).Take(pageSize)
        .ToListAsync();

        var convIds = results.Select(r => r.ConversationId).Distinct().ToList();
        var convMap = await context.Conversations.AsNoTracking()
            .Where(c => convIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c);

        return Ok(new
        {
            data = results.Select(r => new
            {
                id = r.ConversationId,
                subject = convMap.TryGetValue(r.ConversationId, out var conv) ? conv.Title : r.Subject,
                fromAddress = r.FromAddress,
                dateTime = r.DateTime,
                isRead = convMap.TryGetValue(r.ConversationId, out var c) && c.Status != Core.Entities.MailStatus.Open,
                score = r.VectorScore + r.TextBonus
            }),
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }
}
