
using HeatHarmony.Providers;
using System.Formats.Asn1;

namespace HeatHarmony.Workers
{
    public class HeatAutomationWorker : BackgroundService
    {
        private readonly string _serviceName;
        private readonly ILogger<HeatAutomationWorker> _logger;
        private readonly HeishaMonProvider _heishaMonProvider;
        private readonly OumanProvider _oumanProvider;
        private readonly PriceProvider _priceProvider;
        private readonly TRVProvider _tRVProvider;
        private readonly int _heatAddition = 3;
        public bool isRunning;
        public Task? OumanAndHeishamonSyncTask { get; private set; }
        public bool overRide = false;
        public int overRideTemp = 20;
        public DateOnly overRideUntil = DateOnly.MinValue;
        private Task? _overRideTask;
        private CancellationTokenSource _overRideCancellationTokenSource = new CancellationTokenSource();

        public HeatAutomationWorker(ILogger<HeatAutomationWorker> logger, HeishaMonProvider heishaMonProvider, 
            OumanProvider oumanProvider, PriceProvider priceProvider, TRVProvider tRVProvider)
        {
            _serviceName = nameof(HeatAutomationWorker);
            _logger = logger;
            _heishaMonProvider = heishaMonProvider;
            _oumanProvider = oumanProvider;
            _priceProvider = priceProvider;
            _tRVProvider = tRVProvider;
            _logger.LogInformation($"{_serviceName}:: Initialized successfully");
        }

        public async Task OverRideTemp(int hours, double temp, bool overRidePrevious)
        {
            if (overRide && !overRidePrevious)
            {
                _logger.LogInformation($"{_serviceName}:: Overriding already in place, ignoring new request");
                return;
            }
            if (overRidePrevious && _overRideTask != null)
            {
                _overRideCancellationTokenSource.Cancel();
                _overRideTask.Dispose();
                _logger.LogInformation($"{_serviceName}:: Previous override cancelled, starting new one");
                _overRideTask = OverRideTask(hours, temp, _overRideCancellationTokenSource.Token);
            }
            else
            {
                _overRideTask = OverRideTask(hours, temp, _overRideCancellationTokenSource.Token);
            }
        }

        private async Task OverRideTask(int hours, double temp, CancellationToken ct)
        {
            _logger.LogInformation($"{_serviceName}:: Overriding temp to {temp} for {hours} hours");
            overRide = true;
            var _previousTemp = _oumanProvider.LatestInsideTempDemand;
            await _oumanProvider.SetInsideTemp(temp);
            overRideUntil = DateOnly.FromDateTime(DateTime.Now.AddHours(hours));
            try
            {
                await Task.Delay(TimeSpan.FromHours(hours), ct);
                await _oumanProvider.SetInsideTemp(_previousTemp);
            }
            catch (TaskCanceledException tcex)
            {
                _logger.LogInformation($"{_serviceName}:: Override task cancelled: {tcex.Message}");
            }
            overRide = false;
            _logger.LogInformation($"{_serviceName}:: Overriding temp ended, set back to {_previousTemp}");
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"{_serviceName}:: Running at: {DateTime.Now}");
                isRunning = true;
                OumanAndHeishamonSyncTask = SyncOumanAndHeishamon(stoppingToken);
                await Task.WhenAll(OumanAndHeishamonSyncTask);
            }
            _logger.LogInformation($"{_serviceName}:: Stopped running at: {DateTime.Now}");
            isRunning = false;
        }

        private async Task SyncOumanAndHeishamon(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_heishaMonProvider.MainTargetTemp == 0 || _oumanProvider.LatestFlowDemand == 0.0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }
                var latestOuman = (int)Math.Round(_oumanProvider.LatestFlowDemand, 0, MidpointRounding.AwayFromZero);
                if (_heishaMonProvider.MainTargetTemp != latestOuman)
                {
                    _logger.LogInformation($"{_serviceName}:: Syncing Heishamon target temp {_heishaMonProvider.MainTargetTemp} to Ouman flow demand {_oumanProvider.LatestFlowDemand}");
                    var newTarget = latestOuman + _heatAddition;
                    await _heishaMonProvider.SetTargetTemperature(newTarget);
                }
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
