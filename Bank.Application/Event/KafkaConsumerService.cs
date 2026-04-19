using Bank.Application.Interface;
using Bank.Domain.Configs;
using Bank.Domain.Constants;
using Bank.Domain.Dtos.Request;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Bank.Application.Event
{
    public class KafkaConsumerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaSettings _kafkaSettings;
        private readonly ILogger<KafkaConsumerService> _logger;

        public KafkaConsumerService(IServiceScopeFactory scopeFactory, IOptions<KafkaSettings> kafkaOptions, ILogger<KafkaConsumerService> logger)
        {
            _scopeFactory = scopeFactory;
            _kafkaSettings = kafkaOptions.Value;
            _logger = logger;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
            return Task.CompletedTask;
        }

        private void StartConsumerLoop(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = _kafkaSettings.ConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,

                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 3000,
                ClientId = "webhook-consumer"
            };

            var consumer = new ConsumerBuilder<Ignore, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    Console.WriteLine($"Kafka Error: {e.Reason}");
                })
                .Build();

            consumer.Subscribe(EventTopics.Transactions);

            Console.WriteLine("Kafka Consumer started...");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(TimeSpan.FromSeconds(1));

                        if (result?.Message?.Value == null)
                            continue;

                        var transactionEvent =
                            JsonSerializer.Deserialize<TransferRequest>(result.Message.Value);

                        if (transactionEvent == null)
                        {
                            consumer.Commit(result);
                            continue;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var transferService = scope.ServiceProvider
                            .GetRequiredService<IFinacialService>();

                        transferService.Transfer(transactionEvent)
                            .GetAwaiter().GetResult();

                        consumer.Commit(result);
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Consume error: {ex.Error.Reason}");
                        _logger.LogError(ex, "Consume error");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Processing error: {ex.Message}");
                        _logger.LogError(ex, "Processing error");
                    }
                }
            }
            finally
            {
                consumer.Close(); // important: commits final offsets & leaves group cleanly
            }
        }
    }
}
