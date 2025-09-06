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
        private readonly ILogger<VerifyCodeModel> _logger;

        public VerifyCodeModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IVerificationCodeService verificationCodeService,
            ILogger<VerifyCodeModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _verificationCodeService = verificationCodeService;
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
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Get user information from TempData
            if (TempData["UserId"] == null || TempData["UserEmail"] == null)
            {
                return RedirectToPage("./Register");
            }

            UserEmail = TempData["UserEmail"].ToString();

            // Keep the data for potential resubmission
            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("ReturnUrl");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                UserEmail = TempData["UserEmail"]?.ToString();
                TempData.Keep("UserId");
                TempData.Keep("UserEmail");
                TempData.Keep("ReturnUrl");
                return Page();
            }

            var userId = TempData["UserId"]?.ToString();
            var userEmail = TempData["UserEmail"]?.ToString();
            var returnUrl = TempData["ReturnUrl"]?.ToString() ?? "~/";

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("./Register");
            }

            // Validate the verification code
            var isValid = await _verificationCodeService.ValidateCodeAsync(userId, Input.VerificationCode);

            if (isValid)
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user != null)
                {
                    // Mark email as confirmed
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);

                    _logger.LogInformation("User verified their email successfully.");

                    // Sign in the user
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // Clear TempData
                    TempData.Clear();

                    // Show success message
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

            return Page();
        }

        
    }
}