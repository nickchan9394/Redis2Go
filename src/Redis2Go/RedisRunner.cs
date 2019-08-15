﻿using Redis2Go.Exceptions;
using Redis2Go.Helpers;
using System;
using System.Threading.Tasks;

namespace Redis2Go
{
    public class RedisRunner : IDisposable
    {
        private const string BinariesSearchPattern = @"packages\Redis*\tools";
        private const string BinariesSearchPatternSolution = @"Redis*\tools";

        private readonly IRedisProcess _redisProcess;

        public State State { get; private set; }
        public int Port { get; private set; }
        public bool Disposed { get; private set; }

        public static RedisRunner Start()
        {
            return CreateRedisRunner(PortPool.GetInstance, new RedisProcessStarter());
        }

        public static Task<RedisRunner> StartAsync(int timeoutMilliseconds = 5000)
        {
            return CreateRedisRunnerAsync(PortPool.GetInstance, new RedisProcessStarter(), timeoutMilliseconds);
        }

        public static RedisRunner StartForDebugging()
        {
            return new RedisRunner(new ProcessWatcher(), new PortWatcher(), new RedisProcessStarter());
        }

        private RedisRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher, IRedisProcessStarter processStarter)
        {
            Port = RedisDefaults.DefaultPort;

            if (processWatcher.IsProcessRunning(RedisDefaults.ProcessName) && !portWatcher.IsPortAvailable(Port))
            {
                State = State.AlreadyRunning;
                return;
            }

            if (!portWatcher.IsPortAvailable(Port))
            {
                throw new PortTakenException(String.Format("Redis can't be started. The TCP port {0} is already taken.", this.Port));
            }

            _redisProcess = processStarter.Start(BinariesDirectory, Port);

            State = State.Running;
        }

        private RedisRunner(int port, IRedisProcess redisProcess)
        {
            Port = port;
            _redisProcess = redisProcess;
            State = State.Running;
        }

        private static RedisRunner CreateRedisRunner(IPortPool portPool, IRedisProcessStarter processStarter)
        {
            var port = portPool.GetNextOpenPort();

            var redisProcess = processStarter.Start(BinariesDirectory, port);

            return new RedisRunner(port, redisProcess);
        }

        private static async Task<RedisRunner> CreateRedisRunnerAsync(
            IPortPool portPool, IRedisProcessStarter processStarter , int timeoutMilliseconds)
        {
            var port = portPool.GetNextOpenPort();

            var redisProcess = await processStarter.StartAsync(BinariesDirectory, port, timeoutMilliseconds)
                                    .ConfigureAwait(false);

            return new RedisRunner(port, redisProcess);
        }

        private static string BinariesDirectory
        {
            get
            {
                // 1st: path when installed via nuget
                // 2nd: path when started from solution
                string binariesFolder = FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPattern) ??
                                        FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPatternSolution);

                if (binariesFolder == null)
                {
                    throw new RedisBinariesNotFoundException();
                }
                return binariesFolder;
            }
        }

        ~RedisRunner()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (this.Disposed)
                return;

            if (State != State.Running)
                return;

            if (disposing)
                GC.SuppressFinalize(this);

            if (this._redisProcess != null)
                this._redisProcess.Dispose();

            Disposed = true;
            State = State.Stopped;
        }
    }
}
