using System.Threading.Tasks;

namespace BinanceTradeBot.Contracts
{
    public interface IOrderService
    {
        Task<string> SpotOrder(string symbol, decimal quantity);
        Task SpotOrderWithSL(string symbol, decimal quantity, decimal price, decimal slPercent);
        Task<string> SpotSell(string symbol);
    }
}
