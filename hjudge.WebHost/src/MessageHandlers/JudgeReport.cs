﻿using hjudge.Shared.Judge;
using hjudge.WebHost.Services;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;
using hjudge.Shared.Utils;
using System;
using System.Threading.Tasks;
using hjudge.WebHost.Extensions;
using hjudge.WebHost.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading;

namespace hjudge.WebHost.MessageHandlers
{
    public class JudgeReport
    {
        private static ConcurrentQueue<JudgeReportInfo> queue = new ConcurrentQueue<JudgeReportInfo>();
        public static Task JudgeReport_Received(object sender, BasicDeliverEventArgs args)
        {
            if (!(sender is AsyncEventingBasicConsumer consumer)) return Task.CompletedTask;

            queue.Enqueue(args.Body.DeserializeJson<JudgeReportInfo>(false));
            consumer.Model.BasicAck(args.DeliveryTag, false);
            return Task.CompletedTask;
        }

        public static async Task QueueExecutor(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (queue.TryDequeue(out var info))
                {
                    var judgeService = ServiceProviderExtensions.ServiceProvider?.GetService<IJudgeService>();
                    if (judgeService == null) throw new InvalidOperationException("IJudgeService was not registed into service collection.");

                    await judgeService.UpdateJudgeResultAsync(info.JudgeId, info.Type, info.JudgeResult);

                    var judgeHub = Program.RootServiceProvider?.GetService<IHubContext<JudgeHub, IJudgeHub>>();
                    if (judgeHub == null) throw new InvalidOperationException("IHubContext<JudgeHub, IJudgeHub> was not registed into service collection.");
                    var group = judgeHub.Clients.Group($"result_{info.JudgeId}");
                    await judgeHub.Clients.Group($"result_{info.JudgeId}").JudgeCompleteSignalReceived(info.JudgeId);
                }

                if (token.IsCancellationRequested) break;
            }
        }
    }
}
