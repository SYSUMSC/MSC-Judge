﻿using hjudgeWeb.Configurations;
using hjudgeWeb.Data;
using hjudgeWeb.Data.Identity;
using hjudgeWeb.Hubs;
using hjudgeWeb.Models;
using hjudgeWeb.Models.Message;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace hjudgeWeb.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class MessageController : Controller
    {
        private readonly SignInManager<UserInfo> _signInManager;
        private readonly UserManager<UserInfo> _userManager;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly IHubContext<ChatHub> _chatHub;

        public MessageController(SignInManager<UserInfo> signInManager,
            UserManager<UserInfo> userManager,
            DbContextOptions<ApplicationDbContext> dbContextOptions,
            IHubContext<ChatHub> chatHub)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbContextOptions = dbContextOptions;
            _chatHub = chatHub;
        }

        /// <summary>
        /// Get current signed in user and its privilege
        /// </summary>
        /// <returns></returns>
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
            return privilege >= 1 && privilege <= 3;
        }

        /// <summary>
        /// Get messages quantity
        /// </summary>
        /// <param name="type">1 -- unread, 2 -- read, other -- all</param>
        /// <returns>quantity</returns>
        [HttpGet]
        public async Task<int> GetMessageCount(int type = 1)
        {
            var (user, privilege) = await GetUserPrivilegeAsync();
            if (user == null)
            {
                return 0;
            }
            using (var db = new ApplicationDbContext(_dbContextOptions))
            {
                var msgList = db.Message
                    .Where(i => i.ToUserId == user.Id);

                switch (type)
                {
                    case 1:
                        msgList = msgList.Where(i => i.Status == 1);
                        break;
                    case 2:
                        msgList = msgList.Where(i => i.Status == 2);
                        break;
                }

                return await msgList.CountAsync();
            }
        }

        [HttpGet]
        public async Task<MessageListModel> GetMessages(int start = 0, int count = 10)
        {
            var ret = new MessageListModel { IsSucceeded = true };
            var (user, privilege) = await GetUserPrivilegeAsync();
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "没有登录";
                return ret;
            }
            using (var db = new ApplicationDbContext(_dbContextOptions))
            {
                var msgList = db.Message
                    .Where(i => i.FromUserId == user.Id || i.ToUserId == user.Id)
                    .OrderByDescending(i => i.Id)
                    .Skip(start).Take(count);

                foreach (var i in msgList)
                {
                    var userId = i.FromUserId == user.Id ? i.ToUserId : i.FromUserId;
                    ret.Messages.Add(new MessageItemModel
                    {
                        Id = i.Id,
                        RawSendTime = i.SendTime,
                        Status = i.Status,
                        ContentId = i.ContentId,
                        Title = i.Title,
                        UserId = userId,
                        Type = i.Type,
                        Direction = i.ToUserId == user.Id ? 2 : 1,
                        UserName = db.Users.Select(j => new { j.Id, j.UserName }).FirstOrDefault(j => j.Id == userId)?.UserName
                    });
                }
            }
            return ret;
        }

        public class MessageStatusModel
        {
            public int Status { get; set; }
            public int MessageId { get; set; }
        }

        [HttpPost]
        public async Task<ResultModel> SetMessageStatus([FromBody]MessageStatusModel model)
        {
            var ret = new ResultModel { IsSucceeded = true };
            var (user, privilege) = await GetUserPrivilegeAsync();
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "没有登录";
                return ret;
            }
            using (var db = new ApplicationDbContext(_dbContextOptions))
            {
                var msgs = db.Message.Where(i => i.ToUserId == user.Id && i.Status != model.Status);
                if (model.MessageId != 0)
                {
                    msgs = msgs.Where(i => i.Id == model.MessageId);
                }
                foreach (var i in msgs)
                {
                    i.Status = model.Status;
                }
                await db.SaveChangesAsync();
            }
            return ret;
        }

        [HttpGet]
        public async Task<MessageContentModel> GetMessageContent(int msgId)
        {
            var ret = new MessageContentModel { IsSucceeded = true };
            var (user, privilege) = await GetUserPrivilegeAsync();
            if (user == null)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "没有登录";
                return ret;
            }
            using (var db = new ApplicationDbContext(_dbContextOptions))
            {
                var msg = db.Message.Include(i => i.MessageContent).FirstOrDefault(i => i.Id == msgId);
                if (msg == null)
                {
                    ret.IsSucceeded = false;
                    ret.ErrorMessage = "消息不存在";
                    return ret;
                }
                if (msg.FromUserId != user.Id && msg.ToUserId != user.Id)
                {
                    ret.IsSucceeded = false;
                    ret.ErrorMessage = "没有权限";
                    return ret;
                }
                var originalStatus = msg.Status;
                if (msg.ToUserId == user.Id)
                {
                    msg.Status = 2;
                    await db.SaveChangesAsync();
                }
                var userId = msg.FromUserId == user.Id ? msg.ToUserId : msg.FromUserId;

                ret.Id = msg.Id;
                ret.Content = msg.MessageContent.Content;
                ret.Status = originalStatus;
                ret.RawSendTime = msg.SendTime;
                ret.Direction = msg.ToUserId == user.Id ? 2 : 1;
                ret.Type = msg.Type;
                ret.Title = msg.Title;
                ret.UserId = userId;
                ret.UserName = db.Users.Select(i => new { i.Id, i.UserName }).FirstOrDefault(i => i.Id == userId)?.UserName;

            }
            return ret;
        }

        [HttpGet]
        public async Task<ChatMessageListModel> GetChats(int startId = int.MaxValue, int count = 10, int problemId = 0, int contestId = 0, int groupId = 0)
        {
            var ret = new ChatMessageListModel { IsSucceeded = true };
            if (!SystemConfiguration.Config.CanDiscussion)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "管理员未开启讨论功能";
                return ret;
            }
            using (var db = new ApplicationDbContext(_dbContextOptions))
            {
                var pid = problemId == 0 ? null : (int?)problemId;
                var cid = contestId == 0 ? null : (int?)contestId;
                var gid = groupId == 0 ? null : (int?)groupId;

                if (cid != null)
                {
                    var contest = await db.Contest.Select(i => new { i.Id, i.Config }).FirstOrDefaultAsync(i => i.Id == cid);
                    if (contest != null)
                    {
                        var config = JsonConvert.DeserializeObject<ContestConfiguration>(contest.Config ?? "{}");
                        if (!config.CanDiscussion)
                        {
                            ret.IsSucceeded = false;
                            ret.ErrorMessage = "该比赛未开启讨论功能";
                            return ret;
                        }
                    }
                }

                ret.ChatMessages = await db.Discussion.Include(i => i.UserInfo)
                    .OrderByDescending(i => i.Id)
                    .Where(i => i.Id < startId
                        && i.ProblemId == pid
                        && i.ContestId == cid
                        && i.GroupId == gid)
                    .Select(i => new ChatMessageModel
                    {
                        Id = i.Id,
                        UserId = i.UserId,
                        Content = i.Content,
                        RawSendTime = i.SubmitTime,
                        UserName = i.UserInfo.UserName,
                        ReplyId = i.ReplyId
                    }).Take(count)
                    .OrderBy(i => i.RawSendTime).ToListAsync();
            }
            return ret;
        }

        public class SendChatModel
        {
            public string Content { get; set; }
            public int ReplyId { get; set; }
            public int ProblemId { get; set; }
            public int ContestId { get; set; }
            public int GroupId { get; set; }
        }

        [HttpPost]
        public async Task<ResultModel> SendChat([FromBody]SendChatModel model)
        {
            var ret = new ResultModel { IsSucceeded = true };
            if (!SystemConfiguration.Config.CanDiscussion)
            {
                ret.IsSucceeded = false;
                ret.ErrorMessage = "管理员未开启讨论功能";
                return ret;
            }
            var (user, privilege) = await GetUserPrivilegeAsync();
            if (user != null)
            {
                if (!user.EmailConfirmed)
                {
                    ret.ErrorMessage = "没有验证邮箱";
                    ret.IsSucceeded = false;
                    return ret;
                }
                if (user.Coins < 10 && !HasAdminPrivilege(privilege))
                {
                    ret.ErrorMessage = "金币余额不足";
                    ret.IsSucceeded = false;
                    return ret;
                }
                if (string.IsNullOrWhiteSpace(model.Content))
                {
                    ret.ErrorMessage = "请输入消息内容";
                    ret.IsSucceeded = false;
                    return ret;
                }
                if (model.Content.Length > 65536)
                {
                    ret.ErrorMessage = "消息内容过长";
                    ret.IsSucceeded = false;
                    return ret;
                }
                var sendTime = DateTime.Now;
                var content = HttpUtility.HtmlEncode(model.Content);

                var pid = model.ProblemId == 0 ? null : (int?)model.ProblemId;
                var cid = model.ContestId == 0 ? null : (int?)model.ContestId;
                var gid = model.GroupId == 0 ? null : (int?)model.GroupId;

                using (var db = new ApplicationDbContext(_dbContextOptions))
                {
                    if (cid != null)
                    {
                        var contest = await db.Contest.Select(i => new { i.Id, i.Config }).FirstOrDefaultAsync(i => i.Id == cid);
                        if (contest != null)
                        {
                            var config = JsonConvert.DeserializeObject<ContestConfiguration>(contest.Config ?? "{}");
                            if (!config.CanDiscussion)
                            {
                                ret.ErrorMessage = "该比赛未开启讨论功能";
                                ret.IsSucceeded = false;
                                return ret;
                            }
                        }
                    }

                    var lastSubmit = await db.Discussion.OrderByDescending(i => i.SubmitTime).FirstOrDefaultAsync(i => i.UserId == user.Id);
                    if (lastSubmit != null && (DateTime.Now - lastSubmit.SubmitTime) < TimeSpan.FromSeconds(10))
                    {
                        ret.ErrorMessage = "消息发送过于频繁，请等待 10 秒后再试";
                        ret.IsSucceeded = false;
                        return ret;
                    }

                    var dis = new Discussion
                    {
                        UserId = user.Id,
                        Content = content,
                        SubmitTime = sendTime,
                        ReplyId = model.ReplyId,
                        ProblemId = pid,
                        ContestId = cid,
                        GroupId = gid
                    };
                    db.Discussion.Add(dis);

                    await db.SaveChangesAsync();

                    if (model.ReplyId != 0)
                    {
                        var previousDis = await db.Discussion
                                .Include(i => i.UserInfo)
                                .Include(i => i.Problem)
                                .Include(i => i.Contest)
                                .Include(i => i.Group)
                                .Select(i => new
                                {
                                    i.Id,
                                    i.UserId,
                                    i.ProblemId,
                                    ProblemName = i.Problem.Name,
                                    i.ContestId,
                                    ContestName = i.Contest.Name,
                                    i.GroupId,
                                    GroupName = i.Group.Name,
                                    i.Content,
                                    i.SubmitTime
                                }).FirstOrDefaultAsync(i => i.Id == model.ReplyId);

                        if (previousDis != null)
                        {
                            var link = string.Empty;
                            var position = string.Empty;
                            if (cid == null)
                            {
                                if (pid == null)
                                {
                                    link = "/";
                                    position = "主页";
                                }
                                else
                                {
                                    link = $"/ProblemDetails/{pid}";
                                    position = $"题目 {pid} - {previousDis.ProblemName}";
                                }
                            }
                            else
                            {
                                if (pid == null)
                                {
                                    link = $"/ContestDetails/{cid}";
                                    position = $"比赛 {cid} - {previousDis.ContestName}";
                                }
                                else
                                {
                                    link = $"/ProblemDetails/{cid}/{pid}";
                                    position = $"比赛 {cid} - {previousDis.ContestName}，题目 {pid} - {previousDis.ProblemName}";
                                }
                            }
                            var msgContent = new MessageContent
                            {
                                Content = $"<h3><a href=\"/Account/{user.Id}\">{user.UserName}</a> 在 #{dis.Id} 回复了您的帖子 #{previousDis.Id}：</h3><br />" +
                                "<div style=\"width: 90 %; overflow: auto; max-height: 300px; \">" +
                                $"<pre style=\"white-space: pre-wrap; word-wrap: break-word;\">{new string(content.Take(128).ToArray()) + (content.Length > 128 ? "..." : string.Empty)}</pre></div>" +
                                "<br /><hr /><br /><h3>原帖内容：</h3><br />" +
                                "<div style=\"width: 90 %; overflow: auto; max-height: 300px; \">" +
                                $"<pre style=\"white-space: pre-wrap; word-wrap: break-word;\">{new string(previousDis.Content.Take(128).ToArray()) + (previousDis.Content.Length > 128 ? "..." : string.Empty)}</pre></div>" +
                                $"<br /><hr /><br /><h4>位置：{position}，<a href=\"{link}\">点此前往查看</a></h4>"
                            };
                            db.MessageContent.Add(msgContent);
                            await db.SaveChangesAsync();
                            db.Message.Add(new Message
                            {
                                FromUserId = null,
                                ToUserId = previousDis.UserId,
                                ContentId = msgContent.Id,
                                ReplyId = 0,
                                SendTime = DateTime.Now,
                                Status = 1,
                                Title = $"您的帖子 #{previousDis.Id} 有新的回复",
                                Type = 1
                            });
                            await db.SaveChangesAsync();
                        }
                    }

                    await _chatHub.Clients.Group($"pid-{model.ProblemId};cid-{model.ContestId};gid-{model.GroupId};")
                        .SendAsync("ChatMessage", dis.Id, user.Id, user.UserName, $"{sendTime.ToShortDateString()} {sendTime.ToLongTimeString()}", content, model.ReplyId);
                }
                user.Experience += 5;
                if (!HasAdminPrivilege(privilege))
                {
                    user.Coins -= 10;
                }
                await _userManager.UpdateAsync(user);
            }
            else
            {
                ret.ErrorMessage = "没有登录";
                ret.IsSucceeded = false;
            }
            return ret;
        }
    }
}