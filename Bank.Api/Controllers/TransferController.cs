using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers
{

  //  [Authorize]
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TransferController : ControllerBase
    {
        private IFinacialService _finacialService;

        public TransferController(IFinacialService finacialService)
        {
            _finacialService = finacialService;
        }

        [HttpPost("interbank")]
        public async Task<IActionResult> BankTranfer([FromBody] TransferRequest transferRequest)
        {
            var result = await _finacialService.Transfer(transferRequest);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("interbank-event")]
        public async Task<IActionResult> BankTranferEvent([FromBody] TransferRequest transferRequest)
        {
            var result = await _finacialService.PublishTransaction(transferRequest);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("fund-account")]
        public async Task<IActionResult> FundAccount([FromBody] TransferRequest transferRequest)
        {
            var result = await _finacialService.CreditAccount(transferRequest);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
