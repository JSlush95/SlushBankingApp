namespace BankingAppCore.Services
{
    public class SavingsAccountInterestHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SavingsAccountInterestHostedService> _logger;

        public SavingsAccountInterestHostedService(IServiceProvider serviceProvider, ILogger<SavingsAccountInterestHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Adding interest to savings accounts...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var bankAccountService = scope.ServiceProvider.GetRequiredService<BankAccountService>();
                    await bankAccountService.AddInterestToSavingsAccountsAsync();
                }

                await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
            }
        }
    }
}
