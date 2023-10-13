namespace BookingReview.Models;

public class FilterModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? ServiceId { get; set; }
    public string? UserId { get; set; }
    
    public bool? IsRating { get; set; }
    public bool? IsCommon { get; set; }
    public bool? IsService { get; set; }
}