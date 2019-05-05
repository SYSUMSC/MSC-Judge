﻿using hjudge.Shared.MessageQueue;
using hjudge.WebHost.Data;
using hjudge.WebHost.Data.Identity;
using hjudge.WebHost.Extensions;
using hjudge.WebHost.MessageHandlers;
using hjudge.WebHost.Middlewares;
using hjudge.WebHost.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Events;
using SpanJson.AspNetCore.Formatter;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFSecondLevelCache.Core;
using CacheManager.Core;
using hjudge.Shared.Caching;

namespace hjudge.WebHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] {
                    "image/svg+xml",
                    "image/png",
                    "font/woff",
                    "font/woff2",
                    "font/ttf",
                    "font/eof" });
            });

            services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

            services.AddSingleton<AntiForgeryFilter>();
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddScoped<IProblemService, ProblemService>();
            services.AddScoped<IContestService, ContestService>();
            services.AddScoped<IJudgeService, JudgeService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IVoteService, VoteService>();
            services.AddSingleton<ICacheService, CacheService>();
            services.AddSingleton<ILanguageService, LocalLanguageService>();

            services.AddMessageHandlers();

            services.AddSingleton<IMessageQueueService, MessageQueueService>()
                .Configure<MessageQueueServiceOptions>(options => options.MessageQueueFactory = CreateMessageQueueInstance());

            services.AddEFSecondLevelCache();
            services.AddSingleton(typeof(ICacheManagerConfiguration), new CacheManager.Core.ConfigurationBuilder()
                    .WithUpdateMode(CacheUpdateMode.Up)
                    .WithSerializer(typeof(CacheItemJsonSerializer))
                    .WithRedisConfiguration(Configuration["Redis:Configuration"], config =>
                    {
                        config.WithAllowAdmin()
                            .WithDatabase(0)
                            .WithEndpoint(Configuration["Redis:HostName"], int.Parse(Configuration["Redis:Port"]));
                    })
                    .WithMaxRetries(100)
                    .WithRetryTimeout(50)
                    .WithRedisCacheHandle(Configuration["Redis:Configuration"])
                    .WithExpiration(ExpirationMode.Absolute, TimeSpan.FromMinutes(10))
                    .Build());

            services.AddSingleton(typeof(ICacheManager<>), typeof(BaseCacheManager<>));

            services.AddDbContext<WebHostDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
#if DEBUG
                options.EnableDetailedErrors(true);
                options.EnableSensitiveDataLogging(true);
#endif
                options.EnableServiceProviderCaching(true);
            });

            services.AddEntityFrameworkSqlServer();

            services.AddAuthentication(o =>
            {
                o.DefaultScheme = IdentityConstants.ApplicationScheme;
                o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

            services.AddIdentityCore<UserInfo>(options =>
            {
                options.Stores.MaxLengthForKeys = 128;
                options.User.RequireUniqueEmail = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddSignInManager<SignInManager<UserInfo>>()
            .AddUserManager<CachedUserManager<UserInfo>>()
            .AddEntityFrameworkStores<WebHostDbContext>()
            .AddErrorDescriber<TranslatedIdentityErrorDescriber>();

            services.AddMvc(options =>
            {
                options.Filters.AddService<AntiForgeryFilter>();
            }).AddSpanJson();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "wwwroot/dist";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            if (lifetime.ApplicationStopped.IsCancellationRequested)
            {
                var mqService = MessageHandlersServiceExtensions.ServiceProvider.GetService<IMessageQueueService>();
                mqService?.Dispose();
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePages(new Func<StatusCodeContext, Task>(async context =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.Body.WriteAsync(
                    Encoding.UTF8.GetBytes($"{{succeeded: false, errorCode: {context.HttpContext.Response.StatusCode}, errorMessage: '请求失败'}}"));
            }));

            app.UseResponseCompression();

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }

        public MessageQueueFactory CreateMessageQueueInstance()
        {
            var factory = new MessageQueueFactory(
                new MessageQueueFactory.HostOptions
                {
                    HostName = Configuration["MessageQueue:HostName"],
                    VirtualHost = Configuration["MessageQueue:VirtualHost"],
                    Port = int.Parse(Configuration["MessageQueue:Port"]),
                    UserName = Configuration["MessageQueue:UserName"],
                    Password = Configuration["MessageQueue:Password"]
                });

            var cnt = 0;
            while (Configuration.GetSection($"MessageQueue:Producers:{cnt}").Exists())
            {
                factory.CreateProducer(new MessageQueueFactory.ProducerOptions
                {
                    Queue = Configuration[$"MessageQueue:Producers:{cnt}:Queue"],
                    Durable = bool.Parse(Configuration[$"MessageQueue:Producers:{cnt}:Durable"]),
                    AutoDelete = bool.Parse(Configuration[$"MessageQueue:Producers:{cnt}:AutoDelete"]),
                    Exclusive = bool.Parse(Configuration[$"MessageQueue:Producers:{cnt}:Exclusive"]),
                    Exchange = Configuration[$"MessageQueue:Producers:{cnt}:Exchange"],
                    RoutingKey = Configuration[$"MessageQueue:Producers:{cnt}:RoutingKey"]
                });
                ++cnt;
            }

            cnt = 0;
            while (Configuration.GetSection($"MessageQueue:Consumers:{cnt}").Exists())
            {
                factory.CreateConsumer(new MessageQueueFactory.ConsumerOptions
                {
                    Queue = Configuration[$"MessageQueue:Consumers:{cnt}:Queue"],
                    Durable = bool.Parse(Configuration[$"MessageQueue:Consumers:{cnt}:Durable"]),
                    AutoAck = bool.Parse(Configuration[$"MessageQueue:Consumers:{cnt}:AutoAck"]),
                    Exclusive = bool.Parse(Configuration[$"MessageQueue:Consumers:{cnt}:Exclusive"]),
                    Exchange = Configuration[$"MessageQueue:Producers:{cnt}:Exchange"],
                    RoutingKey = Configuration[$"MessageQueue:Producers:{cnt}:RoutingKey"],
                    OnReceived = Configuration[$"MessageQueue:Consumers:{cnt}:Queue"] switch
                    {
                        "JudgeReport" => new AsyncEventHandler<BasicDeliverEventArgs>(JudgeReport.JudgeReport_Received),
                        _ => null
                    }
                });
                ++cnt;
            }

            return factory;
        }
    }
}
