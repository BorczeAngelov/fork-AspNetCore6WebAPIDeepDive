using System.ComponentModel.DataAnnotations;

namespace CourseLibrary.API.Models
{
    public abstract class CourseForManipulationDto : IValidatableObject
    {
        [Required(ErrorMessage = "You should fill out a title.")]
        [MaxLength(100, ErrorMessage = "The title shouldn't have more than 100 characters.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1500, ErrorMessage = "The description shouldn't have more than 1500 characters.")]
        public virtual string Description { get; set; } = string.Empty;

        //  (Cross-property validation) Class-level input validation with IValidatableObject.
        //  Validate a business rule for example
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Title == Description)
            {
                yield return new ValidationResult(
                errorMessage: "The provided description should be different from the title.",
                memberNames: new[] { "Course" });
            }
        }
    }
}
