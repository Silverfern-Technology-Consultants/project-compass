using Microsoft.AspNetCore.Mvc;
using Compass.Core.Models;
using Compass.Data.Repositories;

namespace Compass.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssessmentsController : ControllerBase
    {
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ILogger<AssessmentsController> _logger;

        public AssessmentsController(
            IAssessmentRepository assessmentRepository,
            ILogger<AssessmentsController> logger)
        {
            _assessmentRepository = assessmentRepository;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Assessment>> GetAssessment(Guid id)
        {
            var assessment = await _assessmentRepository.GetByIdAsync(id);
            if (assessment == null)
            {
                return NotFound();
            }
            return Ok(assessment);
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<Assessment>>> GetCustomerAssessments(Guid customerId)
        {
            var assessments = await _assessmentRepository.GetByCustomerIdAsync(customerId);
            return Ok(assessments);
        }

        [HttpPost]
        public async Task<ActionResult<Assessment>> CreateAssessment(CreateAssessmentRequest request)
        {
            var assessment = new Assessment
            {
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                Type = request.Type,
                Status = AssessmentStatus.InProgress,
                OverallScore = 0
            };

            var createdAssessment = await _assessmentRepository.CreateAsync(assessment);
            return CreatedAtAction(nameof(GetAssessment), new { id = createdAssessment.Id }, createdAssessment);
        }

        [HttpGet("{assessmentId}/findings")]
        public async Task<ActionResult<IEnumerable<AssessmentFinding>>> GetAssessmentFindings(Guid assessmentId)
        {
            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);
            return Ok(findings);
        }
    }

    public class CreateAssessmentRequest
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public AssessmentType Type { get; set; }
    }
}