﻿using BookingReview.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookingReview.Services;

public interface IReviewService
{
    Task<bool> AddReviewAsync(ReviewModel model, string talon);

    Task<List<SelectListItem>> GetReviewModelsAsync();
}