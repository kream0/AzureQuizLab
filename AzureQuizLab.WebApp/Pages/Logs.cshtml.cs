using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AzureQuizLab.Pages;

public class LogsModel : PageModel
{
    private readonly ILogger<LogsModel> _logger;

    public LogsModel(ILogger<LogsModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogInformation("Acces a Quiz");
        _logger.LogWarning("Comportement inattendu");
        _logger.LogError("Erreur simulee");
    }
}
