﻿using hjudgeWeb.Data;
using hjudgeWeb.Data.Identity;
using hjudgeWeb.Models;
using hjudgeWeb.Models.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace hjudgeWeb.Controllers
{
    [Consumes("application/json", "multipart/form-data")]
    public class AccountController : Controller
    {
        private readonly SignInManager<UserInfo> _signInManager;
        private readonly UserManager<UserInfo> _userManager;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;

        public AccountController(SignInManager<UserInfo> signInManager,
            UserManager<UserInfo> userManager,
            DbContextOptions<ApplicationDbContext> dbContextOptions)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbContextOptions = dbContextOptions;
        }

        [HttpGet]
        public async Task<string> GetUserAvatar(string userId = null)
        {
            var user = await (string.IsNullOrEmpty(userId) ? _userManager.GetUserAsync(User) : _userManager.FindByIdAsync(userId));
            if (user == null)
            {
                return null;
            }
            if (user.Avatar == null || user.Avatar.Length == 0)
            {
                return Properties.Resource.DefaultAvatar;
            }
            return Convert.ToBase64String(user.Avatar, Base64FormattingOptions.None);
        }

        [HttpPost]
        public async Task<ResultModel> UpdateAvatar(IFormFile file)
        {
            var result = new ResultModel { IsSucceeded = true };
            if (!_signInManager.IsSignedIn(User))
            {
                result.IsSucceeded = false;
                result.ErrorMessage = "未登录";
                return result;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                result.IsSucceeded = false;
                result.ErrorMessage = "找不到当前用户";
                return result;
            }

            if (file == null)
            {
                result.IsSucceeded = false;
                result.ErrorMessage = "文件无效";
                return result;
            }

            if (!file.ContentType.StartsWith("image/"))
            {
                result.IsSucceeded = false;
                result.ErrorMessage = "只能上传图片文件";
                return result;
            }

            if (file.Length > 1048576)
            {
                result.IsSucceeded = false;
                result.ErrorMessage = "图片文件大小不能超过 1 Mb";
                return result;
            }

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer);
                user.Avatar = buffer;
                await _userManager.UpdateAsync(user);
            }
            return result;
        }

        [HttpGet]
        public async Task<UserInfoModel> GetUserInfo(string userId = null)
        {
            var userInfo = new UserInfoModel { IsSignedIn = true };
            if (userId == null)
            {
                if (!_signInManager.IsSignedIn(User))
                {
                    userInfo.IsSignedIn = false;
                    return userInfo;
                }
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    userInfo.IsSignedIn = false;
                    return userInfo;
                }

                userInfo.Id = user.Id;
                userInfo.Email = user.Email;
                userInfo.EmailConfirmed = user.EmailConfirmed;
                userInfo.Experience = user.Experience;
                userInfo.Coins = user.Coins;
                userInfo.PhoneNumber = user.PhoneNumber;
                userInfo.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
                userInfo.UserName = user.UserName;
                userInfo.OtherInfo = IdentityHelper.GetOtherUserInfo(user.OtherInfo);
                userInfo.Name = user.Name;
                userInfo.Privilege = user.Privilege;
            }
            else
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    userInfo.IsSignedIn = false;
                    return userInfo;
                }

                userInfo.Id = user.Id;
                userInfo.Experience = user.Experience;
                userInfo.Coins = user.Coins;
                userInfo.UserName = user.UserName;
                userInfo.OtherInfo = IdentityHelper.GetOtherUserInfo(user.OtherInfo);
                userInfo.Privilege = user.Privilege;
            }
            return userInfo;
        }

        public class LoginModel
        {
            public string UserName { get; set; }
            public string Password { get; set; }
            public bool RememberMe { get; set; }
        }

        [HttpPost]
        public async Task<ResultModel> Login([FromBody]LoginModel loginInfo)
        {
            await _signInManager.SignOutAsync();
            var result = await _signInManager.PasswordSignInAsync(loginInfo.UserName, loginInfo.Password, loginInfo.RememberMe, false);
            var ret = new ResultModel { IsSucceeded = true };
            if (!result.Succeeded)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "用户名或密码不正确";
            }
            return ret;
        }

        public class RegisterModel
        {
            public string UserName { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string ConfirmPassword { get; set; }
        }

        [HttpPost]
        public async Task<ResultModel> Register([FromBody]RegisterModel registerInfo)
        {
            var ret = new ResultModel { IsSucceeded = true };
            if (registerInfo.Password != registerInfo.ConfirmPassword)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "两次输入的密码不一致";
                return ret;
            }
            await _signInManager.SignOutAsync();

            var user = new UserInfo
            {
                UserName = registerInfo.UserName,
                Email = registerInfo.Email,
                Privilege = 4
            };
            var result = await _userManager.CreateAsync(user, registerInfo.Password);

            if (!result.Succeeded)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = result.Errors.Any() ? result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next) : "注册失败";
            }
            else
            {
                await _signInManager.SignInAsync(user, false);
            }
            return ret;
        }

        [HttpPost]
        public async Task Logout()
        {
            await _signInManager.SignOutAsync();
        }

        [HttpPost]
        public async Task<ResultModel> UpdateOtherInfo([FromBody]OtherUserInfo otherUserInfo)
        {
            var ret = new ResultModel { IsSucceeded = true };
            if (!_signInManager.IsSignedIn(User))
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "未登录";
                return ret;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "找不到当前用户";
                return ret;
            }

            if (otherUserInfo != null)
            {
                user.OtherInfo = JsonConvert.SerializeObject(otherUserInfo);
                var result = await _userManager.UpdateAsync(user);
                ret.IsSucceeded = result.Succeeded;
                if (!ret.IsSucceeded)
                {
                    ret.ErrorMessage = result.Errors.Any() ? result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next) : "修改失败";
                }
            }
            return ret;
        }

        public class UpdateInfoModel
        {
            public string Value { get; set; }
        }

        [HttpPost]
        public async Task<ResultModel> UpdateName([FromBody]UpdateInfoModel name)
        {
            var ret = new ResultModel();
            if (!_signInManager.IsSignedIn(User))
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "未登录";
                return ret;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "找不到当前用户";
                return ret;
            }
            user.Name = name.Value;
            var result = await _userManager.UpdateAsync(user);
            ret.IsSucceeded = result.Succeeded;
            if (!ret.IsSucceeded)
            {
                ret.ErrorMessage = result.Errors.Any() ? result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next) : "修改失败";
            }
            return ret;
        }

        [HttpPost]
        public async Task<ResultModel> UpdateEmail([FromBody]UpdateInfoModel email)
        {
            var ret = new ResultModel();
            if (!_signInManager.IsSignedIn(User))
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "未登录";
                return ret;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "找不到当前用户";
                return ret;
            }
            var result = await _userManager.SetEmailAsync(user, email.Value);
            ret.IsSucceeded = result.Succeeded;
            if (!ret.IsSucceeded)
            {
                ret.ErrorMessage = result.Errors.Any() ? result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next) : "修改失败";
            }
            return ret;
        }

        [HttpPost]
        public async Task<ResultModel> UpdatePhoneNumber([FromBody]UpdateInfoModel phoneNumber)
        {
            var ret = new ResultModel();
            if (!_signInManager.IsSignedIn(User))
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "未登录";
                return ret;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "找不到当前用户";
                return ret;
            }
            var result = await _userManager.SetPhoneNumberAsync(user, phoneNumber.Value);

            ret.IsSucceeded = result.Succeeded;
            if (!ret.IsSucceeded)
            {
                ret.ErrorMessage = result.Errors.Any() ? result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next) : "修改失败";
            }
            return ret;
        }
    }
}