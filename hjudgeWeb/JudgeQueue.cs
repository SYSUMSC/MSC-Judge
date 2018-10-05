﻿using hjudgeCore;
using hjudgeWeb.Configurations;
using hjudgeWeb.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace hjudgeWeb
{
    public class JudgeQueue
    {
        public static readonly ConcurrentQueue<int> JudgeIdQueue = new ConcurrentQueue<int>();

        private static string AlphaNumberFilter(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            var re = new Regex("[A-Z]|[a-z]|[0-9]");
            return re.Matches(input)?.Cast<object>()?.Aggregate(string.Empty, (current, t) => current + t);
        }

        private static (JudgeOptionBuilder JudgeOptionBuilder, BuildOptionBuilder BuildOptionBuilder) GetOptionBuilders(Problem problem, Judge judge, ProblemConfiguration config)
        {
            var buildOptionBuilder = new BuildOptionBuilder();
            if (problem.Type == 1)
            {
                var judgeOptionBuilder = new CodeJudgeOptionBuilder();
                var ext = Languages.LanguageConfigurations.FirstOrDefault(i => i.Name == judge.Language)?.Extensions?.Split(',', StringSplitOptions.RemoveEmptyEntries)[0]?.Trim();
                buildOptionBuilder.AddExtensionName(ext);
                var datadir = Path.Combine(Environment.CurrentDirectory, "AppData", "Data", problem.Id.ToString());
                var workingdir = Path.Combine(Path.GetTempPath(), "hjudgeTest", judgeOptionBuilder.GuidStr);
                var file = Path.Combine(workingdir, $"{judgeOptionBuilder.GuidStr}{ext}");
                var outputfile = Path.Combine(workingdir, judgeOptionBuilder.GuidStr + ".exe");
                var name = AlphaNumberFilter(problem.Name);

                judgeOptionBuilder.UseComparingOption(option =>
                {
                    option.IgnoreLineTailWhiteSpaces = config.ComparingOptions.IgnoreLineTailWhiteSpaces;
                    option.IgnoreTextTailLineFeeds = config.ComparingOptions.IgnoreTextTailLineFeeds;
                });

                if (config.ExtraFiles.Count != 0)
                {
                    judgeOptionBuilder.UseExtraFiles(config.ExtraFiles.Select(
                        i => i
                            .Replace("${datadir}", datadir)
                            .Replace("${workingdir}", workingdir)
                            .Replace("${file}", file)
                            .Replace("${outputfile}", outputfile)
                            .Replace("${name}", name))
                            .ToList());
                }

                if (!string.IsNullOrEmpty(config.SpecialJudge))
                {
                    judgeOptionBuilder.UseSpecialJudge(option =>
                    {
                        option.Exec = config.SpecialJudge
                            .Replace("${datadir}", datadir)
                            .Replace("${workingdir}", workingdir)
                            .Replace("${file}", file)
                            .Replace("${outputfile}", outputfile)
                            .Replace("${name}", name);
                        option.UseOutputFile = true;
                        option.UseStdInputFile = true;
                        option.UseStdOutputFile = true;
                    });
                }

                foreach (var point in config.Points)
                {
                    judgeOptionBuilder.AddDataPoint(new DataPoint
                    {
                        MemoryLimit = point.MemoryLimit,
                        Score = point.Score,
                        TimeLimit = point.TimeLimit,
                        StdInFile = point.StdInFile
                                ?.Replace("${datadir}", datadir)
                                ?.Replace("${workingdir}", workingdir)
                                ?.Replace("${file}", file)
                                ?.Replace("${outputfile}", outputfile)
                                ?.Replace("${name}", name) ?? string.Empty,
                        StdOutFile = point.StdOutFile
                                ?.Replace("${datadir}", datadir)
                                ?.Replace("${workingdir}", workingdir)
                                ?.Replace("${file}", file)
                                ?.Replace("${outputfile}", outputfile)
                                ?.Replace("${name}", name) ?? string.Empty
                    });
                }
                judgeOptionBuilder.SetInputFileName(config.InputFileName?.Replace("${name}", name) ?? $"test_{judgeOptionBuilder.GuidStr}.in");
                judgeOptionBuilder.SetOutputFileName(config.OutputFileName?.Replace("${name}", name) ?? $"test_{judgeOptionBuilder.GuidStr}.out");
                if (config.UseStdIO)
                {
                    judgeOptionBuilder.UseStdIO();
                }

                if (Languages.LanguagesList.Contains(judge.Language))
                {
                    var lang = Languages.LanguageConfigurations.FirstOrDefault(i => i.Name == judge.Language);
                    var args = config.CompileArgs?.Split('\n')?.FirstOrDefault(i => i.StartsWith($"[{judge.Language}]"));
                    if (!string.IsNullOrEmpty(lang.RunExec))
                    {
                        judgeOptionBuilder.SetRunOption(option =>
                        {
                            option.Exec = lang.RunExec
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;
                            option.Args = lang.RunArgs
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;
                        });
                    }
                    if (!string.IsNullOrEmpty(lang.CompilerExec))
                    {
                        buildOptionBuilder.UseCompiler(option =>
                        {
                            option.Args = args
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name)
                                ??
                                lang.CompilerArgs
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;
                            option.Exec = lang.CompilerExec
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;
                            option.OutputFile = outputfile;

                            if (!string.IsNullOrEmpty(lang.CompilerProblemMatcher))
                            {
                                option.ProblemMatcher = new ProblemMatcher
                                {
                                    MatchPatterns = lang.CompilerProblemMatcher,
                                    DisplayFormat = lang.CompilerDisplayFormat
                                };
                            }
                            option.ReadStdOutput = lang.CompilerReadStdOutput;
                            option.ReadStdError = lang.CompilerReadStdError;
                        });
                    }
                    if (!string.IsNullOrEmpty(lang.StaticCheckExec))
                    {
                        buildOptionBuilder.UseStaticCheck(option =>
                        {
                            option.Args = lang.StaticCheckArgs
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;

                            option.Exec = lang.StaticCheckExec
                                    ?.Replace("${datadir}", datadir)
                                    ?.Replace("${workingdir}", workingdir)
                                    ?.Replace("${file}", file)
                                    ?.Replace("${outputfile}", outputfile)
                                    ?.Replace("${name}", name) ?? string.Empty;

                            if (!string.IsNullOrEmpty(lang.StaticCheckProblemMatcher))
                            {
                                option.ProblemMatcher = new ProblemMatcher
                                {
                                    MatchPatterns = lang.StaticCheckProblemMatcher,
                                    DisplayFormat = lang.StaticCheckDisplayFormat
                                };
                            }
                            option.ReadStdOutput = lang.StaticCheckReadStdOutput;
                            option.ReadStdError = lang.StaticCheckReadStdError;
                        });
                    }
                }
                return (judgeOptionBuilder, buildOptionBuilder);
            }
            else
            {
                var judgeOptionBuilder = new AnswerJudgeOptionBuilder();
                var datadir = Path.Combine(Environment.CurrentDirectory, "Data", problem.Id.ToString());
                var workingdir = Path.Combine(Path.GetTempPath(), "hjudgeTest", judgeOptionBuilder.GuidStr);
                var file = Path.Combine(workingdir, $"{judgeOptionBuilder.GuidStr}{Languages.LanguageConfigurations.FirstOrDefault(i => i.Name == judge.Language)?.Extensions[0]}");
                var outputfile = Path.Combine(workingdir, judgeOptionBuilder.GuidStr + ".exe");
                var name = AlphaNumberFilter(problem.Name);

                judgeOptionBuilder.UseComparingOption(option =>
                {
                    option.IgnoreLineTailWhiteSpaces = config.ComparingOptions.IgnoreLineTailWhiteSpaces;
                    option.IgnoreTextTailLineFeeds = config.ComparingOptions.IgnoreTextTailLineFeeds;
                });

                judgeOptionBuilder.UseAnswerPoint(new AnswerPoint
                {
                    AnswerFile = config.Answer.AnswerFile
                            ?.Replace("${datadir}", datadir)
                            ?.Replace("${workingdir}", workingdir)
                            ?.Replace("${file}", file)
                            ?.Replace("${outputfile}", outputfile)
                            ?.Replace("${name}", name) ?? string.Empty,
                    Score = config.Answer.Score
                });

                if (config.ExtraFiles.Count != 0)
                {
                    judgeOptionBuilder.UseExtraFiles(config.ExtraFiles.Select(
                        i => i
                            ?.Replace("${datadir}", datadir)
                            ?.Replace("${workingdir}", workingdir)
                            ?.Replace("${file}", file)
                            ?.Replace("${outputfile}", outputfile)
                            ?.Replace("${name}", name))
                            ?.ToList());
                }

                if (!string.IsNullOrEmpty(config.SpecialJudge))
                {
                    judgeOptionBuilder.UseSpecialJudge(option =>
                    {
                        option.Exec = config.SpecialJudge
                            ?.Replace("${datadir}", datadir)
                            ?.Replace("${workingdir}", workingdir)
                            ?.Replace("${file}", file)
                            ?.Replace("${outputfile}", outputfile)
                            ?.Replace("${name}", name) ?? string.Empty;
                        option.UseOutputFile = true;
                        option.UseStdInputFile = true;
                        option.UseStdOutputFile = true;
                    });
                }
                return (judgeOptionBuilder, buildOptionBuilder);
            }
        }

        public static async Task JudgeThread()
        {
            ApplicationDbContext db = null;
            while (!Environment.HasShutdownStarted)
            {
                while (JudgeIdQueue.TryDequeue(out int id))
                {
                    if (db == null)
                    {
                        db = new ApplicationDbContext(Program.DbContextOptionsBuilder.Options);
                    }

                    var judge = await db.Judge.FindAsync(id);
                    if (judge == null)
                    {
                        continue;
                    }

                    judge.ResultType = (int)ResultCode.Judging;
                    await db.SaveChangesAsync();

                    var problem = await db.Problem.FindAsync(judge.ProblemId);
                    if (problem == null)
                    {
                        continue;
                    }

                    var config = JsonConvert.DeserializeObject<ProblemConfiguration>(problem.Config ?? "{}");
                    if (config == null)
                    {
                        judge.ResultType = (int)ResultCode.Problem_Config_Error;
                        await db.SaveChangesAsync();
                        continue;
                    }


                    try
                    {
                        var (judgeOptionBuilder, buildOptionBuilder) = GetOptionBuilders(problem, judge, config);
                        buildOptionBuilder.AddSource(judge.Content);
                        var judgeMain = new JudgeMain(SystemConfiguration.Environments);
                        var result = await judgeMain.JudgeAsync(buildOptionBuilder.Build(), judgeOptionBuilder.Build());
                        judge.Result = JsonConvert.SerializeObject(result);
                        judge.ResultType = (int)new Func<ResultCode>(() =>
                        {
                            if (result.JudgePoints == null)
                            {
                                return ResultCode.Judging;
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
                        judge.FullScore = result.JudgePoints?.Sum(i => i.Score) ?? 0;
                        if (judge.ResultType == (int)ResultCode.Accepted)
                        {
                            if (judge.ContestId == null)
                            {
                                if (problem.AcceptCount == null) problem.AcceptCount = 0;
                                problem.AcceptCount++;
                            }
                            else
                            {
                                var problemConfig = db.ContestProblemConfig.FirstOrDefault(i => i.ContestId == judge.ContestId && i.ProblemId == problem.Id);
                                if (problemConfig != null)
                                {
                                    if (problemConfig.AcceptCount == null) problemConfig.AcceptCount = 0;
                                    problemConfig.AcceptCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        judge.ResultType = (int)ResultCode.Unknown_Error;
                        Console.WriteLine("Judge Error!");
                        Console.WriteLine("----------------------------");
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.Source);
                        Console.WriteLine(ex.StackTrace);
                        Console.WriteLine(ex.TargetSite);
                        Console.WriteLine("----------------------------");
                    }
                    await db.SaveChangesAsync();
                }
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
                await Task.Delay(1000);
            }
        }

    }
}