using AutoMapper;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.ResourceParameters;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Dynamic;
using System.Text.Json;

namespace CourseLibrary.API.Controllers;

[ApiController]
[Route("api/authors")]
public class AuthorsController : ControllerBase
{
    private readonly ICourseLibraryRepository _courseLibraryRepository;
    private readonly IMapper _mapper;
    private readonly IPropertyMappingService _propertyMappingService;
    private readonly IPropertyCheckerService _propertyCheckerService;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public AuthorsController(
        ICourseLibraryRepository courseLibraryRepository,
        IMapper mapper,
        IPropertyMappingService propertyMappingService,
        IPropertyCheckerService propertyCheckerService,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _courseLibraryRepository = courseLibraryRepository ?? throw new ArgumentNullException(nameof(courseLibraryRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _propertyMappingService = propertyMappingService ?? throw new ArgumentNullException(nameof(propertyMappingService));
        _propertyCheckerService = propertyCheckerService ?? throw new ArgumentNullException(nameof(propertyCheckerService));
        _problemDetailsFactory = problemDetailsFactory ?? throw new ArgumentNullException(nameof(problemDetailsFactory));
    }

    [HttpGet(Name = "GetAuthors")]
    [HttpHead] //The HEAD method asks for a response identical to a GET request, but without the response body
    public async Task<IActionResult> GetAuthors(
        [FromQuery] AuthorsResourceParameters authorsResourceParameters)
    {
        if (!_propertyMappingService.ValidMappingExistsFor<AuthorDto, Entities.Author>(authorsResourceParameters.OrderBy))
        {
            return BadRequest();
        }

        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(authorsResourceParameters.Fields))
        {
            ProblemDetails problemDetails = _problemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: 400,
                detail: $"Not all requested data shaping fields exist on the resource: {authorsResourceParameters.Fields}");

            return base.BadRequest(problemDetails);
        }

        PagedList<Entities.Author> authorsFromRepo = await _courseLibraryRepository.GetAuthorsAsync(authorsResourceParameters);

        // replaced by HATEOAS links
        //var previousPageLink = authorsFromRepo.HasPrevious
        //    ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.PreviousPage)
        //    : null;
        //var nextPageLink = authorsFromRepo.HasNext
        //    ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.NextPage)
        //    : null;

        var paginationMetadata = new
        {
            totalCount = authorsFromRepo.TotalCount,
            pageSize = authorsFromRepo.PageSize,
            currentPage = authorsFromRepo.CurrentPage,
            totalPages = authorsFromRepo.TotalPages,
            //previousPageLink = previousPageLink, // replaced by HATEOAS links
            //nextPageLink = nextPageLink // replaced by HATEOAS links
        };

        this.Response.Headers.Add(
            key: "X-Pagination",
            value: JsonSerializer.Serialize(paginationMetadata));

        // create links (HATEOAS)
        var links = CreateLinksForAuthors(
            authorsResourceParameters,
            authorsFromRepo.HasNext,
            authorsFromRepo.HasPrevious);

        var shapedAuthors = _mapper
            .Map<IEnumerable<AuthorDto>>(authorsFromRepo)
            .ShapeData(authorsResourceParameters.Fields);

        var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
        {
            var authorAsDictionary = author as IDictionary<string, object?>;

            // create links (HATEOAS)
            var authorLinks = CreateLinksForAuthor(
                (Guid)authorAsDictionary["Id"],
                null);

            //add the links
            authorAsDictionary.Add("links", authorLinks);
            return authorAsDictionary;
        });

        //add the links
        var linkedCollectionResource = new
        {
            value = shapedAuthorsWithLinks,
            links = links
        };

