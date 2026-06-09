using CargoInbox.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ai")]
public class TranslationController(AiTranslationService translationService) : ControllerBase
{
    public record TranslateRequest(string Text, string TargetLanguage);

    [HttpPost("translate")]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "翻译文本不能为空" });

        var result = await translationService.TranslateAsync(request.Text, request.TargetLanguage);
        return Ok(new { original = request.Text, translated = result, targetLanguage = request.TargetLanguage });
    }

    [HttpPost("detect-language")]
    public async Task<IActionResult> DetectLanguage([FromBody] TranslateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "文本不能为空" });

        var language = await translationService.DetectLanguageAsync(request.Text);
        return Ok(new { detectedLanguage = language });
    }
}
