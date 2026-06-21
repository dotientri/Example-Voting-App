using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;
using Prometheus;
using Microsoft.Extensions.Logging; // Bắt buộc phải có để dùng ILogger

namespace Worker
{
    public class Program
    {
        // Khai báo một biến logger dùng chung cho toàn bộ class
        private static ILogger _logger;

        public static int Main(string[] args)
        {   
            // 1. CẤU HÌNH XUẤT LOG RA ĐỊNH DẠNG JSON CHO LOKI
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddJsonConsole(options =>
                {
                    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
                    {
                        Indented = false // In trên 1 dòng để Promtail dễ gom
                    };
                });
                builder.SetMinimumLevel(LogLevel.Information);
            });
            _logger = loggerFactory.CreateLogger<Program>();

            // 2. KHỞI CHẠY PROMETHEUS METRICS SERVER TRÊN PORT 80
            var metricServer = new MetricServer(port: 80);
            metricServer.Start();
            _logger.LogInformation("Metric server started on port 80");

            try
            {
                var pgsql = OpenDbConnection("Server=db;Username=postgres;Password=postgres;");
                var redisConn = OpenRedisConnection("redis");
                var redis = redisConn.GetDatabase();

                var keepAliveCommand = pgsql.CreateCommand();
                keepAliveCommand.CommandText = "SELECT 1";

                var definition = new { vote = "", voter_id = "" };
                while (true)
                {
                    Thread.Sleep(100);

                    if (redisConn == null || !redisConn.IsConnected) {
                        _logger.LogWarning("Reconnecting Redis");
                        redisConn = OpenRedisConnection("redis");
                        redis = redisConn.GetDatabase();
                    }
                    string json = redis.ListLeftPopAsync("votes").Result;
                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        // Sử dụng structured logging: Grafana sẽ tự bóc tách trường Vote và VoterId
                        _logger.LogInformation("Processing vote for '{Vote}' by '{VoterId}'", vote.vote, vote.voter_id);
                        
                        if (!pgsql.State.Equals(System.Data.ConnectionState.Open))
                        {
                            _logger.LogWarning("Reconnecting DB");
                            pgsql = OpenDbConnection("Server=db;Username=postgres;Password=postgres;");
                        }
                        else
                        { // Normal +1 vote requested
                            UpdateVote(pgsql, vote.voter_id, vote.vote);
                        }
                    }
                    else
                    {
                        keepAliveCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker crashed unexpectedly");
                return 1;
            }
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            NpgsqlConnection connection;

            while (true)
            {
                try
                {
                    connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    break;
                }
                catch (SocketException)
                {
                    _logger.LogWarning("Waiting for db");
                    Thread.Sleep(1000);
                }
                catch (DbException)
                {
                    _logger.LogWarning("Waiting for db");
                    Thread.Sleep(1000);
                }
            }

            _logger.LogInformation("Connected to db");

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS votes (
                                        id VARCHAR(255) NOT NULL UNIQUE,
                                        vote VARCHAR(255) NOT NULL
                                    )";
            command.ExecuteNonQuery();

            return connection;
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            var ipAddress = GetIp(hostname);
            _logger.LogInformation("Found redis at {IpAddress}", ipAddress);

            while (true)
            {
                try
                {
                    _logger.LogInformation("Connecting to redis");
                    return ConnectionMultiplexer.Connect(ipAddress);
                }
                catch (RedisConnectionException)
                {
                    _logger.LogWarning("Waiting for redis");
                    Thread.Sleep(1000);
                }
            }
        }

        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string vote)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote)";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException)
            {
                command.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id";
                command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }
    }
}