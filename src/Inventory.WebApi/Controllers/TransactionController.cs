using Inventory.Application.Transactions.CompleteTransaction;
using Inventory.Application.Transactions.DeliverOrder;
using Joseco.DDD.Core.Results;
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

    /// <summary>The customer picks up the goods of a paid sale: the exit is completed and the stock is deducted.</summary>
    [HttpPost]
    [Route("orders/{orderId:guid}/deliver")]
    [Authorize(Policy = InventoryPermission.TransactionDeliver)]
    public async Task<IActionResult> DeliverOrder([FromRoute] Guid orderId)
    {
        var result = await _mediator.Send(new DeliverOrderCommand(orderId));
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        int status = result.Error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        return StatusCode(status, new ProblemDetails { Status = status, Title = result.Error.Code, Detail = result.Error.Description });
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
