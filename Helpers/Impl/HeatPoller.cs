using HeatHarmony.Models;

namespace HeatHarmony.Helpers.Impl
{
    public class HeatPoller : IHeatPoller
    {
        private readonly IConfiguration _configuration;
        private readonly OumanConsumer _oumanConsumer;
        private readonly HeishaConsumer _heishaConsumer;
        private int _latestReading = 0;
        private readonly bool _isVILPControlRunning = false;
        private PollingStatus _heishaMonPollingStatus;
        private PollingStatus _oumanPollingStatus;
        public List<AirWaterHeatPumpUpdate> Updates { get; private set; } = [];
        public HeatPoller(IConfiguration configuration)
        {
            _configuration = configuration;
            _oumanConsumer = new OumanConsumer(_configuration);
            _heishaConsumer = new HeishaConsumer(_configuration);
            _isVILPControlRunning = true;
            Task.Run(UpdateOumanReadings);
            Task.Run(ControlVILP);
            Task.Run(CleanUpdatesList);
            _oumanPollingStatus = new PollingStatus
            {
                Status = _isVILPControlRunning,
                StatusReason = "Initial start",
                Time = DateTime.Now,
                Poller = Poller.Ouman.ToString()
            };
            _heishaMonPollingStatus = new PollingStatus
            {
                Status = _isVILPControlRunning,
                StatusReason = "Initial start",
                Time = DateTime.Now,
                Poller = Poller.HeishaMon.ToString()
            };
        }

        private async Task UpdateOumanReadings()
        {
            _oumanConsumer.UpdateLatestReading().Wait();
            while (_isVILPControlRunning)
            {
                try
                {
                    await _oumanConsumer.UpdateLatestReading();
                    _oumanPollingStatus = new PollingStatus
                    {
                        Status = _isVILPControlRunning,
                        StatusReason = "In the loop",
                        Time = DateTime.Now,
                        Poller = Poller.Ouman.ToString()
                    };
                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
                catch (Exception ex)
                {
                    _oumanPollingStatus = new PollingStatus
                    {
                        Status = _isVILPControlRunning,
                        StatusReason = ex.Message,
                        Time = DateTime.Now,
                        Poller = Poller.Ouman.ToString()
                    };
                }
            }
        }

        public List<PollingStatus> GetStatuses()
        {
            return [
                _heishaMonPollingStatus,
                _oumanPollingStatus
            ];
        }

        private async Task ControlVILP()
        {
            CheckForUpdate().Wait();
            while (_isVILPControlRunning)
            {
                await CheckForUpdate();
                _heishaMonPollingStatus = new PollingStatus
                {
                    Status = _isVILPControlRunning,
                    StatusReason = "In the loop",
                    Time = DateTime.Now,
                    Poller = Poller.HeishaMon.ToString()
                };
                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }

        private async Task CheckForUpdate()
        {
            try
            {
                if (_latestReading != _oumanConsumer.LatestReading)
                {
                    HeatStatus statusTemp;
                    if (_latestReading < _oumanConsumer.LatestReading)
                    {
                        statusTemp = HeatStatus.Increase;
                    }
                    else
                    {
                        statusTemp = HeatStatus.Decrease;
                    }
                    var newDemand = _oumanConsumer.LatestReading + 3;
                    await _heishaConsumer.UpdateDemand(newDemand);
                    var updateTemp = $"Update demand from {_latestReading} to {newDemand}";
                    _latestReading = _oumanConsumer.LatestReading;

                    Updates.Add(new AirWaterHeatPumpUpdate
                    {
                        Time = DateTime.Now,
                        Update = updateTemp,
                        Status = statusTemp.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _heishaMonPollingStatus = new PollingStatus
                {
                    Status = _isVILPControlRunning,
                    StatusReason = ex.Message,
                    Time = DateTime.Now,
                    Poller = Poller.HeishaMon.ToString()
                };
                Updates.Add(new AirWaterHeatPumpUpdate
                {
                    Time = DateTime.Now,
                    Update = ex.Message,
                    Status = HeatStatus.Error.ToString()
                });
            }
        }

        private async Task CleanUpdatesList()
        {
            while (true)
            {
                if (DateTime.Now.Day % 2 == 0 && DateTime.Now.Hour == 21)
                {
                    Updates.Clear();
                }
                await Task.Delay(TimeSpan.FromMinutes(45));
            }
        }
    }
}