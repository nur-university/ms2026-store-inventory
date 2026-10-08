using Inventory.Application.Transactions.CompleteTransaction;
using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Application.Transactions.GetTransaccionById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nur.Store2026.Access.Inventory.Permissions;

namespace Inventory.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Policy = InventoryPermission.TransactionCreate)]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionCommand command)
    {
        try
        {
            var id = await _mediator.Send(command);
            return Ok(id);
        }
        catch (Exception)
        {
            return StatusCode(500);
        }
    }

    [HttpPost]
    [Route("complete")]
    [Authorize(Policy = InventoryPermission.TransactionApprove)]
    public async Task<IActionResult> CompleteTransaction([FromBody] CompleteTransactionCommand command)
    {
        try
        {
            bool result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500);
        }
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetTransaction([FromRoute] Guid id)
    {
        try
        {
            var result = await _mediator.Send(new GetTransaccionByIdQuery(id));
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500);
        }
    }
}