        // return them
        return Ok(linkedCollectionResource);
    }

    private IEnumerable<LinkDto> CreateLinksForAuthors(
        AuthorsResourceParameters authorsResourceParameters,
        bool hasNext,
        bool hasPrevious)
    {
        var links = new List<LinkDto>();

        // self 
        links.Add(
            new(CreateAuthorsResourceUri(authorsResourceParameters,
                ResourceUriType.Current),
                "self",
                "GET"));

        if (hasNext)
        {
            links.Add(
                new(CreateAuthorsResourceUri(authorsResourceParameters,
                    ResourceUriType.NextPage),
                "nextPage",
                "GET"));
        }

        if (hasPrevious)
        {
            links.Add(
                new(CreateAuthorsResourceUri(authorsResourceParameters,
                    ResourceUriType.PreviousPage),
                "previousPage",
                "GET"));
        }

        return links;
    }

    private string? CreateAuthorsResourceUri(
        AuthorsResourceParameters authorsResourceParameters,
        ResourceUriType type)
    {
        switch (type)
        {
            case ResourceUriType.PreviousPage:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = authorsResourceParameters.Fields,
                        orderBy = authorsResourceParameters.OrderBy,
                        pageNumber = authorsResourceParameters.PageNumber - 1,
                        pageSize = authorsResourceParameters.PageSize,
                        mainCategory = authorsResourceParameters.MainCategory,
                        searchQuery = authorsResourceParameters.SearchQuery
                    });
            case ResourceUriType.NextPage:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = authorsResourceParameters.Fields,
                        orderBy = authorsResourceParameters.OrderBy,
                        pageNumber = authorsResourceParameters.PageNumber + 1,
                        pageSize = authorsResourceParameters.PageSize,
                        mainCategory = authorsResourceParameters.MainCategory,
                        searchQuery = authorsResourceParameters.SearchQuery
                    });
            case ResourceUriType.Current:
            default:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = authorsResourceParameters.Fields,
                        orderBy = authorsResourceParameters.OrderBy,
                        pageNumber = authorsResourceParameters.PageNumber,
                        pageSize = authorsResourceParameters.PageSize,
                        mainCategory = authorsResourceParameters.MainCategory,
                        searchQuery = authorsResourceParameters.SearchQuery
                    });
        }
    }

    [HttpGet("{authorId}", Name = "GetAuthor")]
    public async Task<IActionResult> GetAuthor(
        Guid authorId,
        [FromQuery] string? fields)
    {
        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
        {
            ProblemDetails problemDetails = _problemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: 400,
                detail: $"Not all requested data shaping fields exist on the resource: {fields}");

            return base.BadRequest(problemDetails);
        }

        Entities.Author authorFromRepo = await _courseLibraryRepository.GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }

        // create links (HATEOAS)
        IEnumerable<LinkDto> links = CreateLinksForAuthor(authorId, fields);

        var linkedResourceToReturn = _mapper
            .Map<AuthorDto>(authorFromRepo)
            .ShapeData(fields) as IDictionary<string, object?>;

        //add the links
        linkedResourceToReturn.Add("links", links);

        return Ok(linkedResourceToReturn);
    }

    // links for HATEOAS
    private IEnumerable<LinkDto> CreateLinksForAuthor(
        Guid authorId,
        string? fields)
    {
        var links = new List<LinkDto>();

        if (string.IsNullOrWhiteSpace(fields))
        {
            links.Add(
              new(Url.Link("GetAuthor", new { authorId }),
              "self",
              "GET"));
        }
        else
        {
            links.Add(
              new(Url.Link("GetAuthor", new { authorId, fields }),
              "self",
              "GET"));
        }

        links.Add(
              new(Url.Link("CreateCourseForAuthor", new { authorId }),
              "create_course_for_author",
              "POST"));
        links.Add(
             new(Url.Link("GetCoursesForAuthor", new { authorId }),
             "courses",
             "GET"));

        return links;
    }


    [HttpPost(Name = "CreateAuthor")]
    public async Task<ActionResult<AuthorDto>> CreateAuthor(AuthorForCreationDto author)
    {
        Entities.Author authorEntity = _mapper.Map<Entities.Author>(author);

        _courseLibraryRepository.AddAuthor(authorEntity);
        await _courseLibraryRepository.SaveAsync();

        AuthorDto authorToReturn = _mapper.Map<AuthorDto>(authorEntity);


        // create links (HATEOAS)
        IEnumerable<LinkDto> links = CreateLinksForAuthor(authorToReturn.Id, fields: null);

        var linkedResourceToReturn = authorToReturn.ShapeData(fields: null) as IDictionary<string, object?>;

        //add the links
        linkedResourceToReturn.Add("links", links);

        return CreatedAtRoute(
            routeName: "GetAuthor",
            routeValues: new { authorId = linkedResourceToReturn["Id"] },
            value: linkedResourceToReturn);
    }
}
