using Inventory.Application.Items.CreateItem;
using Inventory.Application.Items.GetItem;
using Inventory.Application.Items.GetItemMovements;
using Inventory.Application.Items.GetItems;
using Inventory.Domain.Items;
using Inventory.WebApi.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nur.Store2026.Access.Inventory.Permissions;

namespace Inventory.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ItemController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ItemController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult> CreateItem([FromBody] CreateItemCommand command)
        {
            try
            {
                var id = await _mediator.Send(command);

                return Ok(id);

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>Every item with its stock, reserved and available units and average cost.</summary>
        [HttpGet]
        [Authorize(Policy = InventoryPermission.TransactionRead)]
        public async Task<ActionResult> GetItems()
        {
            var result = await _mediator.Send(new GetItemsQuery(""));
            return Ok(result);
        }

        /// <summary>The stock of one item.</summary>
        [HttpGet]
        [Route("{id:guid}")]
        [Authorize(Policy = InventoryPermission.TransactionRead)]
        public async Task<IActionResult> GetItem([FromRoute] Guid id)
        {
            ItemDto? item = await _mediator.Send(new GetItemQuery(id));
            return item != null ? Ok(item) : this.ToProblem(ItemErros.ItemNotFound(id));
        }

        /// <summary>
        /// The kardex of the item: its completed movements in chronological order with the running balance of units,
        /// optionally between two dates (a date without time includes the whole day).
        /// </summary>
        [HttpGet]
        [Route("{id:guid}/movements")]
        [Authorize(Policy = InventoryPermission.TransactionRead)]
        public async Task<IActionResult> GetMovements([FromRoute] Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _mediator.Send(new GetItemMovementsQuery(id, from, to, page, pageSize));
            return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
        }
    }
}
