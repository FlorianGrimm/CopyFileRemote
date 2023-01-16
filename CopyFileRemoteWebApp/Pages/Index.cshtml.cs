using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Identity.Web;

namespace CopyFileRemoteWebApp.Pages;

[AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    private readonly GraphServiceClient _graphServiceClient;

    public IndexModel(ILogger<IndexModel> logger,
                        GraphServiceClient graphServiceClient)
    {
        _logger = logger;
        _graphServiceClient = graphServiceClient;
    }

    public async Task OnGet()
    {
        var user = await _graphServiceClient.Me.Request().GetAsync();

        ViewData["ApiResult"] = user.DisplayName;
    }
}
