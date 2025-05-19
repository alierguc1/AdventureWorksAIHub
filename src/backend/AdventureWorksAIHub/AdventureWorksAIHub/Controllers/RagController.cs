using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdventureWorksAIHub.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RagController : ControllerBase
    {
        private readonly IVectorStoreService _vectorStoreService;
        private readonly IRagService _ragService;
        private readonly ILogger<RagController> _logger;

        public RagController(
            IVectorStoreService vectorStoreService,
            IRagService ragService,
            ILogger<RagController> logger)
        {
            _vectorStoreService = vectorStoreService;
            _ragService = ragService;
            _logger = logger;
        }

        // GET: api/Rag/AskQuestion
        [HttpGet("AskQuestion")]
        public async Task<ActionResult<RagResponseDto>> AskQuestion([FromQuery] string question)
        {
            if (string.IsNullOrEmpty(question))
            {
                return BadRequest("Question cannot be empty");
            }

            try
            {
                var response = await _ragService.AskQuestionAsync(question);
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error processing question");
                return StatusCode(500, "An error occurred while processing your question");
            }
        }

        // POST: api/Rag/IndexProducts
        [HttpPost("IndexProducts")]
        public async Task<ActionResult> IndexProducts()
        {
            try
            {
                await _vectorStoreService.IndexProductDescriptionsAsync();
                return Ok("Products indexed successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error indexing products");
                return StatusCode(500, "An error occurred while indexing products");
            }
        }
    }
}