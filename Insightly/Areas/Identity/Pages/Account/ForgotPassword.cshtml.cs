using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Insightly.Models;
using Insightly.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Insightly.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IVerificationCodeService _verificationCodeService;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IVerificationCodeService verificationCodeService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _verificationCodeService = verificationCodeService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Do not reveal that the user does not exist or is not confirmed
                return RedirectToPage("./Login");
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _verificationCodeService.GenerateCodeAsync(userId, "PasswordReset");

            var emailSubject = "Password Reset Verification Code";
            var emailBody = $@
                "<html>\n                <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>\n                    <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>\n                        <h2 style='color: #333; text-align: center;'>Password Reset</h2>\n                        <p style='color: #666; font-size: 16px;'>Use this code to reset your password:</p>\n                        <div style='background-color: #f8f9fb; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0;'>\n                            <h1 style='color: #007bff; letter-spacing: 8px; font-size: 36px; margin: 0;'>{code}</h1>\n                        </div>\n                        <p style='color: #999; font-size: 14px; text-align: center;'>This code will expire in 15 minutes</p>\n                    </div>\n                </body>\n                </html>";

            await _emailSender.SendEmailAsync(Input.Email, emailSubject, emailBody);

            TempData["UserId"] = userId;
            TempData["UserEmail"] = Input.Email;
            TempData["ReturnUrl"] = Url.Content("~/");
            TempData["Purpose"] = "PasswordReset";

            return RedirectToPage("./VerifyCode");
        }
    }
}