﻿using BookingReview.Models;
using BookingReview.Models.Enums;
using BookingReview.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookingReview.Controllers;

public class ExportController : Controller
{
    private readonly IExportService _exportService;
    private readonly IConfiguration _cfg;

    public ExportController(IExportService exportService, IConfiguration cfg)
    {
        _exportService = exportService;
        _cfg = cfg;
    }
    
    public async Task<IActionResult> Generate(ExportType type, FilterModel filter)
    {
        byte[] file;
 
        if (type == ExportType.Excel)
        {
            file = await _exportService.GenerateExcelAsync(filter);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "экспортированный-отчет.xlsx");
        }

        file = await _exportService.GeneratePdfAsync(filter);
        return File(file, "application/pdf", "экспортированный-отчет.pdf");
    }

    public async Task<IActionResult> Index()
    {
        var services = await _exportService.GetServicesAsync();
        var workers = await _exportService.GetWorkersAsync();

        ViewBag.Services = services;

        ViewBag.Workers = workers.Select(s => new SelectListItem
        {
            Text = s.name,
            Value = s.id.ToString()
        });
        
        return View();
    }

    [HttpGet]
    public async Task<PreviewModel> GetPreviewTableAsync(FilterModel filter)
    {
        IEnumerable<dynamic> result;
        List<string> headers;
        
        if (filter.IsCommon ?? false)
        {
            result = await _exportService.GetUserServiceReportAsync(filter);

            headers = new List<string>
            {
                filter.IsService ?? false ? "Услуга" : "Работник",
                "Кол-во клиентов",
                "Максимальная время обслуживания",
                "Средняя время обслуживания",
                "Минимальная время обслуживания",
                "Максимальная время ожидания",
                "Средняя время ожидания",
                "Минимальная время ожидания",
                "Кол-во отзывов"
            };
        }
        else
        {
            result = await _exportService.GetCommonReportDataAsync(filter);
            
            
            
            headers = new List<string>
            {
                "Номер талона",
                "Услуга",
                "Работник",
                "Дата принятие клиента",
                "Дата начало обслуживания",
                "Дата конца обслуживания",
                "Время ожидания клиента (Мин)",
                "Время обслуживания клиента (Мин)"
            };
            
            if(!string.IsNullOrEmpty(filter.UserId))
                headers.Add("Время бездействия работника (Мин)");

            if (_cfg.GetValue<bool>("ShowInputData"))
                headers.Add("Доп. Инфо");
            
            headers.AddRange(new []
            {
                "Отзыв",
                "Комментарий к отзыву",
                "Результат"
            });
        }

        return new PreviewModel
        {
            Headers = headers,
            Results = result
        };
    }
}