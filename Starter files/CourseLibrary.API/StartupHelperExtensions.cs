﻿using CourseLibrary.API.DbContexts;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace CourseLibrary.API;

internal static class StartupHelperExtensions
{
    // Add services to the container
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(configure =>
        {
            configure.ReturnHttpNotAcceptable = true;
            configure.CacheProfiles
            .Add("240SecondsCacheProfile", new() { Duration = 240 });
        })
        .AddNewtonsoftJson(setupAction =>
        {
            setupAction.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        })
        .AddXmlDataContractSerializerFormatters()
        .ConfigureApiBehaviorOptions(setupAction =>
        {
            setupAction.InvalidModelStateResponseFactory = context =>
            {
                // create a validation problem details object
                var problemDetailsFactory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();

                var validationProblemDetails = problemDetailsFactory
                    .CreateValidationProblemDetails(
                        context.HttpContext,
                        context.ModelState);

                // add additional info not added by default
                validationProblemDetails.Detail = "See the errors field for details.";
                validationProblemDetails.Instance = context.HttpContext.Request.Path;

                // report invalid model state responses as validation issues
                validationProblemDetails.Type = "https://courselibrary.com/modelvalidationproblem";
                validationProblemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                validationProblemDetails.Title = "One or more validation errors occurred.";

                return new UnprocessableEntityObjectResult(validationProblemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        builder.Services.Configure<MvcOptions>(config =>
        {
            var newtonsoftJsonOutputFormatter = config.OutputFormatters
                  .OfType<NewtonsoftJsonOutputFormatter>()?.FirstOrDefault();

            if (newtonsoftJsonOutputFormatter != null)
            {
                newtonsoftJsonOutputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.hateoas+json");
            }
        });

        builder.Services.AddScoped<ICourseLibraryRepository, CourseLibraryRepository>();

        //Transient for lightweight stateless services
        builder.Services.AddTransient<IPropertyMappingService, PropertyMappingService>();
        builder.Services.AddTransient<IPropertyCheckerService, PropertyCheckerService>();

        builder.Services.AddDbContext<CourseLibraryContext>(options =>
        {
            options.UseSqlite(@"Data Source=library.db");
        });

        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        builder.Services.AddResponseCaching();

        builder.Services.AddHttpCacheHeaders(
            (experationModelOptions) =>
            {
                experationModelOptions.MaxAge = 60;
                experationModelOptions.CacheLocation = Marvin.Cache.Headers.CacheLocation.Private;
            },
            (validationModelOptions) =>
            {
                validationModelOptions.MustRevalidate = true;
            });

        return builder.Build();
    }

    // Configure the request/response pipelien
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseResponseCaching(); //caching must be executed before anything else

        app.UseHttpCacheHeaders();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }

    public static async Task ResetDatabaseAsync(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var context = scope.ServiceProvider.GetService<CourseLibraryContext>();
                if (context != null)
                {
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
                logger.LogError(ex, "An error occurred while migrating the database.");
            }
        }
    }
}