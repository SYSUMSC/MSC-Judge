﻿using EFSecondLevelCache.Core;
using hjudge.Core;
using hjudge.Shared.Judge;
using hjudge.Shared.Utils;
using hjudge.WebHost.Data;
using hjudge.WebHost.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using static hjudge.Shared.Judge.JudgeInfo;

namespace hjudge.WebHost.Services
{
    public interface IJudgeService
    {
        Task<IQueryable<Judge>> QueryJudgesAsync(int? groupId, int? contestId, int? problemId);
        Task<IQueryable<Judge>> QueryJudgesAsync(string userId, int? groupId, int? contestId, int? problemId);
        Task<Judge?> GetJudgeAsync(int judgeId);
        Task QueueJudgeAsync(Judge judge);
        Task UpdateJudgeResultAsync(int judgeId, JudgeReportInfo.ReportType reportType, JudgeResult? judge);
    }
    public class JudgeService : IJudgeService
    {
        private readonly WebHostDbContext dbContext;
        private readonly IProblemService problemService;
        private readonly ILanguageService languageService;
        private readonly IMessageQueueService messageQueueService;

        public JudgeService(WebHostDbContext dbContext,
            IProblemService problemService,
            ILanguageService languageService,
            IMessageQueueService messageQueueService)
        {
            this.dbContext = dbContext;
            this.problemService = problemService;
            this.languageService = languageService;
            this.messageQueueService = messageQueueService;
        }

        public async Task<Judge?> GetJudgeAsync(int judgeId)
        {
            var result = await dbContext.Judge.Cacheable().FirstOrDefaultAsync(i => i.Id == judgeId);
            if (result != null)
            {
                dbContext.Entry(result).State = EntityState.Detached;
            }
            return result;
        }

        public Task<IQueryable<Judge>> QueryJudgesAsync(int? groupId, int? contestId, int? problemId)
        {
            return Task.FromResult(problemId switch
            {
                null => dbContext.Judge.Where(i => i.GroupId == null && i.ContestId == null),
                _ => dbContext.Judge.Where(i => i.GroupId == null && i.ContestId == null && i.ProblemId == problemId)
            });
        }

        public Task<IQueryable<Judge>> QueryJudgesAsync(string userId, int? groupId, int? contestId, int? problemId)
        {
            return Task.FromResult(problemId switch
            {
                null => dbContext.Judge.Where(i => i.UserId == userId && i.GroupId == groupId && i.ContestId == contestId),
                _ => dbContext.Judge.Where(i => i.UserId == userId && i.GroupId == groupId && i.ContestId == contestId && i.ProblemId == problemId)
            });
        }

        public async Task QueueJudgeAsync(Judge judge)
        {
            judge.ResultType = (int)ResultCode.Pending;
            judge.JudgeTime = DateTime.Now;
            await dbContext.Judge.AddAsync(judge);
            await dbContext.SaveChangesAsync();

            var (judgeOptionsBuilder, buildOptionsBuilder) = await JudgeHelper.GetOptionBuilders(problemService, judge, await languageService.GetLanguageConfigAsync());
            var (judgeOptions, buildOptions) = (judgeOptionsBuilder.Build(), buildOptionsBuilder.Build());

            var (channel, options) = messageQueueService.GetInstance("JudgeQueue");
            var props = channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2;
            channel.BasicPublish(
                options.Exchange,
                options.RoutingKey,
                false,
                props,
                new JudgeInfo
                {
                    JudgeId = judge.Id,
                    Priority = JudgePriority.Normal,
                    JudgeOptions = judgeOptions,
                    BuildOptions = buildOptions
                }.SerializeJson(false));
        }

        public async Task UpdateJudgeResultAsync(int judgeId, JudgeReportInfo.ReportType reportType, JudgeResult? result)
        {
            var judge = await GetJudgeAsync(judgeId);
            if (judge == null) return;

            if (reportType == JudgeReportInfo.ReportType.PostJudge)
            {
                judge.Result = result?.SerializeJsonAsString(false) ?? "{}";
                judge.ResultType = (int)new Func<ResultCode>(() =>
                {
                    if (result?.JudgePoints == null)
                    {
                        return ResultCode.Unknown_Error;
                    }

                    if (result.JudgePoints.Count == 0 || result.JudgePoints.All(i => i.ResultType == ResultCode.Accepted))
                    {
                        return ResultCode.Accepted;
                    }

                    var mostPresentTimes =
                        result.JudgePoints.Select(i => i.ResultType).Distinct().Max(i =>
                            result.JudgePoints.Count(j => j.ResultType == i && j.ResultType != ResultCode.Accepted));
                    var mostPresent =
                        result.JudgePoints.Select(i => i.ResultType).Distinct().FirstOrDefault(
                            i => result.JudgePoints.Count(j => j.ResultType == i && j.ResultType != ResultCode.Accepted) ==
                                 mostPresentTimes
                        );
                    return mostPresent;
                }).Invoke();
                judge.FullScore = result?.JudgePoints?.Sum(i => i.Score) ?? 0;
            }

            if (reportType == JudgeReportInfo.ReportType.PreJudge)
            {
                judge.ResultType = (int)ResultCode.Judging;
            }

            dbContext.Judge.Update(judge);

            await dbContext.SaveChangesAsync();
        }
    }
}