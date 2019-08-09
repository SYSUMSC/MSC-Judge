﻿using hjudge.Shared.Utils;
using hjudge.WebHost.Utils;
using hjudge.WebHost.Data;
using hjudge.WebHost.Data.Identity;
using hjudge.WebHost.Middlewares;
using hjudge.WebHost.Models;
using hjudge.WebHost.Models.Account;
using hjudge.WebHost.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using hjudge.Core;

namespace hjudge.WebHost.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Route("user")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly CachedUserManager<UserInfo> userManager;
        private readonly SignInManager<UserInfo> signInManager;
        private readonly IJudgeService judgeService;

        public AccountController(CachedUserManager<UserInfo> userManager, SignInManager<UserInfo> signInManager, IJudgeService judgeService, WebHostDbContext dbContext)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.judgeService = judgeService;
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        [HttpPost]
        [Route("login")]
        public async Task<ResultModel> Login([FromBody]LoginModel model)
        {
            var ret = new ResultModel();
            if (TryValidateModel(model))
            {
                await signInManager.SignOutAsync();
                var result = await signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, false);
                if (!result.Succeeded)
                {
                    ret.ErrorCode = ErrorDescription.AuthenticationFailed;
                }
            }
            else
            {
                ret.ErrorCode = ErrorDescription.ArgumentError;
            }
            return ret;
        }

        [HttpPut]
        [Route("register")]
        public async Task<ResultModel> Register([FromBody]RegisterModel model)
        {
            var ret = new ResultModel();
            if (TryValidateModel(model))
            {
                await signInManager.SignOutAsync();
                var user = new UserInfo
                {
                    UserName = model.UserName,
                    Name = model.Name,
                    Email = model.Email,
                    Privilege = 4
                };
                var result = await userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    ret.ErrorCode = ErrorDescription.ArgumentError;
                    if (result.Errors.Any()) ret.ErrorMessage = result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next);
                }
                else await signInManager.PasswordSignInAsync(model.UserName, model.Password, false, false);
            }
            else
            {
                ret.ErrorCode = ErrorDescription.ArgumentError;
                var errors = ModelState.ToList().SelectMany(i => i.Value.Errors, (i, j) => j.ErrorMessage);
                if (errors.Any()) ret.ErrorMessage = errors.Aggregate((accu, next) => accu + "\n" + next);
            }
            return ret;
        }

        [HttpPost]
        [Route("logout")]
        public async Task<ResultModel> Logout()
        {
            await signInManager.SignOutAsync();
            return new ResultModel();
        }

        [HttpPost]
        [Route("avatar")]
        [PrivilegeAuthentication.RequireSignedIn]
        public async Task<ResultModel> UserAvatar(IFormFile avatar)
        {
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            var result = new ResultModel();

            if (avatar == null)
            {
                result.ErrorCode = ErrorDescription.FileBadFormat;
                return result;
            }

            if (!avatar.ContentType.StartsWith("image/"))
            {
                result.ErrorCode = ErrorDescription.FileBadFormat;
                return result;
            }

            if (avatar.Length > 1048576)
            {
                result.ErrorCode = ErrorDescription.FileSizeExceeded;
                return result;
            }

            using var stream = new System.IO.MemoryStream();

            await avatar.CopyToAsync(stream);
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer);
            user.Avatar = buffer;
            await userManager.UpdateAsync(user);

            return result;
        }

        [HttpGet]
        [Route("avatar")]
        public async Task<IActionResult> UserAvatar(string? userId)
        {
            if (string.IsNullOrEmpty(userId)) userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            return user.Avatar == null || user.Avatar.Length == 0
                ? File(ImageScaler.ScaleImage(Properties.Resource.DefaultAvatar, 128, 128), "image/png")
                : File(ImageScaler.ScaleImage(user.Avatar, 128, 128), "image/png");
        }

        [HttpPost]
        [Route("profiles")]
        [PrivilegeAuthentication.RequireSignedIn]
        public async Task<ResultModel> UserInfo([FromBody]UserInfoModel model)
        {
            var ret = new ResultModel();
            var userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);

            if (!string.IsNullOrEmpty(model.Name)) user.Name = model.Name;

            if (!string.IsNullOrEmpty(model.Email) && user.Email != model.Email)
            {
                var result = await userManager.SetEmailAsync(user, model.Email);
                if (!result.Succeeded)
                {
                    ret.ErrorCode = ErrorDescription.ArgumentError;
                    if (result.Errors.Any()) ret.ErrorMessage = result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next);
                    return ret;
                }
            }
            if (model.PhoneNumber != null && user.PhoneNumber != model.PhoneNumber)
            {
                var result = await userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!result.Succeeded)
                {
                    ret.ErrorCode = ErrorDescription.ArgumentError;
                    if (result.Errors.Any()) ret.ErrorMessage = result.Errors.Select(i => i.Description).Aggregate((accu, next) => accu + "\n" + next);
                    return ret;
                }
            }
            if (model.OtherInfo != null)
            {
                var otherInfoJson = (string.IsNullOrEmpty(user.OtherInfo) ? "{}" : user.OtherInfo).DeserializeJson<IDictionary<string, object?>>();
                foreach (var info in model.OtherInfo)
                {
                    var prop = IdentityHelper.OtherInfoProperties.FirstOrDefault(i => i.Name == info.Key);
                    if (prop != null)
                    {
                        try
                        {
                            otherInfoJson[info.Key] = Convert.ChangeType(info.Value, prop.PropertyType);
                        }
                        catch
                        {
                            ret.ErrorCode = ErrorDescription.ArgumentError;
                            ret.ErrorMessage = "请填写正确的信息";
                            return ret;
                        }
                    }
                }
                user.OtherInfo = otherInfoJson.SerializeJsonAsString();
            }

            await userManager.UpdateAsync(user);

            return ret;
        }

        [HttpGet]
        [SendAntiForgeryToken]
        [Route("profiles")]
        public async Task<UserInfoModel> UserInfo(string? userId = null)
        {
            var userInfoRet = new UserInfoModel
            {
                SignedIn = signInManager.IsSignedIn(User)
            };
            if (string.IsNullOrEmpty(userId)) userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            if (userId == null || user == null)
            {
                if (!string.IsNullOrEmpty(userId)) userInfoRet.ErrorCode = ErrorDescription.UserNotExist;
                return userInfoRet;
            }
            userInfoRet.Name = user.Name;
            userInfoRet.UserId = user.Id;
            userInfoRet.UserName = user.UserName;
            userInfoRet.Privilege = user.Privilege;
            userInfoRet.Coins = user.Coins;
            userInfoRet.Experience = user.Experience;

            if (userInfoRet.SignedIn)
            {
                userInfoRet.EmailConfirmed = user.EmailConfirmed;
                userInfoRet.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
                userInfoRet.Email = user.Email;
                userInfoRet.PhoneNumber = user.PhoneNumber;
                userInfoRet.OtherInfo = IdentityHelper.GetOtherUserInfo(string.IsNullOrEmpty(user.OtherInfo) ? "{}" : user.OtherInfo);
            }

            return userInfoRet;
        }

        [HttpGet]
        [Route("stats")]
        public async Task<ProblemStatisticsModel> GetUserProblemStatistics(string? userId = null)
        {
            var ret = new ProblemStatisticsModel();
            if (string.IsNullOrEmpty(userId)) userId = userManager.GetUserId(User);
            var user = await userManager.FindByIdAsync(userId);
            if (userId == null || user == null)
            {
                if (!string.IsNullOrEmpty(userId)) ret.ErrorCode = ErrorDescription.UserNotExist;
                return ret;
            }

            var judges = await judgeService.QueryAllJudgesAsync(userId);

            ret.SolvedProblems = await judges.Where(i => i.ResultType == (int)ResultCode.Accepted).Select(i => i.ProblemId).Distinct().OrderBy(i => i).ToListAsync();
            ret.TriedProblems = await judges.Where(i => i.ResultType != (int)ResultCode.Accepted).Select(i => i.ProblemId).Distinct().OrderBy(i => i).ToListAsync();

            return ret;
        }
    }
}
