namespace Redis.Helper
{
    using Models;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class RedisContext
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static RedisContext _redisContext;
        private static IDatabase _redisDB;
        private static IServer _redisServer;

        private RedisContext(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _redisServer = connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0]);
            _redisDB = _connectionMultiplexer.GetDatabase();
        }

        public static RedisContext GetInstance(IConnectionMultiplexer connectionMultiplexer)
        {
            if (_redisContext == null)
            {
                _redisContext = new RedisContext(connectionMultiplexer);
            }

            if (!connectionMultiplexer.IsConnected)
            {
                throw new Exception("At this moment Redis is not accesible.");
            }

            return _redisContext;
        }

        public void Set<T>(string key, T obj, TimeSpan? ttl = null)
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                throw new Exception("At this moment Redis is not accesible.");
            }

            if (typeof(T) == typeof(string))
            {
                _redisDB.StringSet(key, Convert.ToString(obj), expiry: ttl);
            }
            else
            {
                _redisDB.HashSet(key, RedisConverter.ToHashEntries(obj));
            }

            if (ttl.HasValue)
            {
                _redisDB.KeyExpireAsync(key, ttl);
            }
        }

        public T Get<T>(string key)
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                throw new Exception("At this moment Redis is not accesible.");
            }

            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(_redisDB.StringGet(key), typeof(T));
            }
            else
            {
                return RedisConverter.ConvertFromRedis<T>(_redisDB.HashGetAll(key));
            }
        }

        public IEnumerable<RedisKey> GetServerPattern(string pattern)
        {
            return _redisServer.Keys(pattern: pattern);
        }

        public void CleanCacheByKey(string key)
        {
            _redisDB.KeyDelete(key);
        }

        public bool KeyExists(string key)
        {
            return _redisDB.KeyExists(key);
        }

        public static ConfigurationOptions GetConfigurationOptions(string redisMaster, int redisPort)
        {
            return new ConfigurationOptions()
            {
                EndPoints =
                {
                    { redisMaster, redisPort },
                },
                CommandMap = CommandMap.Create(
                    new HashSet<string>
                    {
                        "INFO",
                        "CONFIG",
                        "CLUSTER",
                        "PING",
                        "ECHO",
                        "CLIENT",
                    },
                    available: false
                ),
                KeepAlive = 180,
                SyncTimeout = 600000,
                ConnectRetry = 3,
                AbortOnConnectFail = false,
                AllowAdmin = true,
                DefaultVersion = new Version(2, 8, 8),
                Password = "changeme",
            };
        }

        public Response<T> TryToGetAndSetData<T>(string key, TimeSpan ttl, Func<Response<T>> metodToRun) =>
            Response<T>.DoMethod(resp =>
            {
                bool dbMethodIsInvoked = false;
                try
                {
                    if (!_connectionMultiplexer.IsConnected)
                    {
                        Response<T> result = metodToRun.Invoke();
                        resp.Data = result.Data;
                        resp.Message = result.Message;
                        resp.Code = result.Code;
                        return;
                    }

                    if (!_redisDB.KeyExists(key))
                    {
                        Response<T> result = metodToRun.Invoke();
                        dbMethodIsInvoked = true;
                        if (result != null && result.Code == 0)
                        {
                            Set(key, JsonConvert.SerializeObject(result.Data), ttl);
                        }

                        resp.Data = result.Data;
                        resp.Message = result.Message;
                        resp.Code = result.Code;
                        return;
                    }
                    else
                    {
                        try
                        {
                            resp.Data = JsonConvert.DeserializeObject<T>(Get<string>(key));
                        }
                        catch (Exception ex)
                        {
                            resp.Code = -3;
                        }

                        if (resp.Code != 0 || resp.Data == null)
                        {
                            CleanCacheByKey(key);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!dbMethodIsInvoked)
                    {
                        Response<T> result = metodToRun.Invoke();
                        resp.Data = result.Data;
                        resp.Message = result.Message;
                        resp.Code = result.Code;
                    }

                    Console.WriteLine($"Redis error has happened: {e.Message}");
                }

                return;
            });
    }
}
