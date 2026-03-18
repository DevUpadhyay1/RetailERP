using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using RetailERP.Data.Identity;

namespace RetailERP.Areas.Identity.Pages.Account.Manage;

public class EnableAuthenticatorModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UrlEncoder _urlEncoder;

    public EnableAuthenticatorModel(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _urlEncoder = urlEncoder;
    }

    public string SharedKey { get; set; } = "";
    public string QrCodeDataUri { get; set; } = "";
    public string[] RecoveryCodes { get; set; } = Array.Empty<string>();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Enter a 6-digit code")]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await LoadSharedKeyAndQrCode(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCode(user);
            return Page();
        }

        var code = Input.Code.Replace(" ", "").Replace("-", "");
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            ModelState.AddModelError("Input.Code", "Invalid verification code. Please try again.");
            await LoadSharedKeyAndQrCode(user);
            return Page();
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        RecoveryCodes = recoveryCodes?.ToArray() ?? Array.Empty<string>();

        await _signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "2FA has been enabled. Save your recovery codes in a safe place.";
        TempData["RecoveryCodes"] = string.Join(",", RecoveryCodes);

        return RedirectToPage("./TwoFactorAuthentication");
    }

    private async Task LoadSharedKeyAndQrCode(ApplicationUser user)
    {
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey!);

        var email = await _userManager.GetEmailAsync(user);
        var uri = $"otpauth://totp/RetailERP:{_urlEncoder.Encode(email!)}?secret={unformattedKey}&issuer=RetailERP&digits=6";

        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var png = qrCode.GetGraphic(5);
        QrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        int pos = 0;
        while (pos < key.Length)
        {
            sb.Append(key.AsSpan(pos, Math.Min(4, key.Length - pos)));
            if (pos + 4 < key.Length) sb.Append(' ');
            pos += 4;
        }
        return sb.ToString().ToLowerInvariant();
    }
}
