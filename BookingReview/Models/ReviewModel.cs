using System.ComponentModel.DataAnnotations.Schema;

namespace BookingReview.Models;

[Table("responses")]
public class ReviewModel
{
    [Column("id")]
    public long? Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    public string[] Comments { get; set; }
}