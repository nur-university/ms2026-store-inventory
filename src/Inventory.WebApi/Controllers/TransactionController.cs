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

    /// <summary>The sales the warehouse has to prepare and hand over (oldest first), and whether each one is already paid.</summary>
    [HttpGet]
    [Route("pending-exits")]
    [Authorize(Policy = InventoryPermission.TransactionDeliver)]
    public async Task<IActionResult> GetPendingExits([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPendingExitsQuery(page, pageSize));
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }

    /// <summary>What the customer is entitled to pick up, looked up by the number of the invoice they show.</summary>
    [HttpGet]
    [Route("invoices/{invoiceNumber}")]
    [Authorize(Policy = InventoryPermission.TransactionDeliver)]
    public async Task<IActionResult> GetByInvoice([FromRoute] string invoiceNumber)
    {
        var result = await _mediator.Send(new GetExitByInvoiceQuery(invoiceNumber));
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }

    /// <summary>The customer picks the goods up, identified by the invoice. Same effect as delivering by order.</summary>
    [HttpPost]
    [Route("invoices/{invoiceNumber}/deliver")]
    [Authorize(Policy = InventoryPermission.TransactionDeliver)]
    public async Task<IActionResult> DeliverByInvoice([FromRoute] string invoiceNumber)
    {
        var result = await _mediator.Send(new DeliverInvoiceCommand(invoiceNumber));
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }

    private IActionResult ProblemFor(Error error)
    {
        int status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        return StatusCode(status, new ProblemDetails { Status = status, Title = error.Code, Detail = error.Description });
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
