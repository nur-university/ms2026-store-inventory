using Inventory.Application.Transactions.ReviewTransaction;
using Inventory.Application.Transactions.DeliverOrder;
using Joseco.DDD.Core.Results;
using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Application.Transactions.GetTransaccionById;
using Inventory.Application.Transactions.GetTransactions;
using Inventory.Domain.Transactions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Inventory.WebApi.Infrastructure;
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

    public record CreateTransactionRequest(string? Concept, List<CreateTransacionItemCommand>? Items, string? Notes);

    /// <summary>
    /// Creates a transaction by hand: a write-off (<c>Damaged</c>/<c>Lost</c>, a note is mandatory) or an
    /// <c>OpeningBalance</c>. It is created pending (a write-off reserves the stock) until it is approved or rejected.
    /// Sales, purchases and returns only come from their own flows (400).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = InventoryPermission.TransactionCreate)]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new CreateTransactionCommand(userId, request.Concept ?? string.Empty, request.Items ?? [], request.Notes));
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : this.ToProblem(result.Error);
    }

    public record ApproveTransactionRequest(Guid TransactionId);

    public record RejectTransactionRequest(Guid TransactionId, string? Reason);

    /// <summary>
    /// Approves a write-off or opening balance created by hand: the stock is deducted (write-off) or added (opening
    /// balance). A transaction of a sale is closed at pickup instead (409).
    /// </summary>
    [HttpPost]
    [Route("complete")]
    [Authorize(Policy = InventoryPermission.TransactionApprove)]
    public async Task<IActionResult> CompleteTransaction([FromBody] ApproveTransactionRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new ApproveTransactionCommand(request.TransactionId, userId));
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>Rejects a write-off or opening balance created by hand: nothing moves and a write-off gives its reservation back.</summary>
    [HttpPost]
    [Route("reject")]
    [Authorize(Policy = InventoryPermission.TransactionReject)]
    public async Task<IActionResult> RejectTransaction([FromBody] RejectTransactionRequest request)
    {
        if (!User.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new RejectTransactionCommand(request.TransactionId, userId, request.Reason));
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
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

    /// <summary>
    /// Transactions, newest first, filtered by type (Entry/Exit), concept, status, source type (orders, purchasing, ...)
    /// and creation dates (a date without time includes the whole day).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = InventoryPermission.TransactionRead)]
    public async Task<IActionResult> GetTransactions([FromQuery] string? type, [FromQuery] string? concept, [FromQuery] string? status,
        [FromQuery] string? sourceType, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(type, concept, status, sourceType, from, to, page, pageSize));
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>A transaction with its concept, notes, review and lines.</summary>
    [HttpGet]
    [Route("{id:guid}")]
    [Authorize(Policy = InventoryPermission.TransactionRead)]
    public async Task<IActionResult> GetTransaction([FromRoute] Guid id)
    {
        TransactionDto? transaction = await _mediator.Send(new GetTransaccionByIdQuery(id));
        return transaction != null ? Ok(transaction) : this.ToProblem(TransactionErrors.NotFound(id));
    }
}
