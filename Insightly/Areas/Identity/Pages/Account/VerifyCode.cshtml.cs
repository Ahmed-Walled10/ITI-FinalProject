using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Insightly.Models;
using Insightly.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Insightly.Areas.Identity.Pages.Account
{
    public class VerifyCodeModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<VerifyCodeModel> _logger;

        public VerifyCodeModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IVerificationCodeService verificationCodeService,
            IEmailSender emailSender,
            ILogger<VerifyCodeModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _verificationCodeService = verificationCodeService;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string UserEmail { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Please enter the verification code")]
            [StringLength(5, MinimumLength = 5, ErrorMessage = "Verification code must be 5 digits")]
            [RegularExpression(@"^\d{5}$", ErrorMessage = "Verification code must be 5 digits")]
            [Display(Name = "Verification Code")]
            public string VerificationCode { get; set; }
            public string UserId { get; set; }
            public string UserEmail { get; set; }
            public string ReturnUrl { get; set; }
            public string Purpose { get; set; } = "EmailConfirmation"; // EmailConfirmation | PasswordReset
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Get user information from TempData
            if (TempData["UserId"] == null || TempData["UserEmail"] == null)
            {
                return RedirectToPage("./Register");
            }

            Input = new InputModel
            {
                UserId = TempData["UserId"].ToString(),
                UserEmail = TempData["UserEmail"].ToString(),
                ReturnUrl = TempData["ReturnUrl"]?.ToString() ?? "~/",
                Purpose = TempData["Purpose"]?.ToString() ?? "EmailConfirmation"
            };

            UserEmail = Input.UserEmail;

            // Keep the data for potential resubmission
            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("ReturnUrl");
            TempData.Keep("Purpose");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                UserEmail = Input?.UserEmail ?? TempData["UserEmail"]?.ToString();
                TempData.Keep("UserId");
                TempData.Keep("UserEmail");
                TempData.Keep("ReturnUrl");
                TempData.Keep("Purpose");
                return Page();
            }

            var userId = Input?.UserId ?? TempData["UserId"]?.ToString();
            var userEmail = Input?.UserEmail ?? TempData["UserEmail"]?.ToString();
            var returnUrl = Input?.ReturnUrl ?? TempData["ReturnUrl"]?.ToString() ?? "~/";
            var purpose = Input?.Purpose ?? TempData["Purpose"]?.ToString() ?? "EmailConfirmation";

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("./Register");
            }

            // Validate the verification code for the given purpose
            var isValid = await _verificationCodeService.ValidateCodeAsync(userId, Input.VerificationCode, purpose);

            if (isValid)
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user != null)
                {
                    if (string.Equals(purpose, "PasswordReset", StringComparison.OrdinalIgnoreCase))
                    {
                        // Mark as verified for reset and redirect to reset page
                        TempData["VerifiedForReset"] = true;
                        TempData["UserId"] = userId;
                        TempData.Keep("VerifiedForReset");
                        TempData.Keep("UserId");
                        return RedirectToPage("./ResetPassword");
                    }

                    // Default: Email confirmation
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);

                    _logger.LogInformation("User verified their email successfully.");

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    TempData.Clear();

                    TempData["SuccessMessage"] = "Your email has been verified successfully!";

                    return LocalRedirect(returnUrl);
                }
            }

            // Invalid code
            ModelState.AddModelError(string.Empty, "Invalid verification code. Please try again.");
            UserEmail = userEmail;

            // Keep the data for retry
            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("ReturnUrl");
            TempData.Keep("Purpose");

            return Page();
        }

        public async Task<IActionResult> OnPostResendCodeAsync()
        {
            var userId = Input?.UserId ?? TempData["UserId"]?.ToString();
            var userEmail = Input?.UserEmail ?? TempData["UserEmail"]?.ToString();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
            {
                return RedirectToPage("./Register");
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user != null)
            {
                var purpose = Input?.Purpose ?? TempData["Purpose"]?.ToString() ?? "EmailConfirmation";
                // Reuse existing code if not expired; don't generate a new one
                var code = await _verificationCodeService.GetOrCreateCodeAsync(userId, purpose);

                var emailSubject = "New Verification Code";
                var emailBody = $@
                    "<html>\n                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>\n                        <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>\n                            <h2 style='color: #333; text-align: center;'>New Verification Code</h2>\n                            <p style='color: #666; font-size: 16px;'>Hello {user.Name},</p>\n                            <p style='color: #666; font-size: 16px;'>Here is your verification code:</p>\n                            <div style='background-color: #f8f9fb; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0;'>\n                                <h1 style='color: #007bff; letter-spacing: 8px; font-size: 36px; margin: 0;'>{code}</h1>\n                            </div>\n                            <p style='color: #999; font-size: 14px; text-align: center;'>This code will expire in 15 minutes</p>\n                            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>\n                            <p style='color: #999; font-size: 12px; text-align: center;'>If you didn't request this verification code, please ignore this email.</p>\n                        </div>\n                    </body>\n                    </html>";

                await _emailSender.SendEmailAsync(userEmail, emailSubject, emailBody);

                TempData["InfoMessage"] = "A verification code has been sent to your email.";
            }

            // Ensure Input carries identity values for subsequent posts
            Input = Input ?? new InputModel();
            Input.UserId = userId;
            Input.UserEmail = userEmail;
            Input.ReturnUrl = TempData["ReturnUrl"]?.ToString() ?? "~/";
            Input.Purpose = TempData["Purpose"]?.ToString() ?? "EmailConfirmation";

            UserEmail = userEmail;

            // Keep the data
            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("ReturnUrl");
            TempData.Keep("Purpose");

            return Page();
        }

        public async Task<IActionResult> OnGetResendCodeAsync(string userId, string userEmail, string returnUrl, string purpose)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
            {
                return RedirectToPage("./Register");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var code = await _verificationCodeService.GetOrCreateCodeAsync(userId, string.IsNullOrEmpty(purpose) ? "EmailConfirmation" : purpose);

                var emailSubject = "New Verification Code";
                var emailBody = $@
                    "<html>\n                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>\n                        <div style='max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>\n                            <h2 style='color: #333; text-align: center;'>New Verification Code</h2>\n                            <p style='color: #666; font-size: 16px;'>Hello {user.Name},</p>\n                            <p style='color: #666; font-size: 16px;'>Here is your verification code:</p>\n                            <div style='background-color: #f8f9fb; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0;'>\n                                <h1 style='color: #007bff; letter-spacing: 8px; font-size: 36px; margin: 0;'>{code}</h1>\n                            </div>\n                            <p style='color: #999; font-size: 14px; text-align: center;'>This code will expire in 15 minutes</p>\n                            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>\n                            <p style='color: #999; font-size: 12px; text-align: center;'>If you didn't request this verification code, please ignore this email.</p>\n                        </div>\n                    </body>\n                    </html>";

                await _emailSender.SendEmailAsync(userEmail, emailSubject, emailBody);
                TempData["InfoMessage"] = "A verification code has been sent to your email.";
            }

            // Preserve values for page render
            TempData["UserId"] = userId;
            TempData["UserEmail"] = userEmail;
            TempData["ReturnUrl"] = string.IsNullOrEmpty(returnUrl) ? "~/" : returnUrl;
            TempData["Purpose"] = string.IsNullOrEmpty(purpose) ? "EmailConfirmation" : purpose;

            Input = new InputModel
            {
                UserId = userId,
                UserEmail = userEmail,
                ReturnUrl = string.IsNullOrEmpty(returnUrl) ? "~/" : returnUrl,
                Purpose = string.IsNullOrEmpty(purpose) ? "EmailConfirmation" : purpose
            };
            UserEmail = userEmail;

            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("ReturnUrl");
            TempData.Keep("Purpose");
            return Page();
        }
    }
}