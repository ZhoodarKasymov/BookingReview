using System.Data;
using System.Text.RegularExpressions;
using BookingReview.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookingReview.Services;

public class ReviewService : IReviewService
{
    private readonly IDbConnection _db;

    public ReviewService(IDbConnection db)
    {
        _db = db;
    }

    public async Task<bool> AddReviewAsync(ReviewModel model, string talon)
    {
        CorrectTalon(talon, out var talonKey, out var talonNumber);

        var query = @"SELECT id, service_id, user_id FROM clients
                        WHERE service_prefix = upper(@TalonKey) && number = @TalonNumber;";

        var client = await _db.QueryFirstOrDefaultAsync(query, new { TalonKey = talonKey, TalonNumber = talonNumber });

        if (client is null) throw new Exception("Талон не найден!");

        var clientId = client.id;
        var serviceId = client.service_id;
        var userId = client.user_id;

        await CheckReviewExistOrNo(clientId);

        var result = await _db.ExecuteAsync(
            @"INSERT INTO response_event (resp_date, response_id, services_id, users_id, clients_id, client_data, comment) 
                                                    VALUES (@RespDate, @ResponseId, @ServiceId, @UserId, @ClientId, '', @Comment);",
            new
            {
                RespDate = DateTime.Now,
                ResponseId = model.Id,
                ServiceId = serviceId,
                UserId = userId,
                ClientId = clientId,
                Comment = string.Join("; ", model.Comments)
            });

        if (result <= 0)
            throw new Exception("Отзыв не сохранен, что-то пошло не так!");
        
        return true;
    }

    public async Task<List<SelectListItem>> GetReviewModelsAsync()
    {
        var query = @"SELECT id, name FROM responses
                    WHERE deleted IS NULL && parent_id IS NOT NULL;";

        var result = await _db.QueryAsync<ReviewModel>(query);

        return result.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
    }

    private async Task CheckReviewExistOrNo(long? clientId)
    {
        var query = @"SELECT COUNT(*) FROM response_event
                        WHERE clients_id = @ClientId;";

        var count = await _db.ExecuteScalarAsync<int>(query, new { ClientId = clientId });

        if (count > 0)
            throw new Exception("Вы уже оставляли отзыв!");
    }

    private void CorrectTalon(string talon, out string talonKey, out int talonNumber)
    {
        talonKey = "";
        talonNumber = 0;

        var substrings = Regex.Split(talon, @"(?<=\D)(?=\d)");

        foreach (var substring in substrings)
        {
            if (int.TryParse(substring, out var number))
            {
                talonNumber = number;
            }
            else
            {
                talonKey = substring;
            }
        }
    }
}