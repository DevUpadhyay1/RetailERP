using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace RetailERP.Controllers;

/// <summary>Sprint 15 – Language switcher endpoint. Sets culture cookie.</summary>
public class LanguageController : Controller
{
    [HttpPost]
    public IActionResult Set(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }
}
