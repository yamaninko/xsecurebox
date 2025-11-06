using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SecureBox.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace SecureBox.Infrastructure.Services;

public class RabbitMQService : IMessageBrokerService, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly string _connectionString;
    
    public RabbitMQService(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("RabbitMQ") 
            ?? throw new InvalidOperationException("RabbitMQ connection string not configured");
        
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            // Declare queues
            DeclareQueue("audit-log-queue");
            DeclareQueue("certificate-events");
            DeclareQueue("key-events");
            DeclareQueue("notification-queue");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
            // Non-critical failure, service will continue without messaging
        }
    }
    
    private void DeclareQueue(string queueName)
    {
        if (_channel == null) return;
        
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
    }
    
    public void PublishMessage(string queueName, object message)
    {
        if (_channel == null)
        {
            Console.WriteLine($"RabbitMQ channel not available, message not published to {queueName}");
            return;
        }
        
        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            
            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: body
            );
            
            Console.WriteLine($"Message published to {queueName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to publish message to {queueName}: {ex.Message}");
        }
    }
    
    public void SubscribeToQueue<T>(string queueName, Action<T> onMessage)
    {
        if (_channel == null)
        {
            Console.WriteLine($"RabbitMQ channel not available, cannot subscribe to {queueName}");
            return;
        }
        
        try
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<T>(json);
                    
                    if (message != null)
                    {
                        onMessage(message);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message from {queueName}: {ex.Message}");
                    // Negative acknowledgment - message will be requeued
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };
            
            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
            
            Console.WriteLine($"Subscribed to {queueName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to subscribe to {queueName}: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}

