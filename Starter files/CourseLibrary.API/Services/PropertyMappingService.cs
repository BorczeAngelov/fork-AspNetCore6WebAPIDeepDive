using CourseLibrary.API.Entities;
using CourseLibrary.API.Models;

namespace CourseLibrary.API.Services
{
    public class PropertyMappingService : IPropertyMappingService
    {
        private readonly Dictionary<string, PropertyMappingValue> _authorPropertyMapping =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Id", new(new[] { "Id" }) },
                { "MainCategory", new(new[] { "MainCategory" }) },
                { "Age", new(new[] { "DateOfBirth" }, true) }, //reverse oder
                { "Name", new(new[] { "FirstName", "LastName" }) }
            };

        private readonly IList<IPropertyMapping> _propertyMappings = new List<IPropertyMapping>();


        public PropertyMappingService()
        {
            _propertyMappings.Add(new PropertyMapping<AuthorDto, Author>(_authorPropertyMapping));
        }

        public Dictionary<string, PropertyMappingValue> GetPropertyMapping<TSource, TDestination>()
        {
            // get matching mapping
            var matchingMapping = _propertyMappings.OfType<PropertyMapping<TSource, TDestination>>();

            if (matchingMapping.Count() == 1)
            {
                return matchingMapping.First().MappingDictionary;
            }

            throw new Exception($"Cannot find exact property mapping instance " +
                $"for <{typeof(TSource)},{typeof(TDestination)}");
        }
    }
}
