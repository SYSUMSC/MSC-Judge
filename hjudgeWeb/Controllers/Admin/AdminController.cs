﻿using hjudgeWeb.Data;
using hjudgeWeb.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace hjudgeWeb.Controllers
{
    public partial class AdminController
    {
        private readonly SignInManager<UserInfo> _signInManager;
        private readonly UserManager<UserInfo> _userManager;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;

        public AdminController(SignInManager<UserInfo> signInManager,
            UserManager<UserInfo> userManager,
            DbContextOptions<ApplicationDbContext> dbContextOptions)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbContextOptions = dbContextOptions;
        }

        private async Task<(UserInfo, int)> GetUserPrivilegeAsync()
        {
            if (!_signInManager.IsSignedIn(User))
            {
                return (null, 0);
            }
            var user = await _userManager.GetUserAsync(User);
            return (user, user?.Privilege ?? 0);
        }

        private bool HasAdminPrivilege(int privilege)
        {
            return privilege == 1;
        }
    }
}