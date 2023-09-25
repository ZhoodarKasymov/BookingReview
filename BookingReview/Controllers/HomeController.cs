using BookingReview.Models;
using BookingReview.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BookingReview.Controllers;

public class HomeController : Controller
{
    private readonly IReviewService _reviewService;

    public HomeController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    public async Task<IActionResult> Index()
    {
        var reviews = await _reviewService.GetReviewModelsAsync();
        return View(reviews);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<bool> AddReviewAsync(ReviewModel model, string talon)
    {
        return await _reviewService.AddReviewAsync(model, talon);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}