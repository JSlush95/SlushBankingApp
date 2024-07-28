namespace BankingAppCore.Services
{
    public class CardExpireHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CardExpireHostedService> _logger;

        public CardExpireHostedService(IServiceProvider serviceProvider, ILogger<CardExpireHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Checking for expired cards...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var cardService = scope.ServiceProvider.GetRequiredService<CardService>();
                    await cardService.DeactivateExpiredCardsAsync();
                }

                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
