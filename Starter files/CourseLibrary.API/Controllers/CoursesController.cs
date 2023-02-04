
using AutoMapper;
using CourseLibrary.API.Models;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace CourseLibrary.API.Controllers;

[ApiController]
[Route("api/authors/{authorId}/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseLibraryRepository _courseLibraryRepository;
    private readonly IMapper _mapper;

    public CoursesController(ICourseLibraryRepository courseLibraryRepository,
        IMapper mapper)
    {
        _courseLibraryRepository = courseLibraryRepository ?? throw new ArgumentNullException(nameof(courseLibraryRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet(Name = "GetCoursesForAuthor")]
    public async Task<ActionResult<IEnumerable<CourseDto>>> GetCoursesForAuthor(Guid authorId)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        var coursesForAuthorFromRepo = await _courseLibraryRepository.GetCoursesAsync(authorId);
        return Ok(_mapper.Map<IEnumerable<CourseDto>>(coursesForAuthorFromRepo));
    }

    [HttpGet("{courseId}", Name = "GetCourseForAuthor")]
    public async Task<ActionResult<CourseDto>> GetCourseForAuthor(
        [FromRoute] Guid authorId,
        [FromRoute] Guid courseId)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

        if (courseForAuthorFromRepo == null)
        {
            return NotFound();
        }
        return Ok(_mapper.Map<CourseDto>(courseForAuthorFromRepo));
    }


    [HttpPost(Name = "CreateCourseForAuthor")]
    public async Task<ActionResult<CourseDto>> CreateCourseForAuthor(Guid authorId, CourseForCreationDto course)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        Entities.Course courseEntity = _mapper.Map<Entities.Course>(course);
        _courseLibraryRepository.AddCourse(authorId, courseEntity);
        await _courseLibraryRepository.SaveAsync();

        CourseDto courseToReturn = _mapper.Map<CourseDto>(courseEntity);

        return CreatedAtRoute(
            routeName: "GetCourseForAuthor",
            routeValues: new { authorId, courseId = courseToReturn.Id },
            value: courseToReturn);
    }


    [HttpPut("{courseId}")]
    public async Task<IActionResult> UpdateCourseForAuthor(
        [FromRoute] Guid authorId,
        [FromRoute] Guid courseId,
        [FromBody] CourseForUpdateDto course)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);
        if (courseForAuthorFromRepo == null)
        {//if not found, then create (Upserting with PUT)

            Entities.Course courseToAdd = _mapper.Map<Entities.Course>(course);
            courseToAdd.Id = courseId;
            _courseLibraryRepository.AddCourse(authorId, courseToAdd);
            await _courseLibraryRepository.SaveAsync();

            CourseDto courseToReturn = _mapper.Map<CourseDto>(courseToAdd);
            return CreatedAtRoute(
                routeName: "GetCourseForAuthor",
                routeValues: new { authorId, courseId = courseToReturn.Id },
                value: courseToReturn);
        }

        _mapper.Map(course, courseForAuthorFromRepo);
        _courseLibraryRepository.UpdateCourse(courseForAuthorFromRepo);
        await _courseLibraryRepository.SaveAsync();
        return NoContent();
    }

    [HttpPatch("{courseId}")]
    public async Task<IActionResult> PartiallyUpdateCourseForAuthor(
        [FromRoute] Guid authorId,
        [FromRoute] Guid courseId,
        [FromBody] JsonPatchDocument<CourseForUpdateDto> patchDocument)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);
        if (courseForAuthorFromRepo == null)
        {//if not found, then create (Upserting with PATCH)

            var courseDto = new CourseForUpdateDto();
            patchDocument.ApplyTo(courseDto, ModelState);
            if (!TryValidateModel(courseDto))
            {
                return ValidationProblem(ModelState);
            }

            Entities.Course courseToAdd = _mapper.Map<Entities.Course>(courseDto);
            courseToAdd.Id = courseId;
            _courseLibraryRepository.AddCourse(authorId, courseToAdd);
            await _courseLibraryRepository.SaveAsync();

            CourseDto courseToReturn = _mapper.Map<CourseDto>(courseToAdd);
            return CreatedAtRoute(
                routeName: "GetCourseForAuthor",
                routeValues: new { authorId, courseId = courseToReturn.Id },
                value: courseToReturn);
        }

        CourseForUpdateDto courseToPatch = _mapper.Map<CourseForUpdateDto>(courseForAuthorFromRepo);

        //1) apply patches to DTOs instead to entities
        patchDocument.ApplyTo(courseToPatch, ModelState);
        //2) validate and return status error response
        if (!TryValidateModel(courseToPatch))
        {
            return ValidationProblem(ModelState);
        }

        //3) map the patched DTO to an entity, and save to to repo
        _mapper.Map(courseToPatch, courseForAuthorFromRepo);
        _courseLibraryRepository.UpdateCourse(courseForAuthorFromRepo);
        await _courseLibraryRepository.SaveAsync();
        return NoContent();
    }

    [HttpDelete("{courseId}")]
    public async Task<ActionResult> DeleteCourseForAuthor(Guid authorId, Guid courseId)
    {
        if (!await _courseLibraryRepository.AuthorExistsAsync(authorId))
        {
            return NotFound();
        }

        var courseForAuthorFromRepo = await _courseLibraryRepository.GetCourseAsync(authorId, courseId);

        if (courseForAuthorFromRepo == null)
        {
            return NotFound();
        }

        _courseLibraryRepository.DeleteCourse(courseForAuthorFromRepo);
        await _courseLibraryRepository.SaveAsync();

        return NoContent();
    }

    /// <summary>
    /// If NOK return 422 Status
    /// </summary>
    public override ActionResult ValidationProblem([ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
    {
        var options = HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>();
        return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext); //return custom error response
    }
}