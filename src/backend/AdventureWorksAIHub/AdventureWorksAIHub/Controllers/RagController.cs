using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Requests;
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

        // POST: api/Rag/AskQuestion
        [HttpPost("AskQuestion")]
        public async Task<ActionResult<RagResponseDto>> AskQuestion([FromBody] QuestionRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.question))
            {
                return BadRequest("Question cannot be empty");
            }

            try
            {
                _logger.LogInformation("Processing question: {Question}", request.question);
                var response = await _ragService.AskQuestionAsync(request.question);
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
                _logger.LogInformation("Starting product indexing...");
                await _vectorStoreService.IndexProductDescriptionsAsync();
                _logger.LogInformation("Products indexed successfully");
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