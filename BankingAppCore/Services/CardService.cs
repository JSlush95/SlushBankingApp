using BankingAppCore.Data;

namespace BankingAppCore.Services
{
    public class CardService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CardService> _logger;

        public CardService(ApplicationDbContext context, ILogger<CardService> logger)
        {
            _dbContext = context;
            _logger = logger;
        }

        public async Task DeactivateExpiredCardsAsync()
        {
            var expiredCards = await _dbContext.GetActiveExpiredCardsListAsync(DateTime.Now);

            foreach (var card in expiredCards)
            {
                card.Active = false;
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding interest to the savings account. {ex}");
            }
        }
    }
}
