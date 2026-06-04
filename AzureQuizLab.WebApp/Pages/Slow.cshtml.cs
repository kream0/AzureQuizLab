using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AzureQuizLab.Pages;

public class SlowModel : PageModel
{
    public void OnGet()
    {
        Thread.Sleep(3000);
    }
}
