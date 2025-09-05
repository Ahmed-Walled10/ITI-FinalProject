using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Insightly.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Insightly.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            public string UserId { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet()
        {
            if (!(TempData["VerifiedForReset"] is bool verified && verified) || TempData["UserId"] == null)
            {
                return RedirectToPage("./ForgotPassword");
            }

            Input = new InputModel
            {
                UserId = TempData["UserId"].ToString()
            };

            // Preserve just once for POST
            TempData.Keep("VerifiedForReset");
            TempData.Keep("UserId");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData.Keep("VerifiedForReset");
                TempData.Keep("UserId");
                return Page();
            }

            var userId = Input?.UserId ?? TempData["UserId"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("./ForgotPassword");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToPage("./ForgotPassword");
            }

            // Remove existing password if any and set a new one
            var remove = await _userManager.RemovePasswordAsync(user);
            if (!remove.Succeeded)
            {
                // If user had no password, proceed to add
            }
            var add = await _userManager.AddPasswordAsync(user, Input.Password);
            if (add.Succeeded)
            {
                TempData.Clear();
                return RedirectToPage("./Login");
            }

            foreach (var error in add.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            TempData.Keep("VerifiedForReset");
            TempData.Keep("UserId");
            return Page();
        }
    }
}