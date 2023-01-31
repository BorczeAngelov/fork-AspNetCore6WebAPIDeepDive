using System.ComponentModel.DataAnnotations;

namespace CourseLibrary.API.Models
{
    public class CourseForUpdateDto : CourseForManipulationDto
    {
        [Required(ErrorMessage = "You should fill out a description.")]
        [MaxLength(1500, ErrorMessage = "The description shouldn't have more than 1500 characters.")]
        public override string Description
        {
            get => base.Description;
            set => base.Description = value;
        }
    }
}
