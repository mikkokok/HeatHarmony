using HeatHarmony.Models;
using HeatHarmony.MQ;

namespace HeatHarmony.Workers
{
    public class MQWorker(ILogger<MQWorker> logger, MQClient mQClient) : BackgroundService
    {
        private readonly ILogger<MQWorker> _logger = logger;
        private readonly MQClient _mQClient = mQClient;
        private readonly string _serviceName = nameof(MQWorker);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName}:: Started", _serviceName);
            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleId = Guid.NewGuid();
                var scope = new
                {
                    mqWorkerCycleId = cycleId,
                    mqWorker_Start = DateTime.Now
                };
                using (_logger.BeginScope(scope))
                {
                    try
                    {
                        _logger.LogInformation("{service}:: Running (cycle {cycleId})", _serviceName, cycleId);
                        if (_mQClient.Status == MQStatusEnum.Disconnected || _mQClient.Status == MQStatusEnum.Error)
                        {
                            _mQClient.Initialization = _mQClient.InitializeMqttClient();
                            await _mQClient.Initialization;
                            _logger.LogInformation("{service}:: MQClient initialized successfully (cycle {cycleId})", _serviceName, cycleId);
                            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        }
                        else if (_mQClient.Status == MQStatusEnum.Connected)
                        {
                            _logger.LogInformation("{service}:: MQClient is connected, no action needed (cycle {cycleId})", _serviceName, cycleId);
                            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                        }
                        else if (_mQClient.Status == MQStatusEnum.Connecting)
                        {
                            _logger.LogInformation("{service}:: MQClient is connecting, waiting... (cycle {cycleId})", _serviceName, cycleId);
                            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("{service}:: ExecuteAsync cancelled (cycle {cycleId})", _serviceName, cycleId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{service}:: ExecuteAsync failed, restarting in 30 seconds... (cycle {cycleId})", _serviceName, cycleId);
                    }
                    _logger.LogInformation("{service}:: Cycle completed, restarting in 30 seconds... (cycle {cycleId})", _serviceName, cycleId);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
    }
}
