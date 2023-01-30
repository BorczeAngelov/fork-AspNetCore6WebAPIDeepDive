using System.ComponentModel.DataAnnotations;

namespace CourseLibrary.API.Models;

public class CourseForCreationDto
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1500)]
    public string? Description { get; set; } = string.Empty;
}