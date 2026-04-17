using HeatHarmony.Config;
using HeatHarmony.Models;
using MQTTnet;
using System.Text;

namespace HeatHarmony.MQ
{
    public sealed class MQClient(ILogger<MQClient> logger)
    {
        private string _serviceName = nameof(MQClient);
        private readonly string _clientId = "heatharmony";
        private readonly ILogger<MQClient> _logger = logger;
        private readonly GlobalConfig.RabbitMQ _mqConfig = GlobalConfig.RabbitMQConfig!;
        public MQStatusEnum Status { get; private set; } = MQStatusEnum.Disconnected;

        public Task? Initialization;
        public double ActualConsumption { get; private set; }
        public double ActualReturndelivery { get; private set; }
        public double CumulativePowerConsumption { get; private set; }
        public double CumulativePowerYield { get; private set; }

        public async Task InitializeMqttClient()
        {
            _logger.LogInformation("{ServiceName}:: Initialize MQtt client", _serviceName);
            Status = MQStatusEnum.Connecting;
            try
            {
                var mqttClient = new MqttClientFactory().CreateMqttClient();
                mqttClient.ApplicationMessageReceivedAsync += m => HandleMessage(m.ApplicationMessage);
                mqttClient.DisconnectedAsync += e =>
                {
                    _logger.LogWarning("{ServiceName}:: MQtt client disconnected: {Reason}", _serviceName, e.Reason);
                    Status = MQStatusEnum.Disconnected;
                    return Task.CompletedTask;
                };

                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(_mqConfig.mqttServer, 1883)
                    .WithClientId(_clientId)
                    .WithCredentials(_mqConfig.mqttUser, _mqConfig.mqttPassword)
                    .WithCleanSession()
                    .Build();

                var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                var subResult = await mqttClient.SubscribeAsync(_mqConfig.mqttTopic);
                subResult.Items
                    .ToList()
                    .ForEach(s => _logger.LogInformation(
                        "{ServiceName}:: Subscribed to '{Topic}' with '{ResultCode}'",
                        _serviceName,
                        s.TopicFilter.Topic,
                        s.ResultCode));
            }
            catch (Exception ex)
            {
                Status = MQStatusEnum.Error;
                _logger.LogError(ex, "{ServiceName}:: MQtt client error {ErrorMessage}", _serviceName, ex.Message);
                throw;
            }
            Status = MQStatusEnum.Connected;
            _logger.LogInformation("{ServiceName}:: MQtt client connected successfully", _serviceName);
        }

        private async Task HandleMessage(MqttApplicationMessage applicationMessage)
        {
            var payload = Encoding.UTF8.GetString(applicationMessage.Payload);
            if (double.TryParse(payload, out double consumptionValue))
            {
                switch (applicationMessage.Topic)
                {
                    case "p1meter/actual_consumption":
                        ActualConsumption = consumptionValue;
                        break;
                    case "p1meter/actual_returndelivery":
                        ActualReturndelivery = consumptionValue;
                        break;
                    case "p1meter/cumulative_power_consumption":
                        CumulativePowerConsumption = consumptionValue;
                        break;
                    case "p1meter/cumulative_power_yield":
                        CumulativePowerYield = consumptionValue;
                        break;
                    default:
                        _logger.LogInformation("{ServiceName}:: Received message {Payload} in {Topic}",_serviceName, payload,applicationMessage.Topic);
                        break;
                }
            }
            else
            {
                _logger.LogWarning("{ServiceName}:: Received non-numeric payload '{Payload}' in topic '{Topic}'", _serviceName, payload, applicationMessage.Topic);
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }
    }
}
