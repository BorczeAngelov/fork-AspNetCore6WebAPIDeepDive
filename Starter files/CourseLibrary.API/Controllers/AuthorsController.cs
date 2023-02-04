using AutoMapper;
using CourseLibrary.API.ActionConstraints;
using CourseLibrary.API.Entities;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.ResourceParameters;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;
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


    [RequestHeaderMatchesMediaType("Accept",
        "application/json",
        "application/vnd.marvin.author.friendly+json")]
    [Produces("application/json",
        "application/vnd.marvin.author.friendly+json")]
    [HttpGet("{authorId}", Name = "GetAuthor")]
    public async Task<IActionResult> GetAuthorWithoutLinks(Guid authorId,
        string? fields)
    {
        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
                (fields))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Not all requested data shaping fields exist on " +
                  $"the resource: {fields}"));
        }

        // get author from repo
        var authorFromRepo = await _courseLibraryRepository
            .GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }

        // friendly author
        var friendlyResourceToReturn = _mapper.Map<AuthorDto>(authorFromRepo)
            .ShapeData(fields);

        return Ok(friendlyResourceToReturn);
    }

    [RequestHeaderMatchesMediaType("Accept",
        "application/vnd.marvin.hateoas+json",
        "application/vnd.marvin.author.friendly.hateoas+json")]
    [Produces("application/vnd.marvin.hateoas+json",
        "application/vnd.marvin.author.friendly.hateoas+json")]
    [HttpGet("{authorId}")]
    public async Task<IActionResult> GetAuthorWithLinks(Guid authorId,
        string? fields)
    {
        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
                (fields))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Not all requested data shaping fields exist on " +
                  $"the resource: {fields}"));
        }

        // get author from repo
        var authorFromRepo = await _courseLibraryRepository
            .GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }
        IEnumerable<LinkDto> links = CreateLinksForAuthor(authorId, fields);

        // friendly author
        var friendlyResourceToReturn = _mapper.Map<AuthorDto>(authorFromRepo)
            .ShapeData(fields) as IDictionary<string, object?>;

        friendlyResourceToReturn.Add("links", links);

        return Ok(friendlyResourceToReturn);
    }

    [RequestHeaderMatchesMediaType("Accept",
        "application/vnd.marvin.author.full+json")]
    [Produces("application/vnd.marvin.author.full+json")]
    [HttpGet("{authorId}", Name = "GetAuthor")]
    public async Task<IActionResult> GetFullAuthorWithoutLinks(Guid authorId,
        string? fields)
    {
        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
                (fields))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Not all requested data shaping fields exist on " +
                  $"the resource: {fields}"));
        }

        // get author from repo
        var authorFromRepo = await _courseLibraryRepository
            .GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }

        var fullResourceToReturn = _mapper.Map<AuthorFullDto>(authorFromRepo)
            .ShapeData(fields);

        return Ok(fullResourceToReturn);
    }

    [RequestHeaderMatchesMediaType("Accept",
        "application/vnd.marvin.author.full.hateoas+json")]
    [Produces("application/vnd.marvin.author.full.hateoas+json")]
    [HttpGet("{authorId}")]
    public async Task<IActionResult> GetFullAuthorWithLinks(Guid authorId,
        string? fields)
    {
        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
                (fields))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Not all requested data shaping fields exist on " +
                  $"the resource: {fields}"));
        }

        // get author from repo
        var authorFromRepo = await _courseLibraryRepository
            .GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }

        IEnumerable<LinkDto> links = CreateLinksForAuthor(authorId, fields);

        var fullResourceToReturn = _mapper.Map<AuthorFullDto>(authorFromRepo)
            .ShapeData(fields) as IDictionary<string, object?>;

        fullResourceToReturn.Add("links", links);
        return Ok(fullResourceToReturn);
    }

    // Instead of using "Accept header" we are using the ActionConstraint to contrain a request to certian Accept header value to a specific action
    //[Produces("application/json",
    //    "application/vnd.marvin.hateoas+json",
    //    "application/vnd.marvin.author.full+json",
    //    "application/vnd.marvin.author.full.hateoas+json",
    //    "application/vnd.marvin.author.friendly+json",
    //    "application/vnd.marvin.author.friendly.hateoas+json")]
    //[HttpGet("{authorId}", Name = "GetAuthor")]
    //public async Task<IActionResult> GetAuthor(
    //    Guid authorId,
    //    [FromQuery] string? fields,
    //    [FromHeader(Name = "Accept")] string? mediaType)
    //{
    //    if (!MediaTypeHeaderValue.TryParse(mediaType, out var parsedMediaType))
    //    {
    //        return BadRequest(_problemDetailsFactory.CreateProblemDetails(
    //            HttpContext,
    //            statusCode: 400,
    //            detail: $"Accept header media type value is not a valid media type."));
    //    }

    //    if (!_propertyCheckerService.TypeHasProperties<AuthorDto>(fields))
    //    {
    //        ProblemDetails problemDetails = _problemDetailsFactory.CreateProblemDetails(
    //            HttpContext,
    //            statusCode: 400,
    //            detail: $"Not all requested data shaping fields exist on the resource: {fields}");

    //        return base.BadRequest(problemDetails);
    //    }

    //    var authorFromRepo = await _courseLibraryRepository.GetAuthorAsync(authorId);

    //    if (authorFromRepo == null)
    //    {
    //        return NotFound();
    //    }

    //    var includeLinks = parsedMediaType.SubTypeWithoutSuffix.EndsWith("hateoas", StringComparison.InvariantCultureIgnoreCase);
    //    IEnumerable<LinkDto> links = new List<LinkDto>();

    //    if (includeLinks)
    //    {
    //        links = CreateLinksForAuthor(authorId, fields);
    //    }

    //    var primaryMediaType = includeLinks
    //        ? parsedMediaType.SubTypeWithoutSuffix.Substring(0, parsedMediaType.SubTypeWithoutSuffix.Length - 8)
    //        : parsedMediaType.SubTypeWithoutSuffix;

    //    // full author
    //    if (primaryMediaType == "vnd.marvin.author.full")
    //    {
    //        var fullResourceToReturn = _mapper.Map<AuthorFullDto>(authorFromRepo)
    //            .ShapeData(fields) as IDictionary<string, object?>;

    //        if (includeLinks)
    //        {
    //            fullResourceToReturn.Add("links", links);
    //        }

    //        return Ok(fullResourceToReturn);
    //    }
    //    else
    //    {
    //        // friendly author (not full)
    //        var friendlyResourceToReturn = _mapper.Map<AuthorDto>(authorFromRepo)
    //            .ShapeData(fields) as IDictionary<string, object?>;

    //        if (includeLinks)
    //        {
    //            friendlyResourceToReturn.Add("links", links);
    //        }

    //        return Ok(friendlyResourceToReturn);
    //    }
    //}

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

    [HttpPost(Name = "CreateAuthorWithDateOfDeath")]
    [RequestHeaderMatchesMediaType("Content-Type",
        "application/vnd.marvin.authorforcreationwithdateofdeath+json")]
    [Consumes("application/vnd.marvin.authorforcreationwithdateofdeath+json")]
    public async Task<ActionResult<AuthorDto>> CreateAuthorWithDateOfDeath(AuthorForCreationWithDateOfDeathDto author)
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

    [HttpPost(Name = "CreateAuthor")]

    [RequestHeaderMatchesMediaType("Content-Type",
            "application/json",
            "application/vnd.marvin.authorforcreation+json")]
    [Consumes(
        "application/json",
        "application/vnd.marvin.authorforcreation+json")]
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
