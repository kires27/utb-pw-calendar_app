using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CalendarApp.Domain.Abstraction;
using CalendarApp.Application.Models;
using System.Security.Claims;

namespace CalendarApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly IUserAppService _userAppService;

        public UserManagementController(IUserAppService userAppService)
        {
            _userAppService = userAppService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userAppService.GetAllUsersAsync();
            var viewModel = new UserListViewModel { Users = users };
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new UserCreateViewModel();
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userAppService.CreateUserAsync(
                        viewModel.Username, 
                        viewModel.Email, 
                        viewModel.Password, 
                        viewModel.Role);

                    if (result)
                    {
                        TempData["Success"] = "User created successfully!";
                        return RedirectToAction(nameof(Index));
                    }

                    ModelState.AddModelError("", "Failed to create user. Please check the input and try again.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error creating user: " + ex.Message);
                }
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userAppService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new UserEditViewModel
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Role = user.Role
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel viewModel)
        {
            // Prevent the admin from locking themselves out by demoting their own account.
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentUserIdClaim, out var currentUserId)
                && currentUserId == viewModel.Id
                && viewModel.Role != "Admin")
            {
                ModelState.AddModelError("Role", "You cannot change your own role.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _userAppService.UpdateUserAsync(
                        viewModel.Id, viewModel.Username, viewModel.Email, viewModel.Role);

                    if (!result.Success)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error);
                        }
                        return View(viewModel);
                    }

                    if (!string.IsNullOrEmpty(viewModel.NewPassword))
                    {
                        var passwordResult = await _userAppService.SetPasswordAsync(viewModel.Id, viewModel.NewPassword);
                        if (!passwordResult.Success)
                        {
                            foreach (var error in passwordResult.Errors)
                            {
                                ModelState.AddModelError("NewPassword", error);
                            }
                            return View(viewModel);
                        }
                    }

                    TempData["Success"] = "User updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Error updating user: " + ex.Message);
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id)
                {
                    TempData["Error"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _userAppService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["Success"] = "User deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete user.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting user: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}