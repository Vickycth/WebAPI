﻿using ClassTranscribeDatabase;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TaskEngine.Tasks;
using System.Collections.Specialized;
using Quartz.Impl;
using Quartz;
using TaskEngine.Grpc;
using TaskEngine.MSTranscription;
using Microsoft.Extensions.Options;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Threading;
using ClassTranscribeDatabase.Models;
using static ClassTranscribeDatabase.CommonUtils;
using System.IO;

namespace TaskEngine
{
    public static class TaskEngineGlobals
    {
        public static KeyProvider KeyProvider { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            //setup our DI
            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddOptions()
                .Configure<AppSettings>(CTDbContext.GetConfigurations())
                .AddSingleton<RabbitMQConnection>()
                .AddSingleton<DownloadPlaylistInfoTask>()
                .AddSingleton<DownloadMediaTask>()
                .AddSingleton<ConvertVideoToWavTask>()
                .AddSingleton<TranscriptionTask>()
                .AddSingleton<QueueAwakerTask>()
                .AddSingleton<GenerateVTTFileTask>()
                .AddSingleton<RpcClient>()
                .AddSingleton<ProcessVideoTask>()
                .AddSingleton<MSTranscriptionService>()
                .AddSingleton<EPubGeneratorTask>()
                .BuildServiceProvider();

            //configure console logging
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            if (configuration.GetValue<string>("DEV_ENV", "NULL") == "DOCKER")
            {
                Console.WriteLine("Sleeping");
                Thread.Sleep(15000);
                Console.WriteLine("Waking up");
            }
            serviceProvider
                .GetService<ILoggerFactory>()
                .AddConsole(LogLevel.Debug);

            Globals.appSettings = serviceProvider.GetService<IOptions<AppSettings>>().Value;
            TaskEngineGlobals.KeyProvider = new KeyProvider(Globals.appSettings);

            var logger = serviceProvider.GetService<ILoggerFactory>()
                .CreateLogger<Program>();

            RabbitMQConnection rabbitMQ = serviceProvider.GetService<RabbitMQConnection>();
            CTDbContext context = CTDbContext.CreateDbContext();
            Seeder.Seed(context);
            logger.LogDebug("Starting application");
            rabbitMQ.DeleteAllQueues();
            serviceProvider.GetService<DownloadPlaylistInfoTask>().Consume();
            serviceProvider.GetService<DownloadMediaTask>().Consume();
            serviceProvider.GetService<ConvertVideoToWavTask>().Consume();
            serviceProvider.GetService<TranscriptionTask>().Consume();
            serviceProvider.GetService<QueueAwakerTask>().Consume();
            serviceProvider.GetService<GenerateVTTFileTask>().Consume();
            serviceProvider.GetService<ProcessVideoTask>().Consume();
            serviceProvider.GetService<EPubGeneratorTask>().Consume();
            // RunProgramRunExample(rabbitMQ).GetAwaiter().GetResult();


            DownloadPlaylistInfoTask downloadPlaylistInfoTask = serviceProvider.GetService<DownloadPlaylistInfoTask>();
            DownloadMediaTask downloadMediaTask = serviceProvider.GetService<DownloadMediaTask>();
            ConvertVideoToWavTask convertVideoToWavTask = serviceProvider.GetService<ConvertVideoToWavTask>();
            TranscriptionTask transcriptionTask = serviceProvider.GetService<TranscriptionTask>();
            GenerateVTTFileTask generateVTTFileTask = serviceProvider.GetService<GenerateVTTFileTask>();
            ProcessVideoTask processVideoTask = serviceProvider.GetService<ProcessVideoTask>();
            EPubGeneratorTask ePubGeneratorTask = serviceProvider.GetService<EPubGeneratorTask>();
            RpcClient rpcClient = serviceProvider.GetService<RpcClient>();

            // var ps = context.Playlists.Where(p => p.SourceType == SourceType.Echo360).ToList();
            // ps.ForEach(p =>
            // {
            //     downloadPlaylistInfoTask.Publish(p);
            // });

            // var ps = context.Playlists.Where(p => p.SourceType == SourceType.Youtube).ToList();
            // ps.ForEach(p =>
            // {
            //     downloadPlaylistInfoTask.Publish(p);
            // });


            // var ts = ps.SelectMany(p => p.Medias).SelectMany(m => m.Video.Transcriptions).ToList();
            // ts.ForEach(t =>
            // {
            //     generateVTTFileTask.Publish(t);
            // });



            //// downloadPlaylistInfoTask.Publish(new Playlist { Id = "Test", PlaylistIdentifier = "1_jfkhu08c", SourceType = SourceType.Kaltura });

            //var m = context.Medias.Find("cccb7dc9-e694-419b-ab03-780360b20956");
            //ePubGeneratorTask.Publish(new EPub
            //{
            //    Language = Languages.ENGLISH,
            //    VideoId = m.VideoId
            //});


            // localFix();

            logger.LogDebug("All done!");

            Console.WriteLine("Press any key to close the application");

            while (true)
            {
                Console.Read();
            };
        }

        public static void localFix()
        {
            string path = "/data/cs241";
            string[] entries = Directory.GetFileSystemEntries(path, "*.mp4", SearchOption.AllDirectories);
            Console.WriteLine("Got all files");
            using (var _context = CTDbContext.CreateDbContext())
            {
                foreach (string file in entries)
                {
                    var fileRecord = new FileRecord(file);
                    var dbfr = _context.FileRecords.Where(f => f.Hash == fileRecord.Hash).First();
                    File.Copy(file, dbfr.Path, true);
                    Console.WriteLine("Fixing" + file);
                }
            }
            Console.WriteLine("hi");
        }
        private static async Task RunProgramRunExample(RabbitMQConnection rabbitMQ)
        {
            try
            {
                // Grab the Scheduler instance from the Factory
                NameValueCollection props = new NameValueCollection
                {
                    { "quartz.serializer.type", "binary" }
                };
                StdSchedulerFactory factory = new StdSchedulerFactory(props);
                IScheduler scheduler = await factory.GetScheduler();

                // and start it off
                await scheduler.Start();

                // define the job and tie it to our HelloJob class
                IJobDetail job = JobBuilder.Create<QueueAwakerTask>()
                    .WithIdentity("job1", "group1")
                    .Build();

                job.JobDataMap.Put("rabbitMQ", rabbitMQ);

                // Trigger the job to run now, and then repeat every 10 seconds
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("trigger1", "group1")
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInHours(6)
                        .RepeatForever())
                    .Build();

                // Tell quartz to schedule the job using our trigger
                await scheduler.ScheduleJob(job, trigger);

                // some sleep to show what's happening
                await Task.Delay(TimeSpan.FromSeconds(60));

                // and last shut down the scheduler when you are ready to close your program
                await scheduler.Shutdown();
            }
            catch (SchedulerException se)
            {
                Console.WriteLine(se);
            }
        }
    }
}
