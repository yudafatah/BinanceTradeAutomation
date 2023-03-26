using BinanceTradeBot.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace BinanceTradeBot.Controllers
{
    [Route("webhook/trade")]
    [ApiController]
    public class SpotTradeController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public SpotTradeController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet("order-market")]
        public Task<string> OrderMarket(string symbol, decimal quantity)
        {
            return _orderService.SpotOrder(symbol, quantity);
        }

        [HttpGet("sell-market")]
        public Task<string> SellMarket(string symbol)
        {
            return _orderService.SpotSell(symbol);
        }
    }
}
